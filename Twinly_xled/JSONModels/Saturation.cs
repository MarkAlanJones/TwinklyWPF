namespace Twinkly_xled.JSONModels
{
    public class Saturation
    {
        public string mode { get; set; } // one of “enabled”, “disabled”
        public int value { get; set; } // saturation level in range of 0..100 NOT 255

    }

    public class SaturationResult : Saturation
    {
        public int code { get; set; }
    }
}
