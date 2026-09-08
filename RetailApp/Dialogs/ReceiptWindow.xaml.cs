using System;
using System.Windows;
using System.Windows.Controls;
using RetailApp.Models;

namespace RetailApp.Dialogs
{
    public partial class ReceiptWindow : Window
    {
        private readonly Sale _sale;

        public ReceiptWindow(Sale sale)
        {
            InitializeComponent();
            _sale = sale;

            PopulateReceipt();
        }

        private void PopulateReceipt()
        {
            TxtInvoiceNum.Text = _sale.InvoiceNumber;
            TxtDateTime.Text = _sale.SaleDate.ToString("dd MMM yyyy hh:mm tt");
            TxtCashier.Text = $"Cashier: {_sale.CashierName}";
            TxtPayMethod.Text = $"Payment: {_sale.PaymentMethod}";

            ListReceiptItems.ItemsSource = _sale.Items;

            TxtSubtotal.Text = $"Rs. {_sale.Subtotal:N2}";

            if (_sale.DiscountAmount > 0)
            {
                GridDiscount.Visibility = Visibility.Visible;
                LblDiscount.Text = $"Discount ({_sale.DiscountPercentage:N0}%)";
                TxtDiscount.Text = $"- Rs. {_sale.DiscountAmount:N2}";
            }
            else
            {
                GridDiscount.Visibility = Visibility.Collapsed;
            }

            if (_sale.TaxAmount > 0)
            {
                GridTax.Visibility = Visibility.Visible;
                LblTax.Text = $"Tax ({_sale.TaxPercentage:N0}%)";
                TxtTax.Text = $"+ Rs. {_sale.TaxAmount:N2}";
            }
            else
            {
                GridTax.Visibility = Visibility.Collapsed;
            }

            TxtGrandTotal.Text = $"Rs. {_sale.GrandTotal:N2}";
            TxtTendered.Text = $"Rs. {_sale.AmountTendered:N2}";
            TxtChange.Text = $"Rs. {_sale.ChangeDue:N2}";
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    printDialog.PrintVisual(ReceiptBorder, $"Receipt - {_sale.InvoiceNumber}");
                    MessageBox.Show("Receipt sent to printer.", "Printing", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to print receipt: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
