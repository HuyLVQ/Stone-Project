import joblib
import numpy as np
from pathlib import Path
import pandas as pd

def createPolynomial(p_X):

    if isinstance(p_X, pd.DataFrame):
        p_X = p_X.to_numpy()

    XPoly = p_X.copy()

    XPoly = np.c_[XPoly, p_X[:, 0] * p_X[:, 4]]
    XPoly = np.c_[XPoly, p_X[:, 0] * p_X[:, 8]]
    XPoly = np.c_[XPoly, p_X[:, 4] * p_X[:, 8]]
    XPoly = np.c_[XPoly, p_X[:, 4] ** 2]
    # XPoly = np.c_[XPoly, X[:, 0] * X[:, 4] ** 2]
    #1x2
    XPoly = np.c_[XPoly, p_X[:, 1] * p_X[:, 5]]
    XPoly = np.c_[XPoly, p_X[:, 1] * p_X[:, 9]]
    XPoly = np.c_[XPoly, p_X[:, 5] * p_X[:, 9]]
    XPoly = np.c_[XPoly, p_X[:, 5] ** 2]
    # XPoly = np.c_[XPoly, X[:, 1] * X[:, 5] ** 2]
    #2x4
    XPoly = np.c_[XPoly, p_X[:, 2] * p_X[:, 6]]
    XPoly = np.c_[XPoly, p_X[:, 2] * p_X[:, 10]]
    XPoly = np.c_[XPoly, p_X[:, 6] * p_X[:, 10]]
    XPoly = np.c_[XPoly, p_X[:, 6] ** 2]
    # XPoly = np.c_[XPoly, X[:, 2] * X[:, 6] ** 2]
    #4x6
    XPoly = np.c_[XPoly, p_X[:, 3] * p_X[:, 7]]
    XPoly = np.c_[XPoly, p_X[:, 3] * p_X[:, 11]]
    XPoly = np.c_[XPoly, p_X[:, 7] * p_X[:, 11]]
    XPoly = np.c_[XPoly, p_X[:, 7] ** 2]
    # XPoly = np.c_[XPoly, X[:, 3] * X[:, 7] ** 2]

    return XPoly


def predictPoly(p_XNew):
    loadedModel = joblib.load(Path(__file__).resolve().parent / "poly.pkl")

    XPoly = createPolynomial(p_XNew)
    predictions = loadedModel.predict(XPoly)

    return predictions[0]