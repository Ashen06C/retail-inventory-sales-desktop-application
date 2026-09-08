using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace RetailApp.Data
{
    public class DatabaseService
    {
        private static DatabaseService? _instance;
        public static DatabaseService Instance => _instance ??= new DatabaseService();

        private readonly string _connectionString;

        public DatabaseService()
        {
            // Store DB in the application's base directory for seamless local portability
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dbPath = Path.Combine(baseDir, "retail_store.db");
            _connectionString = $"Data Source={dbPath};";

            InitializeDatabase();
        }

        public SqliteConnection CreateConnection()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var pragmaCmd = connection.CreateCommand();
            pragmaCmd.CommandText = "PRAGMA foreign_keys = ON;";
            pragmaCmd.ExecuteNonQuery();
            return connection;
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            string createTablesSql = @"
                CREATE TABLE IF NOT EXISTS Products (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Sku TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    Barcode TEXT,
                    Name TEXT NOT NULL,
                    Category TEXT NOT NULL DEFAULT 'General',
                    CostPrice DECIMAL(18,2) NOT NULL DEFAULT 0,
                    SellingPrice DECIMAL(18,2) NOT NULL DEFAULT 0,
                    CurrentStock INTEGER NOT NULL DEFAULT 0,
                    ReorderLevel INTEGER NOT NULL DEFAULT 10,
                    Unit TEXT NOT NULL DEFAULT 'Unit',
                    Description TEXT,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS Sales (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    InvoiceNumber TEXT NOT NULL UNIQUE,
                    SaleDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    Subtotal DECIMAL(18,2) NOT NULL DEFAULT 0,
                    DiscountPercentage DECIMAL(18,2) NOT NULL DEFAULT 0,
                    DiscountAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                    TaxPercentage DECIMAL(18,2) NOT NULL DEFAULT 0,
                    TaxAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                    GrandTotal DECIMAL(18,2) NOT NULL DEFAULT 0,
                    PaymentMethod TEXT NOT NULL DEFAULT 'Cash',
                    AmountTendered DECIMAL(18,2) NOT NULL DEFAULT 0,
                    ChangeDue DECIMAL(18,2) NOT NULL DEFAULT 0,
                    CashierName TEXT NOT NULL DEFAULT 'Cashier 01',
                    Notes TEXT
                );

                CREATE TABLE IF NOT EXISTS SaleItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SaleId INTEGER NOT NULL,
                    ProductId INTEGER NOT NULL,
                    ProductSku TEXT NOT NULL,
                    ProductName TEXT NOT NULL,
                    UnitCost DECIMAL(18,2) NOT NULL DEFAULT 0,
                    UnitPrice DECIMAL(18,2) NOT NULL DEFAULT 0,
                    Quantity INTEGER NOT NULL DEFAULT 1,
                    DiscountAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                    TotalPrice DECIMAL(18,2) NOT NULL DEFAULT 0,
                    FOREIGN KEY (SaleId) REFERENCES Sales(Id) ON DELETE CASCADE,
                    FOREIGN KEY (ProductId) REFERENCES Products(Id)
                );

                CREATE TABLE IF NOT EXISTS StockMovements (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProductId INTEGER NOT NULL,
                    ProductSku TEXT NOT NULL,
                    ProductName TEXT NOT NULL,
                    MovementType TEXT NOT NULL,
                    QuantityChange INTEGER NOT NULL,
                    StockBefore INTEGER NOT NULL,
                    StockAfter INTEGER NOT NULL,
                    Reference TEXT,
                    Notes TEXT,
                    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (ProductId) REFERENCES Products(Id)
                );

                CREATE INDEX IF NOT EXISTS idx_products_sku ON Products(Sku);
                CREATE INDEX IF NOT EXISTS idx_products_category ON Products(Category);
                CREATE INDEX IF NOT EXISTS idx_sales_invoice ON Sales(InvoiceNumber);
                CREATE INDEX IF NOT EXISTS idx_sales_date ON Sales(SaleDate);
                CREATE INDEX IF NOT EXISTS idx_stockmovements_product ON StockMovements(ProductId);
            ";

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = createTablesSql;
                cmd.ExecuteNonQuery();
            }

            SeedInitialDataIfEmpty(connection);
        }

        private void SeedInitialDataIfEmpty(SqliteConnection connection)
        {
            // Check if products already exist
            using var countCmd = connection.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM Products;";
            long count = (long)(countCmd.ExecuteScalar() ?? 0);

            if (count > 0) return;

            // Seed realistic retail sample data
            using var transaction = connection.BeginTransaction();
            try
            {
                var seedProducts = new[]
                {
                    ("BEV-101", "8934501", "Ceylon Premium Black Tea 100g", "Beverages", 350.00m, 520.00m, 45, 15, "Pkt", "Pure Ceylon single estate black tea."),
                    ("BEV-102", "8934502", "Jasmine Green Tea 50 Bags", "Beverages", 450.00m, 680.00m, 8, 12, "Box", "Delicate floral green tea bags."), // Low stock example!
                    ("DAI-201", "8934503", "Full Cream Milk Powder 400g", "Dairy", 850.00m, 1150.00m, 60, 20, "Pouch", "Rich imported full cream dairy milk."),
                    ("DAI-202", "8934504", "Anchor Pure Butter 227g", "Dairy", 620.00m, 890.00m, 5, 10, "Pkt", "Salted creamery pure butter."), // Low stock example!
                    ("GRO-301", "8934505", "Basmati Fragrant Rice 5kg", "Groceries", 1850.00m, 2450.00m, 28, 10, "Bag", "Aromatic long grain basmati rice."),
                    ("GRO-302", "8934506", "Extra Virgin Olive Oil 750ml", "Groceries", 2100.00m, 2950.00m, 14, 8, "Btl", "Cold pressed Mediterranean olive oil."),
                    ("GRO-303", "8934507", "Organic Rolled Oats 500g", "Groceries", 480.00m, 720.00m, 32, 12, "Pkt", "Whole grain dietary fiber oats."),
                    ("SNK-401", "8934508", "Dark Chocolate 70% 100g", "Snacks", 320.00m, 490.00m, 50, 15, "Bar", "Belgian rich cocoa chocolate bar."),
                    ("SNK-402", "8934509", "Roasted & Salted Cashews 200g", "Snacks", 750.00m, 1100.00m, 0, 10, "Pkt", "Crunchy oven roasted jumbo cashews."), // Out of stock example!
                    ("PC-501",  "8934510", "Antibacterial Hand Wash 500ml", "Personal Care", 380.00m, 580.00m, 40, 15, "Btl", "Aloe vera moisturizing hand wash."),
                    ("PC-502",  "8934511", "Herbal Nourishing Shampoo 350ml", "Personal Care", 490.00m, 750.00m, 22, 10, "Btl", "Natural botanical strengthening formula."),
                    ("ELC-601", "8934512", "AA Alkaline Batteries (4-Pack)", "Electronics", 300.00m, 480.00m, 35, 15, "Pack", "Long life alkaline power cells.")
                };

                foreach (var p in seedProducts)
                {
                    using var insertCmd = connection.CreateCommand();
                    insertCmd.Transaction = transaction;
                    insertCmd.CommandText = @"
                        INSERT INTO Products (Sku, Barcode, Name, Category, CostPrice, SellingPrice, CurrentStock, ReorderLevel, Unit, Description, IsActive, CreatedAt, UpdatedAt)
                        VALUES (@Sku, @Barcode, @Name, @Category, @CostPrice, @SellingPrice, @CurrentStock, @ReorderLevel, @Unit, @Description, 1, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
                        SELECT last_insert_rowid();
                    ";
                    insertCmd.Parameters.AddWithValue("@Sku", p.Item1);
                    insertCmd.Parameters.AddWithValue("@Barcode", p.Item2);
                    insertCmd.Parameters.AddWithValue("@Name", p.Item3);
                    insertCmd.Parameters.AddWithValue("@Category", p.Item4);
                    insertCmd.Parameters.AddWithValue("@CostPrice", p.Item5);
                    insertCmd.Parameters.AddWithValue("@SellingPrice", p.Item6);
                    insertCmd.Parameters.AddWithValue("@CurrentStock", p.Item7);
                    insertCmd.Parameters.AddWithValue("@ReorderLevel", p.Item8);
                    insertCmd.Parameters.AddWithValue("@Unit", p.Item9);
                    insertCmd.Parameters.AddWithValue("@Description", p.Item10);

                    long newProdId = (long)(insertCmd.ExecuteScalar() ?? 0);

                    // Insert initial stock movement entry
                    using var moveCmd = connection.CreateCommand();
                    moveCmd.Transaction = transaction;
                    moveCmd.CommandText = @"
                        INSERT INTO StockMovements (ProductId, ProductSku, ProductName, MovementType, QuantityChange, StockBefore, StockAfter, Reference, Notes, CreatedAt)
                        VALUES (@ProductId, @ProductSku, @ProductName, 'INITIAL', @Qty, 0, @Qty, 'INIT-SEED', 'Initial system inventory stock', CURRENT_TIMESTAMP);
                    ";
                    moveCmd.Parameters.AddWithValue("@ProductId", newProdId);
                    moveCmd.Parameters.AddWithValue("@ProductSku", p.Item1);
                    moveCmd.Parameters.AddWithValue("@ProductName", p.Item3);
                    moveCmd.Parameters.AddWithValue("@Qty", p.Item7);
                    moveCmd.ExecuteNonQuery();
                }

                // Also seed one sample transaction for immediate testing of transaction history & dashboard
                using var saleCmd = connection.CreateCommand();
                saleCmd.Transaction = transaction;
                saleCmd.CommandText = @"
                    INSERT INTO Sales (InvoiceNumber, SaleDate, Subtotal, DiscountPercentage, DiscountAmount, TaxPercentage, TaxAmount, GrandTotal, PaymentMethod, AmountTendered, ChangeDue, CashierName, Notes)
                    VALUES ('INV-20260908-0001', datetime('now', '-2 hours'), 3900.00, 5.0, 195.00, 0, 0, 3705.00, 'Cash', 4000.00, 295.00, 'Cashier 01', 'Walk-in customer checkout');
                    SELECT last_insert_rowid();
                ";
                long saleId = (long)(saleCmd.ExecuteScalar() ?? 0);

                // Add sale items
                using var itemCmd1 = connection.CreateCommand();
                itemCmd1.Transaction = transaction;
                itemCmd1.CommandText = @"
                    INSERT INTO SaleItems (SaleId, ProductId, ProductSku, ProductName, UnitCost, UnitPrice, Quantity, DiscountAmount, TotalPrice)
                    VALUES (@SaleId, 1, 'BEV-101', 'Ceylon Premium Black Tea 100g', 350.00, 520.00, 2, 0, 1040.00);
                ";
                itemCmd1.Parameters.AddWithValue("@SaleId", saleId);
                itemCmd1.ExecuteNonQuery();

                using var itemCmd2 = connection.CreateCommand();
                itemCmd2.Transaction = transaction;
                itemCmd2.CommandText = @"
                    INSERT INTO SaleItems (SaleId, ProductId, ProductSku, ProductName, UnitCost, UnitPrice, Quantity, DiscountAmount, TotalPrice)
                    VALUES (@SaleId, 5, 'GRO-301', 'Basmati Fragrant Rice 5kg', 1850.00, 2450.00, 1, 0, 2450.00);
                ";
                itemCmd2.Parameters.AddWithValue("@SaleId", saleId);
                itemCmd2.ExecuteNonQuery();

                using var itemCmd3 = connection.CreateCommand();
                itemCmd3.Transaction = transaction;
                itemCmd3.CommandText = @"
                    INSERT INTO SaleItems (SaleId, ProductId, ProductSku, ProductName, UnitCost, UnitPrice, Quantity, DiscountAmount, TotalPrice)
                    VALUES (@SaleId, 8, 'SNK-401', 'Dark Chocolate 70% 100g', 320.00, 490.00, 1, 0, 490.00);
                ";
                itemCmd3.Parameters.AddWithValue("@SaleId", saleId);
                itemCmd3.ExecuteNonQuery();

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }
}
