# Privacy Policy

Legram runs **100% locally** on your Windows machine.

## Zero Telemetry & Zero Internet Access

- **No analytics, crash reporting, or remote network connections:** Legram never connects to the internet, phones home, or transmits system telemetry anywhere.
- **Local loopback (`127.0.0.1:11434`) only for Ollama:** When probing or gracefully unloading idle local [Ollama](https://ollama.com) models from GPU VRAM, Legram sends local HTTP requests strictly to `http://127.0.0.1:11434/api/ps` and `http://127.0.0.1:11434/api/generate` (`"keep_alive": 0`) if and only if a local Ollama daemon is listening on `127.0.0.1:11434`.

## Local Data Stored on Disk

Legram writes only two small files inside `%LOCALAPPDATA%\Legram\`:

- `settings.json` — UI preferences (window position, Mini Capsule state, Always on Top, Auto-Harmonize, EcoQoS toggle).
- `legram.log` — Rolling local diagnostic log (automatically capped in size).

Optional Windows integration (created only when explicitly enabled by the user):

- **Startup Registry Entry:** `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Legram` (`--background`).
- **0-UAC Scheduled Task:** `Legram_PurgeHelper` (`--purge-helper`) for fast, silent low-priority kernel page combining and standby list reclamation.

To completely remove Legram, quit the app from the system tray, optionally run `.\setup-task.ps1 -Unregister` if you enabled the 0-UAC task, and delete the Legram folder along with `%LOCALAPPDATA%\Legram`.
