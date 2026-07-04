import joblib
from pathlib import Path

def predictXGBoost(p_XNew):
    loadedModel = joblib.load(Path(__file__).resolve().parent / "xgboost.pkl")

    predictions = loadedModel.predict(p_XNew)
    return predictions[0]