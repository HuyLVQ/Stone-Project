using System.Drawing;
using Stone_Client.Event;

namespace Stone_Client.Observer
{
    /// <summary>
    /// Shows decoded frames in the main window's picture box. The published
    /// bitmap is disposed after notification, so the window gets its own copy.
    /// </summary>
    public class UIFrameObserver<T> : IEventObserver<T> where T : IFrame
    {
        private readonly MainForm m_mainForm;

        public UIFrameObserver(MainForm p_mainForm)
        {
            m_mainForm = p_mainForm;
        }

        public void Update(T p_frame)
        {
            m_mainForm.showFrame(new Bitmap(p_frame.image));
        }
    }
}
