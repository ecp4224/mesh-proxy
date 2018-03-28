using System.Threading.Tasks;

namespace MeshProxy
{
    public abstract class Service
    {
        public bool IsRunning { get; set; }
        public MeshProxy Owner { get; private set; }

        public async Task Init(MeshProxy _owner)
        {
            this.Owner = _owner;
            IsRunning = true;
            await OnInit();
        }

		public T GetService<T>() where T : Service
		{
			return Owner.GetService<T>();
		}
        
        protected virtual async Task OnInit() { }
    }
}