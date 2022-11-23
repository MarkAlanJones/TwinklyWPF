namespace Twinkly_xled.JSONModels
{
    public class Brightness
    {
        public string mode { get; set; } // one of “enabled”, “disabled”
        public int value { get; set; } // brightness level in range of 0..100 NOT 255

        // public string type { get; set; } // always “A”
    }

    public class BrightnessResult : Brightness
    {
        public int code { get; set; }
    }
}
