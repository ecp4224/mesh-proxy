using System.Threading.Tasks;

namespace MeshProxy
{
    public abstract class Service
    {
        public MeshProxy Owner { get; private set; }

        public async Task Init(MeshProxy Owner)
        {
            this.Owner = Owner;

            await OnInit();
        }
        
        protected async virtual Task OnInit() { }
    }
}