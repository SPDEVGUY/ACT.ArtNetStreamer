using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArtNet.Enums
{
    public enum ChannelFunction
    {
        Unknown = -1,

        Color_R = 0,
        Color_RFine = 1,
        Color_G = 2,
        Color_GFine = 3,
        Color_B = 4,
        Color_BFine = 5,
        Color_W = 6,
        Color_WFine = 7,

        Pan = 8,
        PanFine = 9,
        Tilt = 10,
        TiltFine = 11,
        RotSpeed = 12,
    }
}
