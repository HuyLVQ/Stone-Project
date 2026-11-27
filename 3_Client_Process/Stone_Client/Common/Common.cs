using System.Net;
using Stone_Client.Networking.Iot;

namespace Stone_Client
{
    /// <summary>
    /// Connection state shared between discovery, the stream listener and the UI.
    /// </summary>
    public static class Common
    {
        public static readonly object s_lockState = new object();

        private static bool s_serverOnline;
        private static IPEndPoint s_serverEndPoint;
        private static IotImageEncoding s_negotiatedEncoding = IotImageEncoding.Jpeg;

        public static bool ServerOnline { get { lock (s_lockState) return s_serverOnline; } }
        public static IPEndPoint ServerEndPoint { get { lock (s_lockState) return s_serverEndPoint; } }
        public static IotImageEncoding NegotiatedEncoding { get { lock (s_lockState) return s_negotiatedEncoding; } }

        internal static void SetOnline(IPEndPoint p_serverEndPoint, IotImageEncoding p_encoding)
        {
            lock (s_lockState)
            {
                s_serverOnline = true;
                s_serverEndPoint = p_serverEndPoint;
                s_negotiatedEncoding = p_encoding;
            }
        }

        internal static void SetOffline()
        {
            lock (s_lockState)
            {
                s_serverOnline = false;
                s_serverEndPoint = null;
            }
        }
    }
}
