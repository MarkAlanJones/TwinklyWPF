namespace Twinkly_xled.JSONModels
{
    public class Message
    {
        public string message { get; set; }
    }

    public class MessageResult
    {
        public Message json { get; set; }
        public int code { get; set; }
    }
}
