using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SharpPcap;
using System.Linq;
using PacketDotNet;

namespace MeshProxy
{
	public class EthernetListener : Service
	{
		private MeshProxyLog Log => Owner.GetService<MeshProxyLog>();
		private ICaptureDevice ethernet;
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

			var anyDevice = CaptureDeviceList.Instance.FirstOrDefault(d => d.Name == "any");

			if (anyDevice == null)
				return;

			anyDevice.OnPacketArrival += AnyDevice_OnPacketArrival;

			anyDevice.Open(DeviceMode.Promiscuous, 0);

			Log.Info("Starting capture");

			anyDevice.StartCapture();

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
				filter[destination](raw);
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

			filter.Add(ip, delegate (RawCapture packet)
			{
				peer.ForwardPacket(packet);
			});

			startIp++;
			if (startIp >= 255)
				startIp = 1;

			return true;
		}
	}
}