from pathlib import Path
import utils.utils as utils
from utils.utils import prepare_data, set_video_path, video_to_frame, create_csv
from utils.utils import predict_linear, predict_poly, predict_random_forest, predict_xgboost

ROOT_DIR = Path(__file__).resolve().parent

TRAIN_DIR = ROOT_DIR / "train"

video_path = ROOT_DIR / "video" / "10.4.avi"
video = set_video_path(video_path)
frame = video_to_frame()
csv_video_path = utils.create_csv()

X_test, y_test = utils.prepare_data(csv_video_path)


# print(f'\nKhối lượng gốc: {y_test[0]:.2f} kg')
# print('Khối lượng dự đoán từng model')
# print(f'Linear: {utils.predict_linear(X_test):.2f} kg')
# print(f'Poly: {utils.predict_poly(X_test):.2f} kg')
# print(f'Random forest: {utils.predict_random_forest(X_test):.2f} kg')
# print(f'XGBoost: {utils.predict_xgboost(X_test):.2f} kg')



