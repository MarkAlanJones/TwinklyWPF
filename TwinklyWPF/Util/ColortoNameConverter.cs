using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Data;

namespace TwinklyWPF.Util
{
    // look up named colors form system.drawing
    class ColortoNameConverter : IValueConverter
    {
        static Dictionary<string, System.Drawing.Color> namedcolors = typeof(System.Drawing.Color)
             .GetProperties(BindingFlags.Static | BindingFlags.Public)
             .ToDictionary(p => p.Name, p => (System.Drawing.Color)p.GetValue(null, null));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Do the conversion from color to name 
            var color = (System.Windows.Media.Color)value;
            return namedcolors.Where(c => Math.Abs(c.Value.R - color.R) < 10 &&
                                          Math.Abs(c.Value.G - color.G) < 10 &&
                                          Math.Abs(c.Value.B - color.B) < 10).FirstOrDefault().Key;
        }

        // returns a system.drawing color
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return namedcolors[(string)value];
        }
    }
}
