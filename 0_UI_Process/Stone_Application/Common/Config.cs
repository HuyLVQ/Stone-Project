using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class Config
{
    public const int EXPOSURE_TIME = 5000;
    public const int TIME_INTERVAL = 100;
    public const int IMAGE_WIDTH = 1_920;                           // Image width of the camera
    public const int IMAGE_HEIGHT = 1_200;                          // Image height of the camera

    public const int BUFFER_BOUND = 5;

    public const int ENC_ADDR = 0;
    public const int SENSOR_ADDR = 0;

    public const string pythonEnvPath = @"D:\4_Plastic_Bottle_Classification_Project\1_AI\.venv\Scripts\python.exe";
    public const string pythonScriptPath = @"D:\4_Plastic_Bottle_Classification_Project\1_AI\Main_000_Multiple_Camera\main.py";

    public const string MMF_TAGNAME = "cam_01_map";
    public const int MAP_SIZE = 20_000_000;
    public const int WRITE_READ_SIZE = 9_999_999;
    public const int WRITE_OFFSET = 0;
    public const int READ_OFFSET = 10_000_000;
    public const int OFFSET_IMAGE = 200;

    public const string UI_2_AI_EVENT_TAGNAME = "cam_ui_wrote_event";
    public const string AI_2_UI_EVENT_TAGNAME = "cam_ai_wrote_event";

    public const string TEMP_IMAGE_PATH = @"D:\4_Plastic_Bottle_Classification_Project\0_UI\Common_Resources\0e469c18__data_rac_5Cimage_106.png";

}
