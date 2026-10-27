# Circular-buffer capture, timed inference, and session video recording

## Delivered changes

- Shared-memory region #1 is now a five-slot circular image buffer.
- Camera capture is continuous while inference is scheduled separately at `TIME_INTERVAL`.
- The existing UI-to-AI and AI-to-UI event handshake remains one inference at a time.
- The existing inference-result payload, database persistence, and UI-display flow are retained.
- Each START action starts a new video recording session; STOP and application shutdown finalize it.
- Model and training references formerly using `Weight/` now use `Weight_v2/`.

## Shared-memory protocol

The configured camera image is BGR, 1920 x 1200 x 3 bytes, or 6,912,000 bytes per frame.

Region #1 begins at offset `0` and contains:

| Offset | Size | Contents |
| --- | ---: | --- |
| `0` | 64 bytes | Header; the first `Int32` is the slot selected for inference. |
| `64` | 5 x 6,912,000 bytes | Five BGR image slots. |

Region #2 begins immediately after region #1, at offset `34,560,064`. Its internal layout is unchanged: count values begin at offset zero of region #2, weight values retain their existing positions, and the result image remains at region-relative offset `100`.

The overall mapping is now 44,560,064 bytes. Moving region #2 was necessary because five full-resolution images cannot fit in the previous 10 MB image area. Both the .NET and Python implementations use the same constants.

## Capture and inference behavior

1. The capture worker retrieves frames continuously while streaming is active.
2. Each frame is placed into the next ring slot and appended to the active session video.
3. At each `TIME_INTERVAL`, the inference worker acquires the newest completely written slot.
4. The selected slot is protected until the AI completion event is received and its results have been read.
5. The selected slot index is written to the region #1 header, then the existing UI-to-AI event is signaled.
6. The Python process reads that slot, writes its result to region #2, and signals the existing AI-to-UI event.
7. The UI reads the existing region #2 result format, updates the database/UI through the existing observers, and releases the protected image slot.

### Image-selection assumption

Inference uses the most recently captured completed frame at the time its interval becomes due. This favors the current scene over processing stale imagery.

### Full-buffer handling

The active inference slot is never overwritten. When capture reaches a slot that is not protected, it overwrites the old frame in that slot. Thus the five slots behave as a latest-frame cache; frames that are not selected for inference can be discarded from shared memory without blocking capture. All acquired frames are still sent to the session video recorder.

### Timing limitation

The current event protocol permits only one inference in flight. If inference itself takes longer than `TIME_INTERVAL`, elapsed intervals are coalesced and the next inference uses the newest available frame after the previous response. Supporting overlapping inferences would require a different result correlation/signaling protocol.

## Video recording

The UI now creates Motion-JPEG AVI files without adding a native codec or package dependency.

- **Start:** `FormHome` starts a new `VideoRecorder` session.
- **Frames:** every frame successfully acquired by the camera while streaming is appended, including frames not selected for inference.
- **Stop/exit:** the current AVI index/header is finalized on STOP and during application cleanup.
- **Filename:** `session_yyyyMMdd_HHmmss_fff.avi`.
- **Output folder:** environment variable `STONE_VIDEO_OUTPUT_PATH`; default is `<repository root>\Recordings`.
- **Playback rate:** `VIDEO_FRAME_RATE`, currently 30 FPS.

Example configuration before launching the UI:

```powershell
$env:STONE_VIDEO_OUTPUT_PATH = 'D:\StoneRecordings'
```

### Video concern

The AVI records all captured frames but has a fixed nominal 30 FPS playback rate. It does not encode individual capture timestamps, so a variable-rate camera stream will not preserve exact wall-clock timing during playback. The JPEG encoding and disk speed also set a practical upper bound on sustainable capture throughput.

## Weight path update

The YOLO model now resolves to:

```text
Weight_v2\model\YOLO\best260719.pt
```

The historical absolute output paths in `utils_train.py` were likewise changed from `Weight` to `Weight_v2`.

## Validation

The Python files were syntax-checked with `py_compile`.

The .NET UI was built successfully with:

```powershell
dotnet msbuild Stone_Application.sln /t:Build /p:Configuration=Debug /p:Platform=x64
```

The build completed successfully. It still reports pre-existing warnings about unreachable code and an unused exception variable; no new compile errors were introduced.

## Runtime validation still recommended

Hardware-dependent validation should be performed with the camera, AI environment, and database available:

1. Confirm the Python process opens the larger named shared-memory mapping.
2. Confirm result counts, weights, result image, database rows, and UI updates remain correct.
3. Confirm capture continues while an intentionally slow inference is running.
4. Confirm each START/STOP sequence creates one playable AVI in the configured folder.

## Offline video inference after STOP

After STOP finalizes the AVI, the UI launches `1_AI_Process/src/video_inference.py` synchronously. This is a dedicated offline entry point that imports and uses the same `YOLOImpl` class and `processAndVisualizeWithMultipleWeight` method as the runtime image pipeline; it does not use the shared-memory events and therefore cannot interfere with capture or the live AI process.

Every readable video frame is inferred. The video process emits one machine-readable result line that the UI parses and displays in a structured MessageBox:

- accumulated counts for `MiSang`, `1x2`, `2x4`, and `4x6`;
- average of each per-frame model-weight output; and
- the number of frames processed.

The UI disables a new START action and uses a wait cursor until this operation completes. If offline inference fails, the AVI remains finalized and an error MessageBox is shown.

### Aggregation assumptions and risks

Counts are accumulated across all frames, following the PO's assurance that the model safely handles duplicate objects. This must be treated as a model-level contract: ordinary per-frame object detection does not itself guarantee cross-frame de-duplication. If that assurance is not true in production, the accumulated counts will be inflated and a tracking/de-duplication strategy will be required.

Weight outputs are averaged over processed frames as requested. A frame that yields no usable weight contributes zero with the present runtime model behavior.

### Why finalizing the AVI first matters

AVI files contain an index and header values that are completed only when recording is stopped. Attempting to open the file before finalization can cause OpenCV to see an incomplete frame list, stop early, or reject the file entirely. The STOP flow therefore closes the writer first, patches the AVI metadata, releases the file handle, verifies that a path exists, and only then starts the offline process. Abrupt power loss, process termination, disk-full errors, or exceptions during finalization can still leave a partial AVI; the offline process reports that failure instead of producing a partial aggregate silently.
