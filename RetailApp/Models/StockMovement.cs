using System;

namespace RetailApp.Models
{
    public class StockMovement
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductSku { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string MovementType { get; set; } = "RESTOCK"; // SALE, RESTOCK, ADJUSTMENT_OUT, ADJUSTMENT_IN, INITIAL
        public int QuantityChange { get; set; }
        public int StockBefore { get; set; }
        public int StockAfter { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string DisplayBadgeType
        {
            get
            {
                return MovementType switch
                {
                    "SALE" => "Sale",
                    "RESTOCK" => "Restock",
                    "ADJUSTMENT_OUT" => "Damaged/Out",
                    "ADJUSTMENT_IN" => "Adjustment (+)",
                    "INITIAL" => "Initial Setup",
                    _ => MovementType
                };
            }
        }
    }
}
