using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using PacketDotNet;
using SharpPcap;
using MeshProxy.Services;

namespace MeshProxy.Network
{
	public class Peer
	{

		public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);
		public static readonly TimeSpan MaxHeartbeatInterval = TimeSpan.FromSeconds(60);
		private static int idCount = 0;

		public int Id { get; }
		public string Version { get; private set; }
		public string Name { get; private set; }
		public string InternalIP { get; set; }
        public IPEndPoint ExternalEndPoint { get; private set; }
        public string ExternalIP
        {
            get
            {
                return ExternalEndPoint.ToString().Split(':')[0];
            }
        }
		public PeerManager Owner { get; private set; }
		public MeshProxyLog Log
		{
			get
			{
				return Owner.GetService<MeshProxyLog>();
			}
		}

		public TimeSpan LastHeartbeat
		{
			get
			{
				var now = DateTime.Now;
				var duration = now - lastHeartbeat;

				return duration;
			}
		}

		public bool IsAlive => LastHeartbeat >= MaxHeartbeatInterval;

		private string[] connectedPeers = new string[0];
		private DateTime lastHeartbeat = DateTime.Now;

		public Peer(IPEndPoint peerIp, PacketPayload.Handshake payload, PeerManager owner)
		{
			this.Owner = owner;

            this.ExternalEndPoint = peerIp;
			Id = idCount++;

			this.Name = payload.Name;
			connectedPeers = payload.KnownPeers;
			this.Version = payload.Version;
		}

		public void ForwardPacket(RawCapture rawCapture)
		{
			var packet = new PacketPayload.PacketForward(rawCapture).Compile();

			Owner.SendPacket(this, packet);
		}

		public async Task<bool> HasConnection(string peerName)
		{
			if (!IsAlive)
				return false;

			if (connectedPeers.Contains(peerName))
				return true;

			if (HeartbeatInterval <= LastHeartbeat) return false;

			var waitTime = HeartbeatInterval - LastHeartbeat;
			await Task.Delay(waitTime);

			return connectedPeers.Contains(peerName);
		}
	}
}