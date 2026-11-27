---
name: stone-shared-memory
description: Modify, troubleshoot, or validate the Stone Project C#/Python shared-memory ring, result serialization, Windows events, and live-inference synchronization. Use when either IPC peer, image geometry, payload order, warm-up, or session memory reset changes.
---

# Stone Shared Memory

## Read both peers

Resolve paths from the repository root, three parents above this skill directory.

| Concern | Source |
| --- | --- |
| C# constants | `0_UI_Process/Stone_Application/Common/Config.cs` |
| Ring ownership and serialization | `0_UI_Process/Stone_Application/Infrastructure/IPCServices.cs` |
| Dispatch, completion, and cancellation | `0_UI_Process/Stone_Application/Infrastructure/Thread.cs` |
| Launch arguments and warm-up | `0_UI_Process/Stone_Application/AIHelpers/AIHelper.cs` |
| Python constants and class ordering | `1_AI_Process/utils/config.py` |
| Python mapping, events, and serialization | `1_AI_Process/utils/IPCHelpers.py` |
| Live inference loop | `1_AI_Process/src/main.py` |

Treat the current implementations as the source of truth. Update both peers and warm-up when changing the contract.

## Current wire contract

The mapping is `cam_01_map`; events are `cam_ui_wrote_event` and `cam_ai_wrote_event`. C# creates auto-reset events; Python opens them. Names are machine-local shared resources, so concurrent app instances can interfere.

| Field | Offset / representation |
| --- | --- |
| Selected slot | Absolute 0: little-endian signed Int32, slot 0-4. |
| Image header | 64 bytes. |
| Image slots | Absolute `64 + slot * 6,912,000`; five 1920 x 1200 x 3 uint8 BGR frames, tightly packed. |
| Result base | Absolute 34,560,064; reserve 10,000,000 bytes. |
| Class counts | Result-relative 0, 8, 16, 24: little-endian signed Int64 for MiSang, 1x2, 2x4, 4x6. |
| Model weights | Result-relative 32, 36, 40, 44: little-endian float32. |
| Annotated image | Result-relative 100: 6,912,000 raw BGR bytes. |
| Mapping size | 44,560,064 bytes. |

`taskReadMultiple()` writes all supplied weights. The C# reader reads weights 2-4 only in debug mode and otherwise returns zero for them. The older Python `taskRead()` writes one or four weights according to debug mode. Do not assume these paths are identical.

Class ordering derives from `TYPE_COLORS` insertion order in Python. The current four weight outputs are polynomial, random forest, XGBoost, and polynomial-feature XGBoost. Keep ordering consistent with readers, labels, and downstream persistence.

## Preserve synchronization

1. Capture writes a complete frame under the ring lock, then publishes the newest slot. It skips the slot protected for inference; older unprotected frames may be overwritten.
2. At an eligible interval, acquire the newest completed slot, mark inference in flight, write its index, and signal UI-to-AI. The protocol supports one outstanding request, not a queued job per captured frame.
3. Python waits, reads the selected slot, writes the result, and signals AI-to-UI. C# consumes the event under the ring lock, reads and stamps the result, updates completion accounting, and releases the slot before observer notification. UI callbacks are asynchronous and validate the session token when they execute.
4. STOP disables new dispatch and waits for outstanding inference before zeroing the mapping. Sent, received, and failed counters must settle as `sent == received + failed`. A late result must be consumed without updating the completed session.

Review all exception and cancellation exits between acquisition and release. A missing release can block STOP; releasing before Python finishes can allow overwrite or clearing during inference. A timeout requires proving the old writer has stopped before reuse or reset, not merely abandoning the wait.

Warm-up writes slot zero directly and consumes completion before normal workers run. Check it whenever geometry, offsets, event names, or startup sequencing change. `Config.s_rootPath` depends on executable output depth, so verify launch paths when diagnosing a worker that never connects.

The normal STOP wait is bounded by `AI_STOP_TIMEOUT_MS` (15 seconds). On timeout, Python must exit within `AI_EXIT_TIMEOUT_MS` (5 seconds) before the outstanding request is marked failed and memory/events reset. If exit cannot be confirmed, retain memory and prevent START. Inspect warm-up readiness and old worker event consumption when changing restart logic.

Python now checks the wait return code before reading. Result-write exceptions can still be followed by completion without an explicit wire-level error status. Do not interpret an event alone as proof of a valid payload when extending failure handling.

## Validate the change

- For serialization changes, use meaningful cross-language values to verify field size, endianness, ordering, image boundaries, and total region sizes. Check debug and normal behavior when affected.
- Build changed C# code in Debug/x64; syntax-check changed Python files using the project interpreter. Compilation does not validate the Windows handshake.
- For synchronization changes, exercise slow inference, STOP with a pending response, a second session, and affected failure/cancellation paths. Prefer isolated mapping/event names for a test harness so it cannot collide with an active session.
- State which checks ran and which need the Windows camera/model environment. Keep review notes explicit about remaining races instead of claiming the entire lifecycle is safe from static inspection.

For recording/finalization consequences, read `../stone-session-video/SKILL.md`.
