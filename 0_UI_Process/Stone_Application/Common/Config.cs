using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Camera pixel formats selectable through Config.CAMERA_PIXEL_FORMAT
public enum CameraPixelFormat
{
    Bgr8,
    Bayer8
}

public static class Config
{
    public const bool s_isDebugMode = true;

    public const int EXPOSURE_TIME = 5_000;
    public const int TIME_INTERVAL = 100;
    public const int IPC_CAPTURE_INTERVAL_MS = 100;
    public const int IMAGE_WIDTH = 1_920;                           // Image width of the camera
    public const int IMAGE_HEIGHT = 1_200;                          // Image height of the camera

    public const int BUFFER_BOUND = 5;
    public const int IMAGE_BUFFER_SLOT_COUNT = 5;
    public const int IMAGE_BUFFER_HEADER_SIZE = 64;
    // BGR8 frame size used by shared memory, live inference, UI, and IoT images.
    public const int IMAGE_BYTE_COUNT = IMAGE_WIDTH * IMAGE_HEIGHT * 3;
    // Pixel format the camera streams. Change this one value to switch between the two pipelines:
    //   * Bayer8: 1 byte/pixel, so 1 Gbit/s GigE sustains about 44 FPS. Recording consumes the raw frames;
    //     only frames selected for live inference are converted to BGR8 on the host. The host conversion
    //     does not apply the camera's color transformation, so colors can differ from Bgr8 recordings.
    //   * Bgr8 (legacy): 3 bytes/pixel, color-processed by the camera; GigE saturates near 17 FPS.
    public const CameraPixelFormat CAMERA_PIXEL_FORMAT = CameraPixelFormat.Bayer8;
    public const int CAMERA_BYTES_PER_PIXEL = CAMERA_PIXEL_FORMAT == CameraPixelFormat.Bgr8 ? 3 : 1;
    // Size of one tightly packed camera frame as captured, pooled, and recorded.
    public const int RAW_FRAME_BYTE_COUNT = IMAGE_WIDTH * IMAGE_HEIGHT * CAMERA_BYTES_PER_PIXEL;
    // Bayer8 only: accepted camera Bayer patterns in order of preference. The camera selects the first it supports.
    public static readonly string[] CAMERA_BAYER_PIXEL_FORMATS = { "BayerRG8", "BayerBG8", "BayerGB8", "BayerGR8" };
    public const int IMAGE_BUFFER_SIZE = IMAGE_BUFFER_HEADER_SIZE + IMAGE_BUFFER_SLOT_COUNT * IMAGE_BYTE_COUNT;

    public const int ENC_ADDR = 0;
    public const int SENSOR_ADDR = 0;

    private static readonly string s_baseDir = AppDomain.CurrentDomain.BaseDirectory;
    public static readonly string s_rootPath = FindRepositoryRoot(s_baseDir);

    private static string FindRepositoryRoot(string startDirectory)
    {
        DirectoryInfo directory = new DirectoryInfo(startDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "1_AI_Process")) &&
                Directory.Exists(Path.Combine(directory.FullName, "0_UI_Process", "Stone_Application")))
                return directory.FullName;

            directory = directory.Parent;
        }

        // Preserve the conventional development layout in the error paths below,
        // where the missing repository files can be reported explicitly.
        return Path.GetFullPath(Path.Combine(startDirectory, "..", "..", "..", "..", ".."));
    }

    public static readonly string s_pythonEnvPath = Path.Combine(s_rootPath, "1_AI_Process", ".venv", "Scripts", "python.exe");
    public static readonly string s_pythonScriptPath = Path.Combine(s_rootPath, "1_AI_Process", "src", "main.py");
    public static readonly string s_videoInferenceScriptPath = Path.Combine(s_rootPath, "1_AI_Process", "src", "video_inference.py");
    public static readonly string s_offlineWorkerScriptPath = Path.Combine(s_rootPath, "1_AI_Process", "src", "offline_worker.py");
    public static readonly string s_tempImagePath = Path.Combine(s_rootPath, "1_AI_Process", "test_img.jpg");

    public static readonly string s_templateDir = Path.Combine(s_rootPath, "0_UI_Process", "Stone_Application", "PDF_Reference");
    public static readonly string s_templatePath = Path.Combine(s_templateDir, "template.docx");

    public static readonly string s_excelTemplatePath = s_isDebugMode
        ? Path.Combine(s_templateDir, "Reference.xlsx")
        : Path.Combine(s_templateDir, "Reference2.xlsx");

    public static readonly string s_outputPath = Path.Combine(s_templateDir, "Result_");

    public const string MMF_TAGNAME = "cam_01_map";
    // Region #1 is a five-slot image ring. Region #2 retains its result layout,
    // but begins after region #1 so the regions never overlap.
    public const int RESULT_REGION_SIZE = 10_000_000;
    public const int MAP_SIZE = IMAGE_BUFFER_SIZE + RESULT_REGION_SIZE;
    public const int WRITE_OFFSET = 0;
    public const int READ_OFFSET = IMAGE_BUFFER_SIZE;
    public const int OFFSET_IMAGE = 100;

    public const int VIDEO_FRAME_RATE = 30; // Playback fallback when the camera cannot report its frame rate.
    public const uint VIDEO_WRITER_QUALITY = 80;
    public const long VIDEO_SEGMENT_MAX_BYTES = 1_800_000_000;
    public const int VIDEO_QUEUE_CAPACITY = 8;
    public const int IPC_CAPTURE_QUEUE_CAPACITY = 2;
    public const int AI_STOP_TIMEOUT_MS = 15_000;
    public const int AI_EXIT_TIMEOUT_MS = 5_000;
    public const int AI_WARMUP_TIMEOUT_MS = 120_000;
    public const int AI_WARMUP_ITERATIONS = 2;
    public const int VIDEO_DECODE_RECOVERY_LIMIT = 30;
    public static readonly string s_videoOutputPath =
        Environment.GetEnvironmentVariable("STONE_VIDEO_OUTPUT_PATH") ??
        Path.Combine(s_rootPath, "Recordings");

    public const string UI_2_AI_EVENT_TAGNAME = "cam_ui_wrote_event";
    public const string AI_2_UI_EVENT_TAGNAME = "cam_ai_wrote_event";
}
