using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace RetailApp.Helpers
{
    public class CurrencyConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is decimal d)
            {
                return $"Rs. {d:N2}";
            }
            if (value is double db)
            {
                return $"Rs. {db:N2}";
            }
            if (value is int i)
            {
                return $"Rs. {i:N2}";
            }
            return "Rs. 0.00";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StockStatusColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string status = value?.ToString() ?? string.Empty;
            return status switch
            {
                "In Stock" => new SolidColorBrush(Color.FromRgb(16, 185, 129)),       // Emerald #10B981
                "Low Stock" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),      // Amber #F59E0B
                "Out of Stock" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),    // Red #EF4444
                _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))                // Slate #64748B
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class StockStatusBadgeBgConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string status = value?.ToString() ?? string.Empty;
            return status switch
            {
                "In Stock" => new SolidColorBrush(Color.FromArgb(30, 16, 185, 129)),       // Light Emerald
                "Low Stock" => new SolidColorBrush(Color.FromArgb(30, 245, 158, 11)),      // Light Amber
                "Out of Stock" => new SolidColorBrush(Color.FromArgb(30, 239, 68, 68)),    // Light Red
                _ => new SolidColorBrush(Color.FromArgb(30, 100, 116, 139))
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class MovementTypeColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string type = value?.ToString() ?? string.Empty;
            return type switch
            {
                "RESTOCK" => new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                "ADJUSTMENT_IN" => new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                "SALE" => new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                "ADJUSTMENT_OUT" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                "INITIAL" => new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isNull = value == null;
            if (value is string s) isNull = string.IsNullOrWhiteSpace(s);

            if (Invert)
            {
                return isNull ? Visibility.Visible : Visibility.Collapsed;
            }
            return isNull ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BooleanToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool b = value is bool flag && flag;
            if (Invert) b = !b;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ActiveNavBackgroundConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string current = value?.ToString() ?? string.Empty;
            string target = parameter?.ToString() ?? string.Empty;
            bool isActive = string.Equals(current, target, StringComparison.OrdinalIgnoreCase);

            return isActive 
                ? new SolidColorBrush(Color.FromRgb(30, 58, 102))  // #1E3A66
                : Brushes.Transparent;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ActiveNavForegroundConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string current = value?.ToString() ?? string.Empty;
            string target = parameter?.ToString() ?? string.Empty;
            bool isActive = string.Equals(current, target, StringComparison.OrdinalIgnoreCase);

            return isActive 
                ? Brushes.White 
                : new SolidColorBrush(Color.FromRgb(148, 163, 184)); // #94A3B8
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ActiveNavIndicatorVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string current = value?.ToString() ?? string.Empty;
            string target = parameter?.ToString() ?? string.Empty;
            bool isActive = string.Equals(current, target, StringComparison.OrdinalIgnoreCase);

            return isActive ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
