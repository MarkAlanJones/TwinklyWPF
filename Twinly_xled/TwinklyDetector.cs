using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Twinkly_xled
{
    internal static class TwinklyDetector
    {
        // UDP Scan for the lights - allow multiple sets to be detected
        // tried using async await but the ReceiveAsync does not timeout (hangs when lights are off)
        public static IEnumerable<TwinklyInstance> Locate()
        {
            const int PORT_NUMBER = 5555;
            const int TIMEOUT = 2500; // 2.5 sec

            using var Client = new UdpClient()
            {
                EnableBroadcast = true
            };
            Client.Client.ReceiveTimeout = TIMEOUT;
            var sw = Stopwatch.StartNew();

            // send
            byte[] sendbuf = Encoding.ASCII.GetBytes((char)0x01 + "discover");
            Client.Send(sendbuf, sendbuf.Length, new IPEndPoint(System.Net.IPAddress.Broadcast, PORT_NUMBER));

            while (sw.ElapsedMilliseconds < TIMEOUT)
            {
                Logging.WriteDbg($"{sw.ElapsedMilliseconds}ms...");
                var TwinklyEp = new IPEndPoint(System.Net.IPAddress.Any, 0);
                string TwinklyName = string.Empty;

                // receive
                try
                {
                    byte[] result = Client.Receive(ref TwinklyEp);

                    // <ip>OK<device_name>
                    TwinklyName = System.Text.Encoding.ASCII.GetString(result[6..]);
                    Logging.WriteDbg($"{BitConverter.ToString(result)} from {TwinklyEp}");
                }
                catch (Exception)
                { }

                if (!string.IsNullOrEmpty(TwinklyName))
                    yield return new TwinklyInstance(TwinklyName, TwinklyEp.Address);
            }
        }


    }
}
