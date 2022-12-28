using System;
using System.Threading.Channels;

namespace Twinkly_xled.JSONModels
{
    public class NetworkScanResult : VerifyResult
    {
        public Network[] networks { get; set; }
    }

    public class Network
    {
        public string ssid { get; set; } // WIFI connected to 
        public string mac { get; set; }

        public int rssi { get; set; } // this is a positive integer indicating signal strength - normally rssi is a -ve number (larger = closer)

        public int channel { get; set; }

        public int enc { get; set; }

        //0 OPEN
        //1 WEP
        //2 WPA_PSK
        //3 WPA2_PSK
        //4 WPA_WPA2_PSK
    }

    public class NetworkStatus : VerifyResult
    {
        public int mode { get; set; } // 1 , 2

        public AccessPoint ap { get; set; } // if mode 2
        public Station station { get; set; } // if mode 1
    }

    // The twinkly is it's own Wifi hotspot 
    public class AccessPoint
    {
        public string ssid { get; set; } // SSID of the device - Twinkly_71CBE9
        public int channel { get; set; } // channel number
        public string ip { get; set; } // IP address - 192.168.4.* 
        public int enc { get; set; }        //0 OPEN        <-- will be open always ?
                                            //1 WEP
                                            //2 WPA_PSK
                                            //3 WPA2_PSK
                                            //4 WPA_WPA2_PSK
        public int ssid_hidden { get; set; } // default 0. Since firmware version 2.4.25.
        public int max_connection { get; set; } // default 4. Since firmware version 2.4.25.
        public int password_changed { get; set; } // either hidden or set to 1 if default password for AP was changed.
    }

    // Device is connected to WiFi - normal 
    public class Station
    {
        public string ssid { get; set; } // SSID of a WiFi network to connect to.If empty string is passed it defaults to prefix ESP_ instead of Twinkly_.
        public string ip { get; set; } // IP address of the device
        public string gw { get; set; } // IP address of the gateway  
        public string mask { get; set; } // subnet mask
        public int status { get; set; } // status of the network connection: 5 = connected, 255 = AP is used only FW D?
    }
}
