using System.Collections.Generic;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using DNS.Client.RequestResolver;
using DNS.Protocol;
using DNS.Protocol.ResourceRecords;
using DNS.Server;

namespace MeshProxy
{
    public class PeerManager : Service, IRequestResolver
    {
        private List<Peer> Peers = new List<Peer>();

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
			Discovery.UdpClient.Send(packet, packet.Length, toPeer.ExternalIP);
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
    }
}