using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stone_Application.Event
{
    public class IInformation
    {
        public Int64 countMiSang { get; set; }
        public Int64 count1x2 { get; set; }
        public Int64 count2x4 { get; set; }
        public Int64 count4x6 { get; set; }

        public float measuredWeight1 { get; set; }
        public float measuredWeight2 { get; set; }
        public float measuredWeight3 { get; set; }
        public float measuredWeight4 { get; set; }

        // Compatibility alias for views and repositories that display the
        // primary model result.
        public float measuredWeight
        {
            get { return measuredWeight1; }
            set { measuredWeight1 = value; }
        }

        // Populated by the PostgreSQL session lifecycle. These fields are
        // persisted with every measurement so the first row of each run is
        // an explicit accumulation boundary.
        public bool isStartOfSession { get; set; }
        public int sessionId { get; set; }
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
