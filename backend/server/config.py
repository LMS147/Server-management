"""Read the separately installed Atlas configuration without importing Atlas."""
from dataclasses import dataclass, field
import os
from pathlib import Path
from dotenv import dotenv_values, load_dotenv

MANAGER_ROOT = Path(__file__).resolve().parents[2]


def atlas_root() -> Path:
    load_dotenv(MANAGER_ROOT / "backend/.env", override=False)
    value = os.getenv("ATLAS_ROOT", "")
    if not value:
        raise ValueError("Set ATLAS_ROOT to the absolute path of your Atlas checkout in backend/.env")
    root = Path(value).expanduser()
    if not root.is_absolute() or not (root / "backend/atlas/app/__main__.py").is_file():
        raise ValueError("ATLAS_ROOT must point to an existing Atlas checkout with backend/atlas/app")
    return root.resolve()


def atlas_python() -> str:
    root = atlas_root()
    return os.getenv("ATLAS_PYTHON") or str(root / ".venv/bin/python")


@dataclass(frozen=True)
class Settings:
    api_token: str = field(repr=False)
    host: str = "127.0.0.1"
    port: int = 8000


def load_settings() -> Settings:
    values = {**dotenv_values(atlas_root() / "backend/.env"), **os.environ}
    token = values.get("ATLAS_API_TOKEN", "")
    if not token or len(token) < 32 or not token.isascii() or any(c.isspace() or ord(c) < 33 or ord(c) > 126 for c in token):
        raise ValueError("Configure a 32+ character printable ATLAS_API_TOKEN in the Atlas backend/.env")
    port = int(values.get("ATLAS_PORT", "8000"))
    if not 1 <= port <= 65535:
        raise ValueError("ATLAS_PORT must be between 1 and 65535")
    return Settings(api_token=token, host=values.get("ATLAS_HOST", "127.0.0.1"), port=port)
