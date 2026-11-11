# Move phone management out of Atlas

Two adjacent checkouts are used:

```text
~/server/apps/atlas-agent       Atlas mobile app and AI backend
~/server/apps/Server-management Host management tools and scripts
```

Keep the existing Atlas directory, `backend/.env`, DATA_DIR, signing keys,
`~/token.txt`, home startup scripts and running tunnel in place. Moving repository
source files does not install or change anything on the phone automatically.

1. Clone and configure Server-management following its README. Do this before
   updating Atlas so the management tools are available first. Management's
   `backend/.env` contains `ATLAS_ROOT` pointing to the existing Atlas checkout.
2. Activate the management environment and verify that it recognizes the existing
   service:

   ```sh
   cd ~/server/apps/Server-management
   source .venv/bin/activate
   bash deployment/termux/service.sh status
   ```

3. Stop the API through the manager. Keep SSH/Tailscale available during this work:

   ```sh
   bash deployment/termux/service.sh stop
   ```

4. Update the Atlas application from its own directory:

   ```sh
   cd ~/server/apps/atlas-agent
   git pull --ff-only
   .venv/bin/python -m pip install -r backend/requirements.txt
   ```

5. Return to management, re-register the existing supervisor and check health:

   ```sh
   cd ~/server/apps/Server-management
   source .venv/bin/activate
   bash deployment/termux/install-service.sh --enable --boot
   bash deployment/termux/service.sh health
   ```

The installed `atlas-api` marker continues to identify the same Atlas checkout.
The generated run script still launches `backend.atlas.app` there. Existing
conversation and memory data remain at the configured Atlas DATA_DIR.

Update personal scripts that referenced `atlas-agent/deployment/termux` to use
`Server-management/deployment/termux`. Do not create duplicate host boot launchers.
Review the separate host-template instructions before copying any home scripts.
Finally reboot while you have physical access, unlock the phone, and test
Cloudflare, Tailscale SSH and the API again.
