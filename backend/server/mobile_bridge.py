"""Fixed management commands over authenticated SSH; no HTTP API is required."""
import argparse
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import time
import sys
from dotenv import dotenv_values
from backend.server.config import MANAGER_ROOT, atlas_root
from backend.server.healthcheck import probe

ACTIONS = ("status", "recover", "api-start", "api-stop", "api-restart", "logs-api", "logs-tunnel", "logs-startup")


def process_running(name):
    return subprocess.run(["pgrep", "-x", name], stdout=subprocess.DEVNULL,
                          stderr=subprocess.DEVNULL, check=False, timeout=5).returncode == 0


def host_metrics():
    ram = None
    uptime = None
    try:
        values = {line.split(":")[0]: int(line.split()[1]) * 1024
                  for line in Path("/proc/meminfo").read_text().splitlines()
                  if line.split(":")[0] in ("MemTotal", "MemAvailable")}
        ram = {"total_bytes": values["MemTotal"], "free_bytes": values["MemAvailable"]}
    except (OSError, ValueError, KeyError):
        pass
    try:
        uptime = int(float(Path("/proc/uptime").read_text().split()[0]))
    except (OSError, ValueError, IndexError):
        pass
    disk = shutil.disk_usage(Path.home())
    return {"ram": ram, "disk": {"total_bytes": disk.total, "free_bytes": disk.free}, "uptime_seconds": uptime}


def process_metrics(pid):
    if not isinstance(pid, int) or pid <= 0:
        return None, None
    memory = None
    cpu = None
    try:
        text = Path(f"/proc/{pid}/status").read_text()
        memory = int(next(line for line in text.splitlines() if line.startswith("VmRSS:")).split()[1]) * 1024
    except (OSError, ValueError, IndexError, StopIteration):
        pass
    try:
        def ticks():
            fields = Path(f"/proc/{pid}/stat").read_text().rsplit(")", 1)[1].split()
            return int(fields[11]) + int(fields[12])
        first = ticks()
        started = time.monotonic()
        time.sleep(0.15)
        elapsed = time.monotonic() - started
        cpu = round(max(0, (ticks() - first) / os.sysconf("SC_CLK_TCK") / elapsed * 100), 1)
    except (OSError, ValueError, IndexError, ZeroDivisionError):
        pass
    return memory, cpu


def status():
    healthy, payload = probe()
    pid = payload.get("pid") if healthy and payload else None
    memory, cpu = process_metrics(pid)
    try:
        from backend.server.config import load_settings
        port = load_settings().port
    except (OSError, ValueError):
        port = None
    return {"sampled_at": int(time.time() * 1000), "api_healthy": healthy,
            "api_pid": pid, "api_version": payload.get("version") if healthy and payload else None,
            "api_uptime_seconds": payload.get("uptime_seconds") if healthy and payload else None,
            "api_memory_bytes": memory, "api_cpu_percent": cpu, "api_port": port,
            "hostname": socket.gethostname(),
            "tunnel_running": process_running("cloudflared"),
            "ssh_running": process_running("sshd"), "host": host_metrics()}


def api_action(action):
    prefix = os.getenv("PREFIX")
    if not prefix:
        raise RuntimeError("Run management commands inside Termux.")
    result = subprocess.run([sys.executable, "-m", "backend.server.termux_service", action],
                            cwd=MANAGER_ROOT, capture_output=True, text=True, timeout=65)
    if result.returncode:
        raise RuntimeError("The API action did not complete. Check the API logs and service configuration.")


def start_tunnel():
    if process_running("cloudflared"):
        return
    script = Path.home() / "start.sh"
    if not script.is_file():
        raise RuntimeError("Install the host startup scripts before recovering the tunnel.")
    subprocess.run(["bash", str(script)], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                   check=True, timeout=15)
    # A launcher request is not a confirmed running connector.
    for _ in range(20):
        if process_running("cloudflared"):
            return
        time.sleep(0.25)
    raise RuntimeError("The tunnel did not start. Check tunnel logs and ~/token.txt.")


def read_logs(kind):
    prefix = Path(os.getenv("PREFIX", "/data/data/com.termux/files/usr"))
    candidates = {
        "api": [prefix / "var/log/sv/atlas-api/current", atlas_root() / ".run/api.log"],
        "tunnel": [Path.home() / "server/logs/cloudflared.log"],
        "startup": [Path.home() / "server/logs/startup.log", Path.home() / "server/logs/tunnel-startup.log"],
    }
    sections = []
    for path in candidates[kind]:
        if path.is_file():
            with path.open("rb") as handle:
                handle.seek(max(0, path.stat().st_size - 16384))
                sections.append(path.name + "\n" + "\n".join(handle.read(16384).decode(errors="replace").splitlines()[-100:]))
    text = "\n\n".join(sections) or "No log entries are available yet."
    # Redact configured credentials before returning log content to the app.
    secrets = [v for k, v in dotenv_values(atlas_root() / "backend/.env").items()
               if v and any(word in k.upper() for word in ("TOKEN", "KEY", "PASSWORD"))]
    secrets.extend(os.environ[k] for k in ("ATLAS_API_TOKEN", "GROQ_API_KEY", "TELEGRAM_TOKEN") if os.getenv(k))
    token_file = Path.home() / "token.txt"
    if token_file.is_file():
        secrets.append(token_file.read_text().strip())
    for secret in sorted(set(secrets), key=len, reverse=True):
        if secret:
            text = text.replace(secret, "[redacted]")
    return text


def execute(action):
    if action not in ACTIONS:
        raise ValueError("Unsupported management action.")
    if action.startswith("logs-"):
        return {"ok": True, "log_text": read_logs(action[5:])}
    if action.startswith("api-"):
        api_action(action[4:])
    if action == "recover":
        healthy, _ = probe()
        if not healthy:
            api_action("start")
        start_tunnel()
    return {"ok": True, "status": status()}


def main(argv=None):
    parser = argparse.ArgumentParser(description="Server-management SSH bridge")
    parser.add_argument("action", choices=ACTIONS)
    action = parser.parse_args(argv).action
    try:
        result = execute(action)
    except (OSError, ValueError, RuntimeError, subprocess.SubprocessError):
        # Raw exceptions can contain provider/service configuration; keep the response safe.
        result = {"ok": False, "error": "Management action failed. Check ATLAS_ROOT, service setup and the selected logs."}
    print(json.dumps(result))
    return 0 if result["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
