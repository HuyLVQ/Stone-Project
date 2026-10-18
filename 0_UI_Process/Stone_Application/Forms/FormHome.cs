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
            Stone_Application.IPC.IPCServices.getInstance().beginSession();
            Common.StartPostgreSqlSession();
            Common.videoRecorder.StartSession();
            Common.s_stopWatchMain.Restart();
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

            // This blocks only until a request that was already sent to the live
            // AI process has completed and been discarded. It is then safe to
            // erase both shared-memory regions before the offline pass starts.
            Stone_Application.IPC.IPCServices.getInstance().endSessionAndClear();
            string videoPath = Common.videoRecorder.StopSession();
            //Common.s_imageQueue = new BlockingCollection<Stone_Application.Event.IImage>(new ConcurrentQueue<Stone_Application.Event.IImage>(), Config.BUFFER_BOUND);
            while (Common.s_imageQueue.TryTake(out _))
            {
                ;
            }

            // The offline pass is deliberately synchronous: a new recording cannot begin
            // until the finalized recording has become the authoritative result.
            this.m_startButton.Enabled = false;
            this.m_stopButton.Enabled = false;
            this.UseWaitCursor = true;
            try
            {
                AIHelper.VideoInferenceResult result = AIHelper.runVideoInference(videoPath);
                Common.PersistFinalSessionResult(new Stone_Application.Event.IInformation
                {
                    countMiSang = checked((long)Math.Round(result.MiSang)),
                    count1x2 = checked((long)Math.Round(result.OneByTwo)),
                    count2x4 = checked((long)Math.Round(result.TwoByFour)),
                    count4x6 = checked((long)Math.Round(result.FourBySix)),
                    measuredWeight1 = checked((float)result.AverageWeights[0]),
                    measuredWeight2 = checked((float)result.AverageWeights[1]),
                    measuredWeight3 = checked((float)result.AverageWeights[2]),
                    measuredWeight4 = checked((float)result.AverageWeights[3])
                });
                MessageBox.Show(this, FormatVideoInferenceResult(result), "Video inference result", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "The recording was finalized, but video inference failed.\n\n" + ex.Message,
                    "Video inference error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.UseWaitCursor = false;
                this.m_startButton.Enabled = true;
                this.m_startButton.ForeColor = System.Drawing.SystemColors.ControlText;
            }
        }

        private static string FormatVideoInferenceResult(AIHelper.VideoInferenceResult p_result)
        {
            return String.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Frames processed: {0}\n\nAccumulated counts\n  MiSang: {1:F0}\n  1x2: {2:F0}\n  2x4: {3:F0}\n  4x6: {4:F0}\n\nAverage per-frame weights\n  Polynomial: {5:F2}\n  Random forest: {6:F2}\n  XGBoost: {7:F2}\n  XGBoost v2: {8:F2}",
                p_result.FrameCount, p_result.MiSang, p_result.OneByTwo, p_result.TwoByFour, p_result.FourBySix,
                p_result.AverageWeights[0], p_result.AverageWeights[1], p_result.AverageWeights[2], p_result.AverageWeights[3]);
        }
    }
}
