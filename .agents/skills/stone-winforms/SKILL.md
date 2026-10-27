---
name: stone-winforms
description: Modify or debug the Stone Project WinForms UI, navigation, control state, observer updates, and worker-to-UI interactions. Use for this repository's C# desktop behavior; use stone-shared-memory for wire-protocol changes and stone-session-video for recording/finalization changes.
---

# Stone WinForms

## Locate the implementation

Resolve paths below from the repository root, three parents above this skill directory. Confirm current code before relying on the behavior described here.

- `0_UI_Process/Stone_Application/MainForm.cs`: application shell, embedded forms, navigation, and shutdown.
- `Forms/FormHome.cs`: image display, START/STOP controls, and session orchestration.
- `Forms/FormSettings.cs`: serial Modbus/camera connection and AI initialization.
- `Forms/FormLogs.cs`: measurements and report actions.
- `Common/Common.cs`: shared state, session identity, and repository instance.
- `Infrastructure/Thread.cs`: capture task and inference/result task.
- `Event/AIProcessEvent.cs`, `Event/EventData.cs`, and `Observers/`: result delivery and payloads.

Paths after the first entry are relative to `0_UI_Process/Stone_Application/`.

## Work on the requested behavior

1. Trace the control event through state changes, worker actions, and observers before editing. `MainForm` retains Home, Settings, and Logs instances and embeds them in a panel; navigation does not construct a new session.
2. Inspect the associated `.Designer.cs` and `.resx` when changing layout, control names, or event wiring. Preserve designer compatibility. This is a .NET Framework 4.7.2 project using C# 7.3 in x64 configurations; new source files need explicit project inclusion in the existing non-SDK `.csproj`.
3. Keep control updates on the UI thread. Review `Invoke`/`BeginInvoke`, form disposal, and synchronous waits together: a worker synchronously invoking the UI can deadlock when the UI waits for that worker during STOP or shutdown. Avoid adding synchronous UI callbacks while holding shared state or ring locks.
4. Preserve result routing through `AIProcessEvent` and its observers. `notifyInformation()` stamps the session through `Common.PrepareInformationForPersistence()` before notification; bypassing this can attach measurements to the wrong session.
5. For image updates, track ownership of the observer-created bitmap, the clone stored in the PictureBox, and the previous displayed image. Dispose objects at the end of their actual ownership; do not dispose an image before a queued UI update uses it.

## State and lifecycle details

- States are `UN_INIT`, `READY`, and `STREAMING`, guarded by `Common.s_lockState`. IPC separately tracks whether the session is active; button state alone is not the session authority.
- The names `enableStreaming()` and `disableStreaming()` are misleading: both set `READY`; `startButtonClick()` explicitly sets `STREAMING` afterward. Inspect callers before renaming or moving transitions.
- Settings currently enables AI initialization after both camera and Modbus connection succeed. AI initialization performs warm-up and starts the two workers. Check repeated initialization for duplicate workers and observer registrations.
- STOP currently runs offline inference synchronously and keeps START unavailable until it finishes. Preserve that workflow unless the requested change includes asynchronous processing and session exclusion.
- Review `MainForm` shutdown callers before testing close behavior. `CleanupApplicationResources()` calls `Common.ResetRepository()` and `DisposeService.cleanupService()`; the latter runs `compose down -v`. Closing the application can erase measurements and remove the database volume. Use disposable data for shutdown validation.

## Validate the change

For C# implementation changes, build from the repository root with a compatible installed toolchain:

```powershell
dotnet msbuild .\0_UI_Process\Stone_Application\Stone_Application.sln /t:Build /p:Configuration=Debug /p:Platform=x64
```

For layout changes, inspect the affected form at the target display size and after navigation. For worker/state changes, check only the affected transitions, including repeated START/STOP or closing during pending updates when relevant. Report build results separately from hardware/UI checks that could not run.

If the task changes the byte layout or event handshake, also read `../stone-shared-memory/SKILL.md`. If it changes START/STOP ordering, recording, or offline results, read `../stone-session-video/SKILL.md`.
