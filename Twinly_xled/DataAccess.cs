using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Twinkly_xled
{
    public class DataAccess
    {
        private HttpClient client { get; set; }

        public bool Error { get; set; } = false;
        public HttpStatusCode HttpStatus { get; set; } = HttpStatusCode.OK;

        private string tw_IP { get; set; }
        public string IPAddress
        {
            get { return tw_IP; }
            set
            {
                tw_IP = value;
                client = new HttpClient() { BaseAddress = new Uri($"http://{tw_IP}/xled/v1/") };
            }
        }

        public DateTime ExpiresAt { get; private set; }
        public TimeSpan ExpiresIn { get { return ExpiresAt - DateTime.Now; } }

        public DataAccess()
        {
            // IPAddress is set by UDP locate on port 5555
            Locate();
        }

        // UDP Scan for the lights - can only deal with the first one 
        public void Locate()
        {
            const int PORT_NUMBER = 5555;

            using (var Client = new UdpClient())
            {
                Client.EnableBroadcast = true;
                Client.Client.ReceiveTimeout = 1000; // 1 sec 
                var TwinklyEp = new IPEndPoint(System.Net.IPAddress.Any, 0);

                // send
                byte[] sendbuf = Encoding.ASCII.GetBytes((char)0x01 + "discover");
                Client.Send(sendbuf, sendbuf.Length, new IPEndPoint(System.Net.IPAddress.Broadcast, PORT_NUMBER));

                // receive
                byte[] result = Client.Receive(ref TwinklyEp);

                // don't need to parse the message - we know who responded
                // <ip>OK<device_name>
                Debug.WriteLine($"{BitConverter.ToString(result)} from {TwinklyEp}");
                IPAddress = TwinklyEp.Address.ToString();
            }
        }

        // UDP port 7777 for realtime 
        public void RTFX(byte[] buffer)
        {
            const int PORT_NUMBER = 7777;

            using (var Client = new UdpClient())
            {
               
                // send
                Client.Send(buffer, buffer.Length, new IPEndPoint(System.Net.IPAddress.Parse(IPAddress), PORT_NUMBER));

                // Hope it made it 

            }
        }


        /// <summary>
        /// GET - read information from the twinkly API
        /// </summary>
        public async Task<string> Get(string url)
        {
            Error = false;
            try
            {
                var result = await client.GetAsync(url);
                HttpStatus = result.StatusCode;
                if (HttpStatus == HttpStatusCode.OK)
                {
                    return await result.Content.ReadAsStringAsync();
                }
                else
                {
                    Error = true;
                    return result.StatusCode.ToString();
                }
            }
            catch (Exception ex)
            {
                HttpStatus = HttpStatusCode.InternalServerError;
                Error = true;
                return $"ERROR {ex.Message}";
            }
        }

        /// <summary>
        /// POST - change information on the twinkly device
        /// </summary>
        public async Task<string> Post(string url, string content)
        {
            Error = false;
            try
            {
                var result = await client.PostAsync(url, new StringContent(content));
                HttpStatus = result.StatusCode;
                if (HttpStatus == HttpStatusCode.OK)
                {
                    return await result.Content.ReadAsStringAsync();
                }
                else
                {
                    Error = true;
                    return result.StatusCode.ToString();
                }
            }
            catch (Exception ex)
            {
                HttpStatus = HttpStatusCode.InternalServerError;
                Error = true;
                return $"ERROR {ex.Message}";
            }
        }

        // Note the use of X-Auth-Token indicates a less than state of the art authentication system
        public void Authenticate(string token, int expires)
        {
            if (client.DefaultRequestHeaders.Contains("X-Auth-Token"))
                client.DefaultRequestHeaders.Remove("X-Auth-Token");

            client.DefaultRequestHeaders.Add("X-Auth-Token", token);
            ExpiresAt = DateTime.Now.AddSeconds(expires);

            Debug.WriteLine($"Auth Token {token} expires at {ExpiresAt:T}");
        }

        public string GetAuthToken()
        {
            if (ExpiresIn.TotalMinutes > 0)
            {
                return client.DefaultRequestHeaders.GetValues("X-Auth-Token").FirstOrDefault();
            }
            return string.Empty;
        }
    }
}
