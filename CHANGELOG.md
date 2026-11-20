# Changelog

All notable changes to Legram are documented in this file.

## [2.6.0] - 2026-11-20

### Fixed & Hardened (Paket A)
- **Foreground Game & Anti-Cheat Safety:** `EcoQosManager` now restores only previously throttled PIDs with their exact saved `PriorityClass`, never opening `PROCESS_SET_INFORMATION` handles to unthrottled foreground games or demoting `HIGH_PRIORITY_CLASS` processes.
- **Foreground Browser Process-Tree Freedom:** Exempts active foreground browser GPU/renderer/utility subprocesses from background `EcoQoS` and `IoPriorityLow` throttling to eliminate scroll jank and audio crackling on Windows 11 24H2/25H2.
- **Instant `MemoryPriority` Restoration:** `TrimSingleProcessSafe` immediately restores original `MemoryPriority` (`5`) right after `K32EmptyWorkingSet` so trimmed apps stay snappy.
- **S3 Sleep/Wake & Explorer Restart Recovery:** Handles `TaskbarCreated`, `WM_POWERBROADCAST`, and `WM_DISPLAYCHANGE` to auto-recover the system tray icon, apply a 15s post-resume Auto-Guard grace window, and re-initialize NVML/PDH GPU handles after sleep or driver resets.

### Optimized (Paket B)
- **Honest Kernel Standby & Combine Accounting:** Separates `Available` RAM gain from Standby `P0` cache zeroing, reports deduplicated `PagesCombined`, paces working-set trims, and applies a 10-minute auto-mode cooldown on `SystemCombinePhysicalMemoryInformation`.
- **Zero-WMI Startup & Event-Driven Idle Polling:** Replaced `System.Management.dll` (`Win32_Process`) with native Win32 `CreateProcessW` (`CREATE_BREAKAWAY_FROM_JOB`), cached lightweight Virtual Desktop state, and cached GDI tray icon fonts.

### Added (Paket C)
- **Multi-Engine Local AI VRAM Unloader & Category Breakdown:** Adds automatic idle VRAM release for **LM Studio** (`:1234/api/v1/models/unload`) and **ComfyUI** (`:8188/free`) alongside **Ollama** (`:11434`), plus per-category VRAM & WDDM Shared GPU RAM telemetry.
- **Non-Destructive WSL2 Page-Cache Reclaim:** Drops Linux VM page caches (`drop_caches`) to return gigabytes of `vmmemWSL` memory without shutting down active WSL terminals or Docker containers.
- **Silent Global Hotkey (`Ctrl+Alt+H`), Keyboard Shortcuts & Extended Safe Sweep:** Adds dedicated silent background harmonize hotkey, keyboard controls (`Space`/`Enter`/`M`/`F5`/`Ctrl+1..5`), session harmonize history tooltips, and safe `WER` (`>7d`) / stale shader & Squirrel `.nupkg` cleanup.

## [2.5.0] - 2026-11-15

### Initial Public Release
- **1-Click Unified Harmony Engine:** Selective working-set reclamation, physical page deduplication (`MemoryCombineInformationEx`, Class `130`), low-priority standby purge (`MemoryPurgeLowPriorityStandbyList`, Cmd `5`), registry hive cache reconciliation (`Class 155`), and NTFS system file cache compaction.
- **Commit Charge OOM Shield & ISLC Anti-Microstutter Guard:** Proactive background watchdog for virtual commit exhaustion (`>= 85%`) and gaming/creative standby saturation (`Available < 1.5 GB` + `Standby > 4.0 GB`).
- **Windows 11 EcoQoS + Very-Low Background I/O (`IoPriorityHintVeryLow`):** Automatic E-core and disk-queue throttling for background updaters/indexers with instant foreground restoration.
- **GPU VRAM, DWM & Orphan AI / Idle WSL Reclamation:** Native WDDM PDH GPU telemetry, `dwm.exe` composition leak deflator (`> 450 MB`), idle local Ollama model eviction (`keep_alive: 0`), and idle `vmmemWSL` compaction.
- **Lock-Safe Physical SSD Sweeper:** Cleans stale (`> 24h`) `%TEMP%`, browser HTTP/shader caches, WER crash dumps, and Recycle Bin clutter across IOCTL-verified local physical drives.
- **Obsidian Glass Monolith UI, Foldable Mini Capsule & Live Numeric Tray Icon:** Per-Monitor V2 HiDPI WPF interface in a single ~209 KB native x64 executable with zero external dependencies.
