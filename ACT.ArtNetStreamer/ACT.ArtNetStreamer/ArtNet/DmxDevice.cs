using System;
using System.Collections;
using System.Collections.Generic;

namespace ArtNet
{
    public abstract class DmxDevice
    {
        public byte[] DmxData { get; set; }
        public int StartChannelIx { get; set; }
        public string Name { get; set; }

        public Action<DmxDevice> OnDataUpdated;

        public abstract int NumChannels { get; }

        public virtual void SetData(byte[] dmxData)
        {
            DmxData = dmxData;

            OnDataUpdated?.Invoke(this);
        }
    }
}