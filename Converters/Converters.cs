// =============================================================================
// Converters/Converters.cs
// Value converters para la UI WPF
// =============================================================================
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ReaperPluginManager.Models;

namespace ReaperPluginManager.Converters
{
    /// <summary>Convierte PluginStatus a color de fondo</summary>
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PluginStatus status)
            {
                return status switch
                {
                    PluginStatus.Installed   => new SolidColorBrush(Color.FromRgb(0x1B, 0x5E, 0x20)),
                    PluginStatus.Blocked     => new SolidColorBrush(Color.FromRgb(0x7F, 0x00, 0x00)),
                    PluginStatus.Failed      => new SolidColorBrush(Color.FromRgb(0x6D, 0x1A, 0x1A)),
                    PluginStatus.Scanning    => new SolidColorBrush(Color.FromRgb(0x1A, 0x23, 0x7E)),
                    PluginStatus.Testing     => new SolidColorBrush(Color.FromRgb(0x31, 0x1B, 0x92)),
                    PluginStatus.Downloading => new SolidColorBrush(Color.FromRgb(0x1A, 0x3A, 0x6A)),
                    PluginStatus.Installing  => new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)),
                    PluginStatus.UpdateAvailable => new SolidColorBrush(Color.FromRgb(0xE6, 0x5C, 0x00)),
                    _                        => new SolidColorBrush(Color.FromRgb(0x37, 0x47, 0x4F)),
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>Convierte SecurityClassification a color</summary>
    public class SecurityToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SecurityClassification sec)
            {
                return sec switch
                {
                    SecurityClassification.Safe       => new SolidColorBrush(Color.FromRgb(0x1B, 0x5E, 0x20)),
                    SecurityClassification.Suspicious => new SolidColorBrush(Color.FromRgb(0xE6, 0x5C, 0x00)),
                    SecurityClassification.Blocked    => new SolidColorBrush(Color.FromRgb(0x7F, 0x00, 0x00)),
                    _                                 => new SolidColorBrush(Color.FromRgb(0x37, 0x47, 0x4F)),
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>Convierte bool a Visibility. ConverterParameter="Inverse" para invertir</summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool flag = value is bool b && b;
            bool inverse = parameter is string p && p == "Inverse";
            return (flag ^ inverse) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && v == Visibility.Visible;
    }

    /// <summary>Invierte un bool</summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;
    }

    /// <summary>Convierte bytes a display human-readable</summary>
    public class BytesToSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long bytes)
            {
                return bytes switch
                {
                    > 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
                    > 1_024     => $"{bytes / 1_024.0:F0} KB",
                    _           => $"{bytes} B"
                };
            }
            return "–";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Convierte PluginStatus a Visibility para la barra de progreso indeterminada.
    /// Visible solo cuando el plugin está en proceso activo.
    /// </summary>
    public class StatusToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PluginStatus status)
            {
                bool active = status is PluginStatus.Downloading
                    or PluginStatus.Scanning
                    or PluginStatus.Testing
                    or PluginStatus.Installing;
                return active ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>Convierte null a Visibility</summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNull = value == null;
            bool inverse = parameter is string p && p == "Inverse";
            return (isNull ^ inverse) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
