# Changelog

All notable changes to Legram are documented in this file.

## [2.5.0] - 2026-11-15

### Initial Public Release
- **1-Click Unified Harmony Engine:** Selective working-set reclamation, physical page deduplication (`MemoryCombineInformationEx`, Class `130`), low-priority standby purge (`MemoryPurgeLowPriorityStandbyList`, Cmd `5`), registry hive cache reconciliation (`Class 155`), and NTFS system file cache compaction.
- **Commit Charge OOM Shield & ISLC Anti-Microstutter Guard:** Proactive background watchdog for virtual commit exhaustion (`>= 85%`) and gaming/creative standby saturation (`Available < 1.5 GB` + `Standby > 4.0 GB`).
- **Windows 11 EcoQoS + Very-Low Background I/O (`IoPriorityHintVeryLow`):** Automatic E-core and disk-queue throttling for background updaters/indexers with instant foreground restoration.
- **GPU VRAM, DWM & Orphan AI / Idle WSL Reclamation:** Native WDDM PDH GPU telemetry, `dwm.exe` composition leak deflator (`> 450 MB`), idle local Ollama model eviction (`keep_alive: 0`), and idle `vmmemWSL` compaction.
- **Lock-Safe Physical SSD Sweeper:** Cleans stale (`> 24h`) `%TEMP%`, browser HTTP/shader caches, WER crash dumps, and Recycle Bin clutter across IOCTL-verified local physical drives.
- **Obsidian Glass Monolith UI, Foldable Mini Capsule & Live Numeric Tray Icon:** Per-Monitor V2 HiDPI WPF interface in a single ~209 KB native x64 executable with zero external dependencies.
