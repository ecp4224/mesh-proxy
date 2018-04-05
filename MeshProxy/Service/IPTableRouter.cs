using System;
using System.Threading.Tasks;
using MeshProxy.Network;
using MeshProxy.Utils;

namespace MeshProxy.Services
{
    public class IPTableRouter : Service
	{
        private MeshProxyConfig Config => GetService<MeshProxyConfig>();
        private MeshProxyLog Log => GetService<MeshProxyLog>();

        private async Task ForwardStratOne() {
            Log.Info("Using Forwarding Strategy One");

            //Populate secondary routing table
            await Execute("sudo ip route add default via " + Config.DefaultGateway + " dev eth0 table eth-route");
            //Anything with the mark 0x1 will use secondary routing table
            await Execute("sudo ip rule add fwmark 0x1 table eth-route");

            foreach (var port in Config.TcpPortFowarding.Keys)
            {
                var ip = Config.TcpPortFowarding[port];

                //Mark these packets with 0x1
                await Execute("sudo iptables -A INPUT -t mangle -i wlan0 -p tcp --dport " + port + " -j MARK --set-mark 0x1");
                //Set the destination to be the forwarding ip
                await Execute("sudo iptables -A OUTPUT -t nat -o eth0 -p tcp --dport " + port + " -j DNAT --to " + ip);
                //Set the source to be myself
                await Execute("sudo iptables -A POSTROUTING -t nat -o eth0 -p tcp --dport " + port + " -j SNAT --to " + Config.EthernetBindAddress);
            }

            foreach (var port in Config.UdpPortFowarding.Keys)
            {
                var ip = Config.UdpPortFowarding[port];

                //Mark these packets with 0x1
                await Execute("sudo iptables -A INPUT -t mangle -o wlan0 -p udp --dport " + port + " -j MARK --set-mark 0x1");
                //Set the destination to be the forwarding ip
                await Execute("sudo iptables -A OUTPUT -t nat -o eth0 -p udp --dport " + port + " -j DNAT --to " + ip);
                //Set the source to be myself
                await Execute("sudo iptables -A POSTROUTING -t nat -o eth0 -p udp --dport " + port + " -j SNAT --to " + Config.EthernetBindAddress);
            }
        }

        private async Task ForwardStratTwo() {
            Log.Info("Using Forwarding Strategy Two");

            await Execute("echo '1' | sudo tee /proc/sys/net/ipv4/conf/wlan0/forwarding");
            await Execute("echo '1' | sudo tee /proc/sys/net/ipv4/conf/eth0/forwarding");

            foreach (var port in Config.TcpPortFowarding.Keys)
            {
                var ip = Config.TcpPortFowarding[port];

                await Execute("sudo iptables -t nat -A PREROUTING -p tcp -i wlan0 --dport " + port + " -j DNAT --to-destination " + ip + ":" + port);
                await Execute("sudo iptables -A FORWARD -p tcp -d " + ip + " --dport " + port + " -m state --state NEW,ESTABLISHED,RELATED -j ACCEPT");
            }

            foreach (var port in Config.UdpPortFowarding.Keys)
            {
                var ip = Config.UdpPortFowarding[port];

                await Execute("sudo iptables -t nat -A PREROUTING -p udp -i wlan0 --dport " + port + " -j DNAT --to-destination " + ip + ":" + port);
                await Execute("sudo iptables -A FORWARD -p udp -d " + ip + " --dport " + port + " -m state --state NEW,ESTABLISHED,RELATED -j ACCEPT");
            }
        }

        private async Task ForwardStratThree() {
            Log.Info("Using Forwarding Strategy Three");

            await Execute("sudo iptables -t nat -A POSTROUTING --out-interface eth0 -j MASQUERADE");
            await Execute("sudo iptables -A FORWARD --in-interface wlan0 -j ACCEPT");

            foreach (var port in Config.TcpPortFowarding.Keys)
            {
                var ip = Config.TcpPortFowarding[port];
                await Execute("sudo iptables -t nat -A PREROUTING -p tcp -i eth0 -m tcp --dport " + port + " -j DNAT --to-destination " + ip + ":" + port);
            }

            foreach (var port in Config.UdpPortFowarding.Keys)
            {
                var ip = Config.UdpPortFowarding[port];
                await Execute("sudo iptables -t nat -A PREROUTING -p udp -i eth0 -m tcp --dport " + port + " -j DNAT --to-destination " + ip + ":" + port);
            }
        }

        protected override async Task OnInit()
		{
            switch (Config.ForwardStrategy) {
                case 1:
                    await ForwardStratOne();
                    break;
                case 2:
                    await ForwardStratTwo();
                    break;
                case 3:
                    await ForwardStratThree();
                    break;
                default:
                    Log.Info("No forwarding strategy selected, doing nothing!");
                    break;
            }
		}

		public async Task<bool> AddRoute(Peer peer) {
            //Route peer's ethernet IP to peer's wifi IP
            var result = await Execute("sudo ip route add " + peer.InternalIP + " via " + peer.ExternalIP + " dev wlan0");
            if (result != 0) {
                Log.Error("Could not route ethernet -> wifi!");
                return false;
            }

            //Add peer specific routing table with the ID of the peer + 200
            result = await Execute("sudo ip rule add from " + peer.ExternalIP + "/24 table 20" + peer.Id);
            if (result != 0) {
                Log.Error("Could not create route rule for peer!");
                return false;
            }

            //Route peer's wifi IP to the peer's ethernet IP, changing it's src address to the ethernet IP
            result = await Execute("sudo ip route add " + Config.WifiBindAddress + " via " + Config.DefaultGateway + " dev eth0 table 20" + peer.Id + " src " + peer.InternalIP);

            if (result != 0) {
                Log.Error("Could not route wifi -> ethernet");
                return false;
            }

            return true;
        }

        private static async Task<int> Execute(string command) {
            return await AsyncShellCommand.Execute(command);
        }
	}
}
