---
name: stone-session-video
description: Modify or debug Stone Project START/STOP sequencing, Motion-JPEG AVI recording, offline video inference, and finalized session results used by export. Use for recording lifecycle and result authority; standalone model training and report styling are outside this skill.
---

# Stone Session Video

## Follow the complete session

Resolve paths from the repository root, three parents above this skill directory.

- `0_UI_Process/Stone_Application/Forms/FormHome.cs`: START/STOP and result presentation.
- `0_UI_Process/Stone_Application/CameraClass/BaslerCamera.cs`: acquired-frame delivery.
- `0_UI_Process/Stone_Application/Infrastructure/VideoRecorder.cs`: MJPEG AVI writer.
- `0_UI_Process/Stone_Application/Infrastructure/Thread.cs` and `0_UI_Process/Stone_Application/Infrastructure/IPCServices.cs`: live requests and STOP settlement.
- `0_UI_Process/Stone_Application/AIHelpers/AIHelper.cs`: offline subprocess and summary parser.
- `1_AI_Process/src/video_inference.py`: offline frame loop and summary producer.
- `1_AI_Process/module/YOLO/src/YOLOModel.py`: pipeline shared with live inference.
- `0_UI_Process/Stone_Application/Common/Common.cs` and `0_UI_Process/Stone_Application/Repository/PostgreSQLRepository.cs`: session attribution and finalized persistence.
- `0_UI_Process/Stone_Application/Forms/FormLogs.cs` and `0_UI_Process/Stone_Application/PDFExport/ExcelExport.cs`: export consumers.
- `0_UI_Process/Stone_Application/MainForm.cs`: shutdown recording/resource cleanup.

## Preserve the session contract

START currently activates/reset IPC metadata, begins a PostgreSQL session, opens a new AVI, restarts the interval stopwatch, and enters `STREAMING`. Check failure handling across these actions: a failed start can leave partially initialized resources. Preserve the requested workflow while keeping session ID, recorder, and IPC activity aligned.

STOP currently:

1. Changes state to `READY` and deactivates the IPC session.
2. Lets the pending live request finish; consumes/discards its response, then clears the mapping and ring metadata.
3. Finalizes the AVI and releases its file handle.
4. Runs offline inference synchronously with session controls unavailable.
5. Persists the aggregate for the same session, then shows the result and restores START availability.

Maintain exclusive session ownership while offline processing runs. If the requested work introduces asynchronous processing, carry an explicit immutable session identity and prevent a later session from receiving an earlier result. Inspect UI marshaling together with STOP waits to avoid worker/UI deadlocks.

## Recorder details

`VideoRecorder` writes `session_yyyyMMdd_HHmmss_fff.avi` under `STONE_VIDEO_OUTPUT_PATH`, falling back to `<repository>/Recordings`. Each acquired frame is offered to recording independently of live inference cadence. Inspect the camera call site when changing frame delivery.

The writer encodes BGR frames as JPEG, writes padded `00dc` chunks, and on STOP appends `idx1` and patches RIFF/list sizes and frame counts. Finalize and close before opening a video for inference. Keep repeated STOP/disposal behavior consistent with `LastCompletedPath` and prevent an older path from being attributed to a new failed session.

Nominal playback is 30 FPS without per-frame timestamps. Encoding/disk work runs on the capture path. The writer retains its index in memory and patches sizes using checked signed 32-bit conversions; long/large recordings need explicit format-limit analysis rather than a claim of unlimited recording support.

## Offline results and authority

The offline entry point uses the same `YOLOImpl` as live inference, resizes decoded frames to configured geometry, sums class counts across all readable frames, and averages four weight outputs over processed frames. Missing weight outputs contribute zero. This is not cross-frame tracking or deduplication; do not change aggregation or claim unique-object counts without an explicit requirement and suitable evidence.

The stdout contract is one summary line with this shape:

```text
VIDEO_RESULT|frames=2|MiSang=1|1x2=2|2x4=3|4x6=4|weight1=1.0|weight2=2.0|weight3=3.0|weight4=4.0
```

C# selects the last matching line and parses invariant-culture numbers. Preserve producer/parser agreement and concurrent draining of stdout/stderr to avoid subprocess pipe deadlocks. A direct CLI run prints results but does not write the finalized database row.

`Common.PersistFinalSessionResult()` calls `SaveFinalSessionResult()`, which replaces the finalized row for that session within a transaction. Excel obtains rows marked `isFinalizedResult = TRUE`; live telemetry must not silently become the fallback when offline analysis or persistence fails.

The offline loop currently treats a failed read like EOF and emits zero-frame summaries for an opened video with no decoded frames. Neither behavior guarantees a complete valid recording. When improving failure handling, address these cases explicitly and distinguish inference failure from persistence failure in user feedback.

`Weight_v2/test.py` uses a different YOLO checkpoint, sampling interval, and calibration. Do not substitute it for STOP inference on the assumption that the results are equivalent.

## Validate the change

For recorder changes, use known synthetic frames to check decode count, dimensions, finalization, repeated STOP, and affected file-error cases. For aggregation/parser changes, verify known counts, averages, model order, missing/malformed output, and nonzero subprocess exits. For persistence changes, verify session identity and that export includes finalized results while omitting incomplete sessions, using isolated test data.

For lifecycle changes, check pending live inference at STOP, a second START/STOP, and relevant shutdown paths on the target environment. `MainForm.CleanupApplicationResources()` calls both `Common.ResetRepository()` and the Docker cleanup helper; `InitService/DisposeService.cs` uses `compose down -v`. Closing the application can therefore erase measurements and remove the database volume. Use disposable data for shutdown validation.

Build affected C# code with Debug/x64 and syntax-check changed Python. Report actual checks and hardware-dependent gaps separately. For ring ownership or event changes, also read `../stone-shared-memory/SKILL.md`; for control/thread interactions, read `../stone-winforms/SKILL.md`.
