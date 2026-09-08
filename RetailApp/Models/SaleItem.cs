using System;

namespace RetailApp.Models
{
    public class SaleItem
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        public int ProductId { get; set; }
        public string ProductSku { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal UnitCost { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; } = 1;
        public decimal DiscountAmount { get; set; } = 0m;
        public decimal TotalPrice => Math.Round((UnitPrice * Quantity) - DiscountAmount, 2);
    }
}
