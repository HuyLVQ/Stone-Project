using System;
using System.Diagnostics;
using System.Threading;

public sealed class AIHelper
{
    public static void initializeAI()
    {
        aiClosing();
        var psi = new ProcessStartInfo
        {
            FileName = Config.s_pythonEnvPath,
            Arguments = string.Format(
                "\"{0}\" --input-mmf \"{1}\" --output-mmf \"{2}\" --map-size {3} --width {4} --height {5} --rx \"{6}\" --tx \"{7}\" --router \"{8}\" --worker-count {9} --image-interval {10} --queue-limit {11} --python \"{12}\"",
                Config.s_brokerScriptPath,
                Config.INPUT_MMF_TAGNAME,
                Config.OUTPUT_MMF_TAGNAME,
                Config.MAP_SIZE,
                Config.IMAGE_WIDTH,
                Config.IMAGE_HEIGHT,
                Config.BROKER_RX_ENDPOINT,
                Config.BROKER_TX_ENDPOINT,
                Config.BROKER_ROUTER_ENDPOINT,
                Config.WORKER_COUNT,
                Config.IMAGE_INTERVAL,
                Config.BUFFER_BOUND,
                Config.s_pythonEnvPath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        Common.pythonProcess = new Process { StartInfo = psi };
        Common.pythonProcess.OutputDataReceived += (sender, args) =>
        {
            if (Config.s_isDebugMode && args.Data != null)
                Console.WriteLine("[PY] " + args.Data);
        };
        Common.pythonProcess.ErrorDataReceived += (sender, args) =>
        {
            if (args.Data != null)
                Console.WriteLine("[ERROR] [PY] " + args.Data);
        };
        Common.pythonProcess.Start();
        Common.pythonProcess.BeginOutputReadLine();
        Common.pythonProcess.BeginErrorReadLine();
    }

    public static void warmUpAI()
    {
        // Each worker loads and warms its own model before announcing READY.
        Thread.Sleep(500);
    }

    public static void aiClosing()
    {
        if (Common.pythonProcess != null && !Common.pythonProcess.HasExited)
        {
            try
            {
                using (var killer = Process.Start(new ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    Arguments = string.Format("/PID {0} /T /F", Common.pythonProcess.Id),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }))
                {
                    killer.WaitForExit(5000);
                }
                Common.pythonProcess.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WARN] [AI] Failed to stop broker: " + ex.Message);
            }
        }
        Common.pythonProcess = null;
    }
}
