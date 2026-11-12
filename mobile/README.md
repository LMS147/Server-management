# Server-management application

Independent .NET 10 MAUI Blazor Hybrid app for Android and Windows. Open
`ServerManagement.sln` in Visual Studio with the .NET 10 MAUI workloads.

Android package ID is `com.mletsoalo.servermanager`. It installs alongside Atlas
(`com.atlas.ai.hybrid`) with separate secure credentials and app data.

| Page | Function |
|---|---|
| Dashboard | Explicit SSH status checks, API/tunnel state, host memory/storage/uptime and recovery |
| Services | Confirmed API start, stop and restart; start missing API/tunnel through recovery |
| Connections | Tailscale IPv4, SSH port/user/password, pinned Ed25519 fingerprint and manager checkout |
| Logs | Read-only recent API, tunnel and startup logs with configured credentials redacted |
| Settings | Forget saved connection and view application details |

## Prepare the server phone

Update this repository on the server, activate its management virtualenv, and
install `backend/requirements.txt`. Configure `ATLAS_ROOT` in `backend/.env`, then
install/register the API using the root README and migration guide. Ensure the
reviewed host scripts are installed at `~/start.sh` and `~/tunnel.sh`, with the
private Cloudflare token in `~/token.txt`.

Test the SSH bridge locally from the Server-management checkout:

```sh
.venv/bin/python -m backend.server.mobile_bridge status
```

The bridge runs over existing Termux SSH. It accepts a fixed action allowlist and
is independent of the Atlas API. Recovery starts the API when its authenticated
health check fails, then starts a missing cloudflared process using `~/start.sh`.
A running but unhealthy API may require an explicit confirmed Restart. A running
tunnel process is not proof of Cloudflare connector or public-hostname health.

## Connect the management app

1. Enable Tailscale on the client and server devices using the same tailnet.
2. Verify Termux SSH works to the server's Tailscale IPv4 on port 8022.
3. On the server phone, run:

   ```sh
   ssh-keygen -lf "$PREFIX/etc/ssh/ssh_host_ed25519_key.pub"
   ```

4. In Connections, enter that `SHA256:` value, the Tailscale IPv4, Termux username
   and SSH password, and absolute management checkout path. The default path is
   `/data/data/com.termux/files/home/server/apps/Server-management`.
5. Save the connection and tap Check server on Dashboard. Fingerprint mismatches
   stop authentication; verify the key directly on the server before changing it.
6. Test recovery while physically near the phone first, then across networks.

Remembered credentials use MAUI SecureStorage. Unchecking Remember keeps the
connection in memory for that session. Forget connection removes the persisted
record and in-memory logs. SSH requests have bounded timeouts and actions are
never automatically replayed after a connection failure.

The device must remain powered, online, connected to Tailscale, with Termux SSH
running. This app cannot recover a force-stopped Termux process or an offline phone.

## Build and validate

```sh
dotnet test mobile/ServerManager.Core.Tests/ServerManager.Core.Tests.csproj -c Release
dotnet build mobile/ServerManager.Mobile/ServerManager.Mobile.csproj -f net10.0-android -c Debug
dotnet run --project mobile/tests/ServerManager.VisualChecks/ServerManager.VisualChecks.csproj -c Release -- visual-results
```

The Server management app workflow builds Android and Windows, and uploads the
`server-management-android-apk` development artifact. Permanent release signing
for this new app must be configured separately from Atlas before distribution.
Physical-device validation must cover secure storage, SSH host verification,
keyboard/Back, reconnecting networks, service actions and interrupted operations.

## Reference visual design (1.1.0)

The five pages use the supplied near-black/green design, SVG icons, resource bars,
service cards, a terminal-style log viewer, and fixed bottom navigation. Display
names are editable on Connections. Live log updates are opt-in and refresh every
12 seconds while the Logs page and app are active. Copy uses the native clipboard.
Settings opens official documentation, source and release pages. Status alerts
are persisted in-app banners for changes observed after checks; they are not
background push notifications. API PID, process CPU/RAM, version and uptime come
from the SSH bridge; Android-restricted or older-backend values remain unavailable.
