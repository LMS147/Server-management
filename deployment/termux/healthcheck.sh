#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
cd "$ROOT"
if [[ -n "${PREFIX:-}" && -f "$PREFIX/var/service/atlas-api/atlas-managed.json" ]]; then
  cd "$SERVER_ROOT"
  exec "$SERVER_PYTHON" -m backend.server.termux_service health
fi
process_matches || { echo "No verified Atlas API process is running." >&2; exit 1; }
cd "$SERVER_ROOT"
exec "$SERVER_PYTHON" -m backend.server.healthcheck
