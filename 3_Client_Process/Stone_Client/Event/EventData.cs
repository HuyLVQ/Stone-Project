using System;

namespace Stone_Client.Event
{
    public enum LogLevel { Information, Warning, Error }

    public class ILogEntry
    {
        public LogLevel level { get; set; }
        public DateTime time { get; set; }
        public string source { get; set; }
        public string message { get; set; }
    }

    /// <summary>
    /// Finalized offline result of one server session (InferenceResult on the wire).
    /// Percentages use the 0-100 scale; fields the server omitted are zero.
    /// </summary>
    public class IResult
    {
        public uint schemaVersion { get; set; }
        public string sessionId { get; set; }
        public ulong sequenceNumber { get; set; }
        public DateTime finalizedAt { get; set; }          // Local time; MinValue when the server sent none
        public double resultPerctMiSang { get; set; }
        public double resultPerct1x2 { get; set; }
        public double resultPerct2x4 { get; set; }
        public double resultPerct4x6 { get; set; }
        public double accumulatedWeight { get; set; }
        public string weightUnit { get; set; }
        public string[] warnings { get; set; }             // Contract deviations noticed while mapping
    }
}
