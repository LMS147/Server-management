#!/usr/bin/env bash
# Management runs from this repository; application processes run from ATLAS_ROOT.
SERVER_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SERVER_PYTHON="${SERVER_PYTHON:-python}"
server_python() {
  (cd "$SERVER_ROOT" && "$SERVER_PYTHON" "$@")
}
ROOT="$(server_python -c 'from backend.server.config import atlas_root; print(atlas_root())')"
export ATLAS_ROOT="$ROOT"
ATLAS_PYTHON="$(server_python -c 'from backend.server.config import atlas_python; print(atlas_python())')"
export ATLAS_PYTHON
API_PID_FILE="$ROOT/.run/api.pid"
process_matches() {
  [[ -f "$API_PID_FILE" ]] || return 1
  local api_pid
  api_pid="$(cat "$API_PID_FILE")"
  [[ "$api_pid" =~ ^[0-9]+$ ]] && kill -0 "$api_pid" 2>/dev/null || return 1
  [[ -r "/proc/$api_pid/cmdline" ]] || return 1
  tr '\0' '\n' <"/proc/$api_pid/cmdline" | grep -Fxq -- 'backend.atlas.app'
}
