using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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
            Client.Client.ReceiveTimeout = TIMEOUT;  // for sync only?

            var detected = new List<TwinklyInstance>();
            var TwinklyEp = new IPEndPoint(IPAddress.Any, 0);
            string TwinklyName = string.Empty;

            try
            {
                // send
                Logging.WriteDbg($"Searching...");
                byte[] sendbuf = Encoding.ASCII.GetBytes((char)0x01 + "discover");
                await Client.SendAsync(sendbuf, sendbuf.Length, new IPEndPoint(IPAddress.Broadcast, PORT_NUMBER))
                            .ConfigureAwait(false);

                var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromMilliseconds(TIMEOUT)); // Set timeout
                while (!cts.IsCancellationRequested)
                {
                    // receive - using cancellation token to timeout (receiveTimeout property not used)
                    var udpresult = await Client.ReceiveAsync(cts.Token)
                                                .ConfigureAwait(false);

                    // <ip>OK<device_name>0
                    if (udpresult.Buffer.Length > 6)
                    {
                        TwinklyName = Encoding.ASCII.GetString(udpresult.Buffer[6..]).TrimEnd((char)0x00);
                        detected.Add(new TwinklyInstance(TwinklyName, udpresult.RemoteEndPoint.Address));
                    }
                }
            }
            catch (SocketException) { }
            catch (TimeoutException) { }
            catch (OperationCanceledException) { }

            Logging.WriteDbg($"Done Searching...");
            return detected;
        }
    }
}
