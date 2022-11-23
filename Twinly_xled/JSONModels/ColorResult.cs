namespace Twinkly_xled.JSONModels
{
    public class ColorHSV
    {
        public int hue { get; set; } // hue component of HSV, in range 0..359
        public int saturation { get; set; } // saturation component of HSV, in range 0..255
        public int value { get; set; } // value component of HSV, in range 0..255
    }

    public class ColorRGB
    {
        public int red { get; set; } // red component of RGB, in range 0..255
        public int green { get; set; } // green component of RGB, in range 0..255
        public int blue { get; set; } // blue component of RGB, in range 0..255
    }

    public class ColorRGBW : ColorRGB
    {
        public int white { get; set; } // if RGBW
    }

    public class ColorResult : ColorHSV
    {
        public int white { get; set; } // if RGBW
        public int red { get; set; } // red component of RGB, in range 0..255
        public int green { get; set; } // green component of RGB, in range 0..255
        public int blue { get; set; } // blue component of RGB, in range 0..255

        public int code { get; set; }
    }
}
