"""Paths for the art scripts: outputs go next to the scripts."""
import os

SP = os.path.dirname(os.path.abspath(__file__))
PROJ = os.path.abspath(os.path.join(SP, "..", ".."))
ASSETS = os.path.join(PROJ, "Assets")
