# Changelog

All notable changes to Legram are documented in this file.

## [2.7.0]

### Fixed & Hardened
- **Thrash-Free Commit Shield & Hysteresis Latch:** Separated *Physical RAM pressure* from *Virtual Commit Charge* (`Pagefile + RAM`) in `RamEngine`. When Commit reaches `>= 90%` while physical RAM has `>= 2.8 GB` free, Auto-Guard no longer runs repetitive `K32EmptyWorkingSet` working-set trims; instead, it executes targeted commit-reducing relief (idle AI VRAM/model unload, orphan runner cleanup, non-destructive WSL2 page-cache drop) and arms an `<= 84%` hysteresis latch with exponential backoff (`2m -> 10m -> 30m`).
- **Laptop Battery Support for 0-UAC Kernel Helper:** Updated `RegisterKernelPurgeTaskInteractive` and `setup-task.ps1` to register `Legram_PurgeHelper` via UTF-16 Task Scheduler XML with `DisallowStartIfOnBatteries=false`, `StopIfGoingOnBatteries=false`, and `ExecutionTimeLimit=PT1M`.
- **Universal Process-Tree Immunity:** Generalized virtual/cloud filesystem detection (`IOCTL_STORAGE_QUERY_PROPERTY` + WinFsp/Dokan/ProjFS/rclone), dynamic multi-process browser & `FileVersionInfo` display-name resolution, and descendant propagation for user-whitelisted (`immuneGroups`) and TCP-listening processes.

### Optimized
- **24/7 Background Daemon Footprint:** Increased `schtasks.exe` verification cache TTL from `30s` to `30m` (`60x` fewer background process spawns), cached frozen WPF `RadialGradientBrush` aura instances (`0` per-tick brush allocations), and relaxed hidden-to-tray polling to `8.0s`.
- **Deterministic Build & Showcase Renders:** Added explicit `/debug-` compiler hardening (`0` embedded debug symbol paths) and deterministic showcase snapshots for HiDPI documentation renders.

### Added
- **AMD / Intel / WDDM True Adapter VRAM Capacity:** Added `EnumDisplayDevicesW` + 64-bit WDDM Display Class Registry lookup (`HardwareInformation.qwMemorySize`) when NVML is unavailable, replacing fixed fallback totals on AMD Radeon, Intel Arc, and hybrid GPUs.
- **Kernel Non-Paged / Paged Pool & Modified Page Telemetry:** Surfaced live `KernelNonpaged` (`[High]` warning `>= 1.5 GB`), `KernelPaged`, and `Modified` memory metrics inside the System RAM inspection tooltip and tile status.

## [2.6.0]

### Added & Improved
- **Foreground Game & Browser Tree Safety:** Restores exact saved `PriorityClass` & `MemoryPriority` strictly on previously throttled PIDs and exempts active foreground browser subprocesses (`--type=gpu-process` / `renderer`) from `EcoQoS` and `IoPriorityLow`.
- **Multi-Engine Local AI VRAM Unloader & Non-Destructive WSL2 Reclaim:** Added idle model eviction for **LM Studio** (`:1234`) and **ComfyUI** (`:8188/free`) alongside **Ollama** (`:11434`), per-category VRAM telemetry, and non-destructive WSL2 `drop_caches` reclamation.
- **Silent Global Hotkey (`Ctrl+Alt+H`) & Sleep/Explorer Resilience:** Added global silent harmonize shortcut, `TaskbarCreated` tray recovery, `WM_POWERBROADCAST` S3 resume grace window, and zero-`System.Management` breakaway startup.

## [2.5.0]

### Initial Release
- **1-Click Unified Harmony Engine:** Selective working-set reclamation, physical page deduplication (`MemoryCombineInformationEx`, Class `130`), low-priority standby purge (`MemoryPurgeLowPriorityStandbyList`, Cmd `5`), registry hive cache reconciliation (`Class 155`), and NTFS system file cache compaction.
- **ISLC Anti-Microstutter Guard, Windows 11 EcoQoS, Virtual Desktop Auto-Sleep & Safe Physical SSD Sweeper:** Contained in a single zero-dependency x64 executable.
