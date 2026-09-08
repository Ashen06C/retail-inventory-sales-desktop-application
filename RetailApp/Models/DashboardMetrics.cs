using System;
using System.Collections.Generic;

namespace RetailApp.Models
{
    public class TopSellingProductItem
    {
        public int ProductId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int TotalSoldQty { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class DashboardMetrics
    {
        public decimal TodaySalesTotal { get; set; }
        public int TodayTransactionsCount { get; set; }
        public int TodayItemsSold { get; set; }
        public int TotalProductsCount { get; set; }
        public decimal TotalInventoryCostValue { get; set; }
        public decimal TotalInventoryRetailValue { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
        public List<TopSellingProductItem> TopProducts { get; set; } = new();
        public List<Sale> RecentSales { get; set; } = new();
        public List<Product> LowStockProducts { get; set; } = new();
    }
}
