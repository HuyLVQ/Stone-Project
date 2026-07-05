using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Stone_Application.Forms
{
    public partial class FormHome : Form
    {
        private MainForm m_mainForm;
        private static FormHome s_instance;
        public FormHome(MainForm p_mainForm)
        {
            InitializeComponent();
            this.m_mainForm = p_mainForm;
            s_instance = this;

            this.TopLevel = false;
            this.FormBorderStyle = FormBorderStyle.None;
            this.ControlBox = false;           
            this.ShowIcon = false;
            this.ShowInTaskbar = false;

            // Ensure the PictureBox scales the received image to fit its bounds while preserving aspect ratio.
            this.pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        }

        public static void updatePictureBox(Bitmap p_bmp)
        {
            if (s_instance != null && !s_instance.IsDisposed)
            {
                if (s_instance.pictureBox.InvokeRequired)
                {
                    s_instance.pictureBox.Invoke(new Action(() => updatePictureBox(p_bmp)));
                    return;
                }

                if (s_instance.pictureBox.Image != null) s_instance.pictureBox.Image.Dispose();
                s_instance.pictureBox.Image = (Bitmap)p_bmp.Clone();
            }
        }

        public static void enableStreaming()
        {
            s_instance?.Invoke(new Action(() =>
            {
                s_instance.m_startButton.Enabled = false;
                s_instance.m_startButton.ForeColor = System.Drawing.SystemColors.ActiveBorder;

                s_instance.m_stopButton.Enabled = true;
                s_instance.m_stopButton.ForeColor = System.Drawing.SystemColors.ControlText;

                lock(Common.s_lockState)
                {
                    Common.s_currentState = Common.currentState.READY;
                }
            }));

        }

        public static void disableStreaming()
        {
            s_instance?.Invoke(new Action(() =>
            {
                s_instance.m_startButton.Enabled = true;
                s_instance.m_startButton.ForeColor = System.Drawing.SystemColors.ControlText;

                s_instance.m_stopButton.Enabled = false;
                s_instance.m_stopButton.ForeColor = System.Drawing.SystemColors.ActiveBorder;

                lock (Common.s_lockState)
                {
                    Common.s_currentState = Common.currentState.READY;
                }
            }));
        }

        private void startButtonClick(object sender, EventArgs e)
        {
            enableStreaming();
            lock (Common.s_lockState)
            {
                Common.s_currentState = Common.currentState.STREAMING;
            }
        }

        private void stopButtonClick(object sender, EventArgs e)
        {
            disableStreaming();
            lock (Common.s_lockState)
            {
                Common.s_currentState = Common.currentState.READY;
            }

            Common.s_imageQueue = new BlockingCollection<Stone_Application.Event.IImage>(new ConcurrentQueue<Stone_Application.Event.IImage>(), Config.BUFFER_BOUND);
        }
    }
}
