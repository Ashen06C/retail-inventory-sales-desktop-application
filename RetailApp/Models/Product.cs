using System;

namespace RetailApp.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
        public decimal CostPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public int CurrentStock { get; set; }
        public int ReorderLevel { get; set; } = 10;
        public string Unit { get; set; } = "Unit";
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Computed presentation properties
        public string StockStatus
        {
            get
            {
                if (CurrentStock <= 0) return "Out of Stock";
                if (CurrentStock <= ReorderLevel) return "Low Stock";
                return "In Stock";
            }
        }

        public decimal ProfitMargin => SellingPrice > 0 ? Math.Round(((SellingPrice - CostPrice) / SellingPrice) * 100m, 1) : 0m;
        public decimal TotalStockValue => Math.Round(CurrentStock * CostPrice, 2);
        public decimal TotalRetailValue => Math.Round(CurrentStock * SellingPrice, 2);

        public Product Clone()
        {
            return new Product
            {
                Id = this.Id,
                Sku = this.Sku,
                Barcode = this.Barcode,
                Name = this.Name,
                Category = this.Category,
                CostPrice = this.CostPrice,
                SellingPrice = this.SellingPrice,
                CurrentStock = this.CurrentStock,
                ReorderLevel = this.ReorderLevel,
                Unit = this.Unit,
                Description = this.Description,
                IsActive = this.IsActive,
                CreatedAt = this.CreatedAt,
                UpdatedAt = this.UpdatedAt
            };
        }
    }
}
