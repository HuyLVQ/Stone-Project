using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using Stone_Client.Event;
using Stone_Client.Networking.Iot;
using Stone_Client.Observer;

namespace Stone_Client
{
    public partial class MainForm : Form
    {
        private const string LOG_SOURCE = "Client";
        private readonly List<string> m_configWarnings;
        private UILogObserver<ILogEntry> m_uiLogObserver;
        private UIFrameObserver<IFrame> m_uiFrameObserver;
        private IotStreamListener m_streamListener;
        private IotDiscovery m_discovery;
        private Bitmap m_pendingFrame;      // Newest frame not yet shown; swapped atomically

        public MainForm(List<string> p_configWarnings)
        {
            InitializeComponent();
            m_configWarnings = p_configWarnings ?? new List<string>();

            imageListLevels.Images.Add(LogLevel.Information.ToString(), SystemIcons.Information.ToBitmap());
            imageListLevels.Images.Add(LogLevel.Warning.ToString(), SystemIcons.Warning.ToBitmap());
            imageListLevels.Images.Add(LogLevel.Error.ToString(), SystemIcons.Error.ToBitmap());

            this.Text = "Stone Client - " + Config.ClientId;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            ClientEvent clientEvent = ClientEvent.Instance;
            m_uiLogObserver = new UILogObserver<ILogEntry>(this);
            clientEvent.attachLogObserver(m_uiLogObserver);
            clientEvent.attachLogObserver(new FileLogObserver<ILogEntry>(Config.LogDirectory));
            clientEvent.attachResultObserver(new ResultLogObserver<IResult>());
            m_uiFrameObserver = new UIFrameObserver<IFrame>(this);
            clientEvent.attachFrameObserver(m_uiFrameObserver);

            clientEvent.log(LogLevel.Information, LOG_SOURCE, String.Format(CultureInfo.InvariantCulture,
                "Stone Client {0} started as '{1}'.", typeof(MainForm).Assembly.GetName().Version, Config.ClientId));
            foreach (string warning in m_configWarnings)
                clientEvent.log(LogLevel.Warning, "Config", warning);

            // H.264 is decoded by ffmpeg.exe; without it, ask the server for JPEG instead
            // of receiving a stream this client cannot show.
            IotImageEncoding requestedEncoding = Config.PreferredEncoding;
            if (requestedEncoding == IotImageEncoding.H264 && Config.FfmpegPath == null)
            {
                clientEvent.log(LogLevel.Warning, LOG_SOURCE,
                    "PreferredEncoding is h264 but ffmpeg.exe was not found (FfmpegPath, STONE_FFMPEG_PATH, next to the exe, or PATH); requesting JPEG instead.");
                requestedEncoding = IotImageEncoding.Jpeg;
            }
            else if (requestedEncoding == IotImageEncoding.H264)
            {
                clientEvent.log(LogLevel.Information, LOG_SOURCE, "H.264 frames will be decoded with " + Config.FfmpegPath + ".");
            }

            // The server connects to us, so the listener must be up before we announce ourselves.
            m_streamListener = new IotStreamListener(Config.ClientTcpPort, Config.FfmpegPath);
            if (!m_streamListener.Start())
            {
                clientEvent.log(LogLevel.Error, LOG_SOURCE,
                    "Discovery was not started because the client cannot receive data. Close the program using TCP port " +
                    Config.ClientTcpPort + " (another client instance?) and restart.");
                return;
            }

            m_discovery = new IotDiscovery(Config.ClientId, Config.ServerAddress, Config.HandshakePort,
                Config.ClientTcpPort, requestedEncoding);
            m_discovery.Start();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Stop announcing first so the server stops dialling, then close the listener.
            if (m_discovery != null) { m_discovery.Dispose(); m_discovery = null; }
            if (m_streamListener != null) { m_streamListener.Dispose(); m_streamListener = null; }

            ClientEvent.Instance.detachFrameObserver(m_uiFrameObserver);
            ClientEvent.Instance.detachLogObserver(m_uiLogObserver);
            ClientEvent.Instance.log(LogLevel.Information, LOG_SOURCE, "Stone Client stopped.");

            Bitmap pending = Interlocked.Exchange(ref m_pendingFrame, null);
            if (pending != null) pending.Dispose();
            Image shown = pictureBoxStream.Image;
            pictureBoxStream.Image = null;
            if (shown != null) shown.Dispose();
        }

        /// <summary>
        /// Thread-safe; takes ownership of <paramref name="p_frame"/>. Only the newest
        /// frame is kept: if the UI has not shown the previous one yet, it is dropped,
        /// so a slow UI thread never builds up a backlog of frames.
        /// </summary>
        internal void showFrame(Bitmap p_frame)
        {
            if (p_frame == null) return;
            if (this.IsDisposed || this.Disposing) { p_frame.Dispose(); return; }

            Bitmap superseded = Interlocked.Exchange(ref m_pendingFrame, p_frame);
            if (superseded != null)
            {
                // An update is already scheduled and will pick up the newer frame.
                superseded.Dispose();
                return;
            }

            try { this.BeginInvoke((Action)applyPendingFrame); }
            catch (InvalidOperationException)
            {
                // Handle not created yet or already destroyed.
                Bitmap unshown = Interlocked.Exchange(ref m_pendingFrame, null);
                if (unshown != null) unshown.Dispose();
            }
        }

        private void applyPendingFrame()
        {
            Bitmap frame = Interlocked.Exchange(ref m_pendingFrame, null);
            if (frame == null) return;
            if (this.IsDisposed || this.Disposing) { frame.Dispose(); return; }

            Image previous = pictureBoxStream.Image;
            pictureBoxStream.Image = frame;
            if (previous != null) previous.Dispose();
        }

        /// <summary>
        /// Thread-safe: worker threads post here; rows are added on the UI thread, newest first.
        /// </summary>
        internal void appendLog(ILogEntry p_entry)
        {
            if (p_entry == null || this.IsDisposed || this.Disposing) return;
            if (this.InvokeRequired)
            {
                try { this.BeginInvoke((Action)(() => appendLog(p_entry))); }
                catch (InvalidOperationException) { }    // Handle not created yet or already destroyed
                return;
            }

            var item = new ListViewItem(p_entry.level.ToString(), p_entry.level.ToString());
            item.SubItems.Add(p_entry.time.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            item.SubItems.Add(p_entry.source ?? String.Empty);
            item.SubItems.Add(p_entry.message ?? String.Empty);
            if (p_entry.level == LogLevel.Error) item.ForeColor = Color.Firebrick;
            else if (p_entry.level == LogLevel.Warning) item.ForeColor = Color.DarkGoldenrod;

            listViewLog.BeginUpdate();
            try
            {
                listViewLog.Items.Insert(0, item);
                while (listViewLog.Items.Count > Config.MAX_LOG_ROWS)
                    listViewLog.Items.RemoveAt(listViewLog.Items.Count - 1);
            }
            finally
            {
                listViewLog.EndUpdate();
            }
        }
    }
}
