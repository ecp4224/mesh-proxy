using System.Threading.Tasks;
using System.Linq;
using MeshProxy.Services;

namespace MeshProxy
{
    public class MeshProxy
    {
        public const string Version = "1.0.0";
        
        private Service[] Services = {
            new MeshProxyLog(),
            new MeshProxyConfig(), 
            new IPTableRouter(),
            new EthernetListener(), 
            new PeerManager(),
            new PeerDiscovery()
        };

        public async Task Init()
        {
            foreach (var service in Services)
            {
                await service.Init(this);
            }
        }

        public T GetService<T>() where T : Service
        {
            return Services.OfType<T>().FirstOrDefault();
        }
    }
}