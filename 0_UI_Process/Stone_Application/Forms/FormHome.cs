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
        private bool m_aiInitialized;
        private bool m_memorySafe = true;
        public bool IsCompletingSession { get; private set; }
        public bool HasInitializedAi => m_aiInitialized;

        public void RefreshSessionControls()
        {
            bool connected = Common.camera != null && Common.camera.IsConnected;
            bool streaming;
            lock (Common.s_lockState) streaming = Common.s_currentState == Common.currentState.STREAMING;
            m_startButton.Enabled = connected && m_aiInitialized && m_memorySafe && !IsCompletingSession && !streaming;
            m_stopButton.Enabled = connected && streaming && !IsCompletingSession;
            m_startButton.ForeColor = m_startButton.Enabled ? SystemColors.ControlText : SystemColors.ActiveBorder;
            m_stopButton.ForeColor = m_stopButton.Enabled ? SystemColors.ControlText : SystemColors.ActiveBorder;
        }

        public async Task HandleCameraDisconnectedAsync()
        {
            RefreshSessionControls();
            bool streaming;
            lock (Common.s_lockState) streaming = Common.s_currentState == Common.currentState.STREAMING;
            if (streaming && !IsCompletingSession)
                await CompleteSessionAsync(true);
            else
                ShowCameraDisconnectedMessage(IsCompletingSession);
        }

        private void ShowCameraDisconnectedMessage(bool completing)
        {
            MessageBox.Show(m_mainForm, "The camera connection has been lost or image acquisition has been interrupted.\n\n" +
                (completing ? "The session is stopping. Its recording is being finalized for offline analysis.\n\n" : "") +
                "Check the camera connection and power, then use Connect Camera in Settings to retry.",
                "Camera disconnected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
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

        // Takes ownership of the bitmap, including when the callback becomes stale.
        public static void updatePictureBox(Bitmap bitmap, long sessionToken)
        {
            var form = s_instance;
            if (form == null || form.IsDisposed || !form.IsHandleCreated) { bitmap.Dispose(); return; }
            try
            {
                form.BeginInvoke(new Action(() =>
                {
                    if (form.IsDisposed || !Stone_Application.IPC.IPCServices.getInstance().IsCurrentSession(sessionToken))
                    { bitmap.Dispose(); return; }
                    var previous = form.pictureBox.Image;
                    form.pictureBox.Image = bitmap;
                    previous?.Dispose();
                }));
            }
            catch (InvalidOperationException) { bitmap.Dispose(); }
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

        public static void requireAiInitialization()
        {
            if (s_instance == null || s_instance.IsDisposed) return;
            s_instance.m_aiInitialized = false;
            s_instance.m_startButton.Enabled = false;
            s_instance.m_stopButton.Enabled = false;
            lock (Common.s_lockState) Common.s_currentState = Common.currentState.UN_INIT;
        }

        public static void disableStreaming()
        {
            s_instance?.Invoke(new Action(() =>
            {
                s_instance.m_aiInitialized = true;
                lock (Common.s_lockState)
                {
                    Common.s_currentState = Common.currentState.READY;
                }
                s_instance.RefreshSessionControls();
            }));
        }

        private void startButtonClick(object sender, EventArgs e)
        {
            RefreshSessionControls();
            if (!m_startButton.Enabled) return;
            var ipc = Stone_Application.IPC.IPCServices.getInstance();
            try
            {
                if (!AIHelper.IsLiveReady || Common.pythonProcess == null || Common.pythonProcess.HasExited)
                { AIHelper.initializeAI(); AIHelper.warmUpAI(); }
                _ = AIHelper.InitializeOfflineWorkerAsync();
                if (Common.camera == null || !Common.camera.IsConnected)
                    throw new InvalidOperationException("Reconnect the camera before starting a session.");
                Common.StartPostgreSqlSession();
                ipc.beginSession();
                Common.videoRecorder.StartSession(ipc.SessionToken);
                Common.s_stopWatchMain.Restart();
                enableStreaming();
                lock (Common.s_lockState) Common.s_currentState = Common.currentState.STREAMING;
            }
            catch (Exception ex)
            {
                ipc.deactivateSession();
                try { Common.videoRecorder.StopSession(); } catch (Exception cleanup) { Console.WriteLine(cleanup.Message); }
                lock (Common.s_lockState) Common.s_currentState = Common.currentState.READY;
                RefreshSessionControls();
                MessageBox.Show(this, "Could not start recording.\n\n" + ex.Message, "Session error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void stopButtonClick(object sender, EventArgs e)
        {
            await CompleteSessionAsync(false);
        }

        private async Task CompleteSessionAsync(bool cameraDisconnected)
        {
            if (IsCompletingSession) return;
            lock (Common.s_lockState)
            {
                if (Common.s_currentState != Common.currentState.STREAMING) return;
                Common.s_currentState = Common.currentState.READY;
            }
            IsCompletingSession = true;
            m_mainForm.RefreshCameraControls();
            this.UseWaitCursor = true;
            var ipc = Stone_Application.IPC.IPCServices.getInstance();
            bool memorySafe = false;
            bool aiSettled = false;
            AIHelper.VideoInferenceResult result = null;
            string settlementError = null;
            try
            {
                ipc.deactivateSession();
                Common.videoRecorder.StopAccepting();
                Task completion = Task.Run(() =>
                {
                    string videoPath;
                    try
                    {
                        aiSettled = ipc.endSessionAndClear();
                        memorySafe = true;
                    }
                    catch (Exception ex)
                    {
                        // An unsafe live worker blocks the next START, but must not
                        // prevent analysis of the independently finalized recording.
                        settlementError = ex.Message;
                    }
                    finally
                    {
                        // Recording drains/finalizes even when live AI recovery fails.
                        videoPath = Common.videoRecorder.StopSession();
                    }

                    result = AIHelper.runVideoInference(videoPath);
                    Common.PersistFinalSessionResult(new Stone_Application.Event.IInformation
                    {
                        analysisStatus = result.Status,
                        expectedFrames = result.ExpectedFrames,
                        processedFrames = result.FrameCount,
                        skippedFrames = result.SkippedFrames,
                        droppedFrames = result.DroppedFrames,
                        countMiSang = checked((long)Math.Round(result.MiSang)),
                        count1x2 = checked((long)Math.Round(result.OneByTwo)),
                        count2x4 = checked((long)Math.Round(result.TwoByFour)),
                        count4x6 = checked((long)Math.Round(result.FourBySix)),
                        measuredWeight1 = checked((float)result.AverageWeights[0]),
                        measuredWeight2 = checked((float)result.AverageWeights[1]),
                        measuredWeight3 = checked((float)result.AverageWeights[2]),
                        measuredWeight4 = checked((float)result.AverageWeights[3])
                    });
                });
                // Start finalization before the modal alert: acknowledging the
                // disconnect is not a prerequisite for saving the recording.
                if (cameraDisconnected) ShowCameraDisconnectedMessage(true);
                await completion;
                this.m_mainForm.RefreshLogs();
                string aiWarning = settlementError != null
                    ? "\n\nLive AI could not be stopped safely. START remains disabled.\n" + settlementError
                    : aiSettled ? "" : "\n\nLive AI timed out and was terminated. It will be restarted before the next session.";
                MessageBox.Show(this, FormatVideoInferenceResult(result) + aiWarning,
                    "Video inference result", MessageBoxButtons.OK,
                    result.Status == "Complete" && aiSettled ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "The session could not finish successfully. No finalized aggregate was saved.\n\n" + ex.Message,
                    "Session completion error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.UseWaitCursor = false;
                m_memorySafe = memorySafe;
                IsCompletingSession = false;
                m_mainForm.RefreshCameraControls();
            }
        }

        private static string FormatVideoInferenceResult(AIHelper.VideoInferenceResult p_result)
        {
            return String.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Accumulated inference counts\n  MiSang: {0:F0}\n  1x2: {1:F0}\n  2x4: {2:F0}\n  4x6: {3:F0}\n\nWeight prediction: {4:F2} kg",
                p_result.MiSang, p_result.OneByTwo, p_result.TwoByFour, p_result.FourBySix,
                p_result.AverageWeights[0]);
        }
    }
}
