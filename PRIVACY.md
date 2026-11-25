# Privacy Policy

Legram runs **100% locally** on your Windows machine.

## Zero Telemetry & Zero Internet Access

- **No analytics, crash reporting, or remote network connections:** Legram never connects to the internet, phones home, or transmits system telemetry anywhere.
- **Strict local loopback (`127.0.0.1`) only for optional local AI VRAM unloading:** When GPU utilization is idle, Legram checks local TCP listener tables first (`GetExtendedTcpTable`) and sends strictly local loopback HTTP requests only if a local AI server is actively listening on `127.0.0.1`:
  - **Ollama (`127.0.0.1:11434`):** `GET /api/ps` and `POST /api/generate` (`"keep_alive": 0`).
  - **LM Studio (`127.0.0.1:1234`):** `GET /api/v1/models` and `POST /api/v1/models/unload`.
  - **ComfyUI (`127.0.0.1:8188`):** `GET /queue` and `POST /free` (`"unload_models": true, "free_memory": true`).

## Local Data Stored on Disk

Legram writes only small local state files inside `%LOCALAPPDATA%\Legram\`:

- `ui-state.json` — UI preferences (window position, Mini Capsule state, Always on Top, Auto-Harmonize, Game Mode Auto-Pause, EcoQoS, Virtual Desktop Auto-Sleep, and user-selected immune app keys).
- `legram.log` — Rolling local diagnostic log (automatically capped at 256 KB).
- `primary.pid` — Ephemeral single-instance coordinator PID marker.

Optional Windows integration (created only when explicitly enabled by the user):

- **Startup Registry Entry:** `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Legram` (`--background`).
- **0-UAC Scheduled Task:** `Legram_PurgeHelper` (`--purge-helper`) for fast, silent low-priority kernel page combining and standby list reclamation on both AC and battery power.

To completely remove Legram, quit the app from the system tray, optionally run `.\setup-task.ps1 -Unregister` if you enabled the 0-UAC task, and delete the Legram folder along with `%LOCALAPPDATA%\Legram`.
