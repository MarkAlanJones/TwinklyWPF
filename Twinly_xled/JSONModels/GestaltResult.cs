using System;

namespace Twinkly_xled.JSONModels
{
    public class GestaltResult
    {
        public string product_name { get; set; }
        [Obsolete]
        public string product_version { get; set; }
        public string hardware_version { get; set; }
        public int bytes_per_led { get; set; }
        public string hw_id { get; set; }
        public int flash_size { get; set; }
        public int led_type { get; set; }
        [Obsolete]
        public string led_version { get; set; }
        public string product_code { get; set; }
        public string fw_family { get; set; }
        public string device_name { get; set; }
        [Obsolete]
        public int rssi { get; set; }
        public string uptime { get; set; }  // a string - elapsed milliseconds
        public string mac { get; set; }
        public string uuid { get; set; }
        public int max_supported_led { get; set; }
        [Obsolete]
        public int base_leds_number { get; set; }
        public int number_of_led { get; set; }
        public string led_profile { get; set; }
        public int frame_rate { get; set; }
        public float measured_frame_rate { get; set; }
        public int movie_capacity { get; set; } // describes the number of frames the device can handle
        public int wire_type { get; set; }
        public string copyright { get; set; }
        public int code { get; set; }
    }
}
