from collections import deque
import sys
from pathlib import Path

import zmq

sys.path.append(str(Path(__file__).resolve().parent / ".." / "proto"))
import brokerDealer_pb2


class BrokerRouter:
    def __init__(self, endpoint, image_interval=10, queue_limit=2):
        self.endpoint = endpoint
        self.image_interval = image_interval
        self.image_queue = deque(maxlen=queue_limit)
        self.idle_workers = deque()
        self.idle_set = set()
        self.frame_count = 0

    def connect(self):
        self.context = zmq.Context.instance()
        self.socket = self.context.socket(zmq.ROUTER)
        self.socket.bind(self.endpoint)

    def enqueue_image(self, image_location):
        # deque(maxlen=...) drops the oldest queued frame, preserving low latency.
        self.frame_count += 1
        command_type = (
            brokerDealer_pb2.PROCESS_AND_SAVE
            if self.frame_count % self.image_interval == 0
            else brokerDealer_pb2.PROCESS
        )
        self.image_queue.append((image_location, command_type))

    def receive(self):
        frames = self.socket.recv_multipart()
        if len(frames) != 2:
            raise ValueError(f"Invalid ROUTER frame count: {len(frames)}")
        worker_id = frames[0].decode("utf-8")
        message = brokerDealer_pb2.WorkerMessage()
        message.ParseFromString(frames[1])
        self.mark_idle(worker_id)
        return worker_id, message

    def mark_idle(self, worker_id):
        if worker_id not in self.idle_set:
            self.idle_workers.append(worker_id)
            self.idle_set.add(worker_id)

    def distribute(self):
        while self.image_queue and self.idle_workers:
            worker_id = self.idle_workers.popleft()
            self.idle_set.remove(worker_id)
            image_location, command_type = self.image_queue.popleft()
            command = brokerDealer_pb2.WorkerCommand(
                command_type=command_type,
                image_location=image_location,
            )
            self.socket.send_multipart([worker_id.encode("utf-8"), command.SerializeToString()])
