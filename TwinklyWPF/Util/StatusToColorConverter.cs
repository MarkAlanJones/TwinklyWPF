using System;
using System.Globalization;
using System.Net;
using System.Windows.Data;
using System.Windows.Media;

namespace TwinklyWPF.Util
{
    // look up named colors form system.drawing
    class StatusToColorConverter : IValueConverter
    {      
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Do the conversion - return one of 2 colours
            if ((int)value == (int)HttpStatusCode.OK)
                return new SolidColorBrush(Color.FromRgb(0x2a, 0x2a, 0x2a));
            else
                return new SolidColorBrush(Color.FromRgb(0xff, 0x2a, 0x2a));
        }

        // returns a system.drawing color
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
