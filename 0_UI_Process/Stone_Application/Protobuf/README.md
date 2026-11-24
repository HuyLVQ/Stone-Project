# Stone IoT wire protocol, version 1

The application is the server. It listens for UDP discovery on port `50001` and
connects to each registered client's TCP listener at port `50002`.

## UDP handshake

Send UTF-8 text to the server's LAN broadcast address on UDP port `50001`:

```text
SEARCHING FOR SERVER - my current id is: {client-id}; my IP address is: {client-ip}
```

The server registers the datagram's observed source IP. If the message includes
an IP address, it must match that observed source. The server replies to the
datagram source with:

```text
SERVER CONFIRMED - client id: {client-id}; tcp port: 50002
```

A repeated handshake from an existing ID and IP refreshes the registration. A
repeated ID from a different source IP replaces its prior registration. A
registration expires after 30 seconds without a repeated handshake, so clients
should refresh their handshake lease periodically.

## TCP delivery

The server opens and keeps an outbound TCP connection to each registered client
at `{registered-ip}:50002`. Each record is a four-byte unsigned big-endian length,
then exactly that many bytes containing one serialized `IotEnvelope` protobuf.
The envelope carries either an `ImageFrame` or an `InferenceResult`.

Image payloads are the original annotated `IImage.recvImage` bytes: 1920 x 1200
packed BGR8, three bytes per pixel, without row padding. There are no separate
bounding-box fields. `produced_at_unix_ms` is the server packaging time because
the current image event has no camera capture timestamp. Image frames are coalesced for slow or temporarily
unavailable clients; the latest pending image replaces an older queued image.
Final-result records are retained and retried while the client is unavailable.

Percentages are expressed from `0.00` to `100.00` and rounded to two decimal
places. Rock IDs are `MiSang`, `1x2`, `2x4`, and `4x6`. The weight is currently
the finalized session-average prediction from model 1, in kilograms, matching
the value displayed by the existing application.

The protobuf contracts are in `iot_image.proto` and `iot_result.proto`. The
server currently uses a small internal protobuf wire encoder matching those
schemas, so client implementations can use generated protobuf code normally.
