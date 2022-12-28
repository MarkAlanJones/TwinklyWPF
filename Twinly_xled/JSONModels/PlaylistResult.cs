using System;

namespace Twinkly_xled.JSONModels
{
    public class PlaylistResult
    {
        public PlaylistEntry[] entries { get; set; }

        public int code { get; set; } // Sucess = 1000
    }

    public class PlaylistEntry
    {
        public string unique_id { get; set; }
        public int duration { get; set; }
    }

    public class CurrentPlaylistEntry
    {
        public int id { get; set; }
        public string unique_id { get; set; }
        public string name { get; set; }
        public int code { get; set; } // Sucess = 1000
    }
}
