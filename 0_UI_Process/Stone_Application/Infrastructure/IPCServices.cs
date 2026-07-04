using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Stone_Application.Event;

namespace Stone_Application.IPC
{
    public class IPCServices
    {
        private static IPCServices s_instance;

        private IPCServices()
        {
            Common.ui2aiEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Config.UI_2_AI_EVENT_TAGNAME);
            Common.ai2uiEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Config.AI_2_UI_EVENT_TAGNAME);

            Common.mmf = MemoryMappedFile.CreateOrOpen(Config.MMF_TAGNAME, Config.MAP_SIZE);
        }

        static public void IPCCleanUp()
        {
            if (Common.ui2aiEvent != null)
            {
                Common.ui2aiEvent.Dispose();
                Common.ui2aiEvent = null;
            }

            if (Common.ai2uiEvent != null)
            {
                Common.ai2uiEvent.Dispose();
                Common.ai2uiEvent = null;
            }

            if (Common.mmf != null)
            {
                Common.mmf.Dispose();
                Common.mmf = null;
            }

            if (Common.pythonProcess != null && !Common.pythonProcess.HasExited)
            {
                try
                {
                    Common.pythonProcess.Kill();
                    Common.pythonProcess.WaitForExit();
                    Console.WriteLine("[INFO] [IPC] Python process terminated.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] [IPC] Failed to terminate Python process: {ex.Message}");
                }
                Common.pythonProcess = null;
            }

            Console.WriteLine("[INFO] [IPC] IPC resources cleaned up.");
        }

        public static IPCServices getInstance()
        {
            if (s_instance == null)
            {
                s_instance = new IPCServices();
            }
            return s_instance;
        }

        public void writeTask(IImage p_image)
        {
            using (MemoryMappedViewStream stream = Common.mmf.CreateViewStream(Config.WRITE_OFFSET, Config.WRITE_READ_SIZE))
            {
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, false))
                {
                    writer.Write(p_image.recvImage);
                }
            }
        }

        public (IInformation, IImage) readTask()
        { 
            using (MemoryMappedViewStream stream = Common.mmf.CreateViewStream(Config.READ_OFFSET, Config.WRITE_READ_SIZE))
            {

                using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, false))
                {
                    float delta_perct_misang = reader.ReadSingle();
                    float delta_perct_1_2 = reader.ReadSingle();
                    float delta_perct_2_4 = reader.ReadSingle();
                    float delta_perct_4_6 = reader.ReadSingle();

                    float measured_weight = reader.ReadSingle();

                    stream.Seek(Config.OFFSET_IMAGE, SeekOrigin.Begin);
                    byte[] image_data = reader.ReadBytes(Config.IMAGE_HEIGHT * Config.IMAGE_WIDTH * 3);

                    IInformation information = new IInformation
                    {
                        deltaPerctMiSang = delta_perct_misang,
                        deltaPerct1x2 = delta_perct_1_2,
                        deltaPerct2x4 = delta_perct_2_4,
                        deltaPerct4x6 = delta_perct_4_6,
                        measuredWeight = measured_weight
                    };

                    IImage image = new IImage
                    {
                        recvImage = image_data
                    };

                    return (information, image);
                }
            }
        }
    }
}
