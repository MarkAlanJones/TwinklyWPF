namespace Twinkly_xled.JSONModels
{
    public class MQTTConfig
    {
        public string broker_host { get; set; } //hostname of broker
        public int broker_port { get; set; } // destination port of broker
        public string client_id { get; set; }
        public string encryption_key { get; set; } // length exactly 16 characters? - not Returned !!
        public int keep_alive_interval { get; set; } //cannot be set?
        public string user { get; set; }
    }

    public class MQTTConfigResult : MQTTConfig
    {
        public int code { get; set; }
    }
}
