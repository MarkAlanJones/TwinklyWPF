using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Twinkly_xled
{
    public static class Logging
    {
        public static void WriteDbg(string msg)
        {
#if DEBUG
            Debug.WriteLine(msg);
#endif
        }
    }

    public class TwinklyInstance
    {
        public TwinklyInstance(string name, IPAddress address)
        {
            Name = name;
            Address = address.ToString();
            Logging.WriteDbg($"Discovered {Name} @ {Address}");
        }

        public string Address { get; private set; }
        public string Name { get; private set; }
    }

    public class DataAccess
    {
        private HttpClient client { get; set; }

        public bool Error { get; set; } = false;
        public HttpStatusCode HttpStatus { get; set; } = HttpStatusCode.OK;

        private string tw_IP { get; set; }
        public string IPAddress
        {
            get { return tw_IP; }
            private set
            {
                tw_IP = value;
                client = new HttpClient() { BaseAddress = new Uri($"http://{tw_IP}/xled/v1/") };
            }
        }
        public List<TwinklyInstance> TwinklyDetected { get; private set; }

        public DateTime ExpiresAt { get; private set; }
        public TimeSpan ExpiresIn { get { return ExpiresAt - DateTime.Now; } }

        public DataAccess()
        {
            // IPAddress is set by UDP locate on port 5555
            TwinklyDetected = Locate().OrderBy(tw => tw.Name).ToList();             

            if (TwinklyDetected?.Count > 0)
                IPAddress = TwinklyDetected.First().Address;
        }

        //public static DataAccess Create()
        //{
        //    var DA = new DataAccess
        //    {
        //        // IPAddress is set by UDP locate on port 5555
        //        TwinklyDetected = Locate().OrderBy(s => s.Name).ToList()
        //    };

        //    if (DA.TwinklyDetected?.Count > 0)
        //        DA.IPAddress = DA.TwinklyDetected.First().Address;

        //    return DA;
        //}

        // UDP Scan for the lights - allow multiple sets to be detected
        // tried using async await but the ReceiveAsync does not timeout (hangs when lights are off)
        public IEnumerable<TwinklyInstance> Locate()
        {
            const int PORT_NUMBER = 5555;
            const int TIMEOUT = 2500; // 2.5 sec

            using var Client = new UdpClient();
            Client.EnableBroadcast = true;
            Client.Client.ReceiveTimeout = TIMEOUT;

            var sw = Stopwatch.StartNew();

            // send
            byte[] sendbuf = Encoding.ASCII.GetBytes((char)0x01 + "discover");
            Client.Send(sendbuf, sendbuf.Length, new IPEndPoint(System.Net.IPAddress.Broadcast, PORT_NUMBER));

            while (sw.ElapsedMilliseconds < TIMEOUT)
            {
                Logging.WriteDbg($"{sw.ElapsedMilliseconds}ms...");
                var TwinklyEp = new IPEndPoint(System.Net.IPAddress.Any, 0);

                // receive
                byte[] result = Client.Receive(ref TwinklyEp);

                //UdpReceiveResult udpresult = await Client.ReceiveAsync();
                //byte[] result = udpresult.Buffer;
                //var TwinklyEp = udpresult.RemoteEndPoint;

                // <ip>OK<device_name>
                string TwinklyName = result.ToString()[6..];
                Logging.WriteDbg($"{BitConverter.ToString(result)} from {TwinklyEp}");

                yield return new TwinklyInstance(TwinklyName, TwinklyEp.Address);
            }
        }

        // UDP port 7777 for realtime 
        public void RTFX(byte[] buffer)
        {
            const int PORT_NUMBER = 7777;
            using var Client = new UdpClient();

            // send
            Client.Send(buffer, buffer.Length, new IPEndPoint(System.Net.IPAddress.Parse(IPAddress), PORT_NUMBER));

            // Hope it made it 
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

            Logging.WriteDbg($"Auth Token {token} expires at {ExpiresAt:T}");
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
