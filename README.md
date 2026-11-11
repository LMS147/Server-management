# Server-management

Phone host management for the Termux server. Atlas's mobile app and AI backend
are maintained separately in [atlas-agent](https://github.com/LMS147/atlas-agent).

| Location | Responsibility |
|---|---|
| `backend/server/termux/` | Home startup, Cloudflare retry loop and Termux:Boot templates |
| `backend/server/termux_service.py` | Install/control the separately installed Atlas API service |
| `backend/server/healthcheck.py` | Check Atlas over authenticated local HTTP |
| `backend/server/config.py` | Locate Atlas using `ATLAS_ROOT`, without importing its code |
| `deployment/termux/` | API and Telegram lifecycle commands |
| `deployment/cloudflare/` | HTTPS tunnel setup |
| `deployment/backup/` | Offline workspace backup, verify and restore tools |
| `docs/migration.md` | Move an existing phone installation to the two-repository layout |

## Configure management

Clone this repository alongside your existing Atlas checkout. Keep Atlas and its
data in place.

```sh
cd ~/server/apps
git clone https://github.com/LMS147/Server-management.git
cd Server-management
python -m venv .venv
source .venv/bin/activate
python -m pip install -r backend/requirements.txt
cp backend/.env.example backend/.env
nano backend/.env
```

Set `ATLAS_ROOT` to the absolute Atlas checkout path, for example
`/data/data/com.termux/files/home/server/apps/atlas-agent`. The manager defaults to
that checkout's `.venv/bin/python`; use `ATLAS_PYTHON` to override it if needed.
Atlas's API token, Groq key and DATA_DIR remain in the Atlas checkout's
`backend/.env`. Management reads health credentials there and requires no Groq
package or AI API key of its own.

From this repository, with its management virtualenv active:

```sh
python -m backend.server.healthcheck
bash deployment/termux/service.sh status
```

Before installing/re-registering on an existing phone, follow
[the migration sequence](docs/migration.md). It preserves existing service markers,
application paths and data. Generated supervisor scripts launch Atlas from the
Atlas checkout, using Atlas's interpreter.

## Host startup and connections

[Host startup instructions](backend/server/README.md) cover `~/start.sh`,
`~/tunnel.sh`, the private `~/token.txt`, duplicate startup ownership and boot
verification. The Cloudflare tunnel remains the main access path; Tailscale with
Termux SSH on port 8022 provides independent remote access.

[Atlas service management](deployment/termux/README.md),
[HTTPS access](deployment/cloudflare/README.md) and
[workspace backups](deployment/backup/README.md) are maintained here.

## Tests

```sh
python -m unittest discover -s backend/server/tests -v
python -m unittest discover -s deployment/backup -p 'test_*.py' -v
```

Tests of configuration, host scripts, installer generation and backups run without
Atlas installed. Cross-repository API lifecycle tests are opt-in and use a separate
Atlas checkout with disposable DATA_DIR:

```sh
ATLAS_TEST_ROOT=/absolute/atlas-agent ATLAS_PYTHON=/absolute/atlas-agent/.venv/bin/python \
  python -m unittest discover -s backend/server/tests -v
```

These integration tests start and stop disposable API instances. Run them on a
development checkout with no production API running. CI checks both repositories.

The independent [Server-management app](mobile/README.md) is under `mobile/`. It uses the fixed `backend.server.mobile_bridge` commands through Tailscale SSH, including when the Atlas API is unavailable. No separate public management HTTP endpoint is required.
