# Stone AI Pipeline Migration Summary

## Objective

The legacy UI-to-AI pipeline used one manually coordinated memory-mapped region and Windows events. The migrated pipeline keeps image pixels in shared memory but uses ZeroMQ and protobuf messages for coordination, worker distribution, and result delivery.

## Complete pipeline

```text
Basler Camera
    |
    v
C# UI capture queue
    |
    | writes image bytes to an available input-MMF slot
    v
UI Broker RX PUSH  -- protobuf: RxPullMessage -->  Broker RX PULL
                                                       |
                                                       v
                                             bounded broker image queue
                                                       |
                                                       v
                                      Broker ROUTER / worker scheduler
                                                       |
                         +-----------------------------+-----------------------------+
                         |                             |                             |
                         v                             v                             v
                    AI Worker 0 DEALER            AI Worker 1 DEALER            AI Worker N DEALER
                         |                             |                             |
                         +-----------------------------+-----------------------------+
                                                       |
                                                       v
                                      Broker TX PUSH -- protobuf --> UI TX PULL
                                                       |
                                                       v
                                      UI result observers / database / display
```

## Shared-memory design

The UI creates two named memory-mapped regions:

- `cam_01_input_map`: captured BGR image bytes.
- `cam_01_output_map`: processed BGR image bytes for saved frames.

Each map is divided into five slots. `image_location` is a byte offset into a slot; it is not a native pointer and does not contain the map name.

The slot stride is aligned to the Windows memory-mapped view allocation granularity. This allows the C# UI to create a view directly at each slot offset.

The UI maintains an available-slot queue. A slot is removed before sending a frame and returned only after the corresponding worker result comes back. If all slots are occupied, the newly captured frame is dropped safely instead of overwriting an image still being processed.

## ZeroMQ sockets

| Path | UI socket | Broker socket | Endpoint |
|---|---|---|---|
| UI to Broker | `PUSH` | `PULL` | `tcp://127.0.0.1:5555` |
| Broker to UI | `PULL` | `PUSH` | `tcp://127.0.0.1:5556` |
| Broker to workers | `DEALER` | `ROUTER` | `tcp://127.0.0.1:5557` |

The broker binds its sockets where appropriate; the UI and workers connect to the broker.

## Protobuf messages

### Broker RX

`RxPullMessage` contains an `RxFrame` with the input-MMF byte offset.

### Worker command

`WorkerCommand` contains:

- `PROCESS`: infer and return measurements only.
- `PROCESS_AND_SAVE`: infer, draw annotations, write the processed image to the output MMF, and return its offset.
- `image_location`: input-MMF slot offset.

### Worker result

`WorkerMessage` contains:

- `RESULT` or `RESULT_AND_SAVE`.
- Legacy rock classification values in `rock_percentages`, ordered as `MiSang`, `1x2`, `2x4`, `4x6`, `Khac`.
- Repeated weight predictions.
- `image_save_location` only for `RESULT_AND_SAVE`.
- Internal `image_location` correlation so the UI can release the completed input slot.

### Broker TX

`TxPushMessage` forwards worker results to the UI, including the internal input offset and, for saved results, the output offset.

Python bindings are under `2_Broker_Process/proto`. C# bindings are under `0_UI_Process/Stone_Application/Proto`.

## Broker behavior

The broker:

1. Receives `RAWIMAGE` messages from the UI.
2. Adds image offsets to a bounded queue.
3. Tracks workers that announce `READY`, `REQUEST`, or a completed result.
4. Dispatches queued images to idle workers.
5. Selects the command based on the received-frame count.
6. Sends every `IMAGE_INTERVAL`-th frame as `PROCESS_AND_SAVE`; all other frames use `PROCESS`.
7. Forwards worker results to the UI.

The scheduling queue is availability-based and behaves as round robin when workers have similar processing times. The default broker queue is bounded to keep latency low.

## AI worker behavior

Each worker is a separate Python process and loads its own `YOLOImpl` instance. The worker:

1. Opens both named MMFs using startup arguments.
2. Connects to the broker ROUTER socket as a DEALER with a unique identity.
3. Announces `READY`.
4. Reads the input image from the requested offset.
5. Runs YOLO inference and the legacy weight-regression calculations.
6. Skips all drawing and output-MMF writes for `PROCESS`.
7. Draws and writes the processed image for `PROCESS_AND_SAVE`.
8. Returns measurements and slot correlation data to the broker.

The existing model calculation path was extended with a `p_draw` flag so classification and weight computation remain available without the annotation overhead.

## C# UI changes

The C# UI now:

- Starts the broker process instead of the old single AI process.
- Passes MMF names, dimensions, map size, endpoints, worker count, queue limit, and `IMAGE_INTERVAL` through command-line arguments.
- Uses NetMQ for PUSH/PULL transport.
- Uses generated Google.Protobuf classes for serialization.
- Writes camera frames directly into available input-MMF slots.
- Polls broker results asynchronously.
- Reads saved images from the output MMF.
- Publishes measurements through the existing information and image observers.
- Releases the completed MMF slot after each result.
- Terminates the broker process tree during cleanup so worker processes are not orphaned.

The old UI event handles and legacy MMF result layout are no longer used by the active pipeline.

## Configuration

The main migration settings are in `0_UI_Process/Stone_Application/Common/Config.cs`:

| Setting | Default |
|---|---:|
| Image width | `1920` |
| Image height | `1200` |
| Input/output slot count | `5` |
| Worker count | `3` |
| `IMAGE_INTERVAL` | `10` |
| UI capture queue bound | `2` |
| RX endpoint | `tcp://127.0.0.1:5555` |
| TX endpoint | `tcp://127.0.0.1:5556` |
| Worker ROUTER endpoint | `tcp://127.0.0.1:5557` |

The worker count should be benchmarked on the target RTX 4060 Ti. Three workers are the current conservative default; increasing the count increases GPU memory usage because each process loads its own model.

## Dependency setup

Dependency binaries are intentionally not committed. Follow [MIGRATION_SETUP.md](MIGRATION_SETUP.md) to restore:

- NetMQ
- Google.Protobuf
- AsyncIO
- NaCl.Net
- Python AI dependencies
- Python broker dependencies

## Validation completed

The following checks passed:

- Python compilation for the migrated broker, worker, transport, model, and configuration modules.
- Python protobuf serialization/deserialization round trip.
- Broker ROUTER dispatch and worker-result return smoke test.
- C# protobuf generation.
- C# solution build with zero errors.
- `git diff --check`.

## Remaining runtime validation

The actual camera/GPU integration test still needs to be run on the deployment machine. It should verify:

1. All workers open the same named MMFs.
2. Five-slot reuse does not overwrite in-flight images.
3. `RESULT_AND_SAVE` output images display correctly.
4. The selected worker count fits available GPU memory.
5. Frame dropping maintains the desired live latency.
