using System;
using SharpPcap;

namespace MeshProxy
{
    public class EthernetListener
    {
        
        internal EthernetListener()
        {
            var devices = CaptureDeviceList.Instance;

            foreach (var device in devices)
            {
                Console.WriteLine(device.Name);
            }
        }
        
        public void Init()
        {
            
        }
    }
}