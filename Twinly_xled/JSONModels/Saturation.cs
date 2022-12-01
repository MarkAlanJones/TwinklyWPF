namespace Twinkly_xled.JSONModels
{
    public class SaturationType
    {
        public string mode { get; set; } // one of "enabled", "disabled"
        public string type { get; set; } // "A" (absolute) or "R" (relative)
        public int value { get; set; } // saturation level in range of 0..100 A
                                       //                        or -100..100 R 

    }

    public class Saturation
    {
        public string mode { get; set; } // one of "enabled", "disabled"
        public int value { get; set; } // saturation level in range of 0..100
    }

    public class SaturationResult : Saturation
    {
        public int code { get; set; }
    }
}
