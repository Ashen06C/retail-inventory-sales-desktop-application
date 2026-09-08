using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RetailApp.Data;
using RetailApp.Models;
using Microsoft.Data.Sqlite;

namespace RetailApp.Services
{
    public class ReportService
    {
        private readonly DatabaseService _dbService;

        public ReportService(DatabaseService? dbService = null)
        {
            _dbService = dbService ?? DatabaseService.Instance;
        }

        public DashboardMetrics GetDashboardMetrics()
        {
            var metrics = new DashboardMetrics();
            using var connection = _dbService.CreateConnection();

            // 1. Today's sales metrics
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT 
                        COALESCE(SUM(GrandTotal), 0),
                        COUNT(Id)
                    FROM Sales
                    WHERE date(SaleDate) = date('now', 'localtime');
                ";
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    metrics.TodaySalesTotal = reader.GetDecimal(0);
                    metrics.TodayTransactionsCount = reader.GetInt32(1);
                }
            }

            // 2. Today's total items sold
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT COALESCE(SUM(si.Quantity), 0)
                    FROM SaleItems si
                    INNER JOIN Sales s ON s.Id = si.SaleId
                    WHERE date(s.SaleDate) = date('now', 'localtime');
                ";
                var result = cmd.ExecuteScalar();
                metrics.TodayItemsSold = Convert.ToInt32(result ?? 0);
            }

            // 3. Product & Inventory totals
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT 
                        COUNT(*),
                        COALESCE(SUM(CurrentStock * CostPrice), 0),
                        COALESCE(SUM(CurrentStock * SellingPrice), 0),
                        COALESCE(SUM(CASE WHEN CurrentStock > 0 AND CurrentStock <= ReorderLevel THEN 1 ELSE 0 END), 0),
                        COALESCE(SUM(CASE WHEN CurrentStock <= 0 THEN 1 ELSE 0 END), 0)
                    FROM Products
                    WHERE IsActive = 1;
                ";
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    metrics.TotalProductsCount = reader.GetInt32(0);
                    metrics.TotalInventoryCostValue = reader.GetDecimal(1);
                    metrics.TotalInventoryRetailValue = reader.GetDecimal(2);
                    metrics.LowStockCount = reader.GetInt32(3);
                    metrics.OutOfStockCount = reader.GetInt32(4);
                }
            }

            // 4. Top 5 Best Selling Products
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT 
                        p.Id,
                        p.Sku,
                        p.Name,
                        p.Category,
                        COALESCE(SUM(si.Quantity), 0) AS TotalSold,
                        COALESCE(SUM(si.TotalPrice), 0) AS Revenue
                    FROM SaleItems si
                    INNER JOIN Products p ON p.Id = si.ProductId
                    GROUP BY p.Id, p.Sku, p.Name, p.Category
                    ORDER BY TotalSold DESC
                    LIMIT 5;
                ";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    metrics.TopProducts.Add(new TopSellingProductItem
                    {
                        ProductId = reader.GetInt32(0),
                        Sku = reader.GetString(1),
                        ProductName = reader.GetString(2),
                        Category = reader.GetString(3),
                        TotalSoldQty = reader.GetInt32(4),
                        TotalRevenue = reader.GetDecimal(5)
                    });
                }
            }

            // 5. Recent 5 Sales
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT Id, InvoiceNumber, SaleDate, Subtotal, DiscountAmount, TaxAmount, GrandTotal, PaymentMethod, CashierName
                    FROM Sales
                    ORDER BY SaleDate DESC
                    LIMIT 5;
                ";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    metrics.RecentSales.Add(new Sale
                    {
                        Id = reader.GetInt32(0),
                        InvoiceNumber = reader.GetString(1),
                        SaleDate = reader.GetDateTime(2),
                        Subtotal = reader.GetDecimal(3),
                        DiscountAmount = reader.GetDecimal(4),
                        TaxAmount = reader.GetDecimal(5),
                        GrandTotal = reader.GetDecimal(6),
                        PaymentMethod = reader.GetString(7),
                        CashierName = reader.GetString(8)
                    });
                }
            }

            // 6. Low stock products quick list
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT Id, Sku, Barcode, Name, Category, CostPrice, SellingPrice, CurrentStock, ReorderLevel, Unit
                    FROM Products
                    WHERE CurrentStock <= ReorderLevel AND IsActive = 1
                    ORDER BY CurrentStock ASC
                    LIMIT 8;
                ";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    metrics.LowStockProducts.Add(new Product
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
                        Unit = reader.GetString(9)
                    });
                }
            }

            return metrics;
        }

        public void ExportSalesToCsv(string filePath, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var salesService = new SalesService(_dbService);
            var sales = salesService.GetAllSales(fromDate, toDate);

            var sb = new StringBuilder();
            sb.AppendLine("Invoice Number,Date & Time,Cashier,Payment Method,Items Count,Subtotal (Rs),Discount (Rs),Tax (Rs),Grand Total (Rs),Notes");

            foreach (var sale in sales)
            {
                string invoice = EscapeCsv(sale.InvoiceNumber);
                string date = sale.SaleDate.ToString("yyyy-MM-dd HH:mm:ss");
                string cashier = EscapeCsv(sale.CashierName);
                string method = EscapeCsv(sale.PaymentMethod);
                int itemsCount = sale.TotalItemsCount;
                string subtotal = sale.Subtotal.ToString("F2");
                string discount = sale.DiscountAmount.ToString("F2");
                string tax = sale.TaxAmount.ToString("F2");
                string total = sale.GrandTotal.ToString("F2");
                string notes = EscapeCsv(sale.Notes);

                sb.AppendLine($"{invoice},{date},{cashier},{method},{itemsCount},{subtotal},{discount},{tax},{total},{notes}");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        public void ExportStockReportToCsv(string filePath)
        {
            var productService = new ProductService(_dbService);
            var products = productService.GetAllProducts();

            var sb = new StringBuilder();
            sb.AppendLine("SKU,Barcode,Product Name,Category,Cost Price (Rs),Selling Price (Rs),Current Stock,Reorder Level,Unit,Stock Status,Stock Value (Cost),Status");

            foreach (var p in products)
            {
                string sku = EscapeCsv(p.Sku);
                string barcode = EscapeCsv(p.Barcode);
                string name = EscapeCsv(p.Name);
                string category = EscapeCsv(p.Category);
                string cost = p.CostPrice.ToString("F2");
                string price = p.SellingPrice.ToString("F2");
                int stock = p.CurrentStock;
                int reorder = p.ReorderLevel;
                string unit = EscapeCsv(p.Unit);
                string status = p.StockStatus;
                string val = p.TotalStockValue.ToString("F2");
                string active = p.IsActive ? "Active" : "Inactive";

                sb.AppendLine($"{sku},{barcode},{name},{category},{cost},{price},{stock},{reorder},{unit},{status},{val},{active}");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private static string EscapeCsv(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "\"\"";
            if (text.Contains(',') || text.Contains('\"') || text.Contains('\n') || text.Contains('\r'))
            {
                return $"\"{text.Replace("\"", "\"\"")}\"";
            }
            return text;
        }
    }
}
