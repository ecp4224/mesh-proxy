using System;
using System.Threading.Tasks;

namespace MeshProxy
{
    public class PeerDiscovery : Service
    {
        private bool AutoWifiConnect = false;
        
        protected override async Task OnInit()
        {
            if (AutoWifiConnect)
            {
                //Search for a wifi network
                string wifiNetworks = await AsyncShellCommand.ExecuteWithOutput("wifi list");

                if (wifiNetworks.Contains("MP-"))
                {
                    var ssid = "MP-1";
                    int result = await AsyncShellCommand.Execute("connect " + ssid);

                    if (result != 0)
                    {
                        throw new ApplicationException("Could not connect to MeshProxy wifi network!");
                    }
                }
                else
                {
                    var ssid = "MP-1";
                    int result = await AsyncShellCommand.Execute("create wifi " + ssid);
                    if (result != 0)
                    {
                        throw new ApplicationException("Could not create the MeshProxy wifi network!");
                    }
                }
            }
        }
    }
}