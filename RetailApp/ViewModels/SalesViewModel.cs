using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using RetailApp.Dialogs;
using RetailApp.Helpers;
using RetailApp.Models;
using RetailApp.Services;

namespace RetailApp.ViewModels
{
    public class SalesViewModel : BaseViewModel
    {
        private readonly ProductService _productService;
        private readonly SalesService _salesService;

        private string _productSearchText = string.Empty;
        private string _selectedCategory = "All Categories";
        private Product? _selectedCatalogProduct;
        private SaleItem? _selectedCartItem;

        private decimal _discountPercentage = 0m;
        private decimal _discountAmount = 0m;
        private decimal _taxPercentage = 0m;
        private decimal _taxAmount = 0m;
        private decimal _subtotal = 0m;
        private decimal _grandTotal = 0m;

        private string _paymentMethod = "Cash";
        private decimal _amountTendered = 0m;
        private decimal _changeDue = 0m;
        private string _cashierName = "Cashier 01";
        private string _notes = string.Empty;

        public string ProductSearchText
        {
            get => _productSearchText;
            set
            {
                if (SetProperty(ref _productSearchText, value))
                {
                    FilterCatalog();
                }
            }
        }

        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    FilterCatalog();
                }
            }
        }

        public Product? SelectedCatalogProduct
        {
            get => _selectedCatalogProduct;
            set => SetProperty(ref _selectedCatalogProduct, value);
        }

        public SaleItem? SelectedCartItem
        {
            get => _selectedCartItem;
            set => SetProperty(ref _selectedCartItem, value);
        }

        public decimal Subtotal { get => _subtotal; private set => SetProperty(ref _subtotal, value); }

        public decimal DiscountPercentage
        {
            get => _discountPercentage;
            set
            {
                if (SetProperty(ref _discountPercentage, Math.Max(0, Math.Min(100, value))))
                {
                    RecalculateTotals();
                }
            }
        }

        public decimal DiscountAmount { get => _discountAmount; private set => SetProperty(ref _discountAmount, value); }

        public decimal TaxPercentage
        {
            get => _taxPercentage;
            set
            {
                if (SetProperty(ref _taxPercentage, Math.Max(0, value)))
                {
                    RecalculateTotals();
                }
            }
        }

        public decimal TaxAmount { get => _taxAmount; private set => SetProperty(ref _taxAmount, value); }
        public decimal GrandTotal { get => _grandTotal; private set => SetProperty(ref _grandTotal, value); }

        public string PaymentMethod
        {
            get => _paymentMethod;
            set
            {
                if (SetProperty(ref _paymentMethod, value))
                {
                    if (value != "Cash")
                    {
                        AmountTendered = GrandTotal;
                    }
                    RecalculateChange();
                }
            }
        }

        public decimal AmountTendered
        {
            get => _amountTendered;
            set
            {
                if (SetProperty(ref _amountTendered, Math.Max(0, value)))
                {
                    RecalculateChange();
                }
            }
        }

        public decimal ChangeDue { get => _changeDue; private set => SetProperty(ref _changeDue, value); }
        public string CashierName { get => _cashierName; set => SetProperty(ref _cashierName, value); }
        public string Notes { get => _notes; set => SetProperty(ref _notes, value); }

        public ObservableCollection<Product> CatalogProducts { get; } = new();
        public ObservableCollection<string> Categories { get; } = new();
        public ObservableCollection<SaleItem> CartItems { get; } = new();

        public ICommand AddToCartCommand { get; }
        public ICommand RemoveFromCartCommand { get; }
        public ICommand IncrementQuantityCommand { get; }
        public ICommand DecrementQuantityCommand { get; }
        public ICommand ClearCartCommand { get; }
        public ICommand CompleteSaleCommand { get; }
        public ICommand SetExactCashCommand { get; }
        public ICommand AddTenderedCashCommand { get; }
        public ICommand RefreshCatalogCommand { get; }

        public SalesViewModel(ProductService? productService = null, SalesService? salesService = null)
        {
            _productService = productService ?? new ProductService();
            _salesService = salesService ?? new SalesService();

            AddToCartCommand = new RelayCommand(p => ExecuteAddToCart(p as Product));
            RemoveFromCartCommand = new RelayCommand(i => ExecuteRemoveFromCart(i as SaleItem));
            IncrementQuantityCommand = new RelayCommand(i => ExecuteIncrementQuantity(i as SaleItem));
            DecrementQuantityCommand = new RelayCommand(i => ExecuteDecrementQuantity(i as SaleItem));
            ClearCartCommand = new RelayCommand(ExecuteClearCart, () => CartItems.Count > 0);
            CompleteSaleCommand = new RelayCommand(ExecuteCompleteSale, () => CartItems.Count > 0);
            SetExactCashCommand = new RelayCommand(() => AmountTendered = GrandTotal);
            AddTenderedCashCommand = new RelayCommand(amt =>
            {
                if (decimal.TryParse(amt?.ToString(), out decimal val))
                {
                    AmountTendered += val;
                }
            });
            RefreshCatalogCommand = new RelayCommand(LoadCatalog);

            LoadCategories();
            LoadCatalog();
        }

        public void LoadCategories()
        {
            Categories.Clear();
            Categories.Add("All Categories");
            foreach (var c in _productService.GetCategories())
            {
                Categories.Add(c);
            }
        }

        public void LoadCatalog()
        {
            FilterCatalog();
        }

        private void FilterCatalog()
        {
            try
            {
                var list = _productService.GetAllProducts(
                    string.IsNullOrWhiteSpace(ProductSearchText) ? null : ProductSearchText,
                    SelectedCategory == "All Categories" ? null : SelectedCategory,
                    activeOnly: true
                );

                CatalogProducts.Clear();
                foreach (var p in list)
                {
                    CatalogProducts.Add(p);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load product catalog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteAddToCart(Product? product)
        {
            product ??= SelectedCatalogProduct;
            if (product == null) return;

            if (product.CurrentStock <= 0)
            {
                MessageBox.Show($"Product '{product.Name}' is OUT OF STOCK. Cannot add to sale.", "Out of Stock", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var existingItem = CartItems.FirstOrDefault(i => i.ProductId == product.Id);
            if (existingItem != null)
            {
                if (existingItem.Quantity + 1 > product.CurrentStock)
                {
                    MessageBox.Show($"Cannot add more '{product.Name}'. Available on hand: {product.CurrentStock}.", "Stock Limit Reached", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                existingItem.Quantity++;
                // Refresh item in collection for UI binding update
                int index = CartItems.IndexOf(existingItem);
                CartItems[index] = existingItem;
            }
            else
            {
                CartItems.Add(new SaleItem
                {
                    ProductId = product.Id,
                    ProductSku = product.Sku,
                    ProductName = product.Name,
                    UnitCost = product.CostPrice,
                    UnitPrice = product.SellingPrice,
                    Quantity = 1,
                    DiscountAmount = 0
                });
            }

            RecalculateTotals();
        }

        private void ExecuteIncrementQuantity(SaleItem? item)
        {
            if (item == null) return;

            var product = _productService.GetProductById(item.ProductId);
            if (product == null) return;

            if (item.Quantity + 1 > product.CurrentStock)
            {
                MessageBox.Show($"Maximum available stock for '{product.Name}' is {product.CurrentStock}.", "Stock Limit", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            item.Quantity++;
            int index = CartItems.IndexOf(item);
            CartItems[index] = item;
            RecalculateTotals();
        }

        private void ExecuteDecrementQuantity(SaleItem? item)
        {
            if (item == null) return;

            if (item.Quantity > 1)
            {
                item.Quantity--;
                int index = CartItems.IndexOf(item);
                CartItems[index] = item;
            }
            else
            {
                CartItems.Remove(item);
            }

            RecalculateTotals();
        }

        private void ExecuteRemoveFromCart(SaleItem? item)
        {
            if (item != null)
            {
                CartItems.Remove(item);
                RecalculateTotals();
            }
        }

        private void ExecuteClearCart()
        {
            if (CartItems.Count == 0) return;
            var res = MessageBox.Show("Are you sure you want to clear all items in the cart?", "Clear Cart", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                CartItems.Clear();
                DiscountPercentage = 0;
                TaxPercentage = 0;
                AmountTendered = 0;
                Notes = string.Empty;
                RecalculateTotals();
            }
        }

        private void RecalculateTotals()
        {
            Subtotal = CartItems.Sum(i => i.TotalPrice);

            DiscountAmount = Math.Round((Subtotal * DiscountPercentage) / 100m, 2);
            decimal afterDiscount = Subtotal - DiscountAmount;

            TaxAmount = Math.Round((afterDiscount * TaxPercentage) / 100m, 2);
            GrandTotal = Math.Round(afterDiscount + TaxAmount, 2);

            if (PaymentMethod != "Cash")
            {
                AmountTendered = GrandTotal;
            }

            RecalculateChange();
        }

        private void RecalculateChange()
        {
            if (PaymentMethod == "Cash")
            {
                ChangeDue = Math.Max(0, AmountTendered - GrandTotal);
            }
            else
            {
                ChangeDue = 0m;
            }
        }

        private void ExecuteCompleteSale()
        {
            if (CartItems.Count == 0)
            {
                MessageBox.Show("Please add at least one product to the cart before completing checkout.", "Cart Empty", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (PaymentMethod == "Cash" && AmountTendered < GrandTotal)
            {
                decimal shortage = GrandTotal - AmountTendered;
                MessageBox.Show($"Amount tendered is insufficient! Short by Rs. {shortage:N2}.", "Payment Incomplete", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var sale = new Sale
            {
                Subtotal = this.Subtotal,
                DiscountPercentage = this.DiscountPercentage,
                DiscountAmount = this.DiscountAmount,
                TaxPercentage = this.TaxPercentage,
                TaxAmount = this.TaxAmount,
                GrandTotal = this.GrandTotal,
                PaymentMethod = this.PaymentMethod,
                AmountTendered = this.PaymentMethod == "Cash" ? this.AmountTendered : this.GrandTotal,
                ChangeDue = this.ChangeDue,
                CashierName = this.CashierName,
                Notes = this.Notes,
                Items = this.CartItems.ToList()
            };

            try
            {
                var completedSale = _salesService.ProcessSale(sale);

                // Show Receipt Preview Dialog
                var receiptWindow = new ReceiptWindow(completedSale);
                receiptWindow.ShowDialog();

                // Reset POS state
                CartItems.Clear();
                DiscountPercentage = 0;
                TaxPercentage = 0;
                AmountTendered = 0;
                Notes = string.Empty;
                RecalculateTotals();

                // Refresh catalog to reflect updated on-hand stock!
                LoadCatalog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Transaction Failed: {ex.Message}", "Checkout Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
