using System.Net.Sockets;

namespace MeshProxy
{
    public static class UdpClientExtension
    {
        public static void SendTo(this UdpClient client, Peer peer, byte[] data, int length)
        {
            client.Send(data, length, peer.ExternalIP);
        }
    }
}