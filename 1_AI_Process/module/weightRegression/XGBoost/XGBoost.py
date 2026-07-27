import joblib
from pathlib import Path
import numpy as np
def predictXGBoost(p_XNew):
    if np.all(np.asarray(p_XNew)==0):
        return 0
    else:
        loadedModel = joblib.load(Path(__file__).resolve().parent / "xgboost_v2.pkl")

        predictions = loadedModel.predict(p_XNew)
        return predictions[0]