using System.Text.Json.Serialization;

namespace Twinkly_xled.JSONModels
{
    public class LoginResult
    {
        public string authentication_token { get; set; }
        public int authentication_token_expires_in { get; set; } // 14400 s = 4hr
        [JsonPropertyName("challenge-response")]
        public string challenge_response { get; set; }
        public int code { get; set; }
    }
}
