using System;
using System.Globalization;
using System.Windows.Data;

namespace PitchTracking
{
    public class DetectButtonContentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool) value ? "Stop" : "Detect";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}