using System;
using System.Collections.Generic;
using RetailApp.Data;
using RetailApp.Models;
using Microsoft.Data.Sqlite;

namespace RetailApp.Services
{
    public class StockService
    {
        private readonly DatabaseService _dbService;

        public StockService(DatabaseService? dbService = null)
        {
            _dbService = dbService ?? DatabaseService.Instance;
        }

        public void AdjustStock(int productId, int quantityChange, string movementType, string reference, string notes)
        {
            if (quantityChange == 0)
                throw new ArgumentException("Quantity change cannot be zero.");
            if (string.IsNullOrWhiteSpace(notes))
                throw new ArgumentException("Reason / Notes are required for stock adjustments to ensure audit accountability.");

            using var connection = _dbService.CreateConnection();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Read current product
                int currentStock = 0;
                string sku = string.Empty;
                string name = string.Empty;

                using (var checkCmd = connection.CreateCommand())
                {
                    checkCmd.Transaction = transaction;
                    checkCmd.CommandText = "SELECT CurrentStock, Sku, Name FROM Products WHERE Id = @Id;";
                    checkCmd.Parameters.AddWithValue("@Id", productId);

                    using var reader = checkCmd.ExecuteReader();
                    if (!reader.Read())
                    {
                        throw new InvalidOperationException($"Product with ID {productId} not found.");
                    }
                    currentStock = reader.GetInt32(0);
                    sku = reader.GetString(1);
                    name = reader.GetString(2);
                }

                int newStock = currentStock + quantityChange;
                if (newStock < 0)
                {
                    throw new InvalidOperationException($"Cannot reduce stock below zero. Current stock: {currentStock}, Adjustment: {quantityChange}.");
                }

                // Update product stock
                using (var updateCmd = connection.CreateCommand())
                {
                    updateCmd.Transaction = transaction;
                    updateCmd.CommandText = "UPDATE Products SET CurrentStock = @NewStock, UpdatedAt = CURRENT_TIMESTAMP WHERE Id = @Id;";
                    updateCmd.Parameters.AddWithValue("@NewStock", newStock);
                    updateCmd.Parameters.AddWithValue("@Id", productId);
                    updateCmd.ExecuteNonQuery();
                }

                // Insert movement log
                using (var moveCmd = connection.CreateCommand())
                {
                    moveCmd.Transaction = transaction;
                    moveCmd.CommandText = @"
                        INSERT INTO StockMovements (ProductId, ProductSku, ProductName, MovementType, QuantityChange, StockBefore, StockAfter, Reference, Notes, CreatedAt)
                        VALUES (@ProductId, @ProductSku, @ProductName, @MovementType, @QuantityChange, @StockBefore, @StockAfter, @Reference, @Notes, CURRENT_TIMESTAMP);
                    ";
                    moveCmd.Parameters.AddWithValue("@ProductId", productId);
                    moveCmd.Parameters.AddWithValue("@ProductSku", sku);
                    moveCmd.Parameters.AddWithValue("@ProductName", name);
                    moveCmd.Parameters.AddWithValue("@MovementType", movementType);
                    moveCmd.Parameters.AddWithValue("@QuantityChange", quantityChange);
                    moveCmd.Parameters.AddWithValue("@StockBefore", currentStock);
                    moveCmd.Parameters.AddWithValue("@StockAfter", newStock);
                    moveCmd.Parameters.AddWithValue("@Reference", string.IsNullOrWhiteSpace(reference) ? "MANUAL-ADJ" : reference.Trim());
                    moveCmd.Parameters.AddWithValue("@Notes", notes.Trim());
                    moveCmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public List<StockMovement> GetStockMovements(int? productId = null, string? movementType = null, int limit = 100)
        {
            var movements = new List<StockMovement>();
            using var connection = _dbService.CreateConnection();
            using var cmd = connection.CreateCommand();

            string query = "SELECT Id, ProductId, ProductSku, ProductName, MovementType, QuantityChange, StockBefore, StockAfter, Reference, Notes, CreatedAt FROM StockMovements WHERE 1=1";

            if (productId.HasValue && productId.Value > 0)
            {
                query += " AND ProductId = @ProductId";
                cmd.Parameters.AddWithValue("@ProductId", productId.Value);
            }

            if (!string.IsNullOrWhiteSpace(movementType) && movementType != "All Types")
            {
                query += " AND MovementType = @MovementType";
                cmd.Parameters.AddWithValue("@MovementType", movementType);
            }

            query += $" ORDER BY CreatedAt DESC LIMIT {limit};";
            cmd.CommandText = query;

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                movements.Add(new StockMovement
                {
                    Id = reader.GetInt32(0),
                    ProductId = reader.GetInt32(1),
                    ProductSku = reader.GetString(2),
                    ProductName = reader.GetString(3),
                    MovementType = reader.GetString(4),
                    QuantityChange = reader.GetInt32(5),
                    StockBefore = reader.GetInt32(6),
                    StockAfter = reader.GetInt32(7),
                    Reference = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    Notes = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                    CreatedAt = reader.GetDateTime(10)
                });
            }

            return movements;
        }

        public List<Product> GetLowStockProducts()
        {
            var list = new List<Product>();
            using var connection = _dbService.CreateConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, Sku, Barcode, Name, Category, CostPrice, SellingPrice, CurrentStock, ReorderLevel, Unit, Description, IsActive, CreatedAt, UpdatedAt
                FROM Products
                WHERE CurrentStock <= ReorderLevel AND IsActive = 1
                ORDER BY CurrentStock ASC, Name ASC;
            ";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Product
                {
                    Id = reader.GetInt32(0),
                    Sku = reader.GetString(1),
                    Barcode = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Name = reader.GetString(3),
                    Category = reader.GetString(4),
                    CostPrice = reader.GetDecimal(5),
                    SellingPrice = reader.GetDecimal(6),
                    CurrentStock = reader.GetInt32(7),
                    ReorderLevel = reader.GetInt32(8),
                    Unit = reader.GetString(9),
                    Description = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                    IsActive = reader.GetInt32(11) == 1,
                    CreatedAt = reader.GetDateTime(12),
                    UpdatedAt = reader.GetDateTime(13)
                });
            }

            return list;
        }
    }
}
