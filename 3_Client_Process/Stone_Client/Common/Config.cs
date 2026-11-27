using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Stone_Client.Networking.Iot;

namespace Stone_Client
{
    public static class Config
    {
        public const int MAX_HANDSHAKE_BYTES = 4096;                    // Matches IotProtocol.MaxHandshakeBytes on the server
        public const int MAX_ENVELOPE_BYTES = 16 * 1024 * 1024;         // Matches IotProtocol.MaxEnvelopeBytes on the server
        public const int MAX_CLIENT_ID_LENGTH = 128;                    // Server rejects longer identifiers
        public const int MAX_LOG_ROWS = 2000;                           // On-screen log is trimmed beyond this
        public const int HANDSHAKE_REPLY_TIMEOUT_MS = 2000;             // How long one handshake waits for SERVER CONFIRMED
        public const int SEARCH_RETRY_SECONDS = 3;                      // Handshake cadence while no server is confirmed
        public const int VIDEO_WIDTH = 1920;                            // H.264 frames are decoded at the server's camera size
        public const int VIDEO_HEIGHT = 1200;
        public const int MAX_DECODER_STARTS_PER_SESSION = 3;            // FFmpeg restarts allowed after a crash

        public static string ClientId { get; private set; }
        public static IPAddress ServerAddress { get; private set; }     // null = broadcast discovery
        public static int HandshakePort { get; private set; } = 50001;
        public static int ClientTcpPort { get; private set; } = 50002;
        public static IotImageEncoding PreferredEncoding { get; private set; } = IotImageEncoding.Jpeg;
        public static int HandshakeIntervalSeconds { get; private set; } = 10;
        public static int OfflineAfterMissedHandshakes { get; private set; } = 3;
        public static string LogDirectory { get; private set; }
        public static string FfmpegPath { get; private set; }           // null = not found; H.264 unavailable

        /// <summary>
        /// Reads App.config. Invalid values fall back to defaults; the returned
        /// warnings are logged once the log observers exist.
        /// </summary>
        public static List<string> Load()
        {
            var warnings = new List<string>();

            string clientId = Setting("ClientId");
            if (String.IsNullOrWhiteSpace(clientId))
            {
                clientId = Environment.MachineName;
            }
            else if (!IotProtocol.IsValidClientId(clientId.Trim()))
            {
                warnings.Add("ClientId '" + clientId + "' is invalid (empty, longer than " + MAX_CLIENT_ID_LENGTH +
                             " characters, or contains ';' or control characters); using the computer name instead.");
                clientId = Environment.MachineName;
            }
            ClientId = clientId.Trim();

            string serverAddress = Setting("ServerAddress");
            ServerAddress = null;
            if (!String.IsNullOrWhiteSpace(serverAddress))
            {
                IPAddress parsed;
                if (IPAddress.TryParse(serverAddress.Trim(), out parsed) && parsed.AddressFamily == AddressFamily.InterNetwork)
                    ServerAddress = parsed;
                else
                    warnings.Add("ServerAddress '" + serverAddress + "' is not an IPv4 address; using broadcast discovery.");
            }

            HandshakePort = Port("HandshakePort", 50001, warnings);
            ClientTcpPort = Port("ClientTcpPort", 50002, warnings);
            HandshakeIntervalSeconds = Range("HandshakeIntervalSeconds", 10, 1, 25, warnings);
            OfflineAfterMissedHandshakes = Range("OfflineAfterMissedHandshakes", 3, 1, 100, warnings);

            string encoding = Setting("PreferredEncoding");
            if (String.IsNullOrWhiteSpace(encoding) || String.Equals(encoding.Trim(), "jpeg", StringComparison.OrdinalIgnoreCase))
                PreferredEncoding = IotImageEncoding.Jpeg;
            else if (String.Equals(encoding.Trim(), "h264", StringComparison.OrdinalIgnoreCase))
                PreferredEncoding = IotImageEncoding.H264;
            else
            {
                warnings.Add("PreferredEncoding '" + encoding + "' is not 'jpeg' or 'h264'; using jpeg.");
                PreferredEncoding = IotImageEncoding.Jpeg;
            }

            string logDirectory = Setting("LogDirectory");
            if (String.IsNullOrWhiteSpace(logDirectory)) logDirectory = "logs";
            LogDirectory = Path.IsPathRooted(logDirectory)
                ? logDirectory
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, logDirectory);

            FfmpegPath = FindFfmpeg(Setting("FfmpegPath"), warnings);

            return warnings;
        }

        /// <summary>
        /// Same search order as the server: the FfmpegPath setting, then
        /// STONE_FFMPEG_PATH, then ffmpeg.exe next to the executable, then PATH.
        /// </summary>
        private static string FindFfmpeg(string p_configured, List<string> p_warnings)
        {
            if (!String.IsNullOrWhiteSpace(p_configured))
            {
                string configured = p_configured.Trim();
                if (!Path.IsPathRooted(configured))
                    configured = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configured);
                if (File.Exists(configured)) return configured;
                p_warnings.Add("FfmpegPath '" + p_configured + "' does not exist; searching the usual locations instead.");
            }

            string environment = Environment.GetEnvironmentVariable("STONE_FFMPEG_PATH");
            if (!String.IsNullOrWhiteSpace(environment) && File.Exists(environment)) return environment;

            string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            if (File.Exists(local)) return local;

            foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? String.Empty).Split(Path.PathSeparator))
            {
                if (String.IsNullOrWhiteSpace(directory)) continue;
                try
                {
                    string candidate = Path.Combine(directory.Trim().Trim('"'), "ffmpeg.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                catch (ArgumentException) { }       // Malformed PATH entry
            }
            return null;
        }

        private static string Setting(string p_key)
        {
            try { return ConfigurationManager.AppSettings[p_key]; }
            catch (ConfigurationErrorsException) { return null; }
        }

        private static int Port(string p_key, int p_default, List<string> p_warnings)
        {
            return Range(p_key, p_default, 1, 65535, p_warnings);
        }

        private static int Range(string p_key, int p_default, int p_min, int p_max, List<string> p_warnings)
        {
            string text = Setting(p_key);
            if (String.IsNullOrWhiteSpace(text)) return p_default;
            int value;
            if (Int32.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) &&
                value >= p_min && value <= p_max)
                return value;
            p_warnings.Add(p_key + " '" + text + "' must be an integer from " + p_min + " to " + p_max +
                           "; using " + p_default + ".");
            return p_default;
        }
    }
}
