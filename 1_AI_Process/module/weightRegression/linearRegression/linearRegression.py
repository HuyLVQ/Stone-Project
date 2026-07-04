import joblib
from pathlib import Path

def predictLinear(p_XNew):
    loadedModel = joblib.load(Path(__file__).resolve().parent / "linear.pkl")

    predictions = loadedModel.predict(p_XNew)
    return predictions[0]