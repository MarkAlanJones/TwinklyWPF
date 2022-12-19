using System;
using System.Windows.Media;
using System.Globalization;
using System.Windows.Data;

namespace TwinklyWPF.Util
{
    // Convert a Twinkly Hue - 0-359 to a SolidColorBrush using HSB library that uses 255 as max
    class HuetoColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Do the conversion from Hue to Color
            var hue = HSBColor.FromHSB(new HSBColor((float)((double)value / 360.0 * 255.0), 255, 255));
            return new SolidColorBrush(hue);
        }

        // return a float to represent the hue 0-359.999
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType == typeof(SolidColorBrush))
            {
                // Do the conversion from color to hue 
                return HSBColor.FromColor(((SolidColorBrush)value).Color).H / 255.0 * 360.0;
            }
            else
                return 0.0;
        }
    }
}
