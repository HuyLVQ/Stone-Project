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

START ensures live AI readiness, begins a PostgreSQL session, resets/activates IPC metadata with a new token, opens the recording worker, restarts the interval stopwatch, and enters `STREAMING`. Check failure cleanup across these actions. Preserve session ID, recorder token, and IPC activity alignment.

STOP currently:

1. Changes state to `READY` and deactivates the IPC session.
2. Closes recording acceptance, settles the pending live request independently of UI callbacks, and clears memory/events only after the old writer is settled. The 15-second inference timeout requires confirmed Python exit before reuse; failed requests have separate accounting.
3. Drains accepted recording frames, finalizes AVI segments, releases handles, and writes the session JSON manifest. Recording finalization also runs if AI settlement fails.
4. Runs offline analysis of the manifest synchronously with session controls unavailable.
5. Persists the aggregate for the same session, then shows the result and restores START availability.

Maintain exclusive session ownership while offline processing runs. If the requested work introduces asynchronous processing, carry an explicit immutable session identity and prevent a later session from receiving an earlier result. Inspect UI marshaling together with STOP waits to avoid worker/UI deadlocks.

## Recorder details

`VideoRecorder` writes session AVI segments and a JSON manifest under `STONE_VIDEO_OUTPUT_PATH`, falling back to `<repository>/Recordings`. Capture uses a session token obtained before camera retrieval and offers an owned immutable frame buffer to an eight-frame bounded queue. A dedicated worker encodes/writes frames; overflow is counted. Inspect rejection and STOP acceptance boundaries when changing frame delivery.

The writer encodes BGR frames as JPEG, writes padded `00dc` chunks, and on STOP appends `idx1` and patches RIFF/list sizes and frame counts. Finalize and close before opening a video for inference. Keep repeated STOP/disposal behavior consistent with `LastCompletedPath` and prevent an older path from being attributed to a new failed session.

Nominal playback is 30 FPS without per-frame timestamps. Segments roll over before 1,800,000,000 bytes, reserving index space below signed 32-bit size limits. Encoder failures skip one frame. Partial writes truncate back to the chunk start; a failed rollback/finalization faults the recording. Keep cleanup in failure paths and publish completed paths only after success.

## Offline results and authority

The offline entry point uses the same `YOLOImpl` as live inference, resizes decoded frames to configured geometry, sums class counts across all readable frames, and averages four weight outputs over processed frames. Missing weight outputs contribute zero. This is not cross-frame tracking or deduplication; do not change aggregation or claim unique-object counts without an explicit requirement and suitable evidence.

The stdout contract is one summary line with this shape:

```text
VIDEO_RESULT|frames=2|expected=2|skipped=0|dropped=0|complete=1|known=1|MiSang=1|1x2=2|2x4=3|4x6=4|weight1=1.0|weight2=2.0|weight3=3.0|weight4=4.0
```

C# selects the last matching line and parses invariant-culture numbers. The current summary also requires `expected`, `skipped`, `dropped`, `complete`, and `known` quality fields. Preserve producer/parser agreement, finite-number/accounting validation, and concurrent stdout/stderr draining. A direct CLI run does not write the finalized database row.

`Common.PersistFinalSessionResult()` calls `SaveFinalSessionResult()`, which replaces the finalized row for that session within a transaction. Partial usable results persist under the skip-and-continue policy, with Complete/Partial/Unknown status and frame/loss counts. Excel selects finalized rows and exposes quality fields in J-N. Live telemetry must not become a fallback for failed offline processing.

The manifest supplies expected frames per segment and capture/queue/encoding losses. Offline analysis seeks past failed positions with a configurable limit of 30 consecutive failures, continues with later segments, skips per-frame inference errors, and rejects zero processed frames. Standalone AVI completeness remains Unknown. A Complete manifest analysis does not establish that the camera itself skipped no frames; retain that distinction in status wording.

`Weight_v2/test.py` uses a different YOLO checkpoint, sampling interval, and calibration. Do not substitute it for STOP inference on the assumption that the results are equivalent.

## Validate the change

For recorder changes, use known synthetic frames to check decode count, dimensions, finalization, repeated STOP, and affected file-error cases. For aggregation/parser changes, verify known counts, averages, model order, missing/malformed output, and nonzero subprocess exits. For persistence changes, verify session identity and that export includes finalized results while omitting incomplete sessions, using isolated test data.

For lifecycle changes, check pending live inference at STOP, a second START/STOP, and relevant shutdown paths on the target environment. `MainForm.CleanupApplicationResources()` calls both `Common.ResetRepository()` and the Docker cleanup helper; `InitService/DisposeService.cs` uses `compose down -v`. Closing the application can therefore erase measurements and remove the database volume. Use disposable data for shutdown validation.

Build affected C# code with Debug/x64 and syntax-check changed Python. Report actual checks and hardware-dependent gaps separately. For ring ownership or event changes, also read `../stone-shared-memory/SKILL.md`; for control/thread interactions, read `../stone-winforms/SKILL.md`.
