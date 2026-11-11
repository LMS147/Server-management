"""Management configuration works without importing or installing Atlas."""
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from backend.server.config import atlas_root, atlas_python, load_settings
from backend.server import termux_service


class ConfigTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        app = self.root / "backend/atlas/app"
        app.mkdir(parents=True)
        (app / "__main__.py").write_text("# external application\n")
        (self.root / "backend/.env").write_text("ATLAS_API_TOKEN=" + "x" * 40 + "\nATLAS_PORT=8123\n")

    def test_reads_external_settings_without_groq_key(self):
        with patch.dict(os.environ, {"ATLAS_ROOT": str(self.root)}, clear=True):
            self.assertEqual(self.root, atlas_root())
            self.assertEqual(str(self.root / ".venv/bin/python"), atlas_python())
            settings = load_settings()
            self.assertEqual(8123, settings.port)
            self.assertNotIn("x" * 40, repr(settings))

    def test_environment_overrides_external_file(self):
        with patch.dict(os.environ, {"ATLAS_ROOT": str(self.root), "ATLAS_PORT": "9000", "ATLAS_PYTHON": "/custom/python"}, clear=True):
            self.assertEqual(9000, load_settings().port)
            self.assertEqual("/custom/python", atlas_python())

    def test_wrong_root_and_invalid_port_fail(self):
        with patch.dict(os.environ, {"ATLAS_ROOT": str(self.root / "missing")}, clear=True):
            with self.assertRaises(ValueError):
                atlas_root()
        with patch.dict(os.environ, {"ATLAS_ROOT": str(self.root), "ATLAS_PORT": "99999"}, clear=True):
            with self.assertRaises(ValueError):
                load_settings()

    def test_existing_service_marker_still_identifies_external_atlas(self):
        import json
        service = self.root / "prefix/var/service/atlas-api"
        service.mkdir(parents=True)
        (service / "atlas-managed.json").write_text(json.dumps({"root": str(self.root)}))
        with patch.dict(os.environ, {"ATLAS_ROOT": str(self.root)}, clear=True):
            self.assertEqual(service, termux_service._service(self.root / "prefix"))
