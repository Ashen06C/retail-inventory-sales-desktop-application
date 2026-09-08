using System;
using System.Collections.Generic;
using RetailApp.Data;
using RetailApp.Models;
using Microsoft.Data.Sqlite;

namespace RetailApp.Services
{
    public class SalesService
    {
        private readonly DatabaseService _dbService;

        public SalesService(DatabaseService? dbService = null)
        {
            _dbService = dbService ?? DatabaseService.Instance;
        }

        public string GenerateInvoiceNumber()
        {
            string datePrefix = DateTime.Now.ToString("yyyyMMdd");
            using var connection = _dbService.CreateConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Sales WHERE InvoiceNumber LIKE @Pattern;";
            cmd.Parameters.AddWithValue("@Pattern", $"INV-{datePrefix}-%");
            long count = (long)(cmd.ExecuteScalar() ?? 0);
            return $"INV-{datePrefix}-{(count + 1):D4}";
        }

        public Sale ProcessSale(Sale sale)
        {
            if (sale.Items == null || sale.Items.Count == 0)
                throw new InvalidOperationException("Cannot checkout an empty cart. Please add products to the sale.");

            if (sale.AmountTendered < sale.GrandTotal && sale.PaymentMethod == "Cash")
                throw new InvalidOperationException($"Amount tendered (Rs. {sale.AmountTendered:N2}) is less than grand total (Rs. {sale.GrandTotal:N2}).");

            using var connection = _dbService.CreateConnection();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Verify stock availability for all items before making any modifications
                foreach (var item in sale.Items)
                {
                    using var checkCmd = connection.CreateCommand();
                    checkCmd.Transaction = transaction;
                    checkCmd.CommandText = "SELECT CurrentStock, Name FROM Products WHERE Id = @Id;";
                    checkCmd.Parameters.AddWithValue("@Id", item.ProductId);

                    using var reader = checkCmd.ExecuteReader();
                    if (!reader.Read())
                    {
                        throw new InvalidOperationException($"Product '{item.ProductName}' (ID: {item.ProductId}) does not exist in inventory.");
                    }

                    int currentStock = reader.GetInt32(0);
                    string name = reader.GetString(1);

                    if (currentStock < item.Quantity)
                    {
                        throw new InvalidOperationException($"Insufficient stock for '{name}'. Available: {currentStock}, Requested: {item.Quantity}.");
                    }
                }

                // If invoice number is not provided, generate one
                if (string.IsNullOrWhiteSpace(sale.InvoiceNumber))
                {
                    sale.InvoiceNumber = GenerateInvoiceNumber();
                }

                sale.SaleDate = DateTime.Now;
                sale.ChangeDue = Math.Max(0, sale.AmountTendered - sale.GrandTotal);

                // Insert into Sales table
                using var saleCmd = connection.CreateCommand();
                saleCmd.Transaction = transaction;
                saleCmd.CommandText = @"
                    INSERT INTO Sales (InvoiceNumber, SaleDate, Subtotal, DiscountPercentage, DiscountAmount, TaxPercentage, TaxAmount, GrandTotal, PaymentMethod, AmountTendered, ChangeDue, CashierName, Notes)
                    VALUES (@InvoiceNumber, @SaleDate, @Subtotal, @DiscountPercentage, @DiscountAmount, @TaxPercentage, @TaxAmount, @GrandTotal, @PaymentMethod, @AmountTendered, @ChangeDue, @CashierName, @Notes);
                    SELECT last_insert_rowid();
                ";
                saleCmd.Parameters.AddWithValue("@InvoiceNumber", sale.InvoiceNumber);
                saleCmd.Parameters.AddWithValue("@SaleDate", sale.SaleDate.ToString("yyyy-MM-dd HH:mm:ss"));
                saleCmd.Parameters.AddWithValue("@Subtotal", sale.Subtotal);
                saleCmd.Parameters.AddWithValue("@DiscountPercentage", sale.DiscountPercentage);
                saleCmd.Parameters.AddWithValue("@DiscountAmount", sale.DiscountAmount);
                saleCmd.Parameters.AddWithValue("@TaxPercentage", sale.TaxPercentage);
                saleCmd.Parameters.AddWithValue("@TaxAmount", sale.TaxAmount);
                saleCmd.Parameters.AddWithValue("@GrandTotal", sale.GrandTotal);
                saleCmd.Parameters.AddWithValue("@PaymentMethod", sale.PaymentMethod);
                saleCmd.Parameters.AddWithValue("@AmountTendered", sale.AmountTendered);
                saleCmd.Parameters.AddWithValue("@ChangeDue", sale.ChangeDue);
                saleCmd.Parameters.AddWithValue("@CashierName", string.IsNullOrWhiteSpace(sale.CashierName) ? "Cashier 01" : sale.CashierName);
                saleCmd.Parameters.AddWithValue("@Notes", sale.Notes?.Trim() ?? string.Empty);

                int saleId = Convert.ToInt32(saleCmd.ExecuteScalar());
                sale.Id = saleId;

                // Process line items & update stock atomically
                foreach (var item in sale.Items)
                {
                    item.SaleId = saleId;

                    // Get current stock
                    int stockBefore = 0;
                    using (var getStockCmd = connection.CreateCommand())
                    {
                        getStockCmd.Transaction = transaction;
                        getStockCmd.CommandText = "SELECT CurrentStock FROM Products WHERE Id = @Id;";
                        getStockCmd.Parameters.AddWithValue("@Id", item.ProductId);
                        stockBefore = Convert.ToInt32(getStockCmd.ExecuteScalar());
                    }

                    int stockAfter = stockBefore - item.Quantity;

                    // Deduct stock in Products table
                    using (var updateStockCmd = connection.CreateCommand())
                    {
                        updateStockCmd.Transaction = transaction;
                        updateStockCmd.CommandText = "UPDATE Products SET CurrentStock = @NewStock, UpdatedAt = CURRENT_TIMESTAMP WHERE Id = @Id;";
                        updateStockCmd.Parameters.AddWithValue("@NewStock", stockAfter);
                        updateStockCmd.Parameters.AddWithValue("@Id", item.ProductId);
                        updateStockCmd.ExecuteNonQuery();
                    }

                    // Insert SaleItem record
                    using (var itemCmd = connection.CreateCommand())
                    {
                        itemCmd.Transaction = transaction;
                        itemCmd.CommandText = @"
                            INSERT INTO SaleItems (SaleId, ProductId, ProductSku, ProductName, UnitCost, UnitPrice, Quantity, DiscountAmount, TotalPrice)
                            VALUES (@SaleId, @ProductId, @ProductSku, @ProductName, @UnitCost, @UnitPrice, @Quantity, @DiscountAmount, @TotalPrice);
                            SELECT last_insert_rowid();
                        ";
                        itemCmd.Parameters.AddWithValue("@SaleId", saleId);
                        itemCmd.Parameters.AddWithValue("@ProductId", item.ProductId);
                        itemCmd.Parameters.AddWithValue("@ProductSku", item.ProductSku);
                        itemCmd.Parameters.AddWithValue("@ProductName", item.ProductName);
                        itemCmd.Parameters.AddWithValue("@UnitCost", item.UnitCost);
                        itemCmd.Parameters.AddWithValue("@UnitPrice", item.UnitPrice);
                        itemCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                        itemCmd.Parameters.AddWithValue("@DiscountAmount", item.DiscountAmount);
                        itemCmd.Parameters.AddWithValue("@TotalPrice", item.TotalPrice);

                        item.Id = Convert.ToInt32(itemCmd.ExecuteScalar());
                    }

                    // Insert StockMovement record for audit trail
                    using (var moveCmd = connection.CreateCommand())
                    {
                        moveCmd.Transaction = transaction;
                        moveCmd.CommandText = @"
                            INSERT INTO StockMovements (ProductId, ProductSku, ProductName, MovementType, QuantityChange, StockBefore, StockAfter, Reference, Notes, CreatedAt)
                            VALUES (@ProductId, @ProductSku, @ProductName, 'SALE', @QtyChange, @Before, @After, @Ref, @Notes, CURRENT_TIMESTAMP);
                        ";
                        moveCmd.Parameters.AddWithValue("@ProductId", item.ProductId);
                        moveCmd.Parameters.AddWithValue("@ProductSku", item.ProductSku);
                        moveCmd.Parameters.AddWithValue("@ProductName", item.ProductName);
                        moveCmd.Parameters.AddWithValue("@QtyChange", -item.Quantity);
                        moveCmd.Parameters.AddWithValue("@Before", stockBefore);
                        moveCmd.Parameters.AddWithValue("@After", stockAfter);
                        moveCmd.Parameters.AddWithValue("@Ref", sale.InvoiceNumber);
                        moveCmd.Parameters.AddWithValue("@Notes", $"Sold in invoice {sale.InvoiceNumber}");
                        moveCmd.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
                return sale;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public List<Sale> GetAllSales(DateTime? fromDate = null, DateTime? toDate = null, string? search = null)
        {
            var sales = new List<Sale>();
            using var connection = _dbService.CreateConnection();
            using var cmd = connection.CreateCommand();

            string sql = "SELECT Id, InvoiceNumber, SaleDate, Subtotal, DiscountPercentage, DiscountAmount, TaxPercentage, TaxAmount, GrandTotal, PaymentMethod, AmountTendered, ChangeDue, CashierName, Notes FROM Sales WHERE 1=1";

            if (fromDate.HasValue)
            {
                sql += " AND SaleDate >= @FromDate";
                cmd.Parameters.AddWithValue("@FromDate", fromDate.Value.ToString("yyyy-MM-dd 00:00:00"));
            }

            if (toDate.HasValue)
            {
                sql += " AND SaleDate <= @ToDate";
                cmd.Parameters.AddWithValue("@ToDate", toDate.Value.ToString("yyyy-MM-dd 23:59:59"));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                sql += " AND (InvoiceNumber LIKE @Search OR CashierName LIKE @Search OR PaymentMethod LIKE @Search)";
                cmd.Parameters.AddWithValue("@Search", $"%{search.Trim()}%");
            }

            sql += " ORDER BY SaleDate DESC;";
            cmd.CommandText = sql;

            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    sales.Add(new Sale
                    {
                        Id = reader.GetInt32(0),
                        InvoiceNumber = reader.GetString(1),
                        SaleDate = reader.GetDateTime(2),
                        Subtotal = reader.GetDecimal(3),
                        DiscountPercentage = reader.GetDecimal(4),
                        DiscountAmount = reader.GetDecimal(5),
                        TaxPercentage = reader.GetDecimal(6),
                        TaxAmount = reader.GetDecimal(7),
                        GrandTotal = reader.GetDecimal(8),
                        PaymentMethod = reader.GetString(9),
                        AmountTendered = reader.GetDecimal(10),
                        ChangeDue = reader.GetDecimal(11),
                        CashierName = reader.GetString(12),
                        Notes = reader.IsDBNull(13) ? string.Empty : reader.GetString(13)
                    });
                }
            }

            // Populate items for each sale
            foreach (var sale in sales)
            {
                sale.Items = GetSaleItems(connection, sale.Id);
            }

            return sales;
        }

        public Sale? GetSaleById(int saleId)
        {
            using var connection = _dbService.CreateConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Id, InvoiceNumber, SaleDate, Subtotal, DiscountPercentage, DiscountAmount, TaxPercentage, TaxAmount, GrandTotal, PaymentMethod, AmountTendered, ChangeDue, CashierName, Notes FROM Sales WHERE Id = @Id;";
            cmd.Parameters.AddWithValue("@Id", saleId);

            Sale? sale = null;
            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    sale = new Sale
                    {
                        Id = reader.GetInt32(0),
                        InvoiceNumber = reader.GetString(1),
                        SaleDate = reader.GetDateTime(2),
                        Subtotal = reader.GetDecimal(3),
                        DiscountPercentage = reader.GetDecimal(4),
                        DiscountAmount = reader.GetDecimal(5),
                        TaxPercentage = reader.GetDecimal(6),
                        TaxAmount = reader.GetDecimal(7),
                        GrandTotal = reader.GetDecimal(8),
                        PaymentMethod = reader.GetString(9),
                        AmountTendered = reader.GetDecimal(10),
                        ChangeDue = reader.GetDecimal(11),
                        CashierName = reader.GetString(12),
                        Notes = reader.IsDBNull(13) ? string.Empty : reader.GetString(13)
                    };
                }
            }

            if (sale != null)
            {
                sale.Items = GetSaleItems(connection, sale.Id);
            }

            return sale;
        }

        private List<SaleItem> GetSaleItems(SqliteConnection connection, int saleId)
        {
            var items = new List<SaleItem>();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Id, SaleId, ProductId, ProductSku, ProductName, UnitCost, UnitPrice, Quantity, DiscountAmount, TotalPrice FROM SaleItems WHERE SaleId = @SaleId ORDER BY Id ASC;";
            cmd.Parameters.AddWithValue("@SaleId", saleId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                items.Add(new SaleItem
                {
                    Id = reader.GetInt32(0),
                    SaleId = reader.GetInt32(1),
                    ProductId = reader.GetInt32(2),
                    ProductSku = reader.GetString(3),
                    ProductName = reader.GetString(4),
                    UnitCost = reader.GetDecimal(5),
                    UnitPrice = reader.GetDecimal(6),
                    Quantity = reader.GetInt32(7),
                    DiscountAmount = reader.GetDecimal(8)
                });
            }
            return items;
        }
    }
}
