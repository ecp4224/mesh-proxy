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
        private DnsServer server;

        public PeerManager()
        {
            server = new DnsServer(this);
        }
        
        protected override async Task OnInit()
        {
            await server.Listen();
        }

        public async Task<IResponse> Resolve(IRequest request)
        {
            IResponse response = Response.FromRequest(request);

            foreach (Question question in response.Questions)
            {
                if (!question.Name.ToString().EndsWith(".local")) continue;
                string domain = question.Name.ToString();
                string peerName = domain.Substring(0, domain.Length - ".local".Length);

                List<Peer> resolvedPeers = await PeerLookup(peerName);

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

        private async Task<List<Peer>> PeerLookup(string peerName)
        {
            List<Peer> results = new List<Peer>();
            
            
            //First search our lookup table
            results.AddRange(results.Where(p => p.Name == peerName));
            
            //If we don't have it in our lookup table, ask the network if any of them know it
            if (results.Count == 0)
            {
                foreach (var peer in Peers)
                {
                    if (await peer.HasConnection(peerName))
                    {
                        results.Add(peer);
                    }
                }

                return results;
            }

            return results;
        }
    }
}