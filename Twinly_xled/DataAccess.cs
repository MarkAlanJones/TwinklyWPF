using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using Twinkly_xled.JSONModels;

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

        /// <summary>
        /// IP Address of detected Twinkly
        /// </summary>
        public string Address { get; private set; }

        /// <summary>
        /// Name of Detected Twinkly
        /// </summary>
        public string Name { get; private set; }
    }

    /// <summary>
    /// DataAccess Talks directly to the Twinky with UDP or HTTP GET/POST
    /// </summary>
    internal class DataAccess : IDisposable
    {
        private HttpClient client { get; set; } // we get a new client when the IPadderess is set
        private TimeSpan TimeOut = TimeSpan.FromSeconds(10);
        private DateTime timeoutUntil = DateTime.Now;

        /// <summary>
        /// True if there is an error
        /// </summary>
        public bool Error { get; set; } = false;

        /// <summary>
        /// The Status of the last Http call
        /// </summary>
        public HttpStatusCode HttpStatus { get; set; } = HttpStatusCode.OK;

        private IPAddress tw_IP { get; set; }
        public string IPAddress
        {
            get { return tw_IP.ToString(); }
            private set
            {
                tw_IP = System.Net.IPAddress.Parse(value);
                client = new HttpClient() { BaseAddress = new Uri($"http://{tw_IP}/xled/v1/") };
            }
        }

        public DateTime ExpiresAt { get; private set; }
        public TimeSpan ExpiresIn => ExpiresAt - DateTime.Now;

        public int NumLED { get; set; } // Set when Gestalt is read

        public DataAccess(string IP)
        {
            IPAddress address;
            if (System.Net.IPAddress.TryParse(IP, out address))
            {
                IPAddress = address.ToString();
                Error = false;
            }
            else
            {
                Error = true;
            }
        }

        // UDP port 7777 for realtime 
        private UdpClient udpc;

        // datagrams
        //  V1 - 01 [8 auth] <numLED> [data - 3 or 4 bytes x number of led - 1 Frame]
        //
        //  V2 up to 2.4.6 Movie Format [Obsolete?]
        //      02 [8 auth] 00 [data - Movie e.g. multi frame]
        //
        //  V3 2.4.14 or higher - 900byte chunks
        //      x03 [8 auth] 00 00 [Frame fragment 0..x] [frame data broken into 900 byte chunks]

        /// <summary>
        /// Always use V3 Chunked frames
        /// </summary>
        /// <param name="frame">Array of all leds * btyes per led</param>        
        public async Task RTFX(byte[] frame)
        {
            if (udpc is null)
                udpc = new UdpClient();

            // V3
            const int ChunkSize = 900;
            byte frag = 0;
            int i = 0;

            while (i < frame.Length && ChunkSize <= (frame.Length - (i * ChunkSize)))
            {
                var buffer = GetFrag(frag, ChunkSize, i, frame);
                i += ChunkSize;
                await SendaChunk(udpc, buffer).ConfigureAwait(false);
                frag += 1;
            }

            // Clean up last partial frame
            if (frame.Length % ChunkSize != 0)
            {
                if (i > 0) i -= ChunkSize;
                var buffer = GetFrag(frag, frame.Length % ChunkSize, i, frame);

                await SendaChunk(udpc, buffer).ConfigureAwait(false);
            }

            // Hope it made it - UDP is like a message in a bottle, you don't know if it was received 
        }

        private async Task SendaChunk(UdpClient udpc, byte[] buffer)
        {
            const int PORT_NUMBER = 7777;
            var endpoint = new IPEndPoint(tw_IP, PORT_NUMBER);

            // send;
            Logging.WriteDbg($"UDP Frame {buffer.Length} bytes to {endpoint.Address}:{endpoint.Port}");
            await udpc.SendAsync(buffer, buffer.Length, endpoint).ConfigureAwait(false);
        }

        private byte[] GetFrag(byte frag, int chunksize, int i, byte[] frame)
        {
            var header = GetHeder(frag);
            var framefrag = new byte[chunksize];
            Buffer.BlockCopy(frame, i, framefrag, 0, chunksize);
            return Combine(header, framefrag);
        }
        private byte[] GetHeder(byte frag)
        {
            // V3
            var auth = GetAuthBytes();
            var header = new byte[] { 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, frag };
            auth.CopyTo(header, 1);

            return header;
        }

        /// <summary>
        /// use the Original Frame - max 256 lights
        /// </summary>
        public void RTFX_Classic(byte[] frame)
        {
            const int PORT_NUMBER = 7777;
            using var Client = new UdpClient();
            var endpoint = new IPEndPoint(tw_IP, PORT_NUMBER);

            byte[] header, buffer;

            if (NumLED > byte.MaxValue)
                throw new ArgumentException($"V1 frames can only be used for less than {byte.MaxValue} Leds ({NumLED} detected)");

            // V1
            header = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, (byte)NumLED };
            GetAuthBytes().CopyTo(header, 1);
            buffer = Combine(header, frame);

            // send
            Client.Send(buffer, buffer.Length, endpoint);
            Logging.WriteDbg($"UDP Frame {buffer.Length} bytes to {endpoint.Address}:{endpoint.Port}");

            // Hope it made it - UDP is like a message in a bottle, you don't know if it was recieved 
        }

        private static byte[] Combine(byte[] first, byte[] second)
        {
            byte[] bytes = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, bytes, 0, first.Length);
            Buffer.BlockCopy(second, 0, bytes, first.Length, second.Length);
            return bytes;
        }

        // this API does NOT depend on Version of Firmware - but could
        private async Task<Version> GetFWVer()
        {
            (var json, bool Error) = await Get("fw/version").ConfigureAwait(false);
            if (!Error)
            {
                var FW = JsonSerializer.Deserialize<FWResult>(json);
                Logging.WriteDbg($"FW {FW.version}");
                return Version.Parse(FW.version);
            }
            return new Version(0, 0, 0);
        }

        /// <summary>
        /// GET - read information from the twinkly API
        /// </summary>
        public async Task<(string, bool)> Get(string url)
        {
            if (HttpStatus == HttpStatusCode.RequestTimeout && DateTime.Now < timeoutUntil)
            {
                Logging.WriteDbg($"{IPAddress} is still Timedout until {timeoutUntil.TimeOfDay}");
                return ($"{IPAddress} is still Timedout", true);
            }

            Logging.WriteDbg($"GET {IPAddress} {url}");
            Error = false;
            try
            {
                var result = await client.GetAsync(url)
                                         .WithTimeout(TimeOut)
                                         .ConfigureAwait(false);
                HttpStatus = result.StatusCode;
                if (HttpStatus == HttpStatusCode.OK)
                {
                    return (await result.Content.ReadAsStringAsync().ConfigureAwait(false), Error);
                }
                else
                {
                    Logging.WriteDbg($"  ! {(int)result.StatusCode} {result.StatusCode} *");
                    Error = true;
                    return (result.StatusCode.ToString(), Error);
                }
            }
            catch (TimeoutException tex)
            {
                HttpStatus = HttpStatusCode.RequestTimeout;
                timeoutUntil = DateTime.Now.AddSeconds(TimeOut.TotalSeconds * 3);
                Error = true;
                Logging.WriteDbg($"  Timeout {tex.Message} {(int)HttpStatus} {HttpStatus} *");
                return ($"TIMEOUT {tex.Message}", Error);
            }
            catch (Exception ex)
            {
                HttpStatus = HttpStatusCode.InternalServerError;
                Error = true;
                Logging.WriteDbg($"  Error {ex.Message} {(int)HttpStatus} {HttpStatus} *");
                return ($"ERROR {ex.Message}", Error);
            }
        }

        //private PeriodicTimer posttimr

        /// <summary>
        /// POST - change information on the twinkly device
        /// </summary>
        public async Task<string> Post(string url, string content)
        {
            Logging.WriteDbg($"POST {IPAddress} {url} {content}");
            Error = false;
            try
            {
                //using var timr = new PeriodicTimer(new TimeSpan(0,0,0,0,100));                
                var result = await client.PostAsync(url, new StringContent(content))
                                         .WithTimeout(TimeOut)
                                         .ConfigureAwait(false);
                HttpStatus = result.StatusCode;
                if (HttpStatus == HttpStatusCode.OK)
                {
                    return await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
                else if (HttpStatus == HttpStatusCode.Unauthorized)
                {
                    Logging.WriteDbg($"Lost Authentication");
                    ExpiresAt = DateTime.Now.AddSeconds(-10);
                    return HttpStatus.ToString();
                }
                else
                {
                    Logging.WriteDbg($"  ! {(int)HttpStatus} {HttpStatus} *");
                    Error = true;
                    return result.StatusCode.ToString();
                }
            }
            catch (TimeoutException tex)
            {
                HttpStatus = HttpStatusCode.RequestTimeout;
                Error = true;
                Logging.WriteDbg($"  Timeout {tex.Message} {(int)HttpStatus} {HttpStatus} *");
                return $"TIMEOUT {tex.Message}";
            }
            catch (Exception ex)
            {
                HttpStatus = HttpStatusCode.InternalServerError;
                Error = true;
                Logging.WriteDbg($"  Error {ex.Message} {(int)HttpStatus} {HttpStatus} *");
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

        // Authentication for UDP - assume 8 bytes
        private byte[] GetAuthBytes()
        {
            return Convert.FromBase64String(GetAuthToken());
        }

        public void Dispose()
        {
            client.Dispose();
            if (udpc != null)
                udpc.Dispose();
        }
    }

    public static class AsyncExtention
    {
        public static async Task<TResult> WithTimeout<TResult>(this Task<TResult> task, TimeSpan timeout)
        {
            if (task == await Task.WhenAny(task, Task.Delay(timeout)))
            {
                return await task;
            }
            throw new TimeoutException();
        }
    }


}
