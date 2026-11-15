# Contributing to Legram

Contributions, bug reports, and performance improvements are welcome!

## Design & Engineering Constraints

1. **Zero External Dependencies:** Legram must compile out-of-the-box on any stock Windows 10 or Windows 11 installation using the built-in `.NET Framework 4.x` compiler (`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`, `/langversion:5`). Do not introduce NuGet packages, third-party DLLs, or C# 6+ syntax (`$"..."`, `?.`, `nameof`, `out var`).
2. **Zero Foreground Stutter:** Never flush the entire Windows Standby List (`MemoryPurgeStandbyList` / `cmd=4`) or trim protected foreground, audio, compiler, or local server processes.
3. **Sub-2 MB Self-Trimmed Footprint:** Keep steady-state working set minimal and close idle PDH/Win32 handles promptly.

## Building & Running Verification

```powershell
# Build bin\Legram.exe
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\build.ps1

# Run full automated selftest & HiDPI screenshot verification
Start-Process .\bin\Legram.exe -ArgumentList '--selftest', "$pwd\selftest" -Wait
```

All pull requests must compile with zero errors and pass `--selftest` (`Failures: 0`).
