using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stone_Application.Event
{
    public class IInformation
    {
        public double deltaPerctMiSang { get; set; }
        public double deltaPerct1x2 { get; set; }
        public double deltaPerct2x4 { get; set; }
        public double deltaPerct4x6 { get; set; }
    }

    public class IImage
    {
        public byte[] recvImage { get; set; }
    }
}
