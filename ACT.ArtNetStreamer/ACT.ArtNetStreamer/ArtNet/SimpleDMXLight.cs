using System.Collections;
using System.Collections.Generic;


namespace ArtNet
{
    public class SimpleDmxLight : DmxDevice
    {
        public byte R,G,B,A;
        public bool UseDimmer;

        public int X;
        public int Y;
        public int H;
        public int W;


        public override int NumChannels { get { return UseDimmer ? 4 : 3; } }

        public override void SetData(byte[] dmxData)
        {
            R = dmxData[0];
            G = dmxData[1];
            B = dmxData[2];
            if (UseDimmer) A = dmxData[3];
            base.SetData(dmxData);
        }
    }
}