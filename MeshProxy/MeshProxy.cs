using System.Threading.Tasks;
using System.Linq;

namespace MeshProxy
{
    public class MeshProxy
    {
        private Service[] Services = {
            new PeerDiscovery(),
            new PeerManager()
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