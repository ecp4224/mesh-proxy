using System.Collections.Generic;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using DNS.Client.RequestResolver;
using DNS.Protocol;
using DNS.Protocol.ResourceRecords;
using DNS.Server;
using System;
using SharpPcap;
using PacketDotNet;
using MeshProxy.Network;


namespace MeshProxy.Services
{
    public class PeerManager : Service, IRequestResolver
	{
        public List<Peer> Peers = new List<Peer>();

		private PeerDiscovery Discovery => Owner.GetService<PeerDiscovery>();
        private MeshProxyLog Log => Owner.GetService<MeshProxyLog>();
        private EthernetListener Ethernet => Owner.GetService<EthernetListener>();
        private DnsServer server;

        public string[] KnownPeers
        {
            get { return Peers.Select(p => p.Name).ToArray(); }
        }

        public MeshProxyConfig Config => Owner.GetService<MeshProxyConfig>();
        
        protected override async Task OnInit()
        {
            Log.Info("Starting DNS server");
			server = new DnsServer(this);

			server.Errored += Server_Errored;

            await server.Listen();
        }

		void Server_Errored(System.Exception e)
		{
			Log.Error(e);
		}

		public async Task<IResponse> Resolve(IRequest request)
        {
            IResponse response = Response.FromRequest(request);

            foreach (Question question in response.Questions)
            {
                if (!question.Name.ToString().EndsWith(".local")) continue;
                string domain = question.Name.ToString();
                string peerName = domain.Substring(0, domain.Length - ".local".Length);
                
                Log.Info("Resolving for " + domain + " => " + peerName);

                List<Peer> resolvedPeers = await PeerLookup(peerName);
                
                Log.Info("Found " + resolvedPeers.Count + " peers");

                switch (question.Type)
                {
                    case RecordType.A:
                    {
                        foreach (var peer in resolvedPeers)
                        {
                            IResourceRecord record = new IPAddressResourceRecord(
                                question.Name, IPAddress.Parse(peer.InternalIP));
                            response.AnswerRecords.Add(record);
                        }

                        break;
                    }
                }
            }

            return response;
        }

		public void SendPacket(Peer toPeer, byte[] packet)
		{
            Discovery.UdpClient.Send(packet, packet.Length, toPeer.ExternalEndPoint);
		}

        private async Task<List<Peer>> PeerLookup(string peerName)
        {
			List<Peer> results = new List<Peer>();
            
            //First search our lookup table
            results.AddRange(results.Where(p => p.Name == peerName));
            
            //If we don't have it in our lookup table, ask the network if any of them know it
            if (results.Count != 0) return results;
            foreach (var peer in Peers)
            {
                if (await peer.HasConnection(peerName))
                {
                    results.Add(peer);
                }
            }

            return results;

        }

		public async Task<byte[]> HandshakeNode(IPEndPoint recvBufferRemoteEndPoint, PacketPayload.Handshake payload)
        {
            if (KnownPeers.Contains(payload.Name))
            {
                var rejectPayload = new PacketPayload.Reject("Peer with same name already connected!").Compile();
                
                Log.Warn("Responding with REJECT!\n\tA peer with that name is already connected.");
                
                return rejectPayload;
            }
            
            //Setup UDP client
			Peer peer = new Peer(recvBufferRemoteEndPoint, payload, this);
            bool result = await Ethernet.CreateVirtualFor(peer);

            if (!result)
            {
                Log.Warn("Responding with REJECT\n\tCould not create virtual IP for this peer. Please check you're configuration.");
                
                var rejectPayload = new PacketPayload.Reject("Could not create local virtaul IP!").Compile();
                
                return rejectPayload;
            }
            
            Peers.Add(peer);
            Log.Info("Peer connected and stored " + payload.Name);
            
            var handshake = new PacketPayload.Handshake(Config.Name, MeshProxy.Version, KnownPeers).Compile();

            handshake[1] = 0x02;
            
            return handshake;
        }

		internal async Task<Packet> HandleForwardedPacket(PacketPayload.PacketForward payload)
		{
			var rawPacket = Packet.ParsePacket(payload.type, payload.data);

			//See if we're doing TCP or UDP
			var tcpPacket = (TcpPacket)rawPacket.Extract(typeof(TcpPacket));
			var udpPacket = (UdpPacket)rawPacket.Extract(typeof(UdpPacket));

			string ip;
			if (tcpPacket != null)
			{
				//This is a TCP Packet, let's check our port table

				ip = Config.TCPForwarding(tcpPacket.DestinationPort);
				if (ip == null)
					return null; //No port mapping, let's ignore this forwarded packet
			}
			else if (udpPacket != null)
			{
				//This is a UDP Packet, let's check out port table

				ip = Config.UDPForwarding(udpPacket.DestinationPort);
				if (ip == null)
					return null; //No port mapping, let's ignore this forwarded packet
			}
			else
			{
				return null; //Unusuable packet, let's ignore this forwarded packet
			}


			//We have a port mapping, let's change some things on this packet
			var ipPacket = (IpPacket)tcpPacket.Extract(typeof(IpPacket));

			if (ipPacket != null)
			{
				ipPacket.DestinationAddress = IPAddress.Parse(ip);
				ipPacket.SourceAddress = IPAddress.Parse(Config.EthernetBindAddress); //Set the source to be ourself

				return rawPacket; //Ready to be sent!
			}

			return null; //Unusuable packet, let's ignore this forwarded packet
		}
    }
}