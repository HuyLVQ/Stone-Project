using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stone_Application.Event
{
    public class IInformation
    {
        public float deltaPerctMiSang { get; set; }
        public float deltaPerct1x2 { get; set; }
        public float deltaPerct2x4 { get; set; }
        public float deltaPerct4x6 { get; set; }

        public float measuredWeight { get; set; }
    }

    public class IResultInformation
    {
        public float resultPerctMiSang { get; set; }
        public float resultPerct1x2 { get; set; }
        public float resultPerct2x4 { get; set; }
        public float resultPerct4x6 { get; set; }
        public float resultWeight { get; set; }
    }

    public class IImage
    {
        public byte[] recvImage { get; set; }
    }
}
