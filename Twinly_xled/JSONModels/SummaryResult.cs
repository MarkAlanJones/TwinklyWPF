namespace Twinkly_xled.JSONModels
{
    // HUGE Summary of everything

    public class SummaryMode
    {
        public string mode { get; set; }
        public int detect_mode { get; set; }
        public int shop_mode { get; set; }
        public int id { get; set; }
        public string unique_id { get; set; }
        public string name { get; set; }  // name of Movie if Movie Mode - depends on FW ver > 2.5.9 ?
    }

    public class SummaryTimer : Timer
    {
        public string tz { get; set; }
    }

    public class SummaryMusic
    {
        public int enabled { get; set; } // 1 = true 0 = false
        public int active { get; set; }
        public string mode { get; set; }  // auto
        public string auto_mode { get; set; } // effectsets
        public int current_driverset { get; set; }
        public int mood_index { get; set; }
    }

    public class FilterConfig
    {
        public string mode { get; set; }
        public int value { get; set; }
    }

    public class Filter // brightness Hue Saturation
    {
        public string filter { get; set; }
        public FilterConfig config { get; set; }
    }

    public class Group
    {
        public string mode { get; set; }
        public string uid { get; set; }  // this is the group uid that will match the slave_id and master_id in the move syncdef
        public int compat_mode { get; set; }
    }

    public class SummaryColor : ColorHSV
    {
        public int white { get; set; } // if RGBW
        public int red { get; set; } // red component of RGB, in range 0..255
        public int green { get; set; } // green component of RGB, in range 0..255
        public int blue { get; set; } // blue component of RGB, in range 0..255
    }

    public class SummaryLayout
    {
        public string uuid { get; set; }
    }

    /// <summary>
    /// Summary Result
    /// </summary>
    public class SummaryResult
    {
        public SummaryMode led_mode { get; set; } // corresponds to response of Get LED operation mode without code.
        public SummaryTimer timer { get; set; } // corresponds to response of Get Timer without code.
        public SummaryMusic music { get; set; }
        public Filter[] filters { get; set; } //hue brightness and saturation
        public Group group { get; set; } // Group information that is used with the Syncdef in the led current movie
        public SummaryColor color { get; set; } // when it was last  set to a single color, not current color from movie
        public SummaryLayout layout { get; set; } // ?? just a uuid
        public int code { get; set; }  // status 1000 = ok
    }
}
