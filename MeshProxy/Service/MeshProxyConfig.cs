using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MeshProxy.Services
{
    public class MeshProxyConfig : Service
    {
        private MConfig configProxy;

        public string Name => configProxy.name;
        public string EthernetBindAddress => configProxy.ethernetBindIp;
        public IPAddress WifiBindAddress => IPAddress.Parse(configProxy.wifiBindIp);
		public bool ForwardBroadcastPackets => configProxy.forwardBroadcastPackets;

        protected override async Task OnInit()
        {
            if (!File.Exists("config.json"))
            {
                configProxy = new MConfig();
                File.WriteAllText("config.json", JsonConvert.SerializeObject(configProxy));
            }
            else
            {
                string json = File.ReadAllText("config.json");
                configProxy = JsonConvert.DeserializeObject<MConfig>(json);
            }
        }

        public string TCPForwarding(ushort port)
        {
            if (!configProxy.tcpPorts.ContainsKey(port))
                return null;

            return configProxy.tcpPorts[port];
        }

        public string UDPForwarding(ushort port)
        {
            if (!configProxy.udpPorts.ContainsKey(port))
                return null;

            return configProxy.udpPorts[port];
        }

        private class MConfig
        {
            public string name = "SimplePeer";
            public Dictionary<ushort, string> tcpPorts = new Dictionary<ushort, string>();
            public Dictionary<ushort, string> udpPorts = new Dictionary<ushort, string>();
            public string wifiBindIp = "10.0.0.93";
            public string ethernetBindIp = "10.0.0.94";
			public bool forwardBroadcastPackets = true;
        }
    }
}