namespace Twinkly_xled.JSONModels
{
    public class MQTTConfig // these you can set - but can you really change the host name ?
    {
        public string broker_host { get; set; } //hostname of broker
        public int broker_port { get; set; } // 1883 for unencrypted ? - Settable !
        public string client_id { get; set; }
        public string user { get; set; } // Twinkly32
        public int keep_alive_interval { get; set; } //  60
    }

    public class MQTTConfigResult : MQTTConfig
    {
        public int code { get; set; }
    }
}
