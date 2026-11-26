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

The UI uses Basler Pylon's `VideoWriter` to create MP4 files. The capture loop copies each valid grab into a reusable buffer and offers it to a queue of eight frames. The writer thread consumes the queue, encodes frames, and closes each segment. Live IPC shares references to those buffers at up to 10 FPS.

- **Start:** `FormHome` starts a new `VideoRecorder` session.
- **Frames:** every frame successfully acquired by the camera while streaming is offered to Pylon's writer, including frames not selected for inference.
- **Stop/exit:** each MP4 segment is closed on STOP and during application cleanup.
- **Filename:** `session_yyyyMMdd_HHmmss_fff_part0001.mp4`.
- **Output folder:** environment variable `STONE_VIDEO_OUTPUT_PATH`; default is `<repository root>\Recordings`.
- **Playback rate:** the camera's estimated acquisition rate, with `VIDEO_FRAME_RATE` (30 FPS) as the fallback.
- **Quality:** `VIDEO_WRITER_QUALITY`, currently 80 on Pylon's 1-100 quality scale.

Example configuration before launching the UI:

```powershell
$env:STONE_VIDEO_OUTPUT_PATH = 'D:\StoneRecordings'
```

### Video concern

MP4 segments use constant-rate playback. The manifest stores capture offsets for the offline sampler and counts queue drops, buffer pool drops, observed camera skips, and writer failures. The bounded queue absorbs brief writer delays. Sustained throughput depends on encoding and disk speed.

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
4. Confirm each START/STOP sequence creates playable MP4 segments and a manifest in the configured folder.

## Offline video inference after STOP

After STOP closes the MP4 segments, the UI sends the manifest to `1_AI_Process/src/offline_worker.py`. The worker uses the Weight_v2 pipeline and samples 0.88-second capture intervals using frame offsets from the manifest.

The worker emits one machine-readable result line with:

- accumulated counts for `MiSang`, `1x2`, `2x4`, and `4x6`;
- an aggregate XGBoost weight prediction; and
- processed, expected, and skipped sample counts and recording losses.

The UI disables a new START action and uses a wait cursor until this operation completes. If offline inference fails, the MP4 remains finalized and an error MessageBox is shown.

### Aggregation assumptions and risks

Counts are accumulated across successfully processed samples. Per-frame detection does not deduplicate objects across frames. The Weight_v2 pipeline predicts weight from the aggregate sample features.

### Why closing the Pylon writer first matters

Pylon finalizes MP4 metadata when the writer closes. The STOP flow therefore closes the writer and releases the file handle before starting offline analysis. Abrupt power loss, process termination, disk-full errors, or exceptions during finalization can still leave a partial MP4; the offline process reports that failure instead of producing a partial aggregate silently.
