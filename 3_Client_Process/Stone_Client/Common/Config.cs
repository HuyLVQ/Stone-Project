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

        public static string ClientId { get; private set; }
        public static IPAddress ServerAddress { get; private set; }     // null = broadcast discovery
        public static int HandshakePort { get; private set; } = 50001;
        public static int ClientTcpPort { get; private set; } = 50002;
        public static IotImageEncoding PreferredEncoding { get; private set; } = IotImageEncoding.Jpeg;
        public static int HandshakeIntervalSeconds { get; private set; } = 10;
        public static int OfflineAfterMissedHandshakes { get; private set; } = 3;
        public static string LogDirectory { get; private set; }

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

            return warnings;
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
