using System;
using System.Collections.Generic;
using System.Linq;

namespace RetailApp.Models
{
    public class Sale
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime SaleDate { get; set; } = DateTime.Now;
        public decimal Subtotal { get; set; }
        public decimal DiscountPercentage { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxPercentage { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
        public decimal AmountTendered { get; set; }
        public decimal ChangeDue { get; set; }
        public string CashierName { get; set; } = "Cashier 01";
        public string Notes { get; set; } = string.Empty;

        public List<SaleItem> Items { get; set; } = new();

        public int TotalItemsCount => Items?.Sum(i => i.Quantity) ?? 0;
        public int UniqueItemsCount => Items?.Count ?? 0;
    }
}
