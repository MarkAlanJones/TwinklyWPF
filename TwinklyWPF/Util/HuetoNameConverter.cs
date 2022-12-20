using System;
using System.Windows.Media;
using System.Globalization;
using System.Windows.Data;

namespace TwinklyWPF.Util
{
    // Convert a Twinkly Hue - 0-359 to name 
    class HuetoNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Do the conversion from Hue to Color
            var hue = HSBColor.FromHSB(new HSBColor((float)((double)value / 360.0 * 255.0), 255, 255));
            var temp = new ColortoNameConverter();

            return temp.Convert(Color.FromArgb(hue.A, hue.R, hue.G, hue.B),
                                typeof(Color), null, CultureInfo.InvariantCulture);
        }

        // return a float to represent the hue 0-359.999
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {

            // Do the conversion from color to hue 
            var temp = new ColortoNameConverter();
            return temp.ConvertBack(value, typeof(string), null, CultureInfo.InvariantCulture);
        }
    }
}
