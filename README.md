# Stone Project

Camera-based stone classification system with a WinForms UI, a Python YOLO inference service, PostgreSQL persistence, live result display, session video recording, and offline whole-video analysis.

## System layout

```text
Basler camera
    |
    v
.NET WinForms UI ------------------------------------------------------+
  capture worker -> shared-memory image ring -> Python live AI         |
  video recorder -> session AVI                                        |
  result worker  <- shared-memory result block <- completion event     |
    |                                                                   |
    +--> database persistence                                           |
    +--> result image/count/weight UI display                           |
                                                                        |
STOP -> finalize AVI -> Python offline video AI -> result MessageBox --+
```

| Component | Location | Responsibility |
| --- | --- | --- |
| UI process | `0_UI_Process/Stone_Application` | Camera control, session controls, shared-memory producer/consumer, UI, video recording, database integration. |
| AI process | `1_AI_Process` | YOLO segmentation/classification, weight-model evaluation, shared-memory live inference, offline video analysis. |
| Broker process | `2_Broker_Process` | Reserved broker structure (`proto`, `router`, `rx`, `tx`, `worker`). No executable broker flow is currently implemented in this repository. |
| Model assets | `Weight_v2` | YOLO and regression-model weights/data. |

## Startup flow

1. Start the UI application.
2. Configure/connect the Basler camera in Settings.
3. Initialize AI. The UI creates the named shared-memory mapping and events, starts the Python live AI process, and performs model warm-up.
4. The capture and result workers are started but remain idle until a recording session begins.
5. Press **Start** to create a database session, create a new AVI file, reset the image ring, and begin streaming.

## Live session pipeline

### Continuous capture

While the session state is `STREAMING`:

1. The camera worker retrieves the latest Basler BGR frame.
2. The frame is copied into one of five shared-memory image slots.
3. The same acquired frame is appended to the active Motion-JPEG AVI recording.
4. If the ring has no unused historical slot, the oldest non-protected slot is overwritten. The slot currently being inferred is never overwritten.

The implementation uses `GrabStrategy.LatestImages`; therefore, capture performance must be validated against the camera rate, JPEG encoding rate, and disk throughput.

### Periodic live inference

At each `TIME_INTERVAL` (currently 880 ms):

1. The UI selects the newest completed ring-buffer frame.
2. The selected slot is protected and its index is written to the image-region header.
3. The UI signals the `cam_ui_wrote_event` named event.
4. The Python live AI process reads that selected slot, runs the existing YOLO and weight pipeline, and writes counts, weights, and result image data to the result region.
5. Python signals `cam_ai_wrote_event`.
6. The UI result worker reads the result block, persists information through the existing observer/repository path, and displays the annotated image and values.
7. The protected image slot is released.

Only one live inference is in flight. If model execution takes longer than `TIME_INTERVAL`, the next eligible inference uses the newest available frame instead of building an unbounded backlog.

## Shared-memory protocol

The named mapping is `cam_01_map` and contains two logical regions.

| Region | Absolute offset | Layout |
| --- | ---: | --- |
| Image ring (region #1) | `0` | 64-byte header followed by five 1920 x 1200 BGR frames. The header's first `Int32` is the slot selected for live inference. |
| Result block (region #2) | `34,560,064` | Existing result layout: counts at offsets 0–31, weights beginning at offset 32, and result image at offset 100 relative to region #2. |

Each BGR frame is 6,912,000 bytes. The complete mapping is 44,560,064 bytes.

Named events:

- `cam_ui_wrote_event`: UI has selected a ring slot for Python.
- `cam_ai_wrote_event`: Python has written the corresponding result block.

## STOP and offline video pipeline

Pressing **Stop** follows this sequence:

1. Session state changes to `READY`, preventing new camera frames and periodic live-inference dispatches.
2. If a live request was already sent, the UI waits for it to finish, consumes its event, and discards its result so it cannot update the finished session.
3. The complete shared-memory mapping is overwritten with zeroes; ring metadata is reset.
4. The current AVI is finalized and its file handle is released.
5. The UI synchronously launches `1_AI_Process/src/video_inference.py`.
6. The offline process opens the finalized AVI, infers every readable frame with the same `YOLOImpl` and weight functions used by live inference, then emits one structured summary.
7. The UI displays the summary in a MessageBox: processed frame count, accumulated class counts, and average per-frame weight-model results.

The UI intentionally remains unavailable for a new session while offline inference runs. This makes the recorded video the session's final source of truth.

## Data and results

Live results continue through the existing observer pipeline:

```text
Python result block -> UI result worker -> AIProcessEvent observers
                                      -> database repository
                                      -> image/UI observers
```

Offline video results are persisted as the authoritative finalized record for the session. Excel export selects only these finalized records, not the live per-frame snapshots collected during streaming.

## Configuration

Key configuration is in `0_UI_Process/Stone_Application/Common/Config.cs`.

| Setting | Current value / behavior |
| --- | --- |
| Image dimensions | 1920 x 1200, BGR8 |
| `TIME_INTERVAL` | 880 ms |
| Ring capacity | 5 frames |
| Video format | Motion-JPEG AVI |
| Nominal video frame rate | 30 FPS (`VIDEO_FRAME_RATE`) |
| Video folder | `STONE_VIDEO_OUTPUT_PATH` environment variable, otherwise `<repository>\Recordings` |
| Python executable | `1_AI_Process\.venv\Scripts\python.exe` |
| Live AI entry point | `1_AI_Process\src\main.py` |
| Offline AI entry point | `1_AI_Process\src\video_inference.py` |
| YOLO weights | `Weight_v2\model\YOLO\best260719.pt` |

Example video output configuration before starting the UI:

```powershell
$env:STONE_VIDEO_OUTPUT_PATH = 'D:\StoneRecordings'
```

## Build

The UI is a .NET Framework 4.7.2 WinForms project.

```powershell
dotnet msbuild 0_UI_Process\Stone_Application\Stone_Application.sln /t:Build /p:Configuration=Debug /p:Platform=x64
```

The Python environment must include the dependencies required by YOLO, OpenCV, Torch, Pandas, and the regression models.

## Important operational notes

- STOP waits for an already-dispatched live request before clearing shared memory. This prevents Python from reading/writing memory while the UI erases it.
- If the live AI process never responds, STOP can wait indefinitely. A controlled timeout/AI-process restart is a recommended future hardening step.
- Offline inference reads JPEG-decoded AVI frames, whereas live inference reads raw camera BGR frames. The two paths use the same model pipeline but can produce slightly different detections because of video compression/decoding.
- AVI uses a fixed nominal 30 FPS. If actual capture timing varies, playback timing is approximate rather than timestamp-accurate.

## Related implementation notes

- [Session STOP remediation](SESSION_STOP_REMEDIATIONS.md)
- [Implementation notes](IMPLEMENTATION_NOTES.md)
- [Consolidated review of the latest two commits](REVIEW_LATEST_TWO_COMMITS.md)
