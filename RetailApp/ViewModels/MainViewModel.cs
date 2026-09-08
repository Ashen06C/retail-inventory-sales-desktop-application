using System;
using System.Windows.Input;
using System.Windows.Threading;
using RetailApp.Helpers;
using RetailApp.Services;

namespace RetailApp.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private readonly ProductService _productService;
        private readonly SalesService _salesService;
        private readonly StockService _stockService;
        private readonly ReportService _reportService;

        private object _currentView = null!;
        private string _activeViewName = "Dashboard";
        private string _currentTimeString = string.Empty;
        private int _lowStockAlertCount = 0;
        private string _statusMessage = "Ready";

        public object CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public string ActiveViewName
        {
            get => _activeViewName;
            set => SetProperty(ref _activeViewName, value);
        }

        public string CurrentTimeString
        {
            get => _currentTimeString;
            set => SetProperty(ref _currentTimeString, value);
        }

        public int LowStockAlertCount
        {
            get => _lowStockAlertCount;
            set => SetProperty(ref _lowStockAlertCount, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // Sub-ViewModels
        public DashboardViewModel DashboardVM { get; }
        public SalesViewModel SalesVM { get; }
        public ProductViewModel ProductVM { get; }
        public StockViewModel StockVM { get; }
        public HistoryViewModel HistoryVM { get; }

        public ICommand NavigateCommand { get; }
        public ICommand ViewAlertsCommand { get; }

        public MainViewModel()
        {
            _productService = new ProductService();
            _salesService = new SalesService();
            _stockService = new StockService();
            _reportService = new ReportService();

            DashboardVM = new DashboardViewModel(Navigate, _reportService);
            SalesVM = new SalesViewModel(_productService, _salesService);
            ProductVM = new ProductViewModel(_productService);
            StockVM = new StockViewModel(_productService, _stockService, _reportService);
            HistoryVM = new HistoryViewModel(_salesService, _reportService);

            NavigateCommand = new RelayCommand(p => Navigate(p?.ToString() ?? "Dashboard"));
            ViewAlertsCommand = new RelayCommand(() =>
            {
                Navigate("Stock");
                StockVM.SelectedStockFilter = "Low Stock";
            });

            // Initialize clock
            var clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            clockTimer.Tick += (s, e) =>
            {
                CurrentTimeString = DateTime.Now.ToString("dddd, dd MMM yyyy  •  hh:mm:ss tt");
            };
            clockTimer.Start();
            CurrentTimeString = DateTime.Now.ToString("dddd, dd MMM yyyy  •  hh:mm:ss tt");

            // Set initial view
            Navigate("Dashboard");
            RefreshAlertCount();
        }

        public void Navigate(string viewName)
        {
            ActiveViewName = viewName;
            switch (viewName)
            {
                case "Dashboard":
                    DashboardVM.LoadData();
                    CurrentView = DashboardVM;
                    StatusMessage = "Executive Dashboard & Operational Overview";
                    break;
                case "Sales":
                    SalesVM.LoadCatalog();
                    CurrentView = SalesVM;
                    StatusMessage = "Sales Screen • POS Checkout Ready";
                    break;
                case "Products":
                    ProductVM.LoadProducts();
                    CurrentView = ProductVM;
                    StatusMessage = "Product Catalog & Master Data Management";
                    break;
                case "Stock":
                    StockVM.LoadInventory();
                    StockVM.LoadMovements();
                    CurrentView = StockVM;
                    StatusMessage = "Stock Management & Inventory Movement Audit";
                    break;
                case "History":
                    HistoryVM.LoadSales();
                    CurrentView = HistoryVM;
                    StatusMessage = "Transaction Sales History & Receipts";
                    break;
                default:
                    CurrentView = DashboardVM;
                    break;
            }

            RefreshAlertCount();
        }

        public void RefreshAlertCount()
        {
            try
            {
                var lowList = _stockService.GetLowStockProducts();
                LowStockAlertCount = lowList.Count;
            }
            catch
            {
                LowStockAlertCount = 0;
            }
        }
    }
}
