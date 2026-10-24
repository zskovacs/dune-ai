"""Fail fast when declared or active toolchains drift from repository pins."""

import json
import os
import subprocess
import sys
import tomllib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def output(*command: str) -> str:
    return subprocess.check_output(command, cwd=ROOT, text=True).strip()


def require(condition: bool, message: str) -> None:
    if not condition:
        raise SystemExit(f"Toolchain mismatch: {message}")


def main() -> None:
    config = tomllib.loads((ROOT / "mise.toml").read_text())
    tools = config["tools"]
    sdk = json.loads((ROOT / "global.json").read_text())["sdk"]
    python_pin = (ROOT / ".python-version").read_text().strip()
    require(sdk["version"] == tools["dotnet"], "mise and global.json SDK pins")
    require(sdk["rollForward"] == "disable", "SDK roll-forward must be disabled")
    require(sdk["allowPrerelease"] is False, "SDK prereleases must be disabled")
    require(python_pin == tools["python"], "mise and .python-version pins")
    require(output("dotnet", "--version") == sdk["version"], "active .NET SDK")
    require(sys.version.split()[0] == python_pin, "active Python version")
    python_path = output("mise", "which", "python")
    require(
        Path(sys.executable).resolve() == Path(python_path).resolve(),
        "active Python path",
    )
    require(
        os.environ.get("UV_PYTHON") == python_path,
        "UV_PYTHON must equal mise which python",
    )
    require(
        os.environ.get("UV_PYTHON_DOWNLOADS") == "never",
        "uv Python downloads must be disabled",
    )
    require(
        os.environ.get("UV_PYTHON_PREFERENCE") == "only-system",
        "uv must use mise Python",
    )
    uv_config = tomllib.loads((ROOT / "pyproject.toml").read_text())["tool"]["uv"]
    require(uv_config["python-downloads"] == "never", "uv config Python downloads")
    require(
        uv_config["python-preference"] == "only-system", "uv config Python preference"
    )
    require(
        output("uv", "--version").split()[1] == tools["uv"],
        "active uv version",
    )
    venv_python = ROOT / ".venv" / "bin" / "python"
    require(venv_python.exists(), "run mise run setup to create .venv")
    base_python = output(
        str(venv_python), "-c", "import sys; print(sys._base_executable)"
    )
    require(
        Path(base_python).resolve() == Path(python_path).resolve(),
        "uv .venv base interpreter",
    )
    print(
        f"Toolchains match: .NET {sdk['version']}, "
        f"Python {python_pin}, uv {tools['uv']}"
    )
    print(f"UV_PYTHON={python_path}")


if __name__ == "__main__":
    main()
