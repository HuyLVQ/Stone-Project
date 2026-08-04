import joblib
from pathlib import Path
import numpy as np
import pandas as pd

_CACHED_XGB_POLY_MODEL = None

def create_polynomial(X): 
    density_factors = [0.2, 0.6, 1.3, 1.9]
        
    n_samples, n_features = X.shape
    X_poly = X.copy()
    
    total_vol_proxy = np.zeros(n_samples)
    total_area = np.zeros(n_samples)
    total_count = np.zeros(n_samples)
    
    # Biến tạm để lưu trữ riêng thông số của đá 4x6
    vol_proxy_4x6 = np.zeros(n_samples)
    avg_area_4x6 = np.zeros(n_samples)
    
    for i in range(4):
        count_idx = i
        area_idx = i + 4
        perim_idx = i + 8
        
        avg_area = X[:, area_idx] / (X[:, count_idx] + 1e-6) 
        compactness = X[:, area_idx] / ((X[:, perim_idx] ** 2) + 1e-6) 
        
        vol_proxy = X[:, count_idx] * (avg_area ** 1.35) * density_factors[i] 
        
        X_poly = np.c_[X_poly, avg_area, compactness, vol_proxy]
        
        total_vol_proxy += vol_proxy
        total_area += X[:, area_idx]
        total_count += X[:, count_idx]
        
        if i == 3:
            vol_proxy_4x6 = vol_proxy
            avg_area_4x6 = avg_area

    X_poly = np.c_[X_poly, total_vol_proxy] 
    
    global_avg_area = total_area / (total_count + 1e-6)
    X_poly = np.c_[X_poly, global_avg_area] 
    
    ratio_4x6 = vol_proxy_4x6 / (total_vol_proxy + 1e-6)
    X_poly = np.c_[X_poly, ratio_4x6]      
    
    occlusion_penalty = total_area / (total_count * avg_area_4x6 + 1e-6)
    X_poly = np.c_[X_poly, occlusion_penalty]

    return X_poly

def predictXGBoost(p_XNew):
    loadedModel = joblib.load(Path(__file__).resolve().parent / "xgboost_v4.pkl")
    predictions = loadedModel.predict(p_XNew)
    return predictions[0]

def predictXGBoost_v2(p_XNew) -> float:
    global _CACHED_XGB_POLY_MODEL
    
    if _CACHED_XGB_POLY_MODEL is None:
        model_path = Path(__file__).resolve().parent / "xgboost_poly.pkl"
        _CACHED_XGB_POLY_MODEL = joblib.load(model_path)
    
    if isinstance(p_XNew, pd.DataFrame):
        X_arr = p_XNew.to_numpy()
    elif isinstance(p_XNew, list):
        X_arr = np.array(p_XNew)
    else:
        X_arr = p_XNew
        
    if X_arr.ndim == 1:
        X_arr = X_arr.reshape(1, -1)
    
    X_poly_features = create_polynomial(X_arr)
    
    predictions = _CACHED_XGB_POLY_MODEL.predict(X_poly_features)
    return float(predictions[0])