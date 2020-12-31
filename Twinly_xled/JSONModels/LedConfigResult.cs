namespace Twinkly_xled.JSONModels
{
    public class LedConfig 
    {
        public ConfigStrings[] strings { get; set; }
    }

    public class LedConfigResult : LedConfig
    {
        public int code { get; set; }
    }

    // these are not strings - they are Led start and length
    public class ConfigStrings
    {
        public int first_led_id { get; set; }
        public int length { get; set; }
    }

}
