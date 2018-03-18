using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace MeshProxy
{
    public class Peer
    {
        public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan MaxHeartbeatInterval = TimeSpan.FromSeconds(60);
        public string Name { get; set; }
        public string InternalIP { get; private set; }
        public string ExternalIP { get; private set; }

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

        private UdpClient Client;
        private string[] connectedPeers = new string[0];
        private DateTime lastHeartbeat = DateTime.Now;

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