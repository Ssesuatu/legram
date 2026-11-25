# Security Policy

## Supported Versions

| Version | Supported |
| :--- | :--- |
| `2.7.x` | Yes |
| `< 2.7` | No |

## Privilege Model

- By default, `Legram.exe` runs as a **standard user process** (`asInvoker` manifest) with zero elevated privileges.
- Optional **0-UAC Kernel Standby Purge** registers a Windows Scheduled Task (`Legram_PurgeHelper`) pointing exclusively to the exact `Legram.exe --purge-helper` path approved during UAC registration. Ensure `Legram.exe` is placed in a user-controlled or administrator-protected folder (such as `%LOCALAPPDATA%\Programs\Legram` or `C:\Program Files\Legram`) before registering the scheduled task.

## Reporting a Vulnerability

Please report security issues privately via [GitHub Security Advisories](https://github.com/Ssesuatu/legram/security/advisories/new) rather than opening a public issue. Include steps to reproduce and your Windows 10/11 build version.
