using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MeshProxy
{
    public class PeerDiscovery : Service
    {
        public const int PORT = 1337;
		public UdpClient UdpClient { get; private set; }
        private bool ignoreFirst = true;
        public PeerManager Manager => Owner.GetService<PeerManager>();
        public MeshProxyConfig Config => Owner.GetService<MeshProxyConfig>();
        public MeshProxyLog Log => Owner.GetService<MeshProxyLog>();
        
        protected override async Task OnInit()
        {
            Log.Info("Starting PeerDiscovery Service");

            using (var tempUdpClient = new AdvanceUdpClient(new IPEndPoint(Config.WifiBindAddress, PORT)))
            {

                var config = Owner.GetService<MeshProxyConfig>();
                var handshake =
                    new PacketPayload.Handshake(config.Name, MeshProxy.Version, Manager.KnownPeers).Compile();

                Log.Info("Broadcasting handshack");

                var broadcastAddress = new IPEndPoint(IPAddress.Broadcast, PORT);

                tempUdpClient.Send(handshake, handshake.Length, broadcastAddress);
            }

            await Listen();
        }

        private async Task Listen()
        {
            UdpClient = new UdpClient(PORT);
            
            while (IsRunning)
            {
                Log.Info("Listening for packets");
                var recvBuffer = await UdpClient.ReceiveAsync();
                Log.Info("Got packet from " + recvBuffer.RemoteEndPoint);
                
                var recvData = recvBuffer.Buffer;
                
                var jsonData = new byte[recvData.Length - 2];
                Array.Copy(recvData, 2, jsonData, 0, jsonData.Length);

                var json = Encoding.UTF8.GetString(jsonData);
                if (recvData[0] == 0x00 && recvData[1] == 0x00)
                {
                    var payload = JsonConvert.DeserializeObject<PacketPayload.Handshake>(json);

                    if (ignoreFirst && payload.Name == Config.Name)
                    {
                        ignoreFirst = false;
                        continue; //Ignore this message
                    }
                    
                    Log.Info("Got handshack from " + payload.Name);
                    byte[] response = await Manager.HandshakeNode(recvBuffer.RemoteEndPoint, payload); //See if we can accept peer
                    
                    Log.Info("Sending response");
                    UdpClient.Send(response, response.Length, recvBuffer.RemoteEndPoint.Address.ToString(), PORT);
                } 
                else if (recvData[0] == 0x00 && recvData[1] == 0x01)
                {
                    var payload = JsonConvert.DeserializeObject<PacketPayload.Reject>(json);
                    
                    Log.Warn("REJECTED: " + payload.Reason);
                }
                else if (recvData[0] == 0x00 && recvData[1] == 0x02)
                {
                    var payload = JsonConvert.DeserializeObject<PacketPayload.Handshake>(json);

                    await Manager.HandshakeNode(recvBuffer.RemoteEndPoint, payload); //Accept peer
                    
                    Log.Info("Got handshake response from " + payload.Name);
                }
            }
        }
    }
}