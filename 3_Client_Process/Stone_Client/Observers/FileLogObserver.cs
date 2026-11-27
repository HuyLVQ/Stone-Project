using System;
using System.Globalization;
using System.IO;
using System.Text;
using Stone_Client.Event;

namespace Stone_Client.Observer
{
    /// <summary>
    /// Appends every log entry to logs/client_yyyyMMdd.log. File errors are
    /// reported once to the console and never interrupt the publisher.
    /// </summary>
    public class FileLogObserver<T> : IEventObserver<T> where T : ILogEntry
    {
        private readonly object m_lock = new object();
        private readonly string m_directory;
        private bool m_failureReported;

        public FileLogObserver(string p_directory)
        {
            m_directory = p_directory;
        }

        public void Update(T p_entry)
        {
            string line = String.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] [{2}] {3}{4}",
                p_entry.time, p_entry.level, p_entry.source, p_entry.message, Environment.NewLine);
            string path = Path.Combine(m_directory,
                "client_" + p_entry.time.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");

            lock (m_lock)
            {
                try
                {
                    Directory.CreateDirectory(m_directory);
                    File.AppendAllText(path, line, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    if (!m_failureReported)
                    {
                        m_failureReported = true;
                        Console.WriteLine("[WARN] Could not write the log file " + path + ": " + ex.Message);
                    }
                }
            }
        }
    }
}
