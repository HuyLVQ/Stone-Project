using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using Stone_Client.Event;

namespace Stone_Client
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            List<string> configWarnings = Config.Load();
            Application.Run(new MainForm(configWarnings));
        }

        private static void OnThreadException(object p_sender, ThreadExceptionEventArgs p_args)
        {
            ClientEvent.Instance.log(LogLevel.Error, "Client", "Unhandled UI error: " + p_args.Exception.Message);
        }

        private static void OnUnhandledException(object p_sender, UnhandledExceptionEventArgs p_args)
        {
            Exception ex = p_args.ExceptionObject as Exception;
            ClientEvent.Instance.log(LogLevel.Error, "Client", "Unhandled error: " + (ex != null ? ex.ToString() : "unknown"));
        }
    }
}
