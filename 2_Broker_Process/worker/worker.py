import mmap
import sys
from pathlib import Path

import numpy as np
import zmq

sys.path.append(str(Path(__file__).resolve().parents[2] / "1_AI_Process"))
sys.path.append(str(Path(__file__).resolve().parent / ".." / "proto"))

from module.YOLO.src.YOLOModel import YOLOImpl
import brokerDealer_pb2


class Worker:
    def __init__(self, input_map_name, output_map_name, map_size, width, height, router_endpoint, worker_id):
        self.worker_id = worker_id
        self.width = width
        self.height = height
        self.image_size = width * height * 3
        self.input_map = mmap.mmap(-1, map_size, tagname=input_map_name, access=mmap.ACCESS_READ)
        self.output_map = mmap.mmap(-1, map_size, tagname=output_map_name, access=mmap.ACCESS_WRITE)
        self.model = YOLOImpl()

        self.context = zmq.Context()
        self.socket = self.context.socket(zmq.DEALER)
        self.socket.setsockopt(zmq.IDENTITY, worker_id.encode("utf-8"))
        self.socket.connect(router_endpoint)

    def send(self, message):
        self.socket.send(message.SerializeToString())

    def read_image(self, offset):
        if offset < 0 or offset + self.image_size > self.input_map.size():
            raise ValueError(f"Input image offset is outside MMF: {offset}")
        self.input_map.seek(offset)
        raw = self.input_map.read(self.image_size)
        return np.frombuffer(raw, dtype=np.uint8).reshape((self.height, self.width, 3)).copy()

    def write_image(self, offset, image_bytes):
        if offset < 0 or offset + self.image_size > self.output_map.size():
            raise ValueError(f"Output image offset is outside MMF: {offset}")
        if len(image_bytes) != self.image_size:
            raise ValueError(f"Unexpected processed image size: {len(image_bytes)}")
        self.output_map.seek(offset)
        self.output_map.write(image_bytes)

    def process(self, command):
        image = self.read_image(command.image_location)
        inference = self.model.inference(image)
        draw = command.command_type == brokerDealer_pb2.PROCESS_AND_SAVE
        processed, counts, weights = self.model.processAndVisualizeWithMultipleWeight(
            inference, None, image, p_draw=draw
        )

        result_type = brokerDealer_pb2.RESULT_AND_SAVE if draw else brokerDealer_pb2.RESULT
        result = brokerDealer_pb2.WorkerMessage(
            worker_id=self.worker_id,
            message_type=result_type,
            image_location=command.image_location,
            rock_percentages=[float(counts.get(name, 0)) for name in ("MiSang", "1x2", "2x4", "4x6", "Khac")],
            weight=[float(value) for value in weights],
        )
        if draw:
            self.write_image(command.image_location, processed)
            result.image_save_location = command.image_location
        self.send(result)

    def run(self):
        self.send(brokerDealer_pb2.WorkerMessage(worker_id=self.worker_id, message_type=brokerDealer_pb2.READY))
        while True:
            command = brokerDealer_pb2.WorkerCommand()
            command.ParseFromString(self.socket.recv())
            try:
                if command.command_type not in (brokerDealer_pb2.PROCESS, brokerDealer_pb2.PROCESS_AND_SAVE):
                    raise ValueError(f"Unknown worker command: {command.command_type}")
                self.process(command)
            except Exception as exc:
                print(f"[ERROR] [{self.worker_id}] {exc}", flush=True)
                self.send(brokerDealer_pb2.WorkerMessage(worker_id=self.worker_id, message_type=brokerDealer_pb2.REQUEST))


def main():
    if len(sys.argv) != 8:
        raise SystemExit("usage: worker.py <input_mmf> <output_mmf> <map_size> <width> <height> <router_endpoint> <worker_id>")
    worker = Worker(sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4]), int(sys.argv[5]), sys.argv[6], sys.argv[7])
    print(f"[INFO] Worker {worker.worker_id} model ready", flush=True)
    worker.run()


if __name__ == "__main__":
    main()
