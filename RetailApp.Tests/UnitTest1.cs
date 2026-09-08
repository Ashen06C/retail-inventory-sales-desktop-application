using System;
using System.IO;
using System.Linq;
using RetailApp.Data;
using RetailApp.Models;
using RetailApp.Services;
using Xunit;

namespace RetailApp.Tests
{
    public class RetailAppUnitTests
    {
        private readonly DatabaseService _dbService;
        private readonly ProductService _productService;
        private readonly SalesService _salesService;
        private readonly StockService _stockService;
        private readonly ReportService _reportService;

        public RetailAppUnitTests()
        {
            _dbService = DatabaseService.Instance;
            _productService = new ProductService(_dbService);
            _salesService = new SalesService(_dbService);
            _stockService = new StockService(_dbService);
            _reportService = new ReportService(_dbService);
        }

        [Fact]
        public void Test_1_DatabaseInitializationAndSeedData()
        {
            var products = _productService.GetAllProducts();
            Assert.NotNull(products);
            Assert.True(products.Count >= 10, "Initial seed products should be at least 10 items.");

            var tea = products.FirstOrDefault(p => p.Sku == "BEV-101");
            Assert.NotNull(tea);
            Assert.Equal("Ceylon Premium Black Tea 100g", tea.Name);
            Assert.Equal("Beverages", tea.Category);
            Assert.True(tea.SellingPrice > tea.CostPrice);
        }

        [Fact]
        public void Test_2_ProductService_AddAndUniqueSkuValidation()
        {
            string testSku = $"TEST-SKU-{Guid.NewGuid().ToString().Substring(0, 5).ToUpper()}";

            var newProd = new Product
            {
                Sku = testSku,
                Name = "Test Organic Honey 500g",
                Category = "Groceries",
                CostPrice = 1200m,
                SellingPrice = 1650m,
                CurrentStock = 20,
                ReorderLevel = 5,
                Unit = "Jar"
            };

            int id = _productService.AddProduct(newProd);
            Assert.True(id > 0);

            // Fetch and verify
            var fetched = _productService.GetProductById(id);
            Assert.NotNull(fetched);
            Assert.Equal(testSku, fetched.Sku);
            Assert.Equal("Test Organic Honey 500g", fetched.Name);

            // Attempt duplicate SKU
            var duplicate = new Product
            {
                Sku = testSku,
                Name = "Duplicate Item",
                CostPrice = 100m,
                SellingPrice = 200m
            };

            Assert.Throws<InvalidOperationException>(() => _productService.AddProduct(duplicate));
        }

        [Fact]
        public void Test_3_ProductProfitMarginCalculation()
        {
            var prod = new Product
            {
                CostPrice = 600m,
                SellingPrice = 1000m,
                CurrentStock = 10
            };

            // Profit = (1000 - 600) / 1000 = 40.0%
            Assert.Equal(40.0m, prod.ProfitMargin);
            Assert.Equal(6000m, prod.TotalStockValue);
            Assert.Equal(10000m, prod.TotalRetailValue);
        }

        [Fact]
        public void Test_4_SalesCheckout_StockDeductionAndAuditTrail()
        {
            // Create dedicated test product for sale
            string sku = $"SALE-TST-{Guid.NewGuid().ToString().Substring(0, 5).ToUpper()}";
            var prod = new Product
            {
                Sku = sku,
                Name = "Test Canned Tuna 180g",
                Category = "Groceries",
                CostPrice = 400m,
                SellingPrice = 600m,
                CurrentStock = 15,
                ReorderLevel = 5,
                Unit = "Can"
            };
            int prodId = _productService.AddProduct(prod);

            // Perform sale of 3 units
            int qtyToBuy = 3;
            var sale = new Sale
            {
                Subtotal = 1800m,
                DiscountPercentage = 10m, // 10%
                DiscountAmount = 180m,
                GrandTotal = 1620m,
                PaymentMethod = "Cash",
                AmountTendered = 2000m,
                ChangeDue = 380m,
                CashierName = "Intern Tester",
                Notes = "Automated test sale",
                Items = new System.Collections.Generic.List<SaleItem>
                {
                    new SaleItem
                    {
                        ProductId = prodId,
                        ProductSku = sku,
                        ProductName = prod.Name,
                        UnitCost = prod.CostPrice,
                        UnitPrice = prod.SellingPrice,
                        Quantity = qtyToBuy,
                        DiscountAmount = 0m
                    }
                }
            };

            var processedSale = _salesService.ProcessSale(sale);
            Assert.NotNull(processedSale.InvoiceNumber);
            Assert.StartsWith("INV-", processedSale.InvoiceNumber);

            // Verify stock was reduced from 15 to 12
            var updatedProd = _productService.GetProductById(prodId);
            Assert.NotNull(updatedProd);
            Assert.Equal(12, updatedProd.CurrentStock);

            // Verify StockMovement entry
            var movements = _stockService.GetStockMovements(prodId, "SALE");
            Assert.NotEmpty(movements);
            var lastMove = movements.First();
            Assert.Equal(-qtyToBuy, lastMove.QuantityChange);
            Assert.Equal(15, lastMove.StockBefore);
            Assert.Equal(12, lastMove.StockAfter);
            Assert.Equal(processedSale.InvoiceNumber, lastMove.Reference);
        }

        [Fact]
        public void Test_5_SalesCheckout_PreventOverselling()
        {
            string sku = $"OVER-{Guid.NewGuid().ToString().Substring(0, 5).ToUpper()}";
            var prod = new Product
            {
                Sku = sku,
                Name = "Limited Item",
                CostPrice = 100m,
                SellingPrice = 150m,
                CurrentStock = 2,
                ReorderLevel = 1
            };
            int prodId = _productService.AddProduct(prod);

            var sale = new Sale
            {
                Subtotal = 450m,
                GrandTotal = 450m,
                AmountTendered = 500m,
                Items = new System.Collections.Generic.List<SaleItem>
                {
                    new SaleItem
                    {
                        ProductId = prodId,
                        ProductSku = sku,
                        ProductName = prod.Name,
                        UnitPrice = 150m,
                        Quantity = 5 // Exceeds available stock (2)
                    }
                }
            };

            var ex = Assert.Throws<InvalidOperationException>(() => _salesService.ProcessSale(sale));
            Assert.Contains("Insufficient stock", ex.Message);
        }

        [Fact]
        public void Test_6_StockService_RestockAndDamageAdjustments()
        {
            string sku = $"STK-{Guid.NewGuid().ToString().Substring(0, 5).ToUpper()}";
            var prod = new Product
            {
                Sku = sku,
                Name = "Stock Audit Item",
                CostPrice = 200m,
                SellingPrice = 300m,
                CurrentStock = 10,
                ReorderLevel = 5
            };
            int prodId = _productService.AddProduct(prod);

            // 1. Restock +10
            _stockService.AdjustStock(prodId, 10, "RESTOCK", "PO-TEST-100", "Supplier delivery batch");
            var p1 = _productService.GetProductById(prodId);
            Assert.Equal(20, p1!.CurrentStock);

            // 2. Damage write-off -2
            _stockService.AdjustStock(prodId, -2, "ADJUSTMENT_OUT", "DMG-TEST-01", "Damaged box write off");
            var p2 = _productService.GetProductById(prodId);
            Assert.Equal(18, p2!.CurrentStock);

            // 3. Attempt reduction below zero
            var ex = Assert.Throws<InvalidOperationException>(() =>
                _stockService.AdjustStock(prodId, -25, "ADJUSTMENT_OUT", "ERR-01", "Should fail"));
            Assert.Contains("below zero", ex.Message);
        }

        [Fact]
        public void Test_7_ReportService_MetricsAndCsvExport()
        {
            var metrics = _reportService.GetDashboardMetrics();
            Assert.NotNull(metrics);
            Assert.True(metrics.TotalProductsCount > 0);

            // Test CSV exports
            string tempDir = Path.GetTempPath();
            string salesCsvPath = Path.Combine(tempDir, $"Test_Sales_{Guid.NewGuid():N}.csv");
            string stockCsvPath = Path.Combine(tempDir, $"Test_Stock_{Guid.NewGuid():N}.csv");

            try
            {
                _reportService.ExportSalesToCsv(salesCsvPath);
                Assert.True(File.Exists(salesCsvPath));
                string salesCsvContent = File.ReadAllText(salesCsvPath);
                Assert.Contains("Invoice Number,Date & Time,Cashier", salesCsvContent);

                _reportService.ExportStockReportToCsv(stockCsvPath);
                Assert.True(File.Exists(stockCsvPath));
                string stockCsvContent = File.ReadAllText(stockCsvPath);
                Assert.Contains("SKU,Barcode,Product Name,Category", stockCsvContent);
            }
            finally
            {
                if (File.Exists(salesCsvPath)) File.Delete(salesCsvPath);
                if (File.Exists(stockCsvPath)) File.Delete(stockCsvPath);
            }
        }
    }
}