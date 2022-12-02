using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Twinkly_xled
{
    internal static class TwinklyDetector
    {
        /// <summary>
        ///  UDP Scan for the lights - allow multiple sets to be detected
        /// </summary>
        public static async Task<IEnumerable<TwinklyInstance>> LocateAsync()
        {
            const int PORT_NUMBER = 5555;
            const int TIMEOUT = 2500; // 2.5 sec

            using var Client = new UdpClient() { EnableBroadcast = true };
            Client.Client.ReceiveTimeout = TIMEOUT;

            var detected = new List<TwinklyInstance>();
            var TwinklyEp = new IPEndPoint(IPAddress.Any, 0);
            string TwinklyName = string.Empty;

            // send
            byte[] sendbuf = Encoding.ASCII.GetBytes((char)0x01 + "discover");
            await Client.SendAsync(sendbuf, sendbuf.Length, new IPEndPoint(IPAddress.Broadcast, PORT_NUMBER));
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < TIMEOUT)
            {
                // receive
                try
                {
                    // .net 7 behaves a bit differently, the UDP client will throw an exception when it times out
                    //  tried using ReceiveAsync does not timeout
                    byte[] result = Client.Receive(ref TwinklyEp);

                    // <ip>OK<device_name>0
                    if (result.Length > 6)
                    {
                        TwinklyName = Encoding.ASCII.GetString(result[6..]).TrimEnd((char)0x00);
                        detected.Add(new TwinklyInstance(TwinklyName, TwinklyEp.Address));
                    }
                }
                catch (SocketException Sex)
                {
                    // No Response during timeout period
                    Logging.WriteDbg(Sex.Message);
                }
            }

            return detected;
        }
    }
}
