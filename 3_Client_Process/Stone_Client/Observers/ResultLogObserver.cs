using System;
using System.Globalization;
using Stone_Client.Event;

namespace Stone_Client.Observer
{
    /// <summary>
    /// Records each received offline result as a log entry (screen and file).
    /// </summary>
    public class ResultLogObserver<T> : IEventObserver<T> where T : IResult
    {
        private const string SOURCE = "Result";

        public void Update(T p_result)
        {
            ClientEvent.Instance.log(LogLevel.Information, SOURCE, format(p_result));
            if (p_result.warnings == null) return;
            foreach (string warning in p_result.warnings)
                ClientEvent.Instance.log(LogLevel.Warning, SOURCE, "Session " + p_result.sessionId + ": " + warning);
        }

        internal static string format(IResult p_result)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            string finalized = p_result.finalizedAt == DateTime.MinValue
                ? "unknown time"
                : p_result.finalizedAt.ToString("yyyy-MM-dd HH:mm:ss", inv);
            return String.Format(inv,
                "Offline result for session {0} (finalized {1}): MiSang {2:0.00}% | 1x2 {3:0.00}% | 2x4 {4:0.00}% | 4x6 {5:0.00}% | weight {6:0.00} {7}",
                p_result.sessionId, finalized,
                p_result.resultPerctMiSang, p_result.resultPerct1x2, p_result.resultPerct2x4, p_result.resultPerct4x6,
                p_result.accumulatedWeight, String.IsNullOrEmpty(p_result.weightUnit) ? "(no unit)" : p_result.weightUnit);
        }
    }
}
