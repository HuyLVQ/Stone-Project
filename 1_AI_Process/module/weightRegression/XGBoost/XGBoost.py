import joblib
from pathlib import Path

def predictXGBoost(p_XNew):
    loadedModel = joblib.load(Path(__file__).resolve().parent / "xgboost_v2.pkl")

    predictions = loadedModel.predict(p_XNew)
    return predictions[0]