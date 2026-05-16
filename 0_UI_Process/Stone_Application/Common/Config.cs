using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class Config
{
    public const int EXPOSURE_TIME = 5_000;
    public const int TIME_INTERVAL = 1_000;
    public const int IMAGE_WIDTH = 1_920;                           // Image width of the camera
    public const int IMAGE_HEIGHT = 1_200;                          // Image height of the camera

    public const int BUFFER_BOUND = 5;

    public const int ENC_ADDR = 0;
    public const int SENSOR_ADDR = 0;

    private static readonly string baseDir = AppDomain.CurrentDomain.BaseDirectory;
    public static readonly string RootPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));

    public static readonly string pythonEnvPath = Path.Combine(RootPath, "1_AI_Process", ".venv", "Scripts", "python.exe");
    public static readonly string pythonScriptPath = Path.Combine(RootPath, "1_AI_Process", "src", "main.py");
    public static readonly string TEMP_IMAGE_PATH = Path.Combine(RootPath, "1_AI_Process", "test_img.jpg");

    public static readonly string templateDir = Path.Combine(RootPath, "0_UI_Process", "Stone_Application", "PDF_Reference");
    public static readonly string templatePath = Path.Combine(templateDir, "template.docx");
    public static readonly string outputPath = Path.Combine(templateDir, "Result_");


    public const string MMF_TAGNAME = "cam_01_map";
    public const int MAP_SIZE = 20_000_000;
    public const int WRITE_READ_SIZE = 9_999_999;
    public const int WRITE_OFFSET = 0;
    public const int READ_OFFSET = 10_000_000;
    public const int OFFSET_IMAGE = 100;

    public const string UI_2_AI_EVENT_TAGNAME = "cam_ui_wrote_event";
    public const string AI_2_UI_EVENT_TAGNAME = "cam_ai_wrote_event";


}
