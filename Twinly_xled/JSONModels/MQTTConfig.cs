namespace Twinkly_xled.JSONModels
{
    public class MQTTConfigSet // these you can set - but can you really change the host name ?
    {
        public string broker_host { get; set; } //hostname of broker
        public string client_id { get; set; }
        public int keep_alive_interval { get; set; } //  60
        public string user { get; set; } // Twinkly32
    }

    public class MQTTConfig : MQTTConfigSet
    {
        public int broker_port { get; set; } // destination port of broker
    }

    public class MQTTConfigResult : MQTTConfig
    {
        public int code { get; set; }
    }
}
