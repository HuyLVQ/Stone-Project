using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class Config
{
    public const bool s_isDebugMode = false;

    public const int EXPOSURE_TIME = 5_000;
    public const int TIME_INTERVAL = 880;
    public const int IMAGE_WIDTH = 1_920;                           // Image width of the camera
    public const int IMAGE_HEIGHT = 1_200;                          // Image height of the camera

    public const int BUFFER_BOUND = 2;
    public const int IMAGE_SLOT_COUNT = 5;
    public const int IMAGE_BYTE_SIZE = IMAGE_WIDTH * IMAGE_HEIGHT * 3;
    public const int MMF_ALLOCATION_GRANULARITY = 64 * 1024;
    public const int IMAGE_SLOT_STRIDE = ((IMAGE_BYTE_SIZE + MMF_ALLOCATION_GRANULARITY - 1) / MMF_ALLOCATION_GRANULARITY) * MMF_ALLOCATION_GRANULARITY;

    public const int ENC_ADDR = 0;
    public const int SENSOR_ADDR = 0;

    private static readonly string s_baseDir = AppDomain.CurrentDomain.BaseDirectory;
    public static readonly string s_rootPath = Path.GetFullPath(Path.Combine(s_baseDir, "..", "..", "..", "..", ".."));

    public static readonly string s_pythonEnvPath = Path.Combine(s_rootPath, "1_AI_Process", ".venv", "Scripts", "python.exe");
    public static readonly string s_brokerScriptPath = Path.Combine(s_rootPath, "2_Broker_Process", "broker.py");
    public static readonly string s_tempImagePath = Path.Combine(s_rootPath, "1_AI_Process", "test_img.jpg");

    public static readonly string s_templateDir = Path.Combine(s_rootPath, "0_UI_Process", "Stone_Application", "PDF_Reference");
    public static readonly string s_templatePath = Path.Combine(s_templateDir, "template.docx");

    public static readonly string s_excelTemplatePath = s_isDebugMode
        ? Path.Combine(s_templateDir, "Reference.xlsx")
        : Path.Combine(s_templateDir, "Reference2.xlsx");

    public static readonly string s_outputPath = Path.Combine(s_templateDir, "Result_");

    public const string INPUT_MMF_TAGNAME = "cam_01_input_map";
    public const string OUTPUT_MMF_TAGNAME = "cam_01_output_map";
    public const int MAP_SIZE = IMAGE_SLOT_STRIDE * IMAGE_SLOT_COUNT;
    public const int IMAGE_INTERVAL = 10;
    public const int WORKER_COUNT = 3;
    public const string BROKER_RX_ENDPOINT = "tcp://127.0.0.1:5555";
    public const string BROKER_TX_ENDPOINT = "tcp://127.0.0.1:5556";
    public const string BROKER_ROUTER_ENDPOINT = "tcp://127.0.0.1:5557";
}
