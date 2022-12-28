namespace Twinkly_xled.JSONModels
{
    public class MoviesResult
    {
        public MovieItem[] movies { get; set; }

        // How much room is there for movies ? 
        public int available_frames { get; set; }
        public int max_capacity { get; set; }

        public int code { get; set; }
    }

    public class MovieItem
    {
        public int id { get; set; }
        public string name { get; set; }  
        public string unique_id { get; set; }
        public string descriptor_type { get; set; } // e.g “rgbw_raw” for firmware family “G” or “rgb_raw” for firmware family “F”
        public int leds_per_frame { get; set; }
        public int frames_number { get; set; }
        public int fps { get; set; }
    }
}
