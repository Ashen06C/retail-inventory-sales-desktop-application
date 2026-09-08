using System;
using System.Windows;
using System.Windows.Controls;
using RetailApp.Models;
using RetailApp.Services;

namespace RetailApp.Dialogs
{
    public partial class StockAdjustWindow : Window
    {
        private readonly Product _product;
        private readonly StockService _stockService;
        private readonly bool _isRestock;

        public StockAdjustWindow(Product product, StockService stockService, bool isRestock)
        {
            InitializeComponent();
            _product = product;
            _stockService = stockService;
            _isRestock = isRestock;

            PopulateInitialData();
        }

        private void PopulateInitialData()
        {
            TxtProdName.Text = _product.Name;
            TxtProdSku.Text = $"SKU: {_product.Sku}  •  Category: {_product.Category}";
            TxtCurrentStock.Text = $"{_product.CurrentStock} {_product.Unit}";

            if (_isRestock)
            {
                TxtTitle.Text = "Receive / Restock Inventory";
                TxtSubtitle.Text = "Add newly purchased or received stock to the warehouse/store inventory.";
                LblQuantity.Text = "Quantity to Add (+)";
                BtnSubmit.Content = "Confirm Restock";
                BtnSubmit.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129)); // Emerald

                CmbMovementType.Items.Add("RESTOCK - Goods Received / Purchase Order");
                CmbMovementType.Items.Add("ADJUSTMENT_IN - Inventory Count Overage");
                CmbMovementType.SelectedIndex = 0;

                TxtReference.Text = $"PO-{DateTime.Now:yyyyMMdd}";
                TxtNotes.Text = "Standard supplier delivery restock.";
            }
            else
            {
                TxtTitle.Text = "Stock Adjustment & Write-Off";
                TxtSubtitle.Text = "Record damaged, expired, or inventory discrepancies with compulsory audit trail.";
                LblQuantity.Text = "Quantity to Remove (-)";
                BtnSubmit.Content = "Confirm Reduction";
                BtnSubmit.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)); // Red

                CmbMovementType.Items.Add("ADJUSTMENT_OUT - Damaged Goods");
                CmbMovementType.Items.Add("ADJUSTMENT_OUT - Expired Stock");
                CmbMovementType.Items.Add("ADJUSTMENT_OUT - Discrepancy / Loss");
                CmbMovementType.Items.Add("ADJUSTMENT_OUT - Internal Store Use");
                CmbMovementType.SelectedIndex = 0;

                TxtReference.Text = $"ADJ-{DateTime.Now:yyyyMMdd}";
                TxtNotes.Text = "Damaged during handling in transit.";
            }

            TxtQuantity.Text = "1";
            UpdateResultingStock();
        }

        private void TxtQuantity_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateResultingStock();
        }

        private void CmbMovementType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateResultingStock();
        }

        private void UpdateResultingStock()
        {
            if (TxtResultingStock == null) return;

            if (int.TryParse(TxtQuantity.Text.Trim(), out int qty) && qty > 0)
            {
                int delta = _isRestock ? qty : -qty;
                int resulting = _product.CurrentStock + delta;
                TxtResultingStock.Text = $"{resulting} {_product.Unit}";
                TxtResultingStock.Foreground = resulting < 0
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(13, 32, 64));
            }
            else
            {
                TxtResultingStock.Text = $"{_product.CurrentStock} {_product.Unit}";
            }
        }

        private void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            BorderError.Visibility = Visibility.Collapsed;

            if (!int.TryParse(TxtQuantity.Text.Trim(), out int qty) || qty <= 0)
            {
                ShowError("Please enter a valid positive integer quantity.");
                TxtQuantity.Focus();
                return;
            }

            int change = _isRestock ? qty : -qty;
            if (_product.CurrentStock + change < 0)
            {
                ShowError($"Cannot reduce stock by {qty}. Current stock is only {_product.CurrentStock} {_product.Unit}.");
                TxtQuantity.Focus();
                return;
            }

            string notes = TxtNotes.Text.Trim();
            if (string.IsNullOrWhiteSpace(notes))
            {
                ShowError("Please enter an explanation / audit note for this inventory adjustment.");
                TxtNotes.Focus();
                return;
            }

            string rawType = CmbMovementType.SelectedItem?.ToString() ?? "RESTOCK";
            string movementType = rawType.StartsWith("ADJUSTMENT_OUT")
                ? "ADJUSTMENT_OUT"
                : (rawType.StartsWith("ADJUSTMENT_IN") ? "ADJUSTMENT_IN" : "RESTOCK");

            string reference = TxtReference.Text.Trim();

            try
            {
                _stockService.AdjustStock(_product.Id, change, movementType, reference, notes);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ShowError($"Failed to apply adjustment: {ex.Message}");
            }
        }

        private void ShowError(string message)
        {
            TxtErrorMessage.Text = message;
            BorderError.Visibility = Visibility.Visible;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
