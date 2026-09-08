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
    public class ProductViewModel : BaseViewModel
    {
        private readonly ProductService _productService;

        private string _searchText = string.Empty;
        private string _selectedCategory = "All Categories";
        private bool _activeOnly = true;
        private Product? _selectedProduct;
        private int _totalItemsCount;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    LoadProducts();
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
                    LoadProducts();
                }
            }
        }

        public bool ActiveOnly
        {
            get => _activeOnly;
            set
            {
                if (SetProperty(ref _activeOnly, value))
                {
                    LoadProducts();
                }
            }
        }

        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set => SetProperty(ref _selectedProduct, value);
        }

        public int TotalItemsCount
        {
            get => _totalItemsCount;
            set => SetProperty(ref _totalItemsCount, value);
        }

        public ObservableCollection<Product> Products { get; } = new();
        public ObservableCollection<string> Categories { get; } = new();

        public ICommand RefreshCommand { get; }
        public ICommand AddProductCommand { get; }
        public ICommand EditProductCommand { get; }
        public ICommand DeleteProductCommand { get; }
        public ICommand ClearFilterCommand { get; }

        public ProductViewModel(ProductService? productService = null)
        {
            _productService = productService ?? new ProductService();

            RefreshCommand = new RelayCommand(() =>
            {
                LoadCategories();
                LoadProducts();
            });

            AddProductCommand = new RelayCommand(ExecuteAddProduct);
            EditProductCommand = new RelayCommand(ExecuteEditProduct, () => SelectedProduct != null);
            DeleteProductCommand = new RelayCommand(ExecuteDeleteProduct, () => SelectedProduct != null);
            ClearFilterCommand = new RelayCommand(() =>
            {
                _searchText = string.Empty;
                OnPropertyChanged(nameof(SearchText));
                _selectedCategory = "All Categories";
                OnPropertyChanged(nameof(SelectedCategory));
                _activeOnly = true;
                OnPropertyChanged(nameof(ActiveOnly));
                LoadProducts();
            });

            LoadCategories();
            LoadProducts();
        }

        public void LoadCategories()
        {
            Categories.Clear();
            Categories.Add("All Categories");
            var catList = _productService.GetCategories();
            foreach (var c in catList)
            {
                Categories.Add(c);
            }
        }

        public void LoadProducts()
        {
            try
            {
                var list = _productService.GetAllProducts(
                    string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
                    SelectedCategory == "All Categories" ? null : SelectedCategory,
                    ActiveOnly
                );

                Products.Clear();
                foreach (var p in list)
                {
                    Products.Add(p);
                }
                TotalItemsCount = Products.Count;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load products: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteAddProduct()
        {
            var newProd = new Product
            {
                Category = SelectedCategory != "All Categories" ? SelectedCategory : "General",
                Unit = "Pcs",
                ReorderLevel = 10,
                IsActive = true
            };

            var dialog = new ProductEditWindow(newProd, _productService, isNew: true);
            if (dialog.ShowDialog() == true)
            {
                LoadCategories();
                LoadProducts();
                SelectedProduct = Products.FirstOrDefault(p => p.Id == newProd.Id);
            }
        }

        private void ExecuteEditProduct()
        {
            if (SelectedProduct == null) return;

            var clone = SelectedProduct.Clone();
            var dialog = new ProductEditWindow(clone, _productService, isNew: false);
            if (dialog.ShowDialog() == true)
            {
                LoadCategories();
                LoadProducts();
                SelectedProduct = Products.FirstOrDefault(p => p.Id == clone.Id);
            }
        }

        private void ExecuteDeleteProduct()
        {
            if (SelectedProduct == null) return;

            bool canHardDelete = _productService.CanDeleteProduct(SelectedProduct.Id);
            string prompt = canHardDelete
                ? $"Are you sure you want to permanently delete product '{SelectedProduct.Name}' ({SelectedProduct.Sku})?"
                : $"Product '{SelectedProduct.Name}' has existing transaction sales records.\n\nTo preserve historical transaction accuracy, it will be marked INACTIVE instead of deleting historical data.\n\nProceed?";

            var result = MessageBox.Show(prompt, "Confirm Product Deletion", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _productService.DeleteProduct(SelectedProduct.Id);
                    LoadProducts();
                    SelectedProduct = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not delete product: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
