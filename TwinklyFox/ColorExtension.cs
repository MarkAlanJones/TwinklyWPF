using System.Drawing;

namespace ExtensionMethods
{
    public static class MyExtensionMethods
    {
        /// <summary>
        /// Scale down a RGB to N/256ths of it's current brightness using "video" dimming rules.
        /// "Video" dimming rules means that unless the scale factor is ZERO each channel is 
        /// guaranteed NOT to dim down to zero.If it's already nonzero, it'll stay nonzero, 
        /// even if that means the hue shifts a little at low brightness levels.
        /// </summary>
        public static Color nscale8_video(this Color c, byte N)
        {
            var r = c.R * N / 256;
            var g = c.G * N / 256;
            var b = c.B * N / 256;

            return Color.FromArgb(r, g, b);
        }
    }
}