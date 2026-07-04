import joblib
from pathlib import Path

def predictRandomForest(p_XNew):
    loadedModel = joblib.load(Path(__file__).resolve().parent / "rf.pkl")

    predictions = loadedModel.predict(p_XNew)
    return predictions[0]