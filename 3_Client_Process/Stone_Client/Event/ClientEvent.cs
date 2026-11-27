using System;
using System.Collections.Generic;
using Stone_Client.Observer;

namespace Stone_Client.Event
{
    /// <summary>
    /// Subject that routes log entries and results from worker threads to observers.
    /// Observers run on the publishing thread and must marshal to the UI themselves.
    /// </summary>
    public class ClientEvent
    {
        private static readonly ClientEvent s_instance = new ClientEvent();
        private readonly object m_lock = new object();
        private readonly List<IEventObserver<ILogEntry>> m_logObservers = new List<IEventObserver<ILogEntry>>();
        private readonly List<IEventObserver<IResult>> m_resultObservers = new List<IEventObserver<IResult>>();
        private readonly List<IEventObserver<IFrame>> m_frameObservers = new List<IEventObserver<IFrame>>();

        public static ClientEvent Instance { get { return s_instance; } }

        private ClientEvent() { }

        public void attachLogObserver(IEventObserver<ILogEntry> p_observer)
        {
            lock (m_lock) m_logObservers.Add(p_observer);
        }

        public void detachLogObserver(IEventObserver<ILogEntry> p_observer)
        {
            lock (m_lock) m_logObservers.Remove(p_observer);
        }

        public void attachResultObserver(IEventObserver<IResult> p_observer)
        {
            lock (m_lock) m_resultObservers.Add(p_observer);
        }

        public void detachResultObserver(IEventObserver<IResult> p_observer)
        {
            lock (m_lock) m_resultObservers.Remove(p_observer);
        }

        public void attachFrameObserver(IEventObserver<IFrame> p_observer)
        {
            lock (m_lock) m_frameObservers.Add(p_observer);
        }

        public void detachFrameObserver(IEventObserver<IFrame> p_observer)
        {
            lock (m_lock) m_frameObservers.Remove(p_observer);
        }

        public void log(LogLevel p_level, string p_source, string p_message)
        {
            notifyLog(new ILogEntry { level = p_level, time = DateTime.Now, source = p_source, message = p_message });
        }

        public void notifyLog(ILogEntry p_entry)
        {
            IEventObserver<ILogEntry>[] observers;
            lock (m_lock) observers = m_logObservers.ToArray();
            foreach (var observer in observers)
            {
                try { observer.Update(p_entry); }
                catch (Exception ex) { Console.WriteLine("[WARN] Log observer failed: " + ex.Message); }
            }
        }

        public void notifyResult(IResult p_result)
        {
            IEventObserver<IResult>[] observers;
            lock (m_lock) observers = m_resultObservers.ToArray();
            foreach (var observer in observers)
            {
                try { observer.Update(p_result); }
                catch (Exception ex) { log(LogLevel.Warning, "Client", "Result observer failed: " + ex.Message); }
            }
        }

        /// <summary>Publishes a decoded frame, then disposes its bitmap.</summary>
        public void notifyFrame(IFrame p_frame)
        {
            if (p_frame == null || p_frame.image == null) return;
            IEventObserver<IFrame>[] observers;
            lock (m_lock) observers = m_frameObservers.ToArray();
            try
            {
                foreach (var observer in observers)
                {
                    try { observer.Update(p_frame); }
                    catch (Exception ex) { log(LogLevel.Warning, "Client", "Frame observer failed: " + ex.Message); }
                }
            }
            finally
            {
                p_frame.image.Dispose();
            }
        }
    }
}
