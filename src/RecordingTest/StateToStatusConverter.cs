using System;
using System.Globalization;
using System.Windows.Data;
using ManagedBass;

namespace RecordingTest
{
    public class StateToStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch ((PlaybackState)value)
            {
                case PlaybackState.Paused:
                    return "Paused";

                case PlaybackState.Playing:
                    return "Recording";

                default:
                    return "Ready";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}