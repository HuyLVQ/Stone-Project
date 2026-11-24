# Stone IoT wire protocol

The application is the server. It listens for UDP discovery on port `50001` and
connects to each registered client's TCP listener at port `50002`.

## UDP handshake

Send UTF-8 text to the server's LAN broadcast address on UDP port `50001`:

```text
SEARCHING FOR SERVER - my current id is: {client-id}; my IP address is: {client-ip}; image encoding is: h264
```

Omit `image encoding is` or set it to `jpeg` to retain JPEG delivery. H.264 is
negotiated per client; if the server cannot start FFmpeg with `libx264`, it
confirms the client using JPEG instead. The confirmation includes the selected
encoding.

The server registers the datagram's observed source IP. If the message includes
an IP address, it must match that observed source. The server replies to the
datagram source with:

```text
SERVER CONFIRMED - client id: {client-id}; tcp port: 50002; image encoding: H264_MPEG_TS
```

A repeated handshake from an existing ID and IP refreshes the registration. A
repeated ID from a different source IP replaces its prior registration. A
registration expires after 30 seconds without a repeated handshake, so clients
should refresh their handshake lease periodically.

## TCP delivery

The server opens and keeps an outbound TCP connection to each registered client
at `{registered-ip}:50002`. Each record is a four-byte unsigned big-endian length,
then exactly that many bytes containing one serialized `IotEnvelope` protobuf.
The envelope carries an `ImageFrame`, an `H264TransportChunk`, or an
`InferenceResult`.

Image payloads are JPEG encodings of the annotated `IImage.recvImage` frame at
quality 85 and 1920 x 1200 resolution. There are no separate bounding-box fields.
`produced_at_unix_ms` is the server packaging time because the current image
event has no camera capture timestamp. The server publishes at most 5 images
per second. Image frames are coalesced for slow or temporarily unavailable
clients; the latest pending image replaces an older queued image. Final-result
records are retained and retried while the client is unavailable.

JPEG clients continue to use schema version 2, so the H.264 addition doesn't
change their existing message version. Version 1 clients expecting raw BGR bytes
are incompatible with JPEG version 2.

Clients requesting H.264 receive MPEG-TS chunks containing one continuous H.264
stream. Concatenate `H264TransportChunk.data` values in order and feed the bytes
to an MPEG-TS/H.264 decoder. The bounded per-client queue may skip stale chunks
for a slow receiver; clients should use sequence numbers to detect gaps and
resume decoding at a later repeated keyframe. The encoder uses 1920 x 1200 BGR input at 5 FPS,
libx264 `ultrafast`/`zerolatency`, a 2 Mbps target/max rate, and a keyframe about
every two seconds. H.264 clients use schema version 3, which adds the
video-chunk envelope payload; JPEG clients continue to use version 2.

H.264 requires an FFmpeg executable that includes `libx264`. Set
`STONE_FFMPEG_PATH` to its full path, place `ffmpeg.exe` next to the application,
or make it available on `PATH`. Existing clients that omit the encoding request
continue to receive JPEG. The server doesn't install or bundle FFmpeg.

## Bandwidth estimate

The workspace has no saved annotated AI output frame, so an estimate was measured
with the same Windows GDI+ JPEG encoder and quality setting on the eight available
1920 x 1200 `Weight_v2/data_csv/5.2kg/frame_*.jpg` samples. Re-encoded JPEG sizes
were 247,398 to 275,781 bytes, averaging 257,908 bytes. At the 5 FPS cap this is
about 10.32 Mbps per client or 103.16 Mbps for ten clients, before TCP/IP overhead.
The actual AI-annotated frames may produce different sizes; measure those frames
for a deployment-specific figure.

For an H.264-requesting client, the configured target is about 2 Mbps per client;
ten clients therefore use about 20 Mbps of video payload in total, plus MPEG-TS
and TCP/IP overhead. This is an encoder target, not a measured result on the
current machine.

Percentages are expressed from `0.00` to `100.00` and rounded to two decimal
places. Rock IDs are `MiSang`, `1x2`, `2x4`, and `4x6`. The weight is currently
the finalized session-average prediction from model 1, in kilograms, matching
the value displayed by the existing application.

The protobuf contracts are in `iot_image.proto` and `iot_result.proto`. The
server currently uses a small internal protobuf wire encoder matching those
schemas, so client implementations can use generated protobuf code normally.
