# Stone Client (3_Client_Process)

A minimal Windows client for the IoT server in `0_UI_Process/Stone_Application/Networking/Iot`.
It finds the server, shows its live stream (JPEG or H.264), and logs connection events and
offline results. Its window has a picture area on top and a message log below.

Same stack as `0_UI_Process`: .NET Framework 4.7.2, WinForms, C# 7.3, x64, a
non-SDK `.csproj`, and no NuGet packages.

## Status

| Phase | Scope | State |
|---|---|---|
| 0 | Window, config, screen and file log | Done |
| 1 | UDP discovery, lease refresh, online/offline | Done |
| 2 | TCP stream, protobuf decoding, results in the log | Done |
| 3 | JPEG frames in the picture box | Done |
| 4 | H.264 via `ffmpeg.exe` | Done (tested with a stand-in decoder; not yet against real FFmpeg output) |
| 5 | Hardening | Not started |

## Build and run

```powershell
dotnet msbuild .\3_Client_Process\Stone_Client\Stone_Client.sln /t:Build /p:Configuration=Debug /p:Platform=x64
```

```powershell
.\3_Client_Process\Stone_Client\bin\x64\Debug\Stone_Client.exe
```

Settings live in `App.config` (`Stone_Client.exe.config` next to the exe): `ClientId`,
`ServerAddress` (blank = broadcast), ports, `PreferredEncoding`, `FfmpegPath`, handshake
timing and `LogDirectory`. Logs are written to `logs\client_yyyyMMdd.log` next to the exe.

**H.264** needs `ffmpeg.exe` (any build; decoding uses FFmpeg's own H.264 decoder, not
libx264). The client looks for it in `FfmpegPath`, then `STONE_FFMPEG_PATH`, then next to
the exe, then on `PATH`. If `PreferredEncoding` is `h264` and none is found, the client logs
a warning and asks the server for JPEG instead.

**Firewall:** the server connects *to* the client on TCP 50002. On the first run Windows
asks whether to allow `Stone_Client.exe`. Allow it on the network the server is on,
otherwise discovery succeeds but no data arrives. Same-machine (loopback) testing works
without the rule.

## How it talks to the server

1. **Discovery (UDP 50001).** The client sends
   `SEARCHING FOR SERVER - my current id is:<id>; image encoding is:jpeg|h264`,
   broadcast on every local IPv4 network (plus loopback), or to `ServerAddress`.
2. **Confirmation.** The server replies with
   `SERVER CONFIRMED - client id: <id>; tcp port: 50002; image encoding: JPEG|H264_MPEG_TS`.
   The client then talks only to that server, so the server always sees one source address.
3. **Lease.** The server forgets a client 30 s after its last handshake. The client sends the
   handshake again every 10 s. If 3 in a row go unanswered, it logs *Server offline* and
   goes back to searching.
4. **Stream (TCP 50002, the server dials in).** Each record is a 4-byte big-endian length
   followed by an `IotEnvelope` (see the linked `Protobuf/*.proto`). At STOP the server closes
   the image connection and delivers the result on a new one. A new connection always
   replaces the old one.

5. **Display.** JPEG frames are decoded and shown as they arrive. H.264 chunks are piped to
   one `ffmpeg.exe` per session, which turns them into 1920×1200 frames. The decoder stops
   when the session's result arrives; if it crashes, it is restarted up to 3 times per
   session. After a gap in `sequence_number` the server resumes at a keyframe, so decoding
   simply continues. Only the newest frame is drawn, so a busy UI never falls behind.

The client never sends `my IP address is:`. The server drops the whole handshake if a
declared IP differs from the packet's source address, which happens on machines with
several network adapters.

## Tests

```powershell
.\3_Client_Process\Stone_Client.SelfTest\bin\x64\Debug\Stone_Client.SelfTest.exe
```

These test the wire contract against a copy of the server's `ProtoWriter`, plus JPEG and
BGR24 frame conversion.

`tools/fake_iot_server.py` stands in for the server, using only the Python standard library.
It sends the JPEG frames in `tools/fake_frames/`:

```powershell
python .\3_Client_Process\tools\fake_iot_server.py --sessions 2 --frames 10 --malformed
```

Use `--silence-after 20` to stop answering handshakes and watch the client report offline.
Use `--ts file.ts` to send an MPEG-TS file to a client that requests `h264` (the script's
help text shows an FFmpeg command that makes one).
The real server must not be running at the same time, since both use UDP 50001.
