"""Stand-in for the Stone_Application IoT server, for testing Stone_Client
without the camera, AI process or database.

It mirrors 0_UI_Process/Stone_Application/Networking/Iot/*.cs:
  * answers "SEARCHING FOR SERVER - my current id is:..." on UDP 50001 with
    "SERVER CONFIRMED - client id: ...; tcp port: 50002; image encoding: JPEG"
    (or H264_MPEG_TS when the client asks for h264 and --ts is given);
  * dials the client on TCP 50002 and sends length-prefixed IotEnvelope records
    encoded exactly like the C# ProtoWriter (zero/empty fields omitted);
  * per session: image frames at ~5 fps, close the connection (STOP), then a
    new connection carrying the final result, which stays open for the next
    session, as IotClientConnection does.

Only the Python standard library is used. JPEG frames cycle through the
images in fake_frames/. With --ts, a client that requests h264 instead gets
the given MPEG-TS file as video chunks. One can be made with, for example:
  ffmpeg -f lavfi -i testsrc2=size=1920x1200:rate=5 -t 10 -c:v libx264 -g 10
         -pix_fmt yuv420p -f mpegts test.ts

Examples:
  python fake_iot_server.py
  python fake_iot_server.py --sessions 3 --frames 10
  python fake_iot_server.py --malformed          # also send bad records
  python fake_iot_server.py --silence-after 20   # stop answering -> client reports offline
  python fake_iot_server.py --ts test.ts         # H.264 for clients that request it
"""

import argparse
import pathlib
import socket
import struct
import threading
import time

HANDSHAKE_PORT = 50001
CLIENT_TCP_PORT = 50002
HANDSHAKE_PREFIX = "SEARCHING FOR SERVER - my current id is:"
JPEG_SCHEMA_VERSION = 2
H264_SCHEMA_VERSION = 3
FRAMES_DIR = pathlib.Path(__file__).resolve().parent / "fake_frames"
TS_PACKET = 188
TS_CHUNK = TS_PACKET * 174              # same chunk size as the server's FFmpeg pump
TS_BYTES_PER_SECOND = 250_000           # the server encodes at 2 Mbit/s


def jpeg_size(data: bytes):
    """(width, height) from the first SOF marker of a baseline or progressive JPEG."""
    index = 2
    while index + 9 < len(data):
        if data[index] != 0xFF:
            index += 1
            continue
        marker = data[index + 1]
        length = struct.unpack(">H", data[index + 2:index + 4])[0]
        if marker in (0xC0, 0xC1, 0xC2):
            height, width = struct.unpack(">HH", data[index + 5:index + 9])
            return width, height
        index += 2 + length
    raise ValueError("no SOF marker")


def load_frames():
    frames = []
    for path in sorted(FRAMES_DIR.glob("*.jpg")):
        data = path.read_bytes()
        frames.append((data,) + jpeg_size(data))
    if not frames:
        raise SystemExit(f"No JPEG frames found in {FRAMES_DIR}")
    return frames


# --------------------------------------------------------------------------- protobuf writer

def _varint(value: int) -> bytes:
    out = bytearray()
    value &= (1 << 64) - 1
    while value >= 0x80:
        out.append((value & 0x7F) | 0x80)
        value >>= 7
    out.append(value)
    return bytes(out)


def _tag(field: int, wire_type: int) -> bytes:
    return _varint((field << 3) | wire_type)


class ProtoWriter:
    """Same rules as the server's ProtoWriter: zero numbers and empty strings are omitted."""

    def __init__(self):
        self._buf = bytearray()

    def uint(self, field, value):
        if value:
            self._buf += _tag(field, 0) + _varint(value)
        return self

    def string(self, field, value):
        if value:
            self.bytes(field, value.encode("utf-8"))
        return self

    def bytes(self, field, value):
        self._buf += _tag(field, 2) + _varint(len(value)) + value
        return self

    def double(self, field, value):
        if value != 0.0:
            self._buf += _tag(field, 1) + struct.pack("<d", value)
        return self

    def to_bytes(self):
        return bytes(self._buf)


def now_ms() -> int:
    return int(time.time() * 1000)


def image_envelope(session: str, sequence: int, jpeg: bytes, width: int, height: int) -> bytes:
    frame = (ProtoWriter().uint(1, JPEG_SCHEMA_VERSION).string(2, session).uint(3, sequence)
             .uint(4, now_ms()).uint(5, width).uint(6, height).string(7, "JPEG")
             .bytes(8, jpeg).to_bytes())
    return ProtoWriter().uint(1, JPEG_SCHEMA_VERSION).bytes(10, frame).to_bytes()


def video_chunk_envelope(session: str, sequence: int, data: bytes) -> bytes:
    chunk = (ProtoWriter().uint(1, H264_SCHEMA_VERSION).string(2, session).uint(3, sequence)
             .uint(4, now_ms()).string(5, "MPEG_TS").string(6, "H264").bytes(7, data).to_bytes())
    return ProtoWriter().uint(1, H264_SCHEMA_VERSION).bytes(12, chunk).to_bytes()


def result_envelope(session: str, sequence: int, counts, weight: float, schema: int) -> bytes:
    total = sum(counts)
    result = (ProtoWriter().uint(1, schema).string(2, session)
              .uint(3, sequence).uint(4, now_ms()))
    for rock_id, count in zip(("MiSang", "1x2", "2x4", "4x6"), counts):
        percentage = round(count * 100.0 / total, 2) if total > 0 else 0.0
        result.bytes(5, ProtoWriter().string(1, rock_id).double(2, percentage).to_bytes())
    result.double(6, weight).string(7, "kg")
    return ProtoWriter().uint(1, schema).bytes(11, result.to_bytes()).to_bytes()


def frame(envelope: bytes) -> bytes:
    return struct.pack(">I", len(envelope)) + envelope


# --------------------------------------------------------------------------- handshake

class Registry:
    def __init__(self):
        self.lock = threading.Lock()
        self.clients = {}            # id -> (address, uses_h264)
        self.registered = threading.Event()


def parse_handshake(message: str):
    """Returns (client_id, wants_h264) or None."""
    if not message.upper().startswith(HANDSHAKE_PREFIX.upper()):
        return None
    parts = [p for p in message[len(HANDSHAKE_PREFIX):].strip().split(";") if p]
    if not parts:
        return None
    client_id = parts[0].strip()
    if not client_id or len(client_id) > 128:
        return None
    wants_h264 = any(p.strip().lower() == "image encoding is:h264" for p in parts[1:])
    return client_id, wants_h264


def handshake_loop(registry: Registry, silence_after: float, offer_h264: bool, stop: threading.Event):
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind(("0.0.0.0", HANDSHAKE_PORT))
    sock.settimeout(0.5)
    started = time.monotonic()
    print(f"[fake-server] UDP handshake listener on port {HANDSHAKE_PORT}")
    while not stop.is_set():
        try:
            data, remote = sock.recvfrom(4096)
        except socket.timeout:
            continue
        parsed = parse_handshake(data.decode("utf-8", "replace").strip("\0\r\n "))
        if parsed is None:
            continue
        client_id, wants_h264 = parsed
        if silence_after and time.monotonic() - started > silence_after:
            continue
        entry = (remote[0], wants_h264 and offer_h264)
        with registry.lock:
            known = registry.clients.get(client_id)
            registry.clients[client_id] = entry
        if known != entry:
            print(f"[fake-server] registered {client_id} at {remote[0]} ({'H.264' if entry[1] else 'JPEG'})")
        encoding = "H264_MPEG_TS" if entry[1] else "JPEG"
        reply = (f"SERVER CONFIRMED - client id: {client_id}; tcp port: {CLIENT_TCP_PORT}; "
                 f"image encoding: {encoding}").encode("utf-8")
        sock.sendto(reply, remote)
        registry.registered.set()
    sock.close()


# --------------------------------------------------------------------------- stream

def connect(address: str) -> socket.socket:
    conn = socket.create_connection((address, CLIENT_TCP_PORT), timeout=2)
    conn.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
    return conn


def run_sessions(registry: Registry, sessions: int, frames: int, malformed: bool, ts_data: bytes):
    registry.registered.wait()
    time.sleep(1.0)
    with registry.lock:
        client_id, (address, uses_h264) = next(iter(registry.clients.items()))
    print(f"[fake-server] streaming {'H.264' if uses_h264 else 'JPEG'} to {client_id} at {address}:{CLIENT_TCP_PORT}")
    jpeg_frames = load_frames()

    conn = None
    sequence = 0
    for index in range(sessions):
        session = str(time.time_ns() // 100)     # looks like the server's tick-based token
        if conn is None:
            conn = connect(address)
        if uses_h264:
            print(f"[fake-server] session {session}: START, {len(ts_data)} bytes of MPEG-TS")
            for offset in range(0, len(ts_data), TS_CHUNK):
                sequence += 1
                data = ts_data[offset:offset + TS_CHUNK]
                conn.sendall(frame(video_chunk_envelope(session, sequence, data)))
                time.sleep(len(data) / TS_BYTES_PER_SECOND)
        else:
            print(f"[fake-server] session {session}: START, {frames} frames")
            for number in range(frames):
                sequence += 1
                jpeg, width, height = jpeg_frames[number % len(jpeg_frames)]
                conn.sendall(frame(image_envelope(session, sequence, jpeg, width, height)))
                time.sleep(0.2)

        # STOP: the server closes the image connection, then delivers the result on a new one.
        conn.close()
        time.sleep(1.0)
        sequence += 1
        counts = (index, 3 + index, 5, 0)        # 4x6 = 0 exercises the omitted-zero path
        conn = connect(address)
        schema = H264_SCHEMA_VERSION if uses_h264 else JPEG_SCHEMA_VERSION
        conn.sendall(frame(result_envelope(session, sequence, counts, 2.5 + index, schema)))
        print(f"[fake-server] session {session}: result sent")
        time.sleep(2.0)

    if malformed:
        print("[fake-server] sending a malformed record, then an oversized length prefix")
        conn.sendall(frame(b"\x5a\xff"))                       # field 11 with a truncated length
        conn.sendall(frame(ProtoWriter().uint(1, 2).to_bytes()))  # envelope without payload
        time.sleep(0.5)
        conn.sendall(struct.pack(">I", 64 * 1024 * 1024))       # exceeds the 16 MB limit
        time.sleep(0.5)

    if conn is not None:
        conn.close()
    print("[fake-server] scenario finished; still answering handshakes (Ctrl+C to quit)")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--sessions", type=int, default=2)
    parser.add_argument("--frames", type=int, default=10)
    parser.add_argument("--malformed", action="store_true")
    parser.add_argument("--silence-after", type=float, default=0.0,
                        help="stop confirming handshakes after this many seconds (0 = never)")
    parser.add_argument("--ts", type=pathlib.Path,
                        help="MPEG-TS file (H.264) to stream to clients that request h264")
    args = parser.parse_args()

    ts_data = b""
    if args.ts:
        ts_data = args.ts.read_bytes()
        ts_data = ts_data[:len(ts_data) - len(ts_data) % TS_PACKET]
        if not ts_data or ts_data[0] != 0x47:
            raise SystemExit(f"{args.ts} does not look like an MPEG-TS file")

    registry = Registry()
    stop = threading.Event()
    listener = threading.Thread(target=handshake_loop, args=(registry, args.silence_after, bool(ts_data), stop),
                                daemon=True)
    listener.start()
    try:
        run_sessions(registry, args.sessions, args.frames, args.malformed, ts_data)
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        pass
    finally:
        stop.set()
        listener.join(timeout=2)


if __name__ == "__main__":
    main()
