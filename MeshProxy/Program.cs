using System;
using System.Threading.Tasks;

namespace MeshProxy
{
    class Program
    {
        static void Main(string[] args)
        {
            MeshProxy proxy = new MeshProxy();
            
            Task.Run(async () => { await proxy.Init(); }).GetAwaiter().GetResult();
        }
    }
}