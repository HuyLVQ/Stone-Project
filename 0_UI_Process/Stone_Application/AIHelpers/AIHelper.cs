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
    public static void initializeAI()
    {
        if (Common.pythonProcess != null && !Common.pythonProcess.HasExited)
        {
            try { Common.pythonProcess.Kill(); Common.pythonProcess.WaitForExit(); }
            catch (Exception e) { 
                Console.WriteLine("[WARN] Previous AI process terminated failed\n" + e.Message);
            } 
        }

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = Config.pythonEnvPath,
            Arguments = $"\"{Config.pythonScriptPath}\" " +
                        $"\"{Config.MMF_TAGNAME}\" " +
                        $"\"{Config.UI_2_AI_EVENT_TAGNAME}\" " +
                        $"\"{Config.AI_2_UI_EVENT_TAGNAME}\" " +
                        $"\"{Config.IMAGE_WIDTH}\" " +
                        $"\"{Config.IMAGE_HEIGHT}\" ",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        Common.pythonProcess = new Process { StartInfo = psi };

        Common.pythonProcess.OutputDataReceived += (s, outArgs) =>
        {
            if (outArgs.Data != null)
                Console.WriteLine("[PY] " + outArgs.Data);
        };

        Common.pythonProcess.ErrorDataReceived += (s, errArgs) =>
        {
            if (errArgs.Data != null)
                Console.WriteLine("[ERROR] [PY] " + errArgs.Data);
        };

        Common.pythonProcess.Start();
        Common.pythonProcess.BeginOutputReadLine();
        Common.pythonProcess.BeginErrorReadLine();
    }



    public static void warmUpAI()
    {
        using (Image originalImage = Image.FromFile(Config.TEMP_IMAGE_PATH))
        using (Bitmap originalBitMap = new Bitmap(
                                                    originalImage,
                                                    Config.IMAGE_WIDTH,
                                                    Config.IMAGE_HEIGHT))
        {
            for (int i = 20; i > 0; i--)
            {
                using (MemoryMappedViewStream stream =
                    Common.mmf.CreateViewStream(Config.WRITE_OFFSET, Config.WRITE_READ_SIZE))
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

                Common.ui2aiEvent.Set();

                if (!Common.ai2uiEvent.WaitOne(TimeSpan.FromSeconds(5)))
                {
                    Console.WriteLine("[ERROR] AI warm-up timeout");
                    break;
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
