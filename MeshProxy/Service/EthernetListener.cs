using System;
using System.Net;
using System.Collections.Generic;
using System.Threading.Tasks;
using SharpPcap;
using System.Linq;
using PacketDotNet;
using MeshProxy.Network;
using MeshProxy.Utils;

namespace MeshProxy.Services
{
	public class EthernetListener : Service
	{
        private IPTableRouter Router => Owner.GetService<IPTableRouter>();
		private MeshProxyLog Log => Owner.GetService<MeshProxyLog>();
		private PeerManager PeerManager => Owner.GetService<PeerManager>();
		public ICaptureDevice Device { get; private set; }
		private int startIp;
		private string Iprefix;
		private Dictionary<string, Action<RawCapture>> filter = new Dictionary<string, Action<RawCapture>>();

		protected override async Task OnInit()
		{
			var config = Owner.GetService<MeshProxyConfig>();
			var ip = config.EthernetBindAddress;
			var temp = ip.Split('.');

			for (int i = 0; i < 3; i++)
			{
				Iprefix += temp[i] + ".";
			}

			startIp = int.Parse(temp[3]) + 1;

			Log.Info("Capturing on any device");

			Device = CaptureDeviceList.Instance.FirstOrDefault(d => d.Name == "eth0");

			if (Device == null)
				return;

			Device.OnPacketArrival += AnyDevice_OnPacketArrival;

			Device.Open(DeviceMode.Promiscuous, 0);

			Log.Info("Starting capture");

			Device.StartCapture();

			if (config.ForwardBroadcastPackets)
			{
				Log.Info("Creating filter for broadcast address");
				filter.Add(IPAddress.Broadcast.ToString(), (RawCapture obj) =>
				{
					foreach (var peer in PeerManager.Peers)
					{
						peer.ForwardPacket(obj);
					}
				});
			}

			Log.Info("EthernetListener Init complete");
		}

		void AnyDevice_OnPacketArrival(object sender, CaptureEventArgs e)
		{
			if (e == null)
				return;

			var raw = e.Packet;

			if (raw == null)
				return;

			var packet = Packet.ParsePacket(raw.LinkLayerType, raw.Data);

			if (packet == null)
				return;

			var ipPacket = (IpPacket)packet.Extract(typeof(IpPacket));

			if (ipPacket == null)
				return;

			var destination = ipPacket.DestinationAddress.ToString();

			if (filter.ContainsKey(destination))
			{
                Log.Info("Got packet for " + destination);
                //filter[destination](raw);
			}
		}

		public async Task<bool> CreateVirtualFor(Peer peer)
		{
			Log.Info("Create virtual eth interface");

			var ip = Iprefix + startIp;

			var result = await AsyncShellCommand.Execute("sudo ifconfig eth0:" + peer.Id + " " + ip);

			if (result != 0)
			{
				Log.Error("Could not create virtual interface for peer " + peer.Name);
				return false;
			}

            peer.InternalIP = ip;

            var routeAdded = await Router.AddRoute(peer);

            if (!routeAdded) {
                return false;
            }

			filter.Add(ip, peer.ForwardPacket);

			startIp++;
			if (startIp >= 255)
				startIp = 1;

			return true;
		}
	}
}