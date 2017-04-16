using System;
using System.Globalization;
using System.Windows.Data;
using ManagedBass;

namespace RecordingTest
{
    public class StateToPlayButtonConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch ((PlaybackState)value)
            {
                case PlaybackState.Playing:
                    return "Pause";

                default:
                    return "Record";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}