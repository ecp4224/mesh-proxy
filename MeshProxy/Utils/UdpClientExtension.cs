using System.Net.Sockets;
using MeshProxy.Network;

namespace MeshProxy.Utils
{
    public static class UdpClientExtension
    {
        public static void SendTo(this UdpClient client, Peer peer, byte[] data, int length)
        {
            client.Send(data, length, peer.ExternalIP);
        }
    }
}