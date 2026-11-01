# Stone Project

Camera-based stone classification system with a WinForms UI, a Python YOLO inference service, PostgreSQL persistence, live result display, session video recording, and offline whole-video analysis.

## System layout

```text
Basler camera
    |
    v
.NET WinForms UI ------------------------------------------------------+
  capture worker -> shared-memory image ring -> Python live AI         |
  recording queue -> recorder worker -> session AVI segments           |
  result worker  <- shared-memory result block <- completion event     |
    |                                                                   |
    +--> database persistence                                           |
    +--> result image/count/weight UI display                           |
                                                                        |
STOP -> drain/finalize -> session manifest -> offline AI -> summary ---+
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
3. The acquired frame is offered to an eight-frame recording queue using its session token. A dedicated worker encodes JPEG and writes AVI chunks; queue overflow is counted rather than blocking capture.
4. If the ring has no unused historical slot, the oldest non-protected slot is overwritten. The slot currently being inferred is never overwritten.

The implementation uses `GrabStrategy.LatestImages`; therefore, capture performance must be validated against the camera rate, JPEG encoding rate, and disk throughput.

### Periodic live inference

At each `TIME_INTERVAL` (currently 880 ms):

1. The UI selects the newest completed ring-buffer frame.
2. The selected slot is protected and its index is written to the image-region header.
3. The UI signals the `cam_ui_wrote_event` named event.
4. The Python live AI process reads that selected slot, runs the existing YOLO and weight pipeline, and writes counts, weights, and result image data to the result region.
5. Python signals `cam_ai_wrote_event`.
6. The result worker consumes the completion, reads the result, updates received-request accounting, and releases the protected slot before observer notification.
7. Observers persist live telemetry and post asynchronous UI updates. Session-token checks discard obsolete UI callbacks.

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
2. Recording acceptance closes and already queued frames drain to the recorder. Old-session frames cannot enter a new recording.
3. Sent and received live-request counters settle independently of UI callbacks. The normal AI wait is bounded at 15 seconds; on timeout, the old AI process must exit before memory is cleared. A failed request is tracked separately from a received result.
4. Shared memory and events reset only after the outstanding writer is settled. AVI segments are finalized and their handles released; a JSON manifest records segment paths, frame counts, and recording losses.
5. The UI synchronously launches `1_AI_Process/src/video_inference.py` against that manifest.
6. Offline analysis combines all segments using the same `YOLOImpl` and weight functions as live inference. It attempts bounded recovery past unreadable frames and skips failed frame inference. Counts are summed and weights averaged over successfully processed frames.
7. The summary includes expected/processed/skipped frames, recording losses, and a Complete/Partial/Unknown status. Zero processed frames produce an error instead of a finalized zero result.

The UI intentionally remains unavailable for a new session while offline inference runs. This makes the recorded video the session's final source of truth.

## Data and results

Live results continue through the existing observer pipeline:

```text
Python result block -> UI result worker -> AIProcessEvent observers
                                      -> database repository
                                      -> image/UI observers
```

Offline video results are persisted as the finalized record for the session. Usable partial aggregates remain available with explicit quality metadata. Excel selects finalized records rather than live snapshots and writes status, expected frames, processed frames, skipped frames, and recording losses to columns J-N. Historical rows without quality metadata remain Unknown.

AVI files roll over before reaching 1,800,000,000 bytes, with space reserved for the final index. All segments share one session manifest. Encoding failures skip the affected frame; failed chunk writes truncate back to the last complete chunk before continuing. A failed rollback or finalization faults the recording instead of publishing a completed path.

## Configuration

Key configuration is in `0_UI_Process/Stone_Application/Common/Config.cs`.

| Setting | Current value / behavior |
| --- | --- |
| Image dimensions | 1920 x 1200, BGR8 |
| `TIME_INTERVAL` | 880 ms |
| Ring capacity | 5 frames |
| Video format | Motion-JPEG AVI |
| Nominal video frame rate | 30 FPS (`VIDEO_FRAME_RATE`) |
| Recording queue | 8 raw frames, about 53 MiB (`VIDEO_QUEUE_CAPACITY`) |
| AVI segment limit | 1,800,000,000 bytes (`VIDEO_SEGMENT_MAX_BYTES`) |
| AI STOP wait / process exit wait | 15,000 ms / 5,000 ms |
| Decode recovery | Up to 30 consecutive failed positions per segment |
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

## Focused video checks

From the repository root, run `.\tests\run_video_checks.ps1` in PowerShell. The runner builds Debug/x64, compiles a standalone C# harness, runs Python video tests, and syntax-checks changed Python entry points. It currently locates Roslyn under Visual Studio 2022 Community; adjust that path for another installation.

The checks use synthetic videos, injected file failures, and isolated IPC names. They do not launch the application, connect hardware, or start/reset PostgreSQL. Camera throughput, real AI termination, and database migration still need target-environment validation.

## Important operational notes

- STOP waits for an already-dispatched live request before clearing shared memory. This prevents Python from reading/writing memory while the UI erases it.
- If a live request times out, Python is stopped and its exit confirmed before memory reuse. The next START reinitializes and warms up AI; if exit cannot be confirmed, START remains unavailable.
- Offline inference reads JPEG-decoded AVI frames, whereas live inference reads raw camera BGR frames. The two paths use the same model pipeline but can produce slightly different detections because of video compression/decoding.
- AVI uses a fixed nominal 30 FPS. If actual capture timing varies, playback timing is approximate rather than timestamp-accurate.
- Queue and observed capture/encoding losses are reported; frames skipped internally by the camera's `LatestImages` strategy are not proven absent by a Complete analysis status.
- Standalone AVI analysis without a recorder manifest has Unknown completeness. Preserve the manifest with its segments for repeatable session analysis.

## Related implementation notes

- [Implementation notes](IMPLEMENTATION_NOTES.md)
- [Offline results and export quality](OFFLINE_RESULTS_AS_EXPORT_SOURCE.md)
- [Session-video skill](.agents/skills/stone-session-video/SKILL.md)
