using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace DualSenseBridge.Themes;

/// <summary>Maps a percentage (0..100, negative = unknown) to a 0..1 scale factor.</summary>
public sealed class PercentToFractionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int percent && percent > 0 ? Math.Clamp(percent / 100.0, 0, 1) : 0.0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Visible when the bound value equals the converter parameter (compared as strings).</summary>
public sealed class EqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>True when the bound value equals the converter parameter (compared as strings).</summary>
public sealed class EqualsToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not true)
        {
            return Binding.DoNothing;
        }

        return targetType == typeof(int) ? int.Parse(parameter.ToString()!, CultureInfo.InvariantCulture) : parameter;
    }
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value as string ?? "#000000"));
            brush.Freeze();
            return brush;
        }
        catch (FormatException)
        {
            return Brushes.Transparent;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
