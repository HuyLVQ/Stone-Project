using Stone_Client.Event;

namespace Stone_Client.Observer
{
    public class UILogObserver<T> : IEventObserver<T> where T : ILogEntry
    {
        private readonly MainForm m_mainForm;

        public UILogObserver(MainForm p_mainForm)
        {
            m_mainForm = p_mainForm;
        }

        public void Update(T p_entry)
        {
            m_mainForm.appendLog(p_entry);
        }
    }
}
