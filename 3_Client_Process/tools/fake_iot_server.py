"""Stand-in for the Stone_Application IoT server, for testing Stone_Client
without the camera, AI process or database.

It mirrors 0_UI_Process/Stone_Application/Networking/Iot/*.cs:
  * answers "SEARCHING FOR SERVER - my current id is:..." on UDP 50001 with
    "SERVER CONFIRMED - client id: ...; tcp port: 50002; image encoding: JPEG";
  * dials the client on TCP 50002 and sends length-prefixed IotEnvelope records
    encoded exactly like the C# ProtoWriter (zero/empty fields omitted);
  * per session: image frames at ~5 fps, close the connection (STOP), then a
    new connection carrying the final result, which stays open for the next
    session, as IotClientConnection does.

Only the Python standard library is used. The JPEG payload is a placeholder;
the client only logs frames until image display is implemented.

Examples:
  python fake_iot_server.py
  python fake_iot_server.py --sessions 3 --frames 10
  python fake_iot_server.py --malformed          # also send bad records
  python fake_iot_server.py --silence-after 20   # stop answering -> client reports offline
"""

import argparse
import socket
import struct
import threading
import time

HANDSHAKE_PORT = 50001
CLIENT_TCP_PORT = 50002
HANDSHAKE_PREFIX = "SEARCHING FOR SERVER - my current id is:"
JPEG_SCHEMA_VERSION = 2
PLACEHOLDER_JPEG = b"\xff\xd8\xff\xe0" + b"\x00" * 64 + b"\xff\xd9"


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


def image_envelope(session: str, sequence: int) -> bytes:
    frame = (ProtoWriter().uint(1, JPEG_SCHEMA_VERSION).string(2, session).uint(3, sequence)
             .uint(4, now_ms()).uint(5, 1920).uint(6, 1200).string(7, "JPEG")
             .bytes(8, PLACEHOLDER_JPEG).to_bytes())
    return ProtoWriter().uint(1, JPEG_SCHEMA_VERSION).bytes(10, frame).to_bytes()


def result_envelope(session: str, sequence: int, counts, weight: float) -> bytes:
    total = sum(counts)
    result = (ProtoWriter().uint(1, JPEG_SCHEMA_VERSION).string(2, session)
              .uint(3, sequence).uint(4, now_ms()))
    for rock_id, count in zip(("MiSang", "1x2", "2x4", "4x6"), counts):
        percentage = round(count * 100.0 / total, 2) if total > 0 else 0.0
        result.bytes(5, ProtoWriter().string(1, rock_id).double(2, percentage).to_bytes())
    result.double(6, weight).string(7, "kg")
    return ProtoWriter().uint(1, JPEG_SCHEMA_VERSION).bytes(11, result.to_bytes()).to_bytes()


def frame(envelope: bytes) -> bytes:
    return struct.pack(">I", len(envelope)) + envelope


# --------------------------------------------------------------------------- handshake

class Registry:
    def __init__(self):
        self.lock = threading.Lock()
        self.clients = {}            # id -> address
        self.registered = threading.Event()


def parse_handshake(message: str):
    if not message.upper().startswith(HANDSHAKE_PREFIX.upper()):
        return None
    parts = [p for p in message[len(HANDSHAKE_PREFIX):].strip().split(";") if p]
    if not parts:
        return None
    client_id = parts[0].strip()
    if not client_id or len(client_id) > 128:
        return None
    return client_id


def handshake_loop(registry: Registry, silence_after: float, stop: threading.Event):
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
        client_id = parse_handshake(data.decode("utf-8", "replace").strip("\0\r\n "))
        if client_id is None:
            continue
        if silence_after and time.monotonic() - started > silence_after:
            continue
        with registry.lock:
            known = registry.clients.get(client_id)
            registry.clients[client_id] = remote[0]
        if known != remote[0]:
            print(f"[fake-server] registered {client_id} at {remote[0]}")
        reply = (f"SERVER CONFIRMED - client id: {client_id}; tcp port: {CLIENT_TCP_PORT}; "
                 f"image encoding: JPEG").encode("utf-8")
        sock.sendto(reply, remote)
        registry.registered.set()
    sock.close()


# --------------------------------------------------------------------------- stream

def connect(address: str) -> socket.socket:
    conn = socket.create_connection((address, CLIENT_TCP_PORT), timeout=2)
    conn.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
    return conn


def run_sessions(registry: Registry, sessions: int, frames: int, malformed: bool):
    registry.registered.wait()
    time.sleep(1.0)
    with registry.lock:
        client_id, address = next(iter(registry.clients.items()))
    print(f"[fake-server] streaming to {client_id} at {address}:{CLIENT_TCP_PORT}")

    conn = None
    sequence = 0
    for index in range(sessions):
        session = str(time.time_ns() // 100)     # looks like the server's tick-based token
        if conn is None:
            conn = connect(address)
        print(f"[fake-server] session {session}: START, {frames} frames")
        for _ in range(frames):
            sequence += 1
            conn.sendall(frame(image_envelope(session, sequence)))
            time.sleep(0.2)

        # STOP: the server closes the image connection, then delivers the result on a new one.
        conn.close()
        time.sleep(1.0)
        sequence += 1
        counts = (index, 3 + index, 5, 0)        # 4x6 = 0 exercises the omitted-zero path
        conn = connect(address)
        conn.sendall(frame(result_envelope(session, sequence, counts, 2.5 + index)))
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
    args = parser.parse_args()

    registry = Registry()
    stop = threading.Event()
    listener = threading.Thread(target=handshake_loop, args=(registry, args.silence_after, stop), daemon=True)
    listener.start()
    try:
        run_sessions(registry, args.sessions, args.frames, args.malformed)
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        pass
    finally:
        stop.set()
        listener.join(timeout=2)


if __name__ == "__main__":
    main()
