using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using RetailApp.Dialogs;
using RetailApp.Helpers;
using RetailApp.Models;
using RetailApp.Services;
using Microsoft.Win32;

namespace RetailApp.ViewModels
{
    public class StockViewModel : BaseViewModel
    {
        private readonly ProductService _productService;
        private readonly StockService _stockService;
        private readonly ReportService _reportService;

        private string _searchText = string.Empty;
        private string _selectedCategory = "All Categories";
        private string _selectedStockFilter = "All Items"; // All Items, Low Stock, Out of Stock
        private Product? _selectedProduct;

        private int _totalItemsCount;
        private int _lowStockCount;
        private int _outOfStockCount;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    LoadInventory();
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
                    LoadInventory();
                }
            }
        }

        public string SelectedStockFilter
        {
            get => _selectedStockFilter;
            set
            {
                if (SetProperty(ref _selectedStockFilter, value))
                {
                    LoadInventory();
                }
            }
        }

        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set => SetProperty(ref _selectedProduct, value);
        }

        public int TotalItemsCount { get => _totalItemsCount; set => SetProperty(ref _totalItemsCount, value); }
        public int LowStockCount { get => _lowStockCount; set => SetProperty(ref _lowStockCount, value); }
        public int OutOfStockCount { get => _outOfStockCount; set => SetProperty(ref _outOfStockCount, value); }

        public ObservableCollection<Product> InventoryProducts { get; } = new();
        public ObservableCollection<string> Categories { get; } = new();
        public ObservableCollection<StockMovement> StockMovements { get; } = new();

        public ICommand RefreshCommand { get; }
        public ICommand RestockCommand { get; }
        public ICommand AdjustStockCommand { get; }
        public ICommand ExportStockCsvCommand { get; }
        public ICommand FilterLowStockCommand { get; }

        public StockViewModel(ProductService? productService = null, StockService? stockService = null, ReportService? reportService = null)
        {
            _productService = productService ?? new ProductService();
            _stockService = stockService ?? new StockService();
            _reportService = reportService ?? new ReportService();

            RefreshCommand = new RelayCommand(() =>
            {
                LoadCategories();
                LoadInventory();
                LoadMovements();
            });

            RestockCommand = new RelayCommand(ExecuteRestock, () => SelectedProduct != null);
            AdjustStockCommand = new RelayCommand(ExecuteAdjustStock, () => SelectedProduct != null);
            ExportStockCsvCommand = new RelayCommand(ExecuteExportCsv);
            FilterLowStockCommand = new RelayCommand(() =>
            {
                SelectedStockFilter = "Low Stock";
            });

            LoadCategories();
            LoadInventory();
            LoadMovements();
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

        public void LoadInventory()
        {
            try
            {
                var allProds = _productService.GetAllProducts(
                    string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
                    SelectedCategory == "All Categories" ? null : SelectedCategory,
                    activeOnly: false
                );

                // Calculate summary counts across whole active inventory
                var rawActive = _productService.GetAllProducts(activeOnly: true);
                TotalItemsCount = rawActive.Count;
                LowStockCount = rawActive.Count(p => p.CurrentStock > 0 && p.CurrentStock <= p.ReorderLevel);
                OutOfStockCount = rawActive.Count(p => p.CurrentStock <= 0);

                // Apply Stock Status Filter
                var filtered = allProds.AsEnumerable();
                if (SelectedStockFilter == "Low Stock")
                {
                    filtered = filtered.Where(p => p.CurrentStock > 0 && p.CurrentStock <= p.ReorderLevel);
                }
                else if (SelectedStockFilter == "Out of Stock")
                {
                    filtered = filtered.Where(p => p.CurrentStock <= 0);
                }

                InventoryProducts.Clear();
                foreach (var p in filtered)
                {
                    InventoryProducts.Add(p);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load inventory: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void LoadMovements()
        {
            try
            {
                var list = _stockService.GetStockMovements(limit: 150);
                StockMovements.Clear();
                foreach (var m in list)
                {
                    StockMovements.Add(m);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load stock movement ledger: {ex.Message}", "Audit Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteRestock()
        {
            if (SelectedProduct == null) return;

            var dialog = new StockAdjustWindow(SelectedProduct, _stockService, isRestock: true);
            if (dialog.ShowDialog() == true)
            {
                LoadInventory();
                LoadMovements();
            }
        }

        private void ExecuteAdjustStock()
        {
            if (SelectedProduct == null) return;

            var dialog = new StockAdjustWindow(SelectedProduct, _stockService, isRestock: false);
            if (dialog.ShowDialog() == true)
            {
                LoadInventory();
                LoadMovements();
            }
        }

        private void ExecuteExportCsv()
        {
            try
            {
                var sfd = new SaveFileDialog
                {
                    Filter = "CSV File (*.csv)|*.csv",
                    FileName = $"Retail_Stock_Level_Audit_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                    Title = "Export Stock Level Audit Report"
                };

                if (sfd.ShowDialog() == true)
                {
                    _reportService.ExportStockReportToCsv(sfd.FileName);
                    MessageBox.Show($"Stock audit report exported successfully to:\n{sfd.FileName}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export stock report: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
