using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO.MemoryMappedFiles;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Stone_Application.Forms;
using Stone_Application;

public sealed class AIHelper
{
    private static volatile bool s_liveReady;
    private static readonly object s_pythonOutputLock = new object();
    private static readonly object s_offlineLock = new object();
    private static readonly StringBuilder s_pythonErrorOutput = new StringBuilder();
    private static Process s_offlineProcess;
    private static TaskCompletionSource<bool> s_offlineReady;
    private static TaskCompletionSource<string> s_offlineResponse;
    private static Task s_offlineInitializationTask;
    private static bool s_offlineStopRequested;
    public static bool IsLiveReady { get { return s_liveReady; } }
    public static void MarkUnavailable() { s_liveReady = false; }
    public sealed class VideoInferenceResult
    {
        public int FrameCount { get; set; }
        public long ExpectedFrames { get; set; }
        public long SkippedFrames { get; set; }
        public long DroppedFrames { get; set; }
        public string Status { get; set; }
        public double MiSang { get; set; }
        public double OneByTwo { get; set; }
        public double TwoByFour { get; set; }
        public double FourBySix { get; set; }
        public double[] AverageWeights { get; set; }
    }

    public static VideoInferenceResult runVideoInference(string p_videoPath)
    {
        if (String.IsNullOrWhiteSpace(p_videoPath) || !File.Exists(p_videoPath))
            throw new FileNotFoundException("The finalized recording could not be found.", p_videoPath);
        InitializeOfflineWorker();
        TaskCompletionSource<string> response;
        lock (s_offlineLock)
        {
            if (s_offlineProcess == null || s_offlineProcess.HasExited)
                throw new InvalidOperationException("The offline inference worker is not ready. Reconnect AI and try again.");
            if (s_offlineResponse != null)
                throw new InvalidOperationException("The offline inference worker is already processing a recording.");
            response = s_offlineResponse = new TaskCompletionSource<string>();
            try { s_offlineProcess.StandardInput.WriteLine("offline_signal_inference|" + Path.GetFullPath(p_videoPath)); s_offlineProcess.StandardInput.Flush(); }
            catch { s_offlineResponse = null; throw; }
        }

        string resultLine;
        try
        {
            if (!response.Task.Wait(TimeSpan.FromMinutes(30)))
                throw new TimeoutException("Offline inference did not finish within 30 minutes.");
            resultLine = response.Task.GetAwaiter().GetResult();
        }
        finally
        {
            lock (s_offlineLock) if (ReferenceEquals(s_offlineResponse, response)) s_offlineResponse = null;
        }
        if (resultLine.StartsWith("OFFLINE_ERROR|"))
            throw new InvalidOperationException("Video inference failed: " + resultLine.Substring("OFFLINE_ERROR|".Length));
        return ParseVideoResult(resultLine);
    }

    public static Task InitializeOfflineWorkerAsync()
    {
        lock (s_offlineLock)
        {
            if (s_offlineProcess != null && !s_offlineProcess.HasExited &&
                s_offlineReady != null && s_offlineReady.Task.Status == TaskStatus.RanToCompletion)
                return Task.CompletedTask;

            if (s_offlineInitializationTask != null && !s_offlineInitializationTask.IsCompleted)
                return s_offlineInitializationTask;

            s_offlineStopRequested = false;
            s_offlineInitializationTask = Task.Run((Action)InitializeOfflineWorkerCore);
            s_offlineInitializationTask.ContinueWith(task =>
            {
                Exception error = task.Exception.GetBaseException();
                if (!(error is OperationCanceledException))
                    Console.WriteLine("[ERROR] Offline worker initialization failed: " + error.Message);
            },
                TaskContinuationOptions.OnlyOnFaulted);
            return s_offlineInitializationTask;
        }
    }

    public static void InitializeOfflineWorker()
    {
        InitializeOfflineWorkerAsync().GetAwaiter().GetResult();
    }

    private static void InitializeOfflineWorkerCore()
    {
        lock (s_offlineLock)
        {
            if (s_offlineProcess != null && !s_offlineProcess.HasExited && s_offlineReady != null &&
                s_offlineReady.Task.Status == TaskStatus.RanToCompletion)
                return;
            if (s_offlineStopRequested)
                throw new OperationCanceledException("Offline worker startup was canceled during application shutdown.");
            RequireFile(Config.s_pythonEnvPath, "Python environment executable");
            RequireFile(Config.s_offlineWorkerScriptPath, "Offline inference worker");
            s_offlineReady = new TaskCompletionSource<bool>();
            s_offlineResponse = null;
            lock (s_pythonOutputLock) s_pythonErrorOutput.Length = 0;
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = Config.s_pythonEnvPath,
                WorkingDirectory = Path.Combine(Config.s_rootPath, "1_AI_Process"),
                Arguments = "\"" + Config.s_offlineWorkerScriptPath + "\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            s_offlineProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
            s_offlineProcess.Exited += (sender, args) =>
            {
                lock (s_offlineLock)
                {
                    s_offlineReady?.TrySetException(new InvalidOperationException(
                        "Offline inference worker exited before becoming ready. " + GetOfflineExitDetails()));
                    s_offlineResponse?.TrySetResult("OFFLINE_ERROR|Offline inference worker exited unexpectedly.");
                }
            };
            s_offlineProcess.OutputDataReceived += (sender, args) =>
            {
                if (args.Data == null) return;
                if (args.Data == "OFFLINE_READY") { s_offlineReady.TrySetResult(true); return; }
                if (args.Data.StartsWith("VIDEO_RESULT|") || args.Data.StartsWith("OFFLINE_ERROR|"))
                {
                    lock (s_offlineLock) s_offlineResponse?.TrySetResult(args.Data);
                }
                else if (Config.s_isDebugMode) Console.WriteLine("[PY-OFFLINE] " + args.Data);
            };
            s_offlineProcess.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data == null) return;
                lock (s_pythonOutputLock) s_pythonErrorOutput.AppendLine(args.Data);
                if (Config.s_isDebugMode) Console.WriteLine("[ERROR] [PY-OFFLINE] " + args.Data);
            };
            s_offlineProcess.Start();
            s_offlineProcess.BeginOutputReadLine();
             s_offlineProcess.BeginErrorReadLine();
        }

        bool becameReady;
        try { becameReady = s_offlineReady.Task.Wait(TimeSpan.FromMinutes(3)); }
        catch (AggregateException ex) { throw ex.GetBaseException(); }
        if (!becameReady)
        {
            StopOfflineWorker();
            throw new TimeoutException("Offline model did not load and warm up within 3 minutes.");
        }
        s_offlineReady.Task.GetAwaiter().GetResult();
    }

    private static string GetOfflineExitDetails()
    {
        string output;
        lock (s_pythonOutputLock) output = s_pythonErrorOutput.ToString().Trim();
        return String.IsNullOrWhiteSpace(output) ? "No Python stderr was captured." : Environment.NewLine + output;
    }

    public static void StopOfflineWorker()
    {
        Process process;
        lock (s_offlineLock)
        {
            s_offlineStopRequested = true;
            process = s_offlineProcess;
        }
        if (process == null) return;
        try
        {
            if (!process.HasExited)
            {
                try { process.StandardInput.WriteLine("SHUTDOWN"); process.StandardInput.Flush(); } catch { }
                if (!process.WaitForExit(5000)) { process.Kill(); process.WaitForExit(5000); }
            }
        }
        finally
        {
            lock (s_offlineLock) { if (ReferenceEquals(s_offlineProcess, process)) s_offlineProcess = null; }
            process.Dispose();
        }
    }

    public static VideoInferenceResult ParseVideoResult(string line)
    {
        if (line == null || !line.StartsWith("VIDEO_RESULT|")) throw new FormatException("Missing video summary.");
        var values = line.Split('|').Skip(1).Select(part => part.Split('='))
            .ToDictionary(parts => parts[0], parts => Double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
        if (values.Values.Any(value => Double.IsNaN(value) || Double.IsInfinity(value))) throw new FormatException("Non-finite video summary.");
        Func<string, long> count = name =>
        {
            double value = values[name];
            if (value < 0 || value > Int64.MaxValue || value != Math.Truncate(value)) throw new FormatException("Invalid " + name);
            return checked((long)value);
        };
        long frames = count("frames"), expected = count("expected"), skipped = count("skipped"), dropped = count("dropped");
        if (frames == 0 || frames > Int32.MaxValue || count("complete") > 1 || count("known") > 1)
            throw new FormatException("Invalid completion summary.");
        if (values["known"] == 1 && frames + skipped != expected) throw new FormatException("Inconsistent frame accounting.");
        bool complete = values["complete"] == 1;
        if (complete && (values["known"] != 1 || frames != expected || skipped != 0 || dropped != 0))
            throw new FormatException("Incomplete recording labeled complete.");
        return new VideoInferenceResult
        {
            FrameCount = (int)frames, ExpectedFrames = expected, SkippedFrames = skipped, DroppedFrames = dropped,
            Status = complete ? "Complete" : values["known"] == 1 ? "Partial" : "Unknown",
            MiSang = count("MiSang"), OneByTwo = count("1x2"), TwoByFour = count("2x4"), FourBySix = count("4x6"),
            AverageWeights = new[] { values["weight1"], values["weight2"], values["weight3"], values["weight4"] }
        };
    }

    public static void initializeAI()
    {
        RequireFile(Config.s_pythonEnvPath, "Python environment executable");
        RequireFile(Config.s_pythonScriptPath, "Live AI entry point");
        RequireFile(Config.s_tempImagePath, "AI warm-up image");

        var ipc = Stone_Application.IPC.IPCServices.getInstance();
        if (ipc.isSessionActive()) throw new InvalidOperationException("Cannot restart live AI during an active session.");
        s_liveReady = false;
        if (Common.pythonProcess != null && !Common.pythonProcess.HasExited)
        {
            StopPythonProcess();
        }

        Common.ui2aiEvent.Reset(); Common.ai2uiEvent.Reset();
        lock (s_pythonOutputLock) s_pythonErrorOutput.Length = 0;

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = Config.s_pythonEnvPath,
            WorkingDirectory = Path.Combine(Config.s_rootPath, "1_AI_Process"),
            Arguments = $"\"{Config.s_pythonScriptPath}\" " +
                        $"\"{Config.MMF_TAGNAME}\" " +
                        $"\"{Config.UI_2_AI_EVENT_TAGNAME}\" " +
                        $"\"{Config.AI_2_UI_EVENT_TAGNAME}\" " +
                        $"\"{Config.IMAGE_WIDTH}\" " +
                        $"\"{Config.IMAGE_HEIGHT}\" " +
                        $"\"{Config.s_isDebugMode}\" " +
                        $"\"{Config.AI_WARMUP_ITERATIONS}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        Debug.WriteLine(Config.s_pythonEnvPath);

        Common.pythonProcess = new Process { StartInfo = psi };

        Common.pythonProcess.OutputDataReceived += (p_s, p_outArgs) =>
        {
            if (p_outArgs.Data != null)
                if (Config.s_isDebugMode)
                    Console.WriteLine("[PY] " + p_outArgs.Data);
        };

        // Common.pythonProcess.ErrorDataReceived += (p_s, p_errArgs) =>
        // {
        //     if (p_errArgs.Data != null)
        //     {
        //         lock (s_pythonOutputLock)
        //             s_pythonErrorOutput.AppendLine(p_errArgs.Data);
        //         Console.WriteLine("[ERROR] [PY] " + p_errArgs.Data);
        //     }
        // };

        Common.pythonProcess.Start();
        Common.pythonProcess.BeginOutputReadLine();
         Common.pythonProcess.BeginErrorReadLine();
    }

    private static void RequireFile(string path, string description)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(description + " was not found. Check the Stone Project installation path.", path);
    }



    public static void warmUpAI()
    {
        s_liveReady = false;
        try
        {
            WarmUpCore();
            if (Common.pythonProcess == null || Common.pythonProcess.HasExited)
                throw new InvalidOperationException("AI exited during warm-up.");
            s_liveReady = true;
        }
        catch
        {
            StopPythonProcess();
            throw;
        }
    }

    private static void StopPythonProcess()
    {
        Process process = Common.pythonProcess;
        if (process == null) return;

        try
        {
            if (!process.HasExited)
            {
                process.Kill();
                if (!process.WaitForExit(Config.AI_EXIT_TIMEOUT_MS))
                    throw new TimeoutException("Failed warm-up could not stop Python; shared memory must not be reused.");
            }
        }
        catch (InvalidOperationException)
        {
            // The child can exit between HasExited and Kill. Preserve the
            // initialization error instead of replacing it with a cleanup race.
            if (!HasExited(process)) throw;
        }
    }

    private static bool HasExited(Process process)
    {
        try { return process.HasExited; }
        catch (InvalidOperationException) { return true; }
    }

    private static string GetPythonExitDetails()
    {
        string output;
        lock (s_pythonOutputLock) output = s_pythonErrorOutput.ToString().Trim();

        int? exitCode = null;
        try
        {
            if (Common.pythonProcess != null && Common.pythonProcess.HasExited)
                exitCode = Common.pythonProcess.ExitCode;
        }
        catch (InvalidOperationException) { }

        string details = exitCode.HasValue ? "Python exited with code " + exitCode.Value + "." : "Python exited.";
        if (!String.IsNullOrWhiteSpace(output)) details += Environment.NewLine + output;
        return details;
    }

    private static void WarmUpCore()
    {
        // Ensure IPC resources are initialized before using Common.mmf and events.
        Stone_Application.IPC.IPCServices.getInstance();

        if (Common.mmf == null)
        {
            throw new InvalidOperationException("IPC mapping is not initialized for warm-up.");
        }

        if (Common.ui2aiEvent == null || Common.ai2uiEvent == null)
        {
            throw new InvalidOperationException("IPC events are not initialized for warm-up.");
        }

        using (Image originalImage = Image.FromFile(Config.s_tempImagePath))
        using (Bitmap originalBitMap = new Bitmap(
                                                    originalImage,
                                                    Config.IMAGE_WIDTH,
                                                    Config.IMAGE_HEIGHT))
        {
            for (int i = Config.AI_WARMUP_ITERATIONS; i > 0; i--)
            {
                using (MemoryMappedViewStream stream =
                    Common.mmf.CreateViewStream(Config.IMAGE_BUFFER_HEADER_SIZE, Config.IMAGE_BYTE_COUNT))
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, false))
                {
                    BitmapData data = originalBitMap.LockBits(
                        new Rectangle(0, 0, Config.IMAGE_WIDTH, Config.IMAGE_HEIGHT),
                        ImageLockMode.ReadOnly,
                        PixelFormat.Format24bppRgb);

                    try
                    {
                        int bytesLen = Math.Abs(data.Stride) * Config.IMAGE_HEIGHT;
                        byte[] bytes = new byte[bytesLen];
                        Marshal.Copy(data.Scan0, bytes, 0, bytesLen);

                        writer.Write(bytes);
                    }
                    finally
                    {
                        originalBitMap.UnlockBits(data);
                    }
                }

                // Warm-up always uses slot zero, following the same ring protocol as capture.
                using (MemoryMappedViewStream header = Common.mmf.CreateViewStream(Config.WRITE_OFFSET, Config.IMAGE_BUFFER_HEADER_SIZE))
                using (BinaryWriter writer = new BinaryWriter(header, Encoding.UTF8, false))
                    writer.Write(0);
                Common.ui2aiEvent.Set();

                if (!Common.ai2uiEvent.WaitOne(Config.AI_WARMUP_TIMEOUT_MS))
                {
                    if (Common.pythonProcess != null && HasExited(Common.pythonProcess))
                        throw new InvalidOperationException("AI exited before responding to warm-up. " + GetPythonExitDetails());
                    throw new TimeoutException("AI did not finish warm-up request " +
                        (Config.AI_WARMUP_ITERATIONS - i + 1) + " of " + Config.AI_WARMUP_ITERATIONS + " within " +
                        (Config.AI_WARMUP_TIMEOUT_MS / 1000) + " seconds.");
                }

                Console.WriteLine($"[INFO] [UI-AI] AI Warm-up Count: {i}");
            }
        }

        Console.WriteLine("[INFO] [UI-AI] AI Warm-up finished");
    }


    public static void aiClosing()
    {
        if (Common.pythonProcess != null && !Common.pythonProcess.HasExited)
        {
            try
            {
                Common.pythonProcess.Kill();
                Common.pythonProcess.WaitForExit();
                Console.WriteLine("[INFO] [UI-AI] AI process terminated.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] [UI-AI] AI process terminated failed\n{ex.Message}");
            }
        }
    }
}
