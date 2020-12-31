using System.Text.Json.Serialization;

namespace Twinkly_xled.JSONModels
{
    class Verify
    {
        [JsonPropertyName("challenge-response")]
        public string challenge_response { get; set; }
    }
}
