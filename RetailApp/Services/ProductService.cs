using System;
using System.Collections.Generic;
using RetailApp.Data;
using RetailApp.Models;
using Microsoft.Data.Sqlite;

namespace RetailApp.Services
{
    public class ProductService
    {
        private readonly DatabaseService _dbService;

        public ProductService(DatabaseService? dbService = null)
        {
            _dbService = dbService ?? DatabaseService.Instance;
        }

        public List<Product> GetAllProducts(string? searchTerm = null, string? category = null, bool activeOnly = false)
        {
            var products = new List<Product>();
            using var connection = _dbService.CreateConnection();
            using var cmd = connection.CreateCommand();

            string query = "SELECT Id, Sku, Barcode, Name, Category, CostPrice, SellingPrice, CurrentStock, ReorderLevel, Unit, Description, IsActive, CreatedAt, UpdatedAt FROM Products WHERE 1=1";

            if (activeOnly)
            {
                query += " AND IsActive = 1";
            }

            if (!string.IsNullOrWhiteSpace(category) && category != "All Categories")
            {
                query += " AND Category = @Category";
                cmd.Parameters.AddWithValue("@Category", category);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query += " AND (Sku LIKE @Search OR Name LIKE @Search OR Barcode LIKE @Search)";
                cmd.Parameters.AddWithValue("@Search", $"%{searchTerm.Trim()}%");
            }

            query += " ORDER BY Name ASC;";
            cmd.CommandText = query;

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                products.Add(ReadProduct(reader));
            }

            return products;
        }

        public Product? GetProductById(int id)
        {
            using var connection = _dbService.CreateConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Id, Sku, Barcode, Name, Category, CostPrice, SellingPrice, CurrentStock, ReorderLevel, Unit, Description, IsActive, CreatedAt, UpdatedAt FROM Products WHERE Id = @Id;";
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return ReadProduct(reader);
            }
            return null;
        }

        public Product? GetProductBySku(string sku)
        {
            using var connection = _dbService.CreateConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Id, Sku, Barcode, Name, Category, CostPrice, SellingPrice, CurrentStock, ReorderLevel, Unit, Description, IsActive, CreatedAt, UpdatedAt FROM Products WHERE Sku = @Sku COLLATE NOCASE;";
            cmd.Parameters.AddWithValue("@Sku", sku.Trim());

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return ReadProduct(reader);
            }
            return null;
        }

        public bool IsSkuUnique(string sku, int excludeId = 0)
        {
            using var connection = _dbService.CreateConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Products WHERE Sku = @Sku COLLATE NOCASE AND Id != @ExcludeId;";
            cmd.Parameters.AddWithValue("@Sku", sku.Trim());
            cmd.Parameters.AddWithValue("@ExcludeId", excludeId);

            long count = (long)(cmd.ExecuteScalar() ?? 0);
            return count == 0;
        }

        public int AddProduct(Product product)
        {
            if (string.IsNullOrWhiteSpace(product.Sku))
                throw new ArgumentException("Product SKU / Code is required.");
            if (string.IsNullOrWhiteSpace(product.Name))
                throw new ArgumentException("Product Name is required.");
            if (!IsSkuUnique(product.Sku))
                throw new InvalidOperationException($"A product with SKU '{product.Sku}' already exists.");

            using var connection = _dbService.CreateConnection();
            using var transaction = connection.BeginTransaction();

            try
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO Products (Sku, Barcode, Name, Category, CostPrice, SellingPrice, CurrentStock, ReorderLevel, Unit, Description, IsActive, CreatedAt, UpdatedAt)
                    VALUES (@Sku, @Barcode, @Name, @Category, @CostPrice, @SellingPrice, @CurrentStock, @ReorderLevel, @Unit, @Description, @IsActive, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
                    SELECT last_insert_rowid();
                ";
                cmd.Parameters.AddWithValue("@Sku", product.Sku.Trim().ToUpperInvariant());
                cmd.Parameters.AddWithValue("@Barcode", product.Barcode?.Trim() ?? string.Empty);
                cmd.Parameters.AddWithValue("@Name", product.Name.Trim());
                cmd.Parameters.AddWithValue("@Category", string.IsNullOrWhiteSpace(product.Category) ? "General" : product.Category.Trim());
                cmd.Parameters.AddWithValue("@CostPrice", product.CostPrice);
                cmd.Parameters.AddWithValue("@SellingPrice", product.SellingPrice);
                cmd.Parameters.AddWithValue("@CurrentStock", product.CurrentStock);
                cmd.Parameters.AddWithValue("@ReorderLevel", product.ReorderLevel);
                cmd.Parameters.AddWithValue("@Unit", string.IsNullOrWhiteSpace(product.Unit) ? "Unit" : product.Unit.Trim());
                cmd.Parameters.AddWithValue("@Description", product.Description?.Trim() ?? string.Empty);
                cmd.Parameters.AddWithValue("@IsActive", product.IsActive ? 1 : 0);

                int newId = Convert.ToInt32(cmd.ExecuteScalar());

                // Record initial stock movement if stock > 0
                if (product.CurrentStock > 0)
                {
                    using var moveCmd = connection.CreateCommand();
                    moveCmd.Transaction = transaction;
                    moveCmd.CommandText = @"
                        INSERT INTO StockMovements (ProductId, ProductSku, ProductName, MovementType, QuantityChange, StockBefore, StockAfter, Reference, Notes, CreatedAt)
                        VALUES (@ProductId, @ProductSku, @ProductName, 'INITIAL', @Qty, 0, @Qty, 'INIT', 'Initial product creation', CURRENT_TIMESTAMP);
                    ";
                    moveCmd.Parameters.AddWithValue("@ProductId", newId);
                    moveCmd.Parameters.AddWithValue("@ProductSku", product.Sku.Trim().ToUpperInvariant());
                    moveCmd.Parameters.AddWithValue("@ProductName", product.Name.Trim());
                    moveCmd.Parameters.AddWithValue("@Qty", product.CurrentStock);
                    moveCmd.ExecuteNonQuery();
                }

                transaction.Commit();
                product.Id = newId;
                return newId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public void UpdateProduct(Product product)
        {
            if (string.IsNullOrWhiteSpace(product.Sku))
                throw new ArgumentException("Product SKU / Code is required.");
            if (string.IsNullOrWhiteSpace(product.Name))
                throw new ArgumentException("Product Name is required.");
            if (!IsSkuUnique(product.Sku, product.Id))
                throw new InvalidOperationException($"A product with SKU '{product.Sku}' already exists.");

            using var connection = _dbService.CreateConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE Products
                SET Sku = @Sku,
                    Barcode = @Barcode,
                    Name = @Name,
                    Category = @Category,
                    CostPrice = @CostPrice,
                    SellingPrice = @SellingPrice,
                    CurrentStock = @CurrentStock,
                    ReorderLevel = @ReorderLevel,
                    Unit = @Unit,
                    Description = @Description,
                    IsActive = @IsActive,
                    UpdatedAt = CURRENT_TIMESTAMP
                WHERE Id = @Id;
            ";
            cmd.Parameters.AddWithValue("@Id", product.Id);
            cmd.Parameters.AddWithValue("@Sku", product.Sku.Trim().ToUpperInvariant());
            cmd.Parameters.AddWithValue("@Barcode", product.Barcode?.Trim() ?? string.Empty);
            cmd.Parameters.AddWithValue("@Name", product.Name.Trim());
            cmd.Parameters.AddWithValue("@Category", string.IsNullOrWhiteSpace(product.Category) ? "General" : product.Category.Trim());
            cmd.Parameters.AddWithValue("@CostPrice", product.CostPrice);
            cmd.Parameters.AddWithValue("@SellingPrice", product.SellingPrice);
            cmd.Parameters.AddWithValue("@CurrentStock", product.CurrentStock);
            cmd.Parameters.AddWithValue("@ReorderLevel", product.ReorderLevel);
            cmd.Parameters.AddWithValue("@Unit", string.IsNullOrWhiteSpace(product.Unit) ? "Unit" : product.Unit.Trim());
            cmd.Parameters.AddWithValue("@Description", product.Description?.Trim() ?? string.Empty);
            cmd.Parameters.AddWithValue("@IsActive", product.IsActive ? 1 : 0);

            cmd.ExecuteNonQuery();
        }

        public bool CanDeleteProduct(int productId)
        {
            using var connection = _dbService.CreateConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM SaleItems WHERE ProductId = @ProductId;";
            cmd.Parameters.AddWithValue("@ProductId", productId);
            long count = (long)(cmd.ExecuteScalar() ?? 0);
            return count == 0;
        }

        public void DeleteProduct(int productId)
        {
            if (!CanDeleteProduct(productId))
            {
                // Soft delete by deactivating to preserve audit & sales records integrity
                using var conn = _dbService.CreateConnection();
                using var softCmd = conn.CreateCommand();
                softCmd.CommandText = "UPDATE Products SET IsActive = 0, UpdatedAt = CURRENT_TIMESTAMP WHERE Id = @Id;";
                softCmd.Parameters.AddWithValue("@Id", productId);
                softCmd.ExecuteNonQuery();
                return;
            }

            using var connection = _dbService.CreateConnection();
            using var transaction = connection.BeginTransaction();
            try
            {
                // Delete stock movements first
                using var moveCmd = connection.CreateCommand();
                moveCmd.Transaction = transaction;
                moveCmd.CommandText = "DELETE FROM StockMovements WHERE ProductId = @Id;";
                moveCmd.Parameters.AddWithValue("@Id", productId);
                moveCmd.ExecuteNonQuery();

                // Delete product
                using var prodCmd = connection.CreateCommand();
                prodCmd.Transaction = transaction;
                prodCmd.CommandText = "DELETE FROM Products WHERE Id = @Id;";
                prodCmd.Parameters.AddWithValue("@Id", productId);
                prodCmd.ExecuteNonQuery();

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public List<string> GetCategories()
        {
            var list = new List<string>();
            using var connection = _dbService.CreateConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT Category FROM Products WHERE Category IS NOT NULL AND Category != '' ORDER BY Category ASC;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(reader.GetString(0));
            }
            return list;
        }

        private static Product ReadProduct(SqliteDataReader reader)
        {
            return new Product
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
            };
        }
    }
}
