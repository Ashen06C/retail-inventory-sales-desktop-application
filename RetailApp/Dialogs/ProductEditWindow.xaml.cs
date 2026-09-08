using System;
using System.Windows;
using RetailApp.Models;
using RetailApp.Services;

namespace RetailApp.Dialogs
{
    public partial class ProductEditWindow : Window
    {
        private readonly Product _product;
        private readonly ProductService _productService;
        private readonly bool _isNew;

        public Product Product => _product;

        public ProductEditWindow(Product product, ProductService productService, bool isNew)
        {
            InitializeComponent();
            _product = product;
            _productService = productService;
            _isNew = isNew;

            LoadComboBoxes();
            PopulateFields();
        }

        private void LoadComboBoxes()
        {
            var categories = _productService.GetCategories();
            CmbCategory.ItemsSource = categories;

            CmbUnit.ItemsSource = new[] { "Unit", "Pcs", "Box", "Pkt", "Kg", "g", "Btl", "Can", "Pack" };
        }

        private void PopulateFields()
        {
            if (_isNew)
            {
                TxtDialogTitle.Text = "Add New Product";
                BtnSave.Content = "Add Product";
                TxtStock.IsEnabled = true;
                TxtStockHelp.Text = "Initial inventory stock level";
            }
            else
            {
                TxtDialogTitle.Text = $"Edit Product: {_product.Name}";
                BtnSave.Content = "Update Product";
                TxtStock.IsEnabled = false; // Cannot edit stock directly here; must use Stock Adjustment for audit trail!
                TxtStockHelp.Text = "Stock can only be updated via Stock Adjustments (Audit Trail)";
            }

            TxtSku.Text = _product.Sku;
            TxtBarcode.Text = _product.Barcode;
            TxtName.Text = _product.Name;
            CmbCategory.Text = _product.Category;
            CmbUnit.Text = _product.Unit;
            TxtCostPrice.Text = _product.CostPrice.ToString("F2");
            TxtSellingPrice.Text = _product.SellingPrice.ToString("F2");
            TxtStock.Text = _product.CurrentStock.ToString();
            TxtReorderLevel.Text = _product.ReorderLevel.ToString();
            TxtDescription.Text = _product.Description;
            ChkIsActive.IsChecked = _product.IsActive;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            BorderError.Visibility = Visibility.Collapsed;

            string sku = TxtSku.Text.Trim().ToUpperInvariant();
            string name = TxtName.Text.Trim();
            string barcode = TxtBarcode.Text.Trim();
            string category = string.IsNullOrWhiteSpace(CmbCategory.Text) ? "General" : CmbCategory.Text.Trim();
            string unit = string.IsNullOrWhiteSpace(CmbUnit.Text) ? "Unit" : CmbUnit.Text.Trim();
            string description = TxtDescription.Text.Trim();
            bool isActive = ChkIsActive.IsChecked == true;

            // Validations
            if (string.IsNullOrWhiteSpace(sku))
            {
                ShowError("Product SKU / Code is required.");
                TxtSku.Focus();
                return;
            }

            if (!_productService.IsSkuUnique(sku, _product.Id))
            {
                ShowError($"SKU '{sku}' is already assigned to another product. SKU must be unique.");
                TxtSku.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                ShowError("Product Name is required.");
                TxtName.Focus();
                return;
            }

            if (!decimal.TryParse(TxtCostPrice.Text.Trim(), out decimal costPrice) || costPrice < 0)
            {
                ShowError("Cost Price must be a valid non-negative number.");
                TxtCostPrice.Focus();
                return;
            }

            if (!decimal.TryParse(TxtSellingPrice.Text.Trim(), out decimal sellingPrice) || sellingPrice <= 0)
            {
                ShowError("Selling Price must be a valid positive number.");
                TxtSellingPrice.Focus();
                return;
            }

            if (sellingPrice < costPrice)
            {
                var promptResult = MessageBox.Show(
                    $"Selling price (Rs. {sellingPrice:N2}) is LOWER than cost price (Rs. {costPrice:N2}).\nAre you sure you want to sell this product at a loss?",
                    "Price Warning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (promptResult == MessageBoxResult.No)
                {
                    TxtSellingPrice.Focus();
                    return;
                }
            }

            int stock = _product.CurrentStock;
            if (_isNew)
            {
                if (!int.TryParse(TxtStock.Text.Trim(), out stock) || stock < 0)
                {
                    ShowError("Initial stock must be a valid non-negative integer.");
                    TxtStock.Focus();
                    return;
                }
            }

            if (!int.TryParse(TxtReorderLevel.Text.Trim(), out int reorderLevel) || reorderLevel < 0)
            {
                ShowError("Reorder level must be a valid non-negative integer.");
                TxtReorderLevel.Focus();
                return;
            }

            _product.Sku = sku;
            _product.Barcode = barcode;
            _product.Name = name;
            _product.Category = category;
            _product.Unit = unit;
            _product.CostPrice = Math.Round(costPrice, 2);
            _product.SellingPrice = Math.Round(sellingPrice, 2);
            _product.CurrentStock = stock;
            _product.ReorderLevel = reorderLevel;
            _product.Description = description;
            _product.IsActive = isActive;

            try
            {
                if (_isNew)
                {
                    _productService.AddProduct(_product);
                }
                else
                {
                    _productService.UpdateProduct(_product);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ShowError($"Failed to save product: {ex.Message}");
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
