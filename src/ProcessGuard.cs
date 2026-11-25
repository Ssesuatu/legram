using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Legram
{
    internal sealed class ProcNode
    {
        public int Pid;
        public int ParentPid;
        public string ExeFile = "";      // e.g. "chrome.exe"
        public string BaseName = "";     // e.g. "chrome" (lowercase)
        public string FullPath = "";
        public long WorkingSetBytes;
        public long DedicatedVramBytes;
        public ProtectionReason Protection;
        public bool IsOrphanAi;
        public int DesktopNumber;
        public bool IsOnActiveDesktop;
        public bool IsOnInactiveDesktop;
    }

    internal sealed class GuardScanResult
    {
        public Dictionary<int, ProcNode> Nodes = new Dictionary<int, ProcNode>();
        public HashSet<int> ProtectedPids = new HashSet<int>();
        public HashSet<int> TcpListenPids = new HashSet<int>();
        public HashSet<int> ActiveAudioPids = new HashSet<int>();
        public HashSet<int> InactiveDesktopSleepPids = new HashSet<int>();
        public HashSet<string> DynamicMultiProcFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public int TcpListenerCount;
        public int ForegroundPid;
        public bool VirtualMountActive;
        public bool DevToolchainActive;
        public bool RenderAppPresent;
        public bool GameShieldActive;
        public string ActiveGameName = "";
        public int UserImmuneGroupCount;
        public int ActiveDesktopNumber = 1;
        public int TotalDesktopCount = 1;
        public string ActiveDesktopName = "Desktop 1";
        public int SleepingDesktopAppCount;
        public string VirtualDesktopTooltipSummary = "Active: Desktop 1";
        public List<int> TrimmablePids = new List<int>();
        public List<int> OrphanAiPids = new List<int>();
        public long WorkingSetTrimmableBytes;
        public long OrphanAiBytes;
        public int DwmPid;
        public long DwmWorkingSetBytes;
        public long DwmVramBytes;
        public string GuardTooltipSummary = "";
        public List<ProcessGroupItem> TopGroups = new List<ProcessGroupItem>();
    }

    internal static class ProcessGuard
    {
        // Includes Windows kernel/shell daemons, network/VPN tunnels, and game anti-cheat services (100% untouchable)
        private static readonly HashSet<string> SystemCoreNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "system", "registry", "memcompression", "smss", "csrss", "wininit", "winlogon",
            "services", "lsass", "lsm", "svchost", "fontdrvhost", "dwm", "audiodg",
            "sihost", "taskhostw", "explorer", "spoolsv", "wmiprvse", "conhost",
            "openconsole", "windowsterminal", "powershell", "pwsh", "cmd", "legram",
            // Network, VPN, Proxy & Tunnel Daemons (protect active connections & routing)
            "wireguard", "openvpn", "tailscaled", "tailscale-ipn", "warp-svc", "cloudflare warp",
            "cloudflared", "ngrok", "mullvad-daemon", "zerotier-one", "sing-box", "clash",
            "v2ray", "xray", "ssh", "ssh-agent", "adb", "wslservice", "wslhost",
            // Anti-Cheat & Gaming Services (never open or touch)
            "vgc", "vgtray", "easyanticheat", "easyanticheat_eos", "beservice", "bedaisy", "eaanticheat.gameservice"
        };

        // Public Developer IDEs, Compilers, LSPs & Container Toolchains (automatically protects their spawned child trees)
        private static readonly HashSet<string> DevToolchainNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "code", "code - insiders", "cursor", "windsurf", "zed", "devenv",
            "rider64", "idea64", "pycharm64", "webstorm64", "goland64", "clion64", "rustrover64", "studio64",
            "claude", "codex", "copilot", "msbuild", "vbcscompiler", "csc", "cl", "link",
            "cargo", "rustc", "rust-analyzer", "go", "gopls", "clangd",
            "docker", "com.docker.backend", "podman", "kubectl"
        };

        private static readonly HashSet<string> VirtualMountDaemonNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "winfsp", "winfsp-x64", "dokan", "dokanctl", "rclone"
        };

        private static readonly HashSet<string> CreativeRenderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "blender", "afterfx", "adobe premiere pro", "photoshop", "resolve",
            "davinci resolve", "cinema 4d", "unrealeditor", "obs64", "ffmpeg"
        };

        private static readonly HashSet<string> NonGameFullscreenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "explorer", "dwm", "shellexperiencehost", "searchhost", "startmenuexperiencehost",
            "lockapp", "textinputhost", "applicationframehost", "legram",
            "taskmgr", "cmd", "powershell", "pwsh", "windowsterminal"
        };

        // Strictly public headless AI model runners when orphaned (dead parent + zero TCP listener)
        private static readonly HashSet<string> OrphanAiCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ollama_llama_server", "llama-server"
        };

        private static readonly object FriendlyNameSync = new object();
        private static readonly Dictionary<string, string> FriendlyNameByPathCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static GuardScanResult Scan(
            Dictionary<int, long> vramByPid,
            HashSet<int> ecoThrottledPids,
            bool gpuRenderingActive,
            HashSet<string> userImmuneGroups = null,
            bool gameModeAutoPause = true,
            VirtualDesktopState vdState = null)
        {
            GuardScanResult res = new GuardScanResult();
            int selfPid = Process.GetCurrentProcess().Id;
            if (userImmuneGroups != null)
            {
                res.UserImmuneGroupCount = userImmuneGroups.Count;
            }
            if (vdState != null)
            {
                res.ActiveDesktopNumber = Math.Max(1, vdState.ActiveDesktopNumber);
                res.TotalDesktopCount = Math.Max(1, vdState.TotalDesktopCount);
                res.ActiveDesktopName = string.IsNullOrEmpty(vdState.ActiveDesktopName)
                    ? ("Desktop " + res.ActiveDesktopNumber)
                    : vdState.ActiveDesktopName;
            }

            // 1. Foreground Window PID + Passive Fullscreen Game Detection
            IntPtr fgHwnd = NativeApi.GetForegroundWindow();
            bool fgCoversFullscreen = false;
            bool fgIsD3dExclusive = false;
            if (fgHwnd != IntPtr.Zero)
            {
                uint fgPid;
                NativeApi.GetWindowThreadProcessId(fgHwnd, out fgPid);
                res.ForegroundPid = (int)fgPid;
                if (gameModeAutoPause && res.ForegroundPid > 4 && res.ForegroundPid != selfPid)
                {
                    fgCoversFullscreen = NativeApi.IsForegroundWindowFullscreen(fgHwnd, out fgIsD3dExclusive);
                }
            }

            // 2. TCP LISTEN Ports (IPv4 + IPv6)
            ScanTcpListeners(res);

            // 3. CoreAudio Active Stream PIDs (Speaker + Mic)
            CoreAudioShield.CollectActiveAudioPids(res.ActiveAudioPids);

            // 4. Virtual / Cloud Mount Drive Letters (via IOCTL_STORAGE_QUERY_PROPERTY bus check)
            HashSet<string> virtualDriveLetters = StorageSweeper.GetVirtualDriveLetters();
            if (virtualDriveLetters.Count > 0)
            {
                res.VirtualMountActive = true;
            }

            // 5. Toolhelp32 Snapshot
            Dictionary<string, int> procCountByFamily = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            IntPtr snap = NativeApi.CreateToolhelp32Snapshot(NativeApi.TH32CS_SNAPPROCESS, 0);
            if (snap != IntPtr.Zero && snap != new IntPtr(-1))
            {
                try
                {
                    NativeApi.PROCESSENTRY32W pe = new NativeApi.PROCESSENTRY32W();
                    pe.dwSize = (uint)Marshal.SizeOf(typeof(NativeApi.PROCESSENTRY32W));
                    if (NativeApi.Process32FirstW(snap, ref pe))
                    {
                        do
                        {
                            int pid = (int)pe.th32ProcessID;
                            if (pid <= 0) continue;
                            string exe = pe.szExeFile ?? "";
                            string baseName = Path.GetFileNameWithoutExtension(exe).ToLowerInvariant();
                            ProcNode node = new ProcNode
                            {
                                Pid = pid,
                                ParentPid = (int)pe.th32ParentProcessID,
                                ExeFile = exe,
                                BaseName = baseName
                            };
                            if (vramByPid != null)
                            {
                                long vb;
                                if (vramByPid.TryGetValue(pid, out vb)) node.DedicatedVramBytes = vb;
                            }
                            QueryMemoryAndPath(node);
                            if (baseName == "dwm")
                            {
                                if (node.WorkingSetBytes <= 0)
                                {
                                    try
                                    {
                                        using (Process dp = Process.GetProcessById(pid))
                                        {
                                            node.WorkingSetBytes = dp.WorkingSet64;
                                        }
                                    }
                                    catch { }
                                }
                                if (node.WorkingSetBytes >= res.DwmWorkingSetBytes)
                                {
                                    res.DwmPid = pid;
                                    res.DwmWorkingSetBytes = node.WorkingSetBytes;
                                    res.DwmVramBytes = node.DedicatedVramBytes;
                                }
                            }
                            res.Nodes[pid] = node;

                            string fKey = FamilyKey(baseName);
                            int fc;
                            if (!procCountByFamily.TryGetValue(fKey, out fc)) fc = 0;
                            procCountByFamily[fKey] = fc + 1;
                        }
                        while (NativeApi.Process32NextW(snap, ref pe));
                    }
                }
                finally
                {
                    NativeApi.CloseHandle(snap);
                }
            }

            foreach (KeyValuePair<string, int> kv in procCountByFamily)
            {
                if (kv.Value >= 5 && !SystemCoreNames.Contains(kv.Key) && !DevToolchainNames.Contains(kv.Key))
                {
                    res.DynamicMultiProcFamilies.Add(kv.Key);
                }
            }

            // 6. Build Self Ancestor Chain (never touch our own parent shell or launcher)
            HashSet<int> selfAncestors = new HashSet<int>();
            int cur = selfPid;
            for (int depth = 0; depth < 12 && cur > 4; depth++)
            {
                selfAncestors.Add(cur);
                ProcNode pn;
                if (!res.Nodes.TryGetValue(cur, out pn)) break;
                cur = pn.ParentPid;
            }

            // 7. First-Pass Direct Protection Classification
            HashSet<int> mountRoots = new HashSet<int>();
            HashSet<int> devRoots = new HashSet<int>();
            HashSet<int> tcpRoots = new HashSet<int>();
            HashSet<int> whitelistedRoots = new HashSet<int>();
            HashSet<string> activeAudioFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ProcNode n in res.Nodes.Values)
            {
                if (n.Pid <= 4 || SystemCoreNames.Contains(n.BaseName) || selfAncestors.Contains(n.Pid))
                {
                    n.Protection |= ProtectionReason.SystemCore;
                }

                string famKey = FamilyKey(n.BaseName);
                bool isMultiProcBrowser = IsBrowserKey(famKey) || res.DynamicMultiProcFamilies.Contains(famKey);

                if (userImmuneGroups != null && (userImmuneGroups.Contains(famKey) || userImmuneGroups.Contains(n.BaseName)))
                {
                    n.Protection |= ProtectionReason.UserWhitelisted;
                    whitelistedRoots.Add(n.Pid);
                }

                if (n.Pid == res.ForegroundPid)
                {
                    n.Protection |= ProtectionReason.ForegroundWindow;

                    // Fullscreen Game Mode Detection (D3D Exclusive or Fullscreen App/Game)
                    if (fgCoversFullscreen && !NonGameFullscreenNames.Contains(n.BaseName))
                    {
                        if (fgIsD3dExclusive || !isMultiProcBrowser || gpuRenderingActive)
                        {
                            n.Protection |= ProtectionReason.GameFullscreen;
                            res.GameShieldActive = true;
                            res.ActiveGameName = FriendlyFamilyName(famKey, n.ExeFile, n.FullPath);
                        }
                    }
                }

                if (res.TcpListenPids.Contains(n.Pid))
                {
                    n.Protection |= ProtectionReason.LocalTcpListener;
                    if (!isMultiProcBrowser && !SystemCoreNames.Contains(n.BaseName))
                    {
                        tcpRoots.Add(n.Pid);
                    }
                }

                if (res.ActiveAudioPids.Contains(n.Pid))
                {
                    n.Protection |= ProtectionReason.ActiveAudioStream;
                    if (n.BaseName == "discord" || n.BaseName == "spotify" || n.BaseName == "teams" || n.BaseName == "slack")
                    {
                        activeAudioFamilies.Add(n.BaseName);
                    }
                }

                // Virtual / Cloud Filesystem Mount Daemons & Executables running off virtual drives
                if (VirtualMountDaemonNames.Contains(n.BaseName) || IsPathOnVirtualDrive(n.FullPath, virtualDriveLetters))
                {
                    n.Protection |= ProtectionReason.VirtualMountDaemon;
                    mountRoots.Add(n.Pid);
                    res.VirtualMountActive = true;
                }

                // Developer IDEs, Compilers, LSPs & Container Toolchains
                if (DevToolchainNames.Contains(n.BaseName))
                {
                    n.Protection |= ProtectionReason.DevToolchain;
                    devRoots.Add(n.Pid);
                    res.DevToolchainActive = true;
                }

                // Creative Render Shield
                if (CreativeRenderNames.Contains(n.BaseName))
                {
                    res.RenderAppPresent = true;
                    if (gpuRenderingActive || n.Pid == res.ForegroundPid)
                    {
                        n.Protection |= ProtectionReason.CreativeRender;
                    }
                }

                if (vdState != null)
                {
                    int deskNum;
                    if (vdState.PidPrimaryDesktopNumber.TryGetValue(n.Pid, out deskNum))
                    {
                        n.DesktopNumber = deskNum;
                    }
                    if (vdState.PidsOnActiveDesktop.Contains(n.Pid))
                    {
                        n.IsOnActiveDesktop = true;
                    }
                    else if (vdState.PidsOnInactiveDesktops.Contains(n.Pid))
                    {
                        n.IsOnInactiveDesktop = true;
                    }
                }
            }

            // 8. Second-Pass Descendant, Audio Family & Virtual Desktop Propagation
            foreach (ProcNode n in res.Nodes.Values)
            {
                if (activeAudioFamilies.Contains(n.BaseName))
                {
                    n.Protection |= ProtectionReason.ActiveAudioStream;
                }
                if (HasProtectedRootAncestor(n, res.Nodes, whitelistedRoots))
                {
                    n.Protection |= ProtectionReason.UserWhitelisted;
                }
                if (HasProtectedRootAncestor(n, res.Nodes, mountRoots))
                {
                    n.Protection |= ProtectionReason.VirtualMountDaemon;
                }
                if (HasProtectedRootAncestor(n, res.Nodes, devRoots))
                {
                    n.Protection |= ProtectionReason.DevToolchain;
                }
                if (HasProtectedRootAncestor(n, res.Nodes, tcpRoots))
                {
                    n.Protection |= ProtectionReason.LocalTcpListener;
                }

                string famKey = FamilyKey(n.BaseName);
                bool isMultiProcBrowser = IsBrowserKey(famKey) || res.DynamicMultiProcFamilies.Contains(famKey);

                if (res.ForegroundPid > 4 && HasAncestorPid(n, res.Nodes, res.ForegroundPid))
                {
                    if (res.GameShieldActive)
                    {
                        n.Protection |= ProtectionReason.GameFullscreen;
                    }
                    // Protect foreground app's active helper processes unless it's a huge browser background pool
                    if (!isMultiProcBrowser)
                    {
                        n.Protection |= ProtectionReason.ForegroundWindow;
                    }
                }

                // Propagate Virtual Desktop ownership from windowed parent to child worker processes
                if (vdState != null && !n.IsOnActiveDesktop && !n.IsOnInactiveDesktop)
                {
                    int curP = n.ParentPid;
                    for (int depth = 0; depth < 6 && curP > 4; depth++)
                    {
                        ProcNode anc;
                        if (!res.Nodes.TryGetValue(curP, out anc)) break;
                        if (anc.IsOnActiveDesktop)
                        {
                            n.IsOnActiveDesktop = true;
                            n.DesktopNumber = anc.DesktopNumber;
                            break;
                        }
                        if (anc.IsOnInactiveDesktop)
                        {
                            n.IsOnInactiveDesktop = true;
                            n.DesktopNumber = anc.DesktopNumber;
                            break;
                        }
                        curP = anc.ParentPid;
                    }
                }

                if (n.IsOnInactiveDesktop && !n.IsOnActiveDesktop && n.Protection == ProtectionReason.None)
                {
                    res.InactiveDesktopSleepPids.Add(n.Pid);
                }
            }

            // 9. Identify Orphan AI Processes & Trimmable Background Processes
            foreach (ProcNode n in res.Nodes.Values)
            {
                bool parentAlive = res.Nodes.ContainsKey(n.ParentPid);
                if (OrphanAiCandidates.Contains(n.BaseName) && !parentAlive && n.Protection == ProtectionReason.None)
                {
                    n.IsOrphanAi = true;
                    res.OrphanAiPids.Add(n.Pid);
                    res.OrphanAiBytes += n.WorkingSetBytes;
                    continue;
                }

                if (n.Protection != ProtectionReason.None)
                {
                    res.ProtectedPids.Add(n.Pid);
                    continue;
                }

                // Eligible for safe user-mode Working Set Trim if WorkingSet >= 12 MB
                if (n.WorkingSetBytes >= 12L * 1024 * 1024)
                {
                    res.TrimmablePids.Add(n.Pid);
                    string fKey = FamilyKey(n.BaseName);
                    bool isBrowserOrMulti = IsBrowserKey(fKey) || res.DynamicMultiProcFamilies.Contains(fKey);
                    double factor = isBrowserOrMulti ? 0.38 : 0.26;
                    res.WorkingSetTrimmableBytes += (long)(n.WorkingSetBytes * factor);
                }
            }

            // Check idle vmmemWSL
            foreach (ProcNode n in res.Nodes.Values)
            {
                if ((n.BaseName == "vmmemwsl" || n.BaseName == "vmmem") && n.WorkingSetBytes > 64L * 1024 * 1024)
                {
                    bool hasActiveWslClient = false;
                    foreach (ProcNode c in res.Nodes.Values)
                    {
                        if (c.BaseName == "wsl" || c.BaseName == "bash" || c.BaseName == "ubuntu" || c.BaseName == "debian" || c.BaseName == "docker" || c.BaseName == "com.docker.backend")
                        {
                            hasActiveWslClient = true;
                            break;
                        }
                    }
                    if (!hasActiveWslClient)
                    {
                        res.OrphanAiBytes += n.WorkingSetBytes;
                    }
                }
            }

            BuildGuardTooltipAndGroups(res, ecoThrottledPids, userImmuneGroups, vdState);
            return res;
        }

        private static bool IsPathOnVirtualDrive(string fullPath, HashSet<string> virtualDriveLetters)
        {
            if (string.IsNullOrEmpty(fullPath) || virtualDriveLetters == null || virtualDriveLetters.Count == 0)
                return false;
            if (fullPath.Length >= 2 && fullPath[1] == ':')
            {
                return virtualDriveLetters.Contains(fullPath.Substring(0, 2));
            }
            return false;
        }

        private static void BuildGuardTooltipAndGroups(
            GuardScanResult res,
            HashSet<int> ecoThrottledPids,
            HashSet<string> userImmuneGroups,
            VirtualDesktopState vdState)
        {
            List<string> shieldParts = new List<string>();
            if (res.GameShieldActive)
            {
                shieldParts.Add("Game Shield (" + (string.IsNullOrEmpty(res.ActiveGameName) ? "Fullscreen" : res.ActiveGameName) + " \u2014 Auto-Purge Paused)");
            }
            if (res.VirtualMountActive) shieldParts.Add("Virtual Mount");
            if (res.DevToolchainActive) shieldParts.Add("Dev Toolchain");
            shieldParts.Add(res.TcpListenerCount + " TCP Listen");
            if (res.ActiveAudioPids.Count > 0) shieldParts.Add(res.ActiveAudioPids.Count + " Active Audio");
            if (userImmuneGroups != null && userImmuneGroups.Count > 0)
            {
                shieldParts.Add(userImmuneGroups.Count + " User Immune");
            }
            res.GuardTooltipSummary = "Auto-Protected: " + string.Join(" \u00B7 ", shieldParts.ToArray());

            // Aggregate application families
            Dictionary<string, ProcessGroupItem> families = new Dictionary<string, ProcessGroupItem>(StringComparer.OrdinalIgnoreCase);
            ProcessGroupItem protectedDevGroup = new ProcessGroupItem
            {
                GroupKey = "protected-dev",
                DisplayName = "Dev \u00B7 AI \u00B7 Servers",
                IconKind = GroupIconKind.ProtectedShield,
                IsProtected = true,
                Reason = ProtectionReason.VirtualMountDaemon | ProtectionReason.DevToolchain | ProtectionReason.LocalTcpListener
            };

            ProcessGroupItem orphanGroup = new ProcessGroupItem
            {
                GroupKey = "orphan-ai",
                DisplayName = "Orphan AI & Idle WSL",
                IconKind = GroupIconKind.AiSparkle,
                IsOrphanGroup = true
            };

            foreach (ProcNode n in res.Nodes.Values)
            {
                if (n.Pid <= 4) continue;

                if (n.IsOrphanAi || n.BaseName == "vmmemwsl")
                {
                    orphanGroup.ProcessCount++;
                    orphanGroup.WorkingSetBytes += n.WorkingSetBytes;
                    orphanGroup.EstimTrimmableBytes += n.WorkingSetBytes;
                    if (n.IsOrphanAi) orphanGroup.Pids.Add(n.Pid);
                    continue;
                }

                string key = FamilyKey(n.BaseName);
                bool isUserImmune = userImmuneGroups != null && (userImmuneGroups.Contains(key) || userImmuneGroups.Contains(n.BaseName));

                // Group core protected dev/mount/server ecosystem into the signature Protected Shield row (unless user explicitly whitelisted a specific desktop app row)
                if (!isUserImmune && (n.Protection & (ProtectionReason.VirtualMountDaemon | ProtectionReason.DevToolchain | ProtectionReason.LocalTcpListener)) != 0
                    && !IsBrowserKey(key) && !res.DynamicMultiProcFamilies.Contains(key))
                {
                    protectedDevGroup.ProcessCount++;
                    protectedDevGroup.WorkingSetBytes += n.WorkingSetBytes;
                    protectedDevGroup.DedicatedVramBytes += n.DedicatedVramBytes;
                    protectedDevGroup.Pids.Add(n.Pid);
                    continue;
                }

                // Skip OS low-level daemons from cluttering top user apps list
                if ((n.Protection & ProtectionReason.SystemCore) != 0) continue;

                ProcessGroupItem grp;
                if (!families.TryGetValue(key, out grp))
                {
                    bool isBrowserOrMulti = IsBrowserKey(key) || res.DynamicMultiProcFamilies.Contains(key);
                    grp = new ProcessGroupItem
                    {
                        GroupKey = key,
                        DisplayName = FriendlyFamilyName(key, n.ExeFile, n.FullPath),
                        RepresentativeExePath = n.FullPath ?? "",
                        IconKind = isBrowserOrMulti ? GroupIconKind.Browser : ResolveIconKind(key),
                        IsProtected = (n.Protection != ProtectionReason.None) || isUserImmune,
                        IsUserWhitelisted = isUserImmune
                    };
                    families[key] = grp;
                }
                else if (!string.IsNullOrEmpty(n.FullPath))
                {
                    // Prefer primary browser/app binary path over helper/crashhandler paths for crisp native icon extraction
                    if (string.IsNullOrEmpty(grp.RepresentativeExePath)
                        || (n.BaseName == key && !grp.RepresentativeExePath.EndsWith("\\" + key + ".exe", StringComparison.OrdinalIgnoreCase)))
                    {
                        grp.RepresentativeExePath = n.FullPath;
                        grp.DisplayName = FriendlyFamilyName(key, n.ExeFile, n.FullPath);
                    }
                }

                if (isUserImmune)
                {
                    grp.IsUserWhitelisted = true;
                    grp.IsProtected = true;
                    grp.Reason |= ProtectionReason.UserWhitelisted;
                }

                grp.ProcessCount++;
                grp.WorkingSetBytes += n.WorkingSetBytes;
                grp.DedicatedVramBytes += n.DedicatedVramBytes;
                if (n.Pid == res.ForegroundPid) grp.IsForeground = true;
                if (!isUserImmune && ecoThrottledPids != null && ecoThrottledPids.Contains(n.Pid)) grp.IsEcoQosActive = true;

                if (n.DesktopNumber > 0)
                {
                    if (n.IsOnActiveDesktop)
                    {
                        grp.PrimaryDesktopNumber = n.DesktopNumber;
                        grp.IsOnInactiveDesktop = false;
                    }
                    else if (grp.PrimaryDesktopNumber == 0)
                    {
                        grp.PrimaryDesktopNumber = n.DesktopNumber;
                        grp.IsOnInactiveDesktop = n.IsOnInactiveDesktop;
                    }
                }

                if (n.Protection == ProtectionReason.None && !isUserImmune)
                {
                    grp.Pids.Add(n.Pid);
                    double factor = (IsBrowserKey(key) || res.DynamicMultiProcFamilies.Contains(key)) ? 0.38 : 0.26;
                    grp.EstimTrimmableBytes += (long)(n.WorkingSetBytes * factor);
                    grp.IsProtected = false;
                }
                else if (grp.Pids.Count == 0 || isUserImmune)
                {
                    grp.IsProtected = true;
                    grp.Reason |= n.Protection;
                }
            }

            int sleepingAppCount = 0;
            List<string> sleepingAppNames = new List<string>();
            foreach (ProcessGroupItem g in families.Values)
            {
                string procWord = g.ProcessCount == 1 ? " proc" : " procs";
                string deskTag = g.PrimaryDesktopNumber > 0
                    ? (" \u00B7 Desk " + g.PrimaryDesktopNumber + (g.IsOnInactiveDesktop ? " (Sleep)" : ""))
                    : "";
                if (g.IsOnInactiveDesktop && !g.IsProtected && !g.IsUserWhitelisted)
                {
                    sleepingAppCount++;
                    if (sleepingAppNames.Count < 4)
                    {
                        sleepingAppNames.Add(g.DisplayName + " (D" + g.PrimaryDesktopNumber + ")");
                    }
                }

                if (g.IsUserWhitelisted)
                {
                    g.Subtitle = g.ProcessCount + procWord + deskTag + " \u00B7 Immune (User Shield) \u00B7 Click to manage";
                }
                else if (g.IsProtected)
                {
                    g.IconKind = GroupIconKind.ProtectedShield;
                    g.Subtitle = g.ProcessCount + procWord + deskTag + " \u00B7 Protected \u00B7 Click to manage";
                }
                else
                {
                    string trimPart = g.EstimTrimmableBytes >= 32L * 1024 * 1024
                        ? " \u00B7 +" + Fmt.BytesCompactG(g.EstimTrimmableBytes) + " trimmable"
                        : "";
                    g.Subtitle = g.ProcessCount + procWord + deskTag + trimPart + " \u00B7 Click for Shield/Trim";
                }
            }
            res.SleepingDesktopAppCount = sleepingAppCount;

            StringBuilder vdTip = new StringBuilder();
            vdTip.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                "Active Virtual Desktop: #{0} of {1} ({2})\n",
                res.ActiveDesktopNumber, res.TotalDesktopCount, res.ActiveDesktopName);
            if (vdState != null && vdState.DesktopNamesByNumber.Count > 1)
            {
                List<string> deskLabels = new List<string>();
                for (int d = 1; d <= res.TotalDesktopCount; d++)
                {
                    string dName;
                    if (!vdState.DesktopNamesByNumber.TryGetValue(d, out dName) || string.IsNullOrEmpty(dName))
                        dName = "Desktop " + d;
                    deskLabels.Add(d == res.ActiveDesktopNumber ? ("[" + d + ": " + dName + " \u2605]") : (d + ": " + dName));
                }
                vdTip.AppendLine("Workspaces: " + string.Join(" \u00B7 ", deskLabels.ToArray()));
            }
            if (sleepingAppCount > 0)
            {
                vdTip.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                    "Eco-Sleeping on Other Desktops ({0}): {1}\n",
                    sleepingAppCount, string.Join(", ", sleepingAppNames.ToArray()));
            }
            else if (res.TotalDesktopCount > 1)
            {
                vdTip.AppendLine("Other Desktops: Quiet (All background power routed to Active Desktop)");
            }
            vdTip.Append("Smooth Transition: 1.2s settle delay after Win+Ctrl+Arrow \u00B7 Instant wake on return");
            res.VirtualDesktopTooltipSummary = vdTip.ToString();

            List<string> devTags = new List<string>();
            if (res.VirtualMountActive) devTags.Add("Virtual Mount");
            if (res.DevToolchainActive) devTags.Add("Dev Tools");
            devTags.Add(res.TcpListenerCount + " TCP");
            protectedDevGroup.Subtitle = string.Join(" \u00B7 ", devTags.ToArray());

            if (orphanGroup.ProcessCount > 0)
            {
                orphanGroup.Subtitle = orphanGroup.ProcessCount + " idle orphan procs \u00B7 Safe to close";
            }

            // Select top 4 rows: Top user families + Protected Dev Shield row (+ Orphan AI row if present)
            List<ProcessGroupItem> sorted = new List<ProcessGroupItem>(families.Values);
            sorted.Sort(delegate(ProcessGroupItem a, ProcessGroupItem b) { return b.WorkingSetBytes.CompareTo(a.WorkingSetBytes); });

            List<ProcessGroupItem> top = new List<ProcessGroupItem>();
            int slotsForApps = (orphanGroup.ProcessCount > 0 && orphanGroup.WorkingSetBytes >= 20L * 1024 * 1024) ? 2 : 3;
            for (int i = 0; i < sorted.Count && top.Count < slotsForApps; i++)
            {
                if (sorted[i].WorkingSetBytes < 15L * 1024 * 1024) continue;
                top.Add(sorted[i]);
            }

            if (protectedDevGroup.ProcessCount > 0)
            {
                top.Add(protectedDevGroup);
            }

            if (orphanGroup.ProcessCount > 0 && orphanGroup.WorkingSetBytes >= 20L * 1024 * 1024)
            {
                top.Add(orphanGroup);
            }

            for (int i = slotsForApps; i < sorted.Count && top.Count < 4; i++)
            {
                if (!top.Contains(sorted[i])) top.Add(sorted[i]);
            }

            res.TopGroups = top;
        }

        private static string FamilyKey(string baseName)
        {
            if (baseName == "chrome" || baseName == "googlecrashhandler64") return "chrome";
            if (baseName == "msedge" || baseName == "msedgewebview2") return "msedge";
            if (baseName == "brave") return "brave";
            if (baseName == "firefox") return "firefox";
            if (baseName == "vivaldi") return "vivaldi";
            if (baseName == "opera") return "opera";
            if (baseName == "lm studio" || baseName == "lms") return "lm studio";
            if (baseName == "ollama" || baseName == "ollama app" || baseName == "ollama_llama_server") return "ollama";
            if (baseName == "llama-server" || baseName == "llama-cli") return "llama-server";
            return baseName;
        }

        internal static bool IsBrowserKey(string key)
        {
            return GpuVramMonitor.IsBrowserProcess(key);
        }

        private static GroupIconKind ResolveIconKind(string key)
        {
            if (IsBrowserKey(key)) return GroupIconKind.Browser;
            if (key == "lm studio" || key == "lms" || key == "ollama" || key == "llama-server")
                return GroupIconKind.AiSparkle;
            if (key == "node" || key == "python" || key == "pythonw" || key == "pwsh" || key == "powershell" || key == "cmd" || key == "bun" || key == "deno" || key == "git")
                return GroupIconKind.TerminalNode;
            if (key == "photoshop" || key == "blender" || key == "premiere" || key == "afterfx" || key == "figma" || key == "figma_agent" || key == "spotify" || key == "obs64" || key == "vlc")
                return GroupIconKind.CreativeMedia;
            return GroupIconKind.AppCube;
        }

        private static string FriendlyFamilyName(string key, string exeFile, string fullPath)
        {
            switch (key)
            {
                case "chrome": return "Google Chrome";
                case "msedge": return "Microsoft Edge";
                case "brave": return "Brave Browser";
                case "firefox": return "Mozilla Firefox";
                case "vivaldi": return "Vivaldi";
                case "opera": return "Opera";
                case "arc": return "Arc Browser";
                case "zen": return "Zen Browser";
                case "code": return "Visual Studio Code";
                case "devenv": return "Visual Studio";
                case "cursor": return "Cursor";
                case "lm studio":
                case "lms": return "LM Studio";
                case "ollama": return "Ollama";
                case "llama-server": return "llama.cpp Server";
                case "node": return "Node.js Runtime";
                case "python":
                case "pythonw": return "Python Runtime";
                case "pwsh":
                case "powershell": return "PowerShell";
                case "explorer": return "Windows Explorer";
                case "figma_agent":
                case "figma": return "Figma Desktop";
                case "photoshop": return "Adobe Photoshop";
                case "blender": return "Blender 3D";
                case "discord": return "Discord";
                case "spotify": return "Spotify";
                case "telegram": return "Telegram";
                case "whatsapp": return "WhatsApp";
                default:
                    string fromVer = TryReadFriendlyExeDescription(fullPath);
                    if (!string.IsNullOrEmpty(fromVer)) return fromVer;
                    string clean = Path.GetFileNameWithoutExtension(exeFile ?? key);
                    if (string.IsNullOrEmpty(clean)) return key;
                    return char.ToUpperInvariant(clean[0]) + clean.Substring(1);
            }
        }

        private static string TryReadFriendlyExeDescription(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return "";
            lock (FriendlyNameSync)
            {
                string cached;
                if (FriendlyNameByPathCache.TryGetValue(fullPath, out cached)) return cached;
                if (FriendlyNameByPathCache.Count > 96) FriendlyNameByPathCache.Clear();

                string resolved = "";
                try
                {
                    if (File.Exists(fullPath))
                    {
                        FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(fullPath);
                        string candidate = !string.IsNullOrEmpty(fvi.FileDescription)
                            ? fvi.FileDescription.Trim()
                            : (fvi.ProductName != null ? fvi.ProductName.Trim() : "");
                        if (candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            candidate = candidate.Substring(0, candidate.Length - 4).Trim();
                        }
                        if (!string.IsNullOrEmpty(candidate) && candidate.Length <= 36)
                        {
                            resolved = char.ToUpperInvariant(candidate[0]) + candidate.Substring(1);
                        }
                    }
                }
                catch { }
                FriendlyNameByPathCache[fullPath] = resolved;
                return resolved;
            }
        }

        private static bool HasProtectedRootAncestor(ProcNode node, Dictionary<int, ProcNode> map, HashSet<int> roots)
        {
            if (roots.Count == 0) return false;
            int cur = node.ParentPid;
            for (int depth = 0; depth < 8 && cur > 4; depth++)
            {
                if (roots.Contains(cur)) return true;
                ProcNode p;
                if (!map.TryGetValue(cur, out p)) break;
                cur = p.ParentPid;
            }
            return false;
        }

        private static bool HasAncestorPid(ProcNode node, Dictionary<int, ProcNode> map, int targetPid)
        {
            int cur = node.ParentPid;
            for (int depth = 0; depth < 6 && cur > 4; depth++)
            {
                if (cur == targetPid) return true;
                ProcNode p;
                if (!map.TryGetValue(cur, out p)) break;
                cur = p.ParentPid;
            }
            return false;
        }

        private static void QueryMemoryAndPath(ProcNode node)
        {
            IntPtr h = NativeApi.OpenProcess(NativeApi.PROCESS_QUERY_LIMITED_INFORMATION, false, node.Pid);
            if (h == IntPtr.Zero) return;
            try
            {
                NativeApi.PROCESS_MEMORY_COUNTERS pmc;
                if (NativeApi.K32GetProcessMemoryInfo(h, out pmc, (uint)Marshal.SizeOf(typeof(NativeApi.PROCESS_MEMORY_COUNTERS))))
                {
                    node.WorkingSetBytes = (long)(ulong)pmc.WorkingSetSize;
                }
                StringBuilder sb = new StringBuilder(512);
                uint cap = (uint)sb.Capacity;
                if (NativeApi.QueryFullProcessImageNameW(h, 0, sb, ref cap))
                {
                    node.FullPath = sb.ToString();
                }
            }
            finally
            {
                NativeApi.CloseHandle(h);
            }
        }

        private static void ScanTcpListeners(GuardScanResult res)
        {
            // IPv4 TCP_TABLE_OWNER_PID_LISTENER = 3, AF_INET = 2
            int size = 0;
            NativeApi.GetExtendedTcpTable(IntPtr.Zero, ref size, false, 2, 3, 0);
            if (size > 0)
            {
                IntPtr buf = Marshal.AllocHGlobal(size);
                try
                {
                    if (NativeApi.GetExtendedTcpTable(buf, ref size, false, 2, 3, 0) == 0)
                    {
                        int count = Marshal.ReadInt32(buf);
                        IntPtr rowPtr = new IntPtr(buf.ToInt64() + 4);
                        for (int i = 0; i < count; i++)
                        {
                            // MIB_TCPROW_OWNER_PID: state(4), localAddr(4), localPort(4), remoteAddr(4), remotePort(4), owningPid(4) = 24 bytes
                            int pid = Marshal.ReadInt32(rowPtr, 20);
                            if (pid > 4)
                            {
                                res.TcpListenPids.Add(pid);
                            }
                            rowPtr = new IntPtr(rowPtr.ToInt64() + 24);
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buf);
                }
            }

            // IPv6 TCP_TABLE_OWNER_PID_LISTENER = 3, AF_INET6 = 23
            int size6 = 0;
            NativeApi.GetExtendedTcpTable(IntPtr.Zero, ref size6, false, 23, 3, 0);
            if (size6 > 0)
            {
                IntPtr buf6 = Marshal.AllocHGlobal(size6);
                try
                {
                    if (NativeApi.GetExtendedTcpTable(buf6, ref size6, false, 23, 3, 0) == 0)
                    {
                        int count = Marshal.ReadInt32(buf6);
                        IntPtr rowPtr = new IntPtr(buf6.ToInt64() + 4);
                        for (int i = 0; i < count; i++)
                        {
                            // MIB_TCP6ROW_OWNER_PID: localAddr(16), localScope(4), localPort(4), remoteAddr(16), remoteScope(4), remotePort(4), state(4), owningPid(4) = 56 bytes
                            int pid = Marshal.ReadInt32(rowPtr, 52);
                            if (pid > 4)
                            {
                                res.TcpListenPids.Add(pid);
                            }
                            rowPtr = new IntPtr(rowPtr.ToInt64() + 56);
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buf6);
                }
            }

            res.TcpListenerCount = res.TcpListenPids.Count;
        }
    }
}
