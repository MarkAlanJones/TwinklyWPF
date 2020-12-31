namespace Twinkly_xled.JSONModels
{


    public class DeviceName
    {
        public string name { get; set; }
    }

    public class DeviceNameResult : DeviceName
    {
        public int code { get; set; }
    }
}
