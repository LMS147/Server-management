"""Run the real API launcher and Termux helpers against loopback, without Groq calls."""
import os
import http.client
import json
import time
from pathlib import Path
import shutil
import socket
import subprocess
import sys
import tempfile
import unittest

def proc_pids_match():
    try:
        actual = next(line.split()[1] for line in Path("/proc/self/status").read_text().splitlines() if line.startswith("Pid:"))
        return int(actual) == os.getpid()
    except (OSError, StopIteration):
        return False

@unittest.skipUnless(os.getenv("ATLAS_TEST_ROOT"), "Set ATLAS_TEST_ROOT for cross-repository Atlas integration")
class LifecycleTests(unittest.TestCase):
    @unittest.skipUnless(sys.platform.startswith("linux") and proc_pids_match(), "Requires Linux/Termux with a matching /proc PID namespace")
    def test_start_duplicate_health_and_stop(self):
        repo = Path(__file__).resolve().parents[3]
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            scripts = root / "deployment" / "termux"
            shutil.copytree(repo / "deployment" / "termux", scripts)
            with socket.socket() as sock:
                sock.bind(("127.0.0.1", 0))
                port = sock.getsockname()[1]
            env = {"PATH": os.environ.get("PATH", ""), "PYTHONPATH": str(repo),
                   "ATLAS_ROOT": os.environ["ATLAS_TEST_ROOT"], "ATLAS_PYTHON": os.getenv("ATLAS_PYTHON", sys.executable),
                   "ATLAS_API_TOKEN": "test-lifecycle-token-" + "x" * 32, "GROQ_API_KEY": "test-only",
                   "DATA_DIR": str(root / "data"), "ATLAS_HOST": "127.0.0.1", "ATLAS_PORT": str(port), "GH_REPO": ""}
            def run(name):
                return subprocess.run(["bash", str(scripts / name)], cwd=root, env=env, capture_output=True, text=True, timeout=35)
            try:
                first = run("start.sh")
                self.assertEqual(0, first.returncode, first.stdout + first.stderr + (Path(os.environ["ATLAS_TEST_ROOT"]) / ".run" / "api.log").read_text())
                pid = (Path(os.environ["ATLAS_TEST_ROOT"]) / ".run" / "api.pid").read_text()
                self.assertEqual(0, run("healthcheck.sh").returncode)
                self.assertEqual(0, run("start.sh").returncode)
                self.assertEqual(pid, (Path(os.environ["ATLAS_TEST_ROOT"]) / ".run" / "api.pid").read_text())
                self.assertEqual(0, run("stop.sh").returncode)
                self.assertNotEqual(0, run("healthcheck.sh").returncode)
                self.assertFalse((Path(os.environ["ATLAS_TEST_ROOT"]) / ".run" / "api.pid").exists())
            finally:
                # If a wrapper failed, still clean up this test's own explicitly recorded child.
                pid_file = Path(os.environ["ATLAS_TEST_ROOT"]) / ".run" / "api.pid"
                recorded = int(pid_file.read_text()) if pid_file.exists() else None
                run("stop.sh")
                if recorded:
                    try:
                        os.kill(recorded, 15)
                    except ProcessLookupError:
                        pass
