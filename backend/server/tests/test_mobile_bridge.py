"""SSH bridge keeps actions bounded, returns JSON and redacts configured secrets."""
import contextlib
import io
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from backend.server import mobile_bridge as bridge


class MobileBridgeTests(unittest.TestCase):
    def test_status_survives_restricted_android_metrics(self):
        with patch.object(bridge, "probe", return_value=(False, None)), patch.object(bridge, "process_running", return_value=True), patch.object(Path, "read_text", side_effect=PermissionError):
            data = bridge.status()
        self.assertFalse(data["api_healthy"])
        self.assertTrue(data["tunnel_running"])
        self.assertIsNone(data["host"]["ram"])
        self.assertIsNone(data["host"]["uptime_seconds"])

    def test_recovery_starts_only_unavailable_services(self):
        with patch.object(bridge, "probe", return_value=(True, {"pid": 1})), patch.object(bridge, "api_action") as api, patch.object(bridge, "start_tunnel") as tunnel, patch.object(bridge, "status", return_value={}):
            self.assertTrue(bridge.execute("recover")["ok"])
            api.assert_not_called()
            tunnel.assert_called_once()
        with patch.object(bridge, "probe", return_value=(False, None)), patch.object(bridge, "api_action") as api, patch.object(bridge, "start_tunnel"), patch.object(bridge, "status", return_value={}):
            bridge.execute("recover")
            api.assert_called_once_with("start")

    def test_rejects_arbitrary_command_and_emits_only_safe_json(self):
        with self.assertRaises(ValueError):
            bridge.execute("reboot; rm -rf /")
        output = io.StringIO()
        with patch.object(bridge, "execute", side_effect=RuntimeError("secret-token")), contextlib.redirect_stdout(output):
            self.assertEqual(1, bridge.main(["status"]))
        result = json.loads(output.getvalue())
        self.assertFalse(result["ok"])
        self.assertNotIn("secret-token", output.getvalue())

    def test_logs_redact_provider_and_tunnel_credentials(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "backend").mkdir()
            (root / "backend/.env").write_text("GROQ_API_KEY=test-groq-secret\nATLAS_API_TOKEN=test-atlas-secret\n")
            (root / "token.txt").write_text("test-tunnel-secret")
            (root / "server/logs").mkdir(parents=True)
            (root / "server/logs/cloudflared.log").write_text("test-groq-secret test-atlas-secret test-tunnel-secret")
            with patch.object(bridge, "atlas_root", return_value=root), patch.object(Path, "home", return_value=root):
                text = bridge.read_logs("tunnel")
            self.assertEqual(3, text.count("[redacted]"))
            self.assertNotIn("secret", text)

    def test_process_metrics_are_null_when_android_blocks_proc(self):
        with patch.object(Path, "read_text", side_effect=PermissionError):
            self.assertEqual((None, None), bridge.process_metrics(1234))
        self.assertEqual((None, None), bridge.process_metrics(None))

    def test_status_is_compatible_with_older_health_payloads(self):
        with patch.object(bridge, "probe", return_value=(True, {"pid": 1234})), patch.object(bridge, "process_running", return_value=True), patch.object(bridge, "process_metrics", return_value=(None, None)):
            data = bridge.status()
        self.assertTrue(data["api_healthy"])
        self.assertIsNone(data["api_version"])
        self.assertIsNone(data["api_uptime_seconds"])
