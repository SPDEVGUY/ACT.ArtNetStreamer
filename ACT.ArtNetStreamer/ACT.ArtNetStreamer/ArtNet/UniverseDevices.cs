using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArtNet
{
    public class UniverseDevices
    {
        public string UniverseName { get; set; }
        public int UniverseIndex { get; set; }

        public List<DmxDevice> Devices { get; set; }

        public UniverseDevices(List<DmxDevice> devices, int universeIndex, string universeName=null)
        {
            Devices = devices ?? new List<DmxDevice>();
            UniverseIndex = universeIndex;
            UniverseName = universeName ?? $"Universe{universeIndex}";
        }

        public void Initialize()
        {
            var startChannel = 0;
            foreach (var d in Devices)
                if (d != null)
                {
                    d.StartChannelIx = startChannel;
                    startChannel += d.NumChannels;
                    d.Name = string.Format("{0}:({1},{2:d3}-{3:d3})", d.GetType().ToString(), UniverseIndex, d.StartChannelIx, startChannel - 1);
                }
            if (512 < startChannel)
                Console.WriteLine("The number({0}) of channels of the universe {1} exceeds the upper limit(512 channels)!", startChannel, UniverseIndex);
        }
    }
}
