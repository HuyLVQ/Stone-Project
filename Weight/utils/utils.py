import pandas as pd
import numpy as np
from sklearn.ensemble import RandomForestRegressor
from xgboost import XGBRegressor
import joblib
import cv2
import os
from ultralytics import YOLO
import csv

from pathlib import Path
ROOT_DIR = Path(__file__).resolve().parent.parent

MODEL_DIR = ROOT_DIR / "model"
DATA_DIR = ROOT_DIR / "data"
TRAIN_DIR = ROOT_DIR / "train"
VIDEO_DIR = ROOT_DIR / "video"
#======================== Globals ======================
video_cut_time = 3.13
pixel2cm = 18.82
#=======================================================

def prepare_data(csv_path):
    data = pd.read_csv(csv_path)
    if 'folder_name' in data.columns:
        data = data.drop(columns=['folder_name'])
    
    X = X = data.drop(columns=['actual_weight']).to_numpy()
    y = data['actual_weight'].to_numpy()
    
    return X, y

def create_polynomial(X):
    n_samples, n_features = X.shape
    X_poly = X.copy()
    # print(f'Số mẫu: {n_samples}\nSố biến: {n_features}')
    #mi_sang
    X_poly = np.c_[X_poly, X[:, 0] * X[:, 4]]
    X_poly = np.c_[X_poly, X[:, 0] * X[:, 8]]
    X_poly = np.c_[X_poly, X[:, 4] * X[:, 8]]
    X_poly = np.c_[X_poly, X[:, 4] ** 2]
    # X_poly = np.c_[X_poly, X[:, 0] * X[:, 4] ** 2]
    #1x2
    X_poly = np.c_[X_poly, X[:, 1] * X[:, 5]]
    X_poly = np.c_[X_poly, X[:, 1] * X[:, 9]]
    X_poly = np.c_[X_poly, X[:, 5] * X[:, 9]]
    X_poly = np.c_[X_poly, X[:, 5] ** 2]
    # X_poly = np.c_[X_poly, X[:, 1] * X[:, 5] ** 2]
    #2x4
    X_poly = np.c_[X_poly, X[:, 2] * X[:, 6]]
    X_poly = np.c_[X_poly, X[:, 2] * X[:, 10]]
    X_poly = np.c_[X_poly, X[:, 6] * X[:, 10]]
    X_poly = np.c_[X_poly, X[:, 6] ** 2]
    # X_poly = np.c_[X_poly, X[:, 2] * X[:, 6] ** 2]
    #4x6
    X_poly = np.c_[X_poly, X[:, 3] * X[:, 7]]
    X_poly = np.c_[X_poly, X[:, 3] * X[:, 11]]
    X_poly = np.c_[X_poly, X[:, 7] * X[:, 11]]
    X_poly = np.c_[X_poly, X[:, 7] ** 2]
    # X_poly = np.c_[X_poly, X[:, 3] * X[:, 7] ** 2]

    return X_poly

class ManualLinearRegression:
    def __init__(self):
        self.theta = None
    def fit(self, X, y):
        m = X.shape[0]
        X_b = np.c_[np.ones((m, 1)), X]
        X_T = X_b.T
        self.theta = np.linalg.pinv(X_T.dot(X_b)).dot(X_T).dot(y)
    def predict(self, X):
        m = X.shape[0]
        X_b = np.c_[np.ones((m, 1)), X]
        return X_b.dot(self.theta)

def predict_linear(X_new):
    model_file = MODEL_DIR / "Linear_Regression" / "linear.pkl"
    loaded_model = joblib.load(model_file)

    predictions = loaded_model.predict(X_new)
    return predictions[0]

def predict_poly(X_new):
    model_file = MODEL_DIR / "Polynominal_Regression" / "poly.pkl"
    loaded_model = joblib.load(model_file)

    X_poly = create_polynomial(X_new)
    predictions = loaded_model.predict(X_poly)

    return predictions[0]

def predict_random_forest(X_new):
    model_file = MODEL_DIR / "Random_Forest" / "rf.pkl"
    loaded_model = joblib.load(model_file)

    predictions = loaded_model.predict(X_new)
    return predictions[0]

def predict_xgboost(X_new):
    model_file = MODEL_DIR / "XGBoost" / "xgboost.pkl"
    loaded_model = joblib.load(model_file)

    predictions = loaded_model.predict(X_new)
    return predictions[0]

#======================== Path prepare ==========================

video_path = ''
base_name = ''
folder_name = ''
actual_weight = 0.0
data_folder = ''
model_path = MODEL_DIR / "YOLO" / "best260620.pt"
csv_path = ''

def set_video_path(new_path):
    global video_path, base_name, folder_name
    global actual_weight, data_folder, csv_path

    video_path = new_path

    base_name = os.path.basename(video_path)

    folder_name = os.path.splitext(base_name)[0].replace(',', '.')

    actual_weight = float(folder_name.replace('kg', ''))

    data_folder = DATA_DIR / folder_name

    csv_path = data_folder / f"Data_Weights_{folder_name}.csv"

def video_to_frame():
    if not os.path.exists(data_folder):
        os.makedirs(data_folder)

    cap = cv2.VideoCapture(video_path)

    if not cap.isOpened():
        print("Lỗi: Không thể mở video. Hãy kiểm tra lại đường dẫn file.")
    else:
        fps = round(cap.get(cv2.CAP_PROP_FPS))
        print(f"Video gốc có tốc độ: {fps} FPS")

        cut_time = video_cut_time
        interval_frames = fps * cut_time

        frame_count = 0
        saved_count = 0
        next_target_frame = 0
        print("Đang tiến hành cắt frame...")
        while True:
            ret, frame = cap.read()
            
            if not ret:
                break

            if frame_count == round(next_target_frame):
                filename = f"frame_{saved_count:04d}.jpg"
                output_path = os.path.join(data_folder, filename)
                
                cv2.imwrite(output_path, frame)
                saved_count += 1
                
                next_target_frame = saved_count * interval_frames
            frame_count += 1
    cap.release()

def classify_rock(length_cm, width_cm):

    L_mm = length_cm * 10
    W_mm = width_cm * 10

    if W_mm < 10 or L_mm < 10:
        return 'mi_sang'

    elif (L_mm < 20 or W_mm < 20) and \
         L_mm >= 10 and W_mm >= 10:
        return '1x2'

    elif (L_mm < 40 or W_mm < 40) and \
         L_mm >= 20 and W_mm >= 20:
        return '2x4'

    elif L_mm >= 40 and W_mm >= 40:
        return '4x6'

    return None

def create_csv():
    rock_types = ['mi_sang', '1x2', '2x4', '4x6']
    stone_counts = {}
    area_cm2 = {}
    perimeter_cm = {}

    for rock in rock_types:
        stone_counts[rock] = 0
        area_cm2[rock] = 0
        perimeter_cm[rock] = 0

    pixel_to_cm = pixel2cm

    model = YOLO(model_path)

    for filename in os.listdir(data_folder):
        if filename.endswith(".jpg"):
                img_path = os.path.join(data_folder, filename)
                results = model(img_path, verbose=False)

                for r in results:
                    if r.masks is not None:
                        masks_xy = r.masks.xy

                        for contour in masks_xy:
                            contour = np.array(contour, dtype=np.float32)

                            if len(contour) >= 3:
                                rect = cv2.minAreaRect(contour)
                                center, dimensions, angle = rect
                                width, height = dimensions

                                length_cm = max(width, height) / pixel_to_cm
                                width_cm = min(width, height) / pixel_to_cm
                                rock_type = classify_rock(length_cm, width_cm)
                                stone_counts[rock_type] += 1
                                area_cm2[rock_type] += cv2.contourArea(contour) / (pixel_to_cm ** 2)
                                perimeter_cm[rock_type] += cv2.arcLength(contour, True) / pixel_to_cm

    header = ['folder_name', 'actual_weight', 'count_mi_sang', 'count_1x2', 'count_2x4', 'count_4x6', 'area_cm2_mi_sang', 'area_cm2_1x2', 'area_cm2_2x4', 'area_cm2_4x6', 'perimeter_cm_mi_sang', 'perimeter_cm_1x2', 'perimeter_cm_2x4', 'perimeter_cm_4x6']

    with open(csv_path, mode='w', newline='', encoding='utf-8') as file:
        writer = csv.writer(file)
        writer.writerow(header)
        row_data = [folder_name, actual_weight]
        for rock in rock_types:
            row_data.append(stone_counts[rock])
        for rock in rock_types:
            row_data.append(round(area_cm2[rock], 2))
        for rock in rock_types:
            row_data.append(round(perimeter_cm[rock], 2))
        writer.writerow(row_data)

    return csv_path