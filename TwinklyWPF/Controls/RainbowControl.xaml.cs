using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using TwinklyWPF.Util;
using WpfRotaryControl;

namespace TwinklyWPF.Controls
{
    /// <summary>
    /// Interaction logic for RainbowControl.xaml
    /// </summary>
    public partial class RainbowControl : UserControl
    {
        public RainbowControl()
        {
            InitializeComponent();

            // Rainbow around the dial
            var newsegments = new List<RotaryControlSegment>();
            for (var i = 0; i < 360; i++)
            {
                newsegments.Add(new RotaryControlSegment()
                {
                    Fill = new SolidColorBrush(
                        HSBColor.FromHSB(
                            new HSBColor((float)((i / 360.0) * 255), 255, 255))),
                    AngleInDegrees = 1
                });

            }

            _dialRainbow.Segments = newsegments;
        }
    }
}
