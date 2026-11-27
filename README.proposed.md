# Stone Project

Windows application for camera-based stone classification and weight estimation. A C# WinForms application captures Basler camera frames, exchanges live inference data with a Python YOLO worker, records each session, and saves measurements in PostgreSQL. After STOP, a separate Python process analyzes the recorded video and supplies the finalized session result used by Excel export.

The reported stone categories are `MiSang`, `1x2`, `2x4`, and `4x6`. The inference code also defines an auxiliary `Khac` category.

## Repository map

| Path | Purpose |
| --- | --- |
| `0_UI_Process/Stone_Application/` | .NET Framework 4.7.2 WinForms application and Visual Studio solution. |
| `0_UI_Process/Stone_Application/CameraClass/` | Basler camera integration. |
| `0_UI_Process/Stone_Application/Infrastructure/` | Capture/inference workers, shared-memory IPC, and AVI recorder. |
| `0_UI_Process/Stone_Application/Repository/` | PostgreSQL persistence and session queries. |
| `0_UI_Process/Stone_Application/InitService/` | Docker/PostgreSQL startup and application cleanup. |
| `0_UI_Process/Stone_Application/PDFExport/` | Excel exporter and a document-export implementation. |
| `0_UI_Process/Stone_Application/PDF_Reference/` | Excel/Word templates and existing generated reports. |
| `1_AI_Process/src/` | Live worker (`main.py`) and offline session analysis (`video_inference.py`). |
| `1_AI_Process/module/` | Runtime YOLO implementation, regression wrappers/models, and SAM code. |
| `1_AI_Process/utils/` | IPC protocol, feature definitions, calibration, and training utilities. |
| `Weight_v2/` | Separate video evaluation/data utilities and model assets; its YOLO model differs from the runtime model. |
| `Weight/` | Older model assets and utilities; confirm maintenance status before changing them. |
| `2_Broker_Process/` | Broker directory skeleton and cached Python bytecode; no executable source workflow is present in this checkout. |

## Architecture

```text
Basler camera -> C# capture worker -> five-slot shared-memory ring
                       |                         |
                       v                         v
                   session AVI             Python live YOLO
                                                 |
                                      shared-memory result block
                                                 |
                                      C# result worker/observers
                                                 |
                                       live display + PostgreSQL

STOP -> settle live request -> clear memory -> drain/finalize AVI segments
     -> session manifest -> offline analysis -> quality-tagged row -> Excel
```

Capture continues independently of periodic live inference. The worker selects the newest completed frame, protects its ring slot until completion, and permits one inference in flight. A separate recording worker drains an eight-frame bounded queue into AVI segments with a JSON manifest. Live measurements support monitoring; finalized offline measurements and their completeness status supply exported session results.

## Prerequisites

- Windows, because the UI uses WinForms, Basler drivers, and Windows named memory/events.
- Visual Studio or compatible MSBuild tooling with .NET Framework 4.7.2 targeting support and NuGet package restore.
- Basler pylon runtime/drivers and a connected compatible camera. The project references `Resources/Basler.Pylon.dll`; verify that this file and the installed native runtime match the selected build architecture.
- Python and a project virtual environment at `1_AI_Process/.venv/`. The local environment inspected for this proposal is Python 3.11.9; compatibility of a fresh install with the current requirements has not been verified.
- Docker Desktop/Engine with Docker Compose, for the supplied PostgreSQL service.
- A serial Modbus device for the current Settings connection flow, which enables AI initialization after both camera and Modbus connections succeed.
- The YOLO and serialized regression assets described below, plus writable recording/report destinations.

Torch selects CUDA when available and otherwise uses CPU. Required inference throughput must be measured on the target machine.

## Developer setup

Run these commands from the repository root in PowerShell.

### 1. Prepare the Python environment

```powershell
python -m venv .\1_AI_Process\.venv
.\1_AI_Process\.venv\Scripts\python.exe -m pip install -r .\1_AI_Process\requirements.txt
```

The requirements include Ultralytics, Torch/Torchvision, OpenCV, NumPy, Pandas, pywin32, joblib, scikit-learn, and XGBoost. Most packages are pinned, while scikit-learn and XGBoost have lower bounds. Resolve interpreter/package and serialized-model compatibility on a fresh machine before treating this as a reproducible environment specification. `Weight_v2/requirements.txt` is currently empty; it is not a standalone dependency specification.

### 2. Verify model assets

| Consumer | Asset |
| --- | --- |
| Runtime live and STOP video inference | `Weight_v2/model/YOLO/best260719.pt` |
| Linear regression wrapper (outside the active four-model sequence) | `1_AI_Process/module/weightRegression/linearRegression/linear.pkl` |
| Runtime polynomial regression | `1_AI_Process/module/weightRegression/polynominalRegression/poly.pkl` |
| Runtime random forest | `1_AI_Process/module/weightRegression/randomForest/rf_v3.pkl` |
| XGBoost wrappers | `1_AI_Process/module/weightRegression/XGBoost/xgboost_v4.pkl` and `xgboost_poly.pkl`; the selected prediction function determines which is used. |
| Separate `Weight_v2` video utility | `Weight_v2/model/YOLO/best260807.pt` and `Weight_v2/model/XGBoost/xgboost.pkl` |

The active four-model sequence is polynomial regression, random forest, XGBoost, and polynomial-feature XGBoost. The runtime regression wrappers load assets beside their own modules. Replacing only the regression files under `Weight_v2/model/` does not update those runtime assets.

### 3. Prepare PostgreSQL

```powershell
docker compose -f .\0_UI_Process\Stone_Application\InitService\docker-compose.yml up -d
docker exec measurements_postgres pg_isready -U postgres -d measurements_db
```

The supplied Compose service uses PostgreSQL 13, container `measurements_postgres`, database `measurements_db`, and host port `5432`. Its local defaults use `postgres` for both username and password; the repository constructor uses matching defaults. Change both locations when configuring different credentials. Data persists in the `measurements_pgdata` Compose volume.

Application startup also attempts to start Docker Desktop when necessary, run Compose, and wait for database readiness. The repository creates/extends `measurement_information`, including session and finalized-result fields.

### 4. Restore and build the UI

Open `0_UI_Process/Stone_Application/Stone_Application.sln` in Visual Studio, restore NuGet packages, and select **Debug / x64**. The project uses `packages.config` and local assembly references.

Alternatively, with a compatible toolchain available:

```powershell
dotnet msbuild .\0_UI_Process\Stone_Application\Stone_Application.sln /t:Build /p:Configuration=Debug /p:Platform=x64
```

The expected executable is `0_UI_Process/Stone_Application/bin/x64/Debug/Stone_Application.exe`. `Config.cs` derives the repository root by walking five parents from the executable directory; launching a binary from a different output depth or copying it elsewhere requires checking path resolution.

## Operating a session

1. Launch the UI and confirm database startup succeeds.
2. In Settings, connect the serial Modbus device and initialize the Basler camera, then initialize AI. The UI creates IPC resources, launches Python, warms up inference, and starts the workers.
3. Press **Start**. The application begins a database session, opens an AVI recording, resets the ring, and streams camera frames.
4. During streaming, live inference runs at an eligible interval and updates the display and database through observers.
5. Press **Stop**. New capture/dispatch stops; an outstanding live result is settled independently of UI callbacks before memory is cleared. Accepted recording frames drain, AVI segments finalize, and offline analysis runs synchronously against the manifest. AI settlement has a 15-second wait plus confirmed process-exit recovery on timeout.
6. On success, the offline aggregate is saved as the finalized session result and displayed. A new session remains unavailable while this analysis runs.
7. Export session results through the application. Excel selects finalized rows with explicit Complete/Partial/Unknown status and frame/loss counts. Partial usable results are retained; failed sessions have no fallback to live estimates.

Counts are summed across readable video frames; four weight outputs are averaged across processed frames. This aggregation does not implement cross-frame object tracking. Its suitability depends on the physical capture process and the intended counting contract.

## Configuration and protocol

| Setting | Source/default |
| --- | --- |
| Image geometry | `Common/Config.cs`: 1920 x 1200, three BGR bytes per pixel. |
| Live inference interval | `TIME_INTERVAL = 880` ms; elapsed intervals coalesce when inference is slower. |
| Ring | Five frames after a 64-byte header; first `Int32` selects the inference slot. |
| Mapping | `cam_01_map`, 44,560,064 bytes total. |
| Result region | Absolute offset 34,560,064; 10,000,000 bytes reserved; result image at relative offset 100. |
| Events | `cam_ui_wrote_event` and `cam_ai_wrote_event`. |
| Video | Motion-JPEG AVI, nominal 30 FPS. |
| Recording queue and rollover | Eight frames; AVI segments below 1,800,000,000 bytes, with a session JSON manifest. |
| Recording location | `STONE_VIDEO_OUTPUT_PATH`, otherwise `<repository>/Recordings`. |
| Runtime calibration/features | `1_AI_Process/utils/config.py`: `RATIO_SCALE = 1/18.82` and ordered 12-feature schema. |
| Excel template | `PDF_Reference/Reference2.xlsx` normally, `Reference.xlsx` when `s_isDebugMode` is true. |

The C# protocol in `Infrastructure/IPCServices.cs` and Python protocol in `utils/IPCHelpers.py` must agree on offsets, data types, image geometry, class/model ordering, and signaling. Change both sides together.

To choose a recording folder before launching the UI from the same environment:

```powershell
$env:STONE_VIDEO_OUTPUT_PATH = 'D:\StoneRecordings'
```

Excel writes data from row 4: session ID in A, class percentages in B-E, and the first model weight in F. Debug mode also exports model weights 2-4 in G-I. Columns J-N contain analysis status, expected/processed/skipped frames, and recording losses.

## Offline utilities

Re-analyze a finalized recording manifest with the runtime pipeline (a standalone AVI is also accepted, with Unknown completeness):

```powershell
.\1_AI_Process\.venv\Scripts\python.exe .\1_AI_Process\src\video_inference.py 'D:\StoneRecordings\session_example.json'
```

The script emits a `VIDEO_RESULT|...` summary for the C# parser. Running it directly does not persist a finalized database row.

The separate evaluation utility offers count/weight tasks and an optional actual weight in kilograms:

```powershell
.\1_AI_Process\.venv\Scripts\python.exe .\Weight_v2\test.py 'D:\StoneRecordings\session_example.avi' --task both --weight 10.3
```

This utility samples video using `VIDEO_CUT_TIME = 3.13` seconds and uses independent count/weight pixel calibrations. Its model, calibration, and sampling differ from STOP analysis, so results are not interchangeable. Training helpers under `1_AI_Process/utils/utils_train.py` also contain machine-specific output paths that need review before training.

## Verification and limitations

This README proposal was checked against source and local file layout. No build, dependency installation, camera/PLC operation, database startup, or model inference was performed during this documentation review.

For runtime changes, verify a complete START/STOP session on the target hardware: live display and persistence, capture during slow inference, a playable finalized AVI, offline aggregate persistence, export of completed sessions, and a clean second session.

Known limitations visible in source:

- STOP waits up to 15 seconds for AI; timed-out Python must exit before memory reuse. If exit cannot be confirmed, START remains unavailable.
- Offline inference blocks the UI while it runs.
- Live raw BGR frames and JPEG-decoded AVI frames can yield different model results.
- Nominal AVI FPS does not preserve variable frame capture timing; JPEG encoding and disk throughput limit capture performance.
- Offline analysis attempts bounded seek recovery past unreadable positions, skips failed frame inference, rejects zero processed frames, and explicitly labels usable partial results. Camera-internal dropped frames are outside manifest completeness accounting.
- Model units, calibration, accuracy targets, and the repeated-object counting contract need project-owner confirmation.
- The repository contains evaluation scripts, but no dedicated automated test suite was identified in the inspected source files.

## Additional notes

- [Implementation notes](IMPLEMENTATION_NOTES.md)
- [Finalized offline results and export](OFFLINE_RESULTS_AS_EXPORT_SOURCE.md)
- [Session-video skill](.agents/skills/stone-session-video/SKILL.md)

These notes provide change-specific context; inspect the current source when behavior and historical notes differ.
