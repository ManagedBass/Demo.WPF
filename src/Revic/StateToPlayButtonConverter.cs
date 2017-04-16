using System;
using System.Globalization;
using System.Windows.Data;
using ManagedBass;

namespace Revic
{
    public class StateToPlayButtonConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch ((PlaybackState)value)
            {
                case PlaybackState.Playing:
                    return "/Resources/Pause.png";

                default:
                    return "/Resources/Play.png";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}