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
    public class HistoryViewModel : BaseViewModel
    {
        private readonly SalesService _salesService;
        private readonly ReportService _reportService;

        private string _searchText = string.Empty;
        private string _selectedDateFilter = "Today"; // Today, Last 7 Days, This Month, All Time
        private Sale? _selectedSale;

        private int _totalTransactionsCount;
        private decimal _totalSalesAmount;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    LoadSales();
                }
            }
        }

        public string SelectedDateFilter
        {
            get => _selectedDateFilter;
            set
            {
                if (SetProperty(ref _selectedDateFilter, value))
                {
                    LoadSales();
                }
            }
        }

        public Sale? SelectedSale
        {
            get => _selectedSale;
            set => SetProperty(ref _selectedSale, value);
        }

        public int TotalTransactionsCount { get => _totalTransactionsCount; set => SetProperty(ref _totalTransactionsCount, value); }
        public decimal TotalSalesAmount { get => _totalSalesAmount; set => SetProperty(ref _totalSalesAmount, value); }

        public ObservableCollection<Sale> SalesList { get; } = new();

        public ICommand RefreshCommand { get; }
        public ICommand ViewReceiptCommand { get; }
        public ICommand ExportSalesCsvCommand { get; }

        public HistoryViewModel(SalesService? salesService = null, ReportService? reportService = null)
        {
            _salesService = salesService ?? new SalesService();
            _reportService = reportService ?? new ReportService();

            RefreshCommand = new RelayCommand(LoadSales);
            ViewReceiptCommand = new RelayCommand(ExecuteViewReceipt, () => SelectedSale != null);
            ExportSalesCsvCommand = new RelayCommand(ExecuteExportCsv);

            LoadSales();
        }

        public void LoadSales()
        {
            try
            {
                DateTime? from = null;
                DateTime? to = null;

                DateTime today = DateTime.Today;
                switch (SelectedDateFilter)
                {
                    case "Today":
                        from = today;
                        to = today;
                        break;
                    case "Last 7 Days":
                        from = today.AddDays(-7);
                        to = today;
                        break;
                    case "This Month":
                        from = new DateTime(today.Year, today.Month, 1);
                        to = today;
                        break;
                    case "All Time":
                    default:
                        from = null;
                        to = null;
                        break;
                }

                var list = _salesService.GetAllSales(
                    from,
                    to,
                    string.IsNullOrWhiteSpace(SearchText) ? null : SearchText
                );

                SalesList.Clear();
                foreach (var s in list)
                {
                    SalesList.Add(s);
                }

                TotalTransactionsCount = SalesList.Count;
                TotalSalesAmount = SalesList.Sum(s => s.GrandTotal);

                if (SelectedSale != null)
                {
                    SelectedSale = SalesList.FirstOrDefault(s => s.Id == SelectedSale.Id) ?? SalesList.FirstOrDefault();
                }
                else
                {
                    SelectedSale = SalesList.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load sales history: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteViewReceipt()
        {
            if (SelectedSale == null) return;

            var fullSale = _salesService.GetSaleById(SelectedSale.Id) ?? SelectedSale;
            var dialog = new ReceiptWindow(fullSale);
            dialog.ShowDialog();
        }

        private void ExecuteExportCsv()
        {
            try
            {
                DateTime? from = null;
                DateTime? to = null;
                DateTime today = DateTime.Today;

                switch (SelectedDateFilter)
                {
                    case "Today":
                        from = today;
                        to = today;
                        break;
                    case "Last 7 Days":
                        from = today.AddDays(-7);
                        to = today;
                        break;
                    case "This Month":
                        from = new DateTime(today.Year, today.Month, 1);
                        to = today;
                        break;
                }

                var sfd = new SaveFileDialog
                {
                    Filter = "CSV File (*.csv)|*.csv",
                    FileName = $"Retail_Sales_Transactions_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                    Title = "Export Sales Transactions"
                };

                if (sfd.ShowDialog() == true)
                {
                    _reportService.ExportSalesToCsv(sfd.FileName, from, to);
                    MessageBox.Show($"Sales history exported successfully to:\n{sfd.FileName}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export sales history: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
