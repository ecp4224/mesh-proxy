using System;

namespace MeshProxy
{
    public class MeshProxyLog : Service
    {
        public MeshProxyConfig Config => Owner.GetService<MeshProxyConfig>();

        public void Info(string message)
        {
            Console.WriteLine(message);
        }

        public void Info(object message)
        {
            Console.WriteLine(message);
        }

        public void Warn(string message)
        {
            var old = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine(message);
            Console.ForegroundColor = old;
        }

        public void Error(Exception message)
        {
            var old = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ForegroundColor = old; 
        }
        
        public void Error(string message)
        {
            var old = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ForegroundColor = old; 
        }
    }
}