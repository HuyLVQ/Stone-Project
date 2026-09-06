# ZeroMQ migration setup

The dependency binaries are intentionally not committed. Restore them locally before building.

## C# UI dependencies

From Visual Studio, restore the solution's `packages.config` dependencies. Alternatively, with `nuget.exe` installed:

```powershell
nuget restore .\0_UI_Process\Stone_Application\Stone_Application.sln `
  -PackagesDirectory .\0_UI_Process\Stone_Application\packages
```

The required migration packages are listed in `0_UI_Process/Stone_Application/packages.config`:

- `NetMQ` 4.0.1.13
- `Google.Protobuf` 3.25.5
- `AsyncIO` 0.1.69
- `NaCl.Net` 0.1.13

## Python dependencies

```powershell
& .\1_AI_Process\.venv\Scripts\python.exe -m pip install -r .\1_AI_Process\requirements.txt
& .\1_AI_Process\.venv\Scripts\python.exe -m pip install -r .\2_Broker_Process\requirements.txt
```

## Regenerating protobuf bindings

Python bindings:

```powershell
& .\1_AI_Process\.venv\Scripts\python.exe -m grpc_tools.protoc `
  -I .\3_Proto `
  --python_out .\2_Broker_Process\proto `
  .\3_Proto\brokerRx.proto .\3_Proto\brokerRouter.proto `
  .\3_Proto\brokerDealer.proto .\3_Proto\brokerTx.proto
```

C# bindings require the `Grpc.Tools` NuGet package. Use its `protoc.exe` and `grpc_csharp_plugin.exe` to regenerate the files under `0_UI_Process/Stone_Application/Proto`.
