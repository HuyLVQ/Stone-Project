import argparse
import subprocess
import sys
from pathlib import Path

import zmq

sys.path.append(str(Path(__file__).resolve().parent / "proto"))
from router.brokerRouter import BrokerRouter
from rx.rawDataRetriever import RxPathPull
from tx.processedDataTransfer import TxPathPush
import brokerDealer_pb2
import brokerRx_pb2
import brokerTx_pb2


class Broker:
    def __init__(self, args):
        self.args = args
        self.router = BrokerRouter(args.router, args.image_interval, args.queue_limit)
        self.router.connect()
        self.rx = RxPathPull(args.rx)
        self.rx.connect()
        self.tx = TxPathPush(args.tx)
        self.tx.connect()
        self.workers = []
        self.start_workers()

    def start_workers(self):
        worker_script = str(Path(__file__).resolve().parent / "worker" / "worker.py")
        for index in range(self.args.worker_count):
            worker_id = f"worker-{index}"
            process = subprocess.Popen([
                self.args.python,
                worker_script,
                self.args.input_mmf,
                self.args.output_mmf,
                str(self.args.map_size),
                str(self.args.width),
                str(self.args.height),
                self.args.router,
                worker_id,
            ])
            self.workers.append(process)

    def run(self):
        poller = zmq.Poller()
        poller.register(self.rx.socket, zmq.POLLIN)
        poller.register(self.router.socket, zmq.POLLIN)
        try:
            while True:
                for socket, _ in poller.poll(100):
                    if socket is self.rx.socket:
                        message = self.rx.receive()
                        if message.message_type != brokerRx_pb2.RAWIMAGE:
                            continue
                        self.router.enqueue_image(self.rx.image_location(message))
                    elif socket is self.router.socket:
                        _, message = self.router.receive()
                        if message.message_type in (brokerDealer_pb2.RESULT, brokerDealer_pb2.RESULT_AND_SAVE):
                            self.tx.send(brokerTx_pb2.TxPushMessage(
                                message_type=message.message_type,
                                image_location=message.image_location,
                                rock_percentages=message.rock_percentages,
                                weight=message.weight,
                                image_save_location=message.image_save_location,
                            ))
                self.router.distribute()
        finally:
            for process in self.workers:
                if process.poll() is None:
                    process.terminate()
            for process in self.workers:
                process.wait(timeout=5)


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input-mmf", required=True)
    parser.add_argument("--output-mmf", required=True)
    parser.add_argument("--map-size", type=int, required=True)
    parser.add_argument("--width", type=int, required=True)
    parser.add_argument("--height", type=int, required=True)
    parser.add_argument("--rx", required=True, help="UI PUSH endpoint, broker PULL connects")
    parser.add_argument("--tx", required=True, help="UI PULL endpoint, broker PUSH binds")
    parser.add_argument("--router", required=True, help="Broker ROUTER endpoint")
    parser.add_argument("--worker-count", type=int, default=3)
    parser.add_argument("--image-interval", type=int, default=10)
    parser.add_argument("--queue-limit", type=int, default=2)
    parser.add_argument("--python", default=sys.executable)
    return parser.parse_args()


if __name__ == "__main__":
    Broker(parse_args()).run()
