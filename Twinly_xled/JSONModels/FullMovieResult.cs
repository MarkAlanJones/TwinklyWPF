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
        public string mode { get; set; }  // none master slave
        public int compat_mode { get; set; }
        public string slave_id { get; set; } // only if mode = slave - the id is the same as the group uid 
        public string master_id { get; set; } // only if mode = master
    }

    public class CurrentMovieId
    {
        public int id { get; set; } // 0..15
    }

    public class CurrentMovieInfo : CurrentMovieId
    {
        public string name { get; set; }
        public string unique_id { get; set; }
        public int code { get; set; }
    }
}
