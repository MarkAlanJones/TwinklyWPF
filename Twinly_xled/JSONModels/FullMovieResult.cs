using System;

namespace Twinkly_xled.JSONModels
{
    public class FullMovieResult
    {
        public int frames_number { get; set; } // check that this is the number of frames you sent ?
        public int code { get; set; }
    }

    public class MovieConfig
    {
        public int frame_delay { get; set; } // milliseconds 
        public int leds_number { get; set; } //  seems to be total number of LEDs to use
        public int frames_number { get; set; } // how many frames in the movie
    }

    public class CurrentMovieConfig : MovieConfig
    {
        public int loop_type { get; set; }
        public SyncDef sync { get; set; }
        public int code { get; set; }
    }

    public class SyncDef
    {
        public string mode { get; set; }
        public int compat_mode { get; set; }
        [Obsolete]
        public string slave_id { get; set; }
        [Obsolete]
        public string master_id { get; set; }
    }

}
