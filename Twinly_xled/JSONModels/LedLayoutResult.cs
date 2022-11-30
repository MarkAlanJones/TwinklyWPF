namespace Twinkly_xled.JSONModels
{
    public class LedLayout
    {
        public int aspectXY { get; set; }  // only for 3d?
        public int aspectXZ { get; set; }

        public Coordinates[] coordinates { get; set; }

        public string source { get; set; }  // linear 2d 3d
        public bool synthesized { get; set; }

        public string uuid { get; set; }
    }

    public class LedLayoutResult : LedLayout
    {
        public int code { get; set; }
    }

    public class Coordinates
    {
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }
    }

    public class LedLayoutUploadResult
    {
        public int parsed_coordinates { get; set; }
        public int code { get; set; }
    }
}
