import pandas as pd
import numpy as np
from sklearn.model_selection import train_test_split
from sklearn.preprocessing import StandardScaler
from sklearn.ensemble import RandomForestRegressor
from xgboost import XGBRegressor
import joblib

def prepare_data_train(csv_paths):
    all_data = []

    for csv_path in csv_paths:
        df_temp = pd.read_csv(csv_path)
        all_data.append(df_temp)

    data = pd.concat(all_data, ignore_index=True)

    if 'folder_name' in data.columns:
        data = data.drop(columns=['folder_name'])

    
    X = X = data.drop(columns=['actual_weight']).to_numpy()
    y = data['actual_weight'].to_numpy()
    
    
    return X, y

def manual_r2_score(y_true, y_pred):
    ss_res = np.sum((y_true - y_pred)**2, axis = 0)
    ss_tot = np.sum((y_true - np.mean(y_true, axis = 0))**2, axis = 0)
    return 1 - (ss_res/ss_tot)

def manual_mae(y_true, y_pred):
    absolute_errors = np.abs(y_true - y_pred)
    return np.mean(absolute_errors, axis = 0)

def manual_RMSE(y_true, y_pred):
    squared_errors = (y_true - y_pred)**2
    return np.sqrt(np.mean(squared_errors, axis = 0))

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

def fit_linear(X, y):
    X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42)
    model = ManualLinearRegression()
    model.fit(X_train, y_train)
    y_pred = model.predict(X_test)

    r2_scores = manual_r2_score(y_test, y_pred)
    mae_results = manual_mae(y_test, y_pred)
    rmse_result = manual_RMSE(y_test, y_pred)


    print("\n--- KẾT QUẢ ĐÁNH GIÁ MÔ HÌNH LINEAR REGRESSION---")
    print(f"Độ phù hợp R2 Score:   {r2_scores:.4f}")
    print(f"Sai số RMSE:           {rmse_result:.4f} kg")
    print(f"Độ lệch tuyệt đối MAE: {mae_results:.4f} kg")

    save_path = r'D:\CODE\LAB\Project_Stone\Weight\model\Linear_Regression\linear.pkl'
    joblib.dump(model, save_path)   

def fit_poly(X, y):
    X_poly = create_polynomial(X)
    X_train, X_test, y_train, y_test = train_test_split(X_poly, y, test_size=0.2, random_state=42)
    model = ManualLinearRegression()
    model.fit(X_train, y_train)
    y_pred = model.predict(X_test)

    r2_scores = manual_r2_score(y_test, y_pred)
    mae_results = manual_mae(y_test, y_pred)
    rmse_result = manual_RMSE(y_test, y_pred)


    print("\n--- KẾT QUẢ ĐÁNH GIÁ MÔ HÌNH POLYNOMINAL REGRESSION ---")
    print(f"Độ phù hợp R2 Score:   {r2_scores:.4f}")
    print(f"Sai số RMSE:           {rmse_result:.4f} kg")
    print(f"Độ lệch tuyệt đối MAE: {mae_results:.4f} kg")

    save_path = r'D:\CODE\LAB\Project_Stone\Weight\model\Polynominal_Regression\poly.pkl'
    joblib.dump(model, save_path)   

class ManualRidgeRegression:
    def __init__(self, alpha=1.0):
        self.alpha = alpha 
        self.theta = None
        self.mean = None
        self.std = None
        
    def fit(self, X, y):
        self.mean = np.mean(X, axis=0)
        self.std = np.std(X, axis=0)
        self.std[self.std == 0] = 1 
        X_scaled = (X - self.mean) / self.std
        
        m, n = X_scaled.shape
        X_b = np.c_[np.ones((m, 1)), X_scaled]
        X_T = X_b.T

        I = np.eye(n + 1)
        I[0, 0] = 0 
        
        self.theta = np.linalg.pinv(X_T.dot(X_b) + self.alpha * I).dot(X_T).dot(y)
        
    def predict(self, X):
        X_scaled = (X - self.mean) / self.std
        
        m = X.shape[0]
        X_b = np.c_[np.ones((m, 1)), X_scaled]
        return X_b.dot(self.theta)
    
def fit_random_forest(X, y):
    X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42)

    model = RandomForestRegressor(n_estimators=300, random_state=42)
    model.fit(X_train, y_train)

    y_pred = model.predict(X_test)

    r2_scores = manual_r2_score(y_test, y_pred)
    mae_results = manual_mae(y_test, y_pred)
    rmse_result = manual_RMSE(y_test, y_pred)


    print("\n--- KẾT QUẢ ĐÁNH GIÁ MÔ HÌNH RANDOM FOREST ---")
    print(f"Độ phù hợp R2 Score:   {r2_scores:.4f}")
    print(f"Sai số RMSE:           {rmse_result:.4f} kg")
    print(f"Độ lệch tuyệt đối MAE: {mae_results:.4f} kg")

    save_path = r'D:\CODE\LAB\Project_Stone\Weight\model\Random_Forest\rf.pkl'
    joblib.dump(model, save_path)

def fit_xgboost(X, y):
    X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42)

    model = XGBRegressor(
        n_estimators=500,       
        max_depth=5,            
        learning_rate=0.05,     
        random_state=42, 
        objective='reg:squarederror'
    )
    
    model.fit(X_train, y_train)
    
    y_pred = model.predict(X_test)

    r2_scores = manual_r2_score(y_test, y_pred)
    mae_results = manual_mae(y_test, y_pred)
    rmse_result = manual_RMSE(y_test, y_pred)


    print("\n--- KẾT QUẢ ĐÁNH GIÁ MÔ HÌNH XGBoost ---")
    print(f"Độ phù hợp R2 Score:   {r2_scores:.4f}")
    print(f"Sai số RMSE:           {rmse_result:.4f} kg")
    print(f"Độ lệch tuyệt đối MAE: {mae_results:.4f} kg")

    save_path = r'D:\CODE\LAB\Project_Stone\Weight\model\XGBoost\xgboost.pkl'
    joblib.dump(model, save_path)