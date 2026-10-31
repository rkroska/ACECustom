"""Where the analysis scripts in this folder find the source tree and the server's Config.js.

ACE_SOURCE_ROOT: the repo's Source folder. Default: the Source folder of the checkout this script is in.
ACE_CONFIG: the server's Config.js. Default: the x64 Release output under the source root.
A worktree that was never run as a server has no Config.js, so point ACE_CONFIG at the one that does.
"""
import json
import os
import re
import sys
from pathlib import Path


def _require(path, what, variable):
    if not path.exists():
        sys.exit(f"{what} not found: {path}\nSet {variable} to its location.")
    return path


def source_root():
    default = Path(__file__).resolve().parents[2] / "Source"
    return _require(Path(os.environ.get("ACE_SOURCE_ROOT", default)), "Source folder", "ACE_SOURCE_ROOT")


def source_path(*parts):
    return _require(source_root().joinpath(*parts), "Source file", "ACE_SOURCE_ROOT")


def read_source(*parts):
    return source_path(*parts).read_text(encoding="utf-8")


def load_config():
    default = source_root() / "ACE.Server" / "bin" / "x64" / "Release" / "net10.0" / "Config.js"
    text = _require(Path(os.environ.get("ACE_CONFIG", default)), "Config.js", "ACE_CONFIG").read_text(encoding="utf-8")
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    text = re.sub(r"(?m)^\s*//.*$", "", text)
    return json.loads(text)
