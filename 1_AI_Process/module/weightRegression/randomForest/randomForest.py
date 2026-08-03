import joblib
from pathlib import Path

def predictRandomForest(p_XNew):
    loadedModel = joblib.load(Path(__file__).resolve().parent / "rf_v2.pkl")

    predictions = loadedModel.predict(p_XNew)
    return predictions[0]