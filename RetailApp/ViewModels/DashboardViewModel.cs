using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using RetailApp.Helpers;
using RetailApp.Models;
using RetailApp.Services;
using Microsoft.Win32;

namespace RetailApp.ViewModels
{
    public class DashboardViewModel : BaseViewModel
    {
        private readonly ReportService _reportService;
        private readonly Action<string> _navigateAction;

        private decimal _todaySalesTotal;
        private int _todayTransactionsCount;
        private int _todayItemsSold;
        private int _totalProductsCount;
        private decimal _totalInventoryCostValue;
        private decimal _totalInventoryRetailValue;
        private int _lowStockCount;
        private int _outOfStockCount;

        public decimal TodaySalesTotal { get => _todaySalesTotal; set => SetProperty(ref _todaySalesTotal, value); }
        public int TodayTransactionsCount { get => _todayTransactionsCount; set => SetProperty(ref _todayTransactionsCount, value); }
        public int TodayItemsSold { get => _todayItemsSold; set => SetProperty(ref _todayItemsSold, value); }
        public int TotalProductsCount { get => _totalProductsCount; set => SetProperty(ref _totalProductsCount, value); }
        public decimal TotalInventoryCostValue { get => _totalInventoryCostValue; set => SetProperty(ref _totalInventoryCostValue, value); }
        public decimal TotalInventoryRetailValue { get => _totalInventoryRetailValue; set => SetProperty(ref _totalInventoryRetailValue, value); }
        public int LowStockCount { get => _lowStockCount; set => SetProperty(ref _lowStockCount, value); }
        public int OutOfStockCount { get => _outOfStockCount; set => SetProperty(ref _outOfStockCount, value); }

        public ObservableCollection<TopSellingProductItem> TopProducts { get; } = new();
        public ObservableCollection<Sale> RecentSales { get; } = new();
        public ObservableCollection<Product> LowStockProducts { get; } = new();

        public ICommand RefreshCommand { get; }
        public ICommand QuickPosCommand { get; }
        public ICommand QuickStockCommand { get; }
        public ICommand QuickProductsCommand { get; }
        public ICommand ExportReportCommand { get; }

        public DashboardViewModel(Action<string> navigateAction, ReportService? reportService = null)
        {
            _navigateAction = navigateAction;
            _reportService = reportService ?? new ReportService();

            RefreshCommand = new RelayCommand(LoadData);
            QuickPosCommand = new RelayCommand(() => _navigateAction("Sales"));
            QuickStockCommand = new RelayCommand(() => _navigateAction("Stock"));
            QuickProductsCommand = new RelayCommand(() => _navigateAction("Products"));
            ExportReportCommand = new RelayCommand(ExportInventoryReport);

            LoadData();
        }

        public void LoadData()
        {
            try
            {
                var metrics = _reportService.GetDashboardMetrics();

                TodaySalesTotal = metrics.TodaySalesTotal;
                TodayTransactionsCount = metrics.TodayTransactionsCount;
                TodayItemsSold = metrics.TodayItemsSold;
                TotalProductsCount = metrics.TotalProductsCount;
                TotalInventoryCostValue = metrics.TotalInventoryCostValue;
                TotalInventoryRetailValue = metrics.TotalInventoryRetailValue;
                LowStockCount = metrics.LowStockCount;
                OutOfStockCount = metrics.OutOfStockCount;

                TopProducts.Clear();
                foreach (var tp in metrics.TopProducts)
                    TopProducts.Add(tp);

                RecentSales.Clear();
                foreach (var s in metrics.RecentSales)
                    RecentSales.Add(s);

                LowStockProducts.Clear();
                foreach (var lp in metrics.LowStockProducts)
                    LowStockProducts.Add(lp);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error refreshing dashboard metrics: {ex.Message}", "Dashboard Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportInventoryReport()
        {
            try
            {
                var sfd = new SaveFileDialog
                {
                    Filter = "CSV File (*.csv)|*.csv",
                    FileName = $"Retail_Inventory_Report_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                    Title = "Export Inventory Report"
                };

                if (sfd.ShowDialog() == true)
                {
                    _reportService.ExportStockReportToCsv(sfd.FileName);
                    MessageBox.Show($"Inventory report exported successfully to:\n{sfd.FileName}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export inventory report: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
