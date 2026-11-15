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
    }

    internal sealed class GuardScanResult
    {
        public Dictionary<int, ProcNode> Nodes = new Dictionary<int, ProcNode>();
        public HashSet<int> ProtectedPids = new HashSet<int>();
        public HashSet<int> TcpListenPids = new HashSet<int>();
        public HashSet<int> ActiveAudioPids = new HashSet<int>();
        public int TcpListenerCount;
        public int ForegroundPid;
        public bool Obsidian27124Active;
        public bool WigramActive;
        public bool T3CodeActive;
        public bool RenderAppPresent;
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
        private static readonly HashSet<string> SystemCoreNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "system", "registry", "memcompression", "smss", "csrss", "wininit", "winlogon",
            "services", "lsass", "lsm", "svchost", "fontdrvhost", "dwm", "audiodg",
            "sihost", "taskhostw", "explorer", "spoolsv", "wmiprvse", "conhost",
            "openconsole", "windowsterminal", "powershell", "pwsh", "cmd", "legram", "legaram"
        };

        private static readonly HashSet<string> CreativeRenderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "blender", "afterfx", "adobe premiere pro", "photoshop", "resolve",
            "davinci resolve", "cinema 4d", "unrealEditor", "obs64", "ffmpeg"
        };

        private static readonly HashSet<string> OrphanAiCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "agy_acp_server", "localharness_external", "node_repl"
        };

        public static GuardScanResult Scan(
            Dictionary<int, long> vramByPid,
            HashSet<int> ecoThrottledPids,
            bool gpuRenderingActive)
        {
            GuardScanResult res = new GuardScanResult();
            int selfPid = Process.GetCurrentProcess().Id;

            // 1. Foreground Window PID
            IntPtr fgHwnd = NativeApi.GetForegroundWindow();
            if (fgHwnd != IntPtr.Zero)
            {
                uint fgPid;
                NativeApi.GetWindowThreadProcessId(fgHwnd, out fgPid);
                res.ForegroundPid = (int)fgPid;
            }

            // 2. TCP LISTEN Ports (IPv4 + IPv6) including Obsidian :27124
            ScanTcpListeners(res);

            // 3. CoreAudio Active Stream PIDs (Speaker + Mic)
            CoreAudioShield.CollectActiveAudioPids(res.ActiveAudioPids);

            // 4. Toolhelp32 Snapshot
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
                        }
                        while (NativeApi.Process32NextW(snap, ref pe));
                    }
                }
                finally
                {
                    NativeApi.CloseHandle(snap);
                }
            }

            // 5. Build Self Ancestor Chain (never touch our own parent shell / T3 harness)
            HashSet<int> selfAncestors = new HashSet<int>();
            int cur = selfPid;
            for (int depth = 0; depth < 12 && cur > 4; depth++)
            {
                selfAncestors.Add(cur);
                ProcNode pn;
                if (!res.Nodes.TryGetValue(cur, out pn)) break;
                cur = pn.ParentPid;
            }

            // 6. First-Pass Direct Protection Classification
            HashSet<int> wigramRoots = new HashSet<int>();
            HashSet<int> t3Roots = new HashSet<int>();
            HashSet<string> activeAudioFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ProcNode n in res.Nodes.Values)
            {
                if (n.Pid <= 4 || SystemCoreNames.Contains(n.BaseName) || selfAncestors.Contains(n.Pid))
                {
                    n.Protection |= ProtectionReason.SystemCore;
                }

                if (n.Pid == res.ForegroundPid)
                {
                    n.Protection |= ProtectionReason.ForegroundWindow;
                }

                if (res.TcpListenPids.Contains(n.Pid))
                {
                    n.Protection |= ProtectionReason.LocalTcpListener;
                }

                if (res.ActiveAudioPids.Contains(n.Pid))
                {
                    n.Protection |= ProtectionReason.ActiveAudioStream;
                    if (n.BaseName == "discord" || n.BaseName == "spotify" || n.BaseName == "teams" || n.BaseName == "slack")
                    {
                        activeAudioFamilies.Add(n.BaseName);
                    }
                }

                string pathLow = (n.FullPath ?? "").ToLowerInvariant();

                // Wigram V:\ mount or wigram binary
                if (n.BaseName.Contains("wigram") || pathLow.StartsWith("v:\\") || pathLow.Contains("\\wigram"))
                {
                    n.Protection |= ProtectionReason.WigramMountOrNode;
                    wigramRoots.Add(n.Pid);
                    res.WigramActive = true;
                }

                // Obsidian (:27124 or Obsidian app)
                if (n.BaseName == "obsidian")
                {
                    n.Protection |= ProtectionReason.ObsidianLocalRest;
                }

                // Batereai sibling
                if (n.BaseName == "batereai" || n.BaseName == "t3usage")
                {
                    n.Protection |= ProtectionReason.BatereaiSibling;
                }

                // Hermes_Gateway
                if (n.BaseName.Contains("hermes") || pathLow.Contains("hermes"))
                {
                    n.Protection |= ProtectionReason.HermesGateway;
                }

                // T3 Code / Antigravity / Active Coding Agents
                if (n.BaseName == "t3" || n.BaseName == "t3-code" || n.BaseName.StartsWith("t3 code") ||
                    pathLow.Contains("\\.t3\\") || pathLow.Contains("\\t3-code\\") || pathLow.Contains("t3 code") ||
                    n.BaseName == "codebase-memory-mcp")
                {
                    n.Protection |= ProtectionReason.T3CodeActive;
                    t3Roots.Add(n.Pid);
                    res.T3CodeActive = true;
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
            }

            if (Directory.Exists(@"V:\")) res.WigramActive = true;

            // 7. Second-Pass Descendant & Audio Family Propagation
            foreach (ProcNode n in res.Nodes.Values)
            {
                if (activeAudioFamilies.Contains(n.BaseName))
                {
                    n.Protection |= ProtectionReason.ActiveAudioStream;
                }
                if (HasProtectedRootAncestor(n, res.Nodes, wigramRoots))
                {
                    n.Protection |= ProtectionReason.WigramMountOrNode;
                }
                if (HasProtectedRootAncestor(n, res.Nodes, t3Roots))
                {
                    n.Protection |= ProtectionReason.T3CodeActive;
                }
                if (res.ForegroundPid > 4 && HasAncestorPid(n, res.Nodes, res.ForegroundPid))
                {
                    // Protect foreground app's active helper processes unless it's a huge browser background pool
                    if (n.BaseName != "ego" && n.BaseName != "chrome" && n.BaseName != "msedge")
                    {
                        n.Protection |= ProtectionReason.ForegroundWindow;
                    }
                }
            }

            // 8. Identify Orphan AI Processes & Trimmable Background Processes
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
                    // Realistic trimmable portion of bloated working sets (~38% for browsers, ~28% for general apps)
                    double factor = (n.BaseName == "ego" || n.BaseName == "chrome" || n.BaseName == "msedge" || n.BaseName == "msedgewebview2" || n.BaseName == "brave")
                        ? 0.38 : 0.26;
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
                        if (c.BaseName == "wsl" || c.BaseName == "bash" || c.BaseName == "ubuntu")
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

            BuildGuardTooltipAndGroups(res, ecoThrottledPids);
            return res;
        }

        private static void BuildGuardTooltipAndGroups(GuardScanResult res, HashSet<int> ecoThrottledPids)
        {
            List<string> shieldParts = new List<string>();
            if (res.WigramActive) shieldParts.Add("Virtual Mount");
            if (res.Obsidian27124Active) shieldParts.Add("Local REST (:27124)");
            if (res.T3CodeActive) shieldParts.Add("Coding Agent");
            shieldParts.Add(res.TcpListenerCount + " TCP Listen");
            if (res.ActiveAudioPids.Count > 0) shieldParts.Add(res.ActiveAudioPids.Count + " Active Audio");
            res.GuardTooltipSummary = "Auto-Protected: " + string.Join(" \u00B7 ", shieldParts.ToArray());

            // Aggregate application families
            Dictionary<string, ProcessGroupItem> families = new Dictionary<string, ProcessGroupItem>(StringComparer.OrdinalIgnoreCase);
            ProcessGroupItem protectedDevGroup = new ProcessGroupItem
            {
                GroupKey = "protected-dev",
                DisplayName = "Dev \u00B7 AI \u00B7 Servers",
                IconKind = GroupIconKind.ProtectedShield,
                IsProtected = true,
                Reason = ProtectionReason.WigramMountOrNode | ProtectionReason.T3CodeActive | ProtectionReason.ObsidianLocalRest
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

                // Group core protected dev ecosystem into the signature Protected Shield row
                if ((n.Protection & (ProtectionReason.WigramMountOrNode | ProtectionReason.ObsidianLocalRest |
                    ProtectionReason.BatereaiSibling | ProtectionReason.HermesGateway | ProtectionReason.T3CodeActive)) != 0)
                {
                    protectedDevGroup.ProcessCount++;
                    protectedDevGroup.WorkingSetBytes += n.WorkingSetBytes;
                    protectedDevGroup.DedicatedVramBytes += n.DedicatedVramBytes;
                    protectedDevGroup.Pids.Add(n.Pid);
                    continue;
                }

                // Skip OS low-level daemons from cluttering top user apps list
                if ((n.Protection & ProtectionReason.SystemCore) != 0) continue;

                string key = FamilyKey(n.BaseName);
                ProcessGroupItem grp;
                if (!families.TryGetValue(key, out grp))
                {
                    grp = new ProcessGroupItem
                    {
                        GroupKey = key,
                        DisplayName = FriendlyFamilyName(key, n.ExeFile),
                        RepresentativeExePath = n.FullPath ?? "",
                        IconKind = ResolveIconKind(key),
                        IsProtected = (n.Protection != ProtectionReason.None)
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
                    }
                }

                grp.ProcessCount++;
                grp.WorkingSetBytes += n.WorkingSetBytes;
                grp.DedicatedVramBytes += n.DedicatedVramBytes;
                if (n.Pid == res.ForegroundPid) grp.IsForeground = true;
                if (ecoThrottledPids != null && ecoThrottledPids.Contains(n.Pid)) grp.IsEcoQosActive = true;

                if (n.Protection == ProtectionReason.None)
                {
                    grp.Pids.Add(n.Pid);
                    double factor = IsBrowserKey(key) ? 0.38 : 0.26;
                    grp.EstimTrimmableBytes += (long)(n.WorkingSetBytes * factor);
                    grp.IsProtected = false; // Family has trimmable background workers (e.g., chrome/msedge)
                }
                else if (grp.Pids.Count == 0)
                {
                    grp.IsProtected = true;
                    grp.Reason |= n.Protection;
                }
            }

            // Format subtitles concisely
            foreach (ProcessGroupItem g in families.Values)
            {
                string procWord = g.ProcessCount == 1 ? " proc" : " procs";
                if (g.IsProtected)
                {
                    g.IconKind = GroupIconKind.ProtectedShield;
                    g.Subtitle = g.ProcessCount + procWord + " \u00B7 Protected";
                }
                else
                {
                    string trimPart = g.EstimTrimmableBytes >= 32L * 1024 * 1024
                        ? " \u00B7 +" + Fmt.BytesCompactG(g.EstimTrimmableBytes) + " trimmable"
                        : "";
                    g.Subtitle = g.ProcessCount + procWord + trimPart;
                }
            }

            List<string> devTags = new List<string>();
            if (res.WigramActive) devTags.Add("Virtual Mount");
            if (res.Obsidian27124Active) devTags.Add("REST :27124");
            devTags.Add(res.TcpListenerCount + " TCP");
            protectedDevGroup.Subtitle = string.Join(" \u00B7 ", devTags.ToArray());

            if (orphanGroup.ProcessCount > 0)
            {
                orphanGroup.Subtitle = orphanGroup.ProcessCount + " idle orphan procs \u00B7 Safe to close";
            }

            // Select top 4 rows: Top user families + always include Protected Dev Shield row (+ Orphan AI row if present)
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
            if (baseName == "ego" || baseName.StartsWith("ego_")) return "ego";
            if (baseName == "chrome" || baseName == "googlecrashhandler64") return "chrome";
            if (baseName == "msedge" || baseName == "msedgewebview2") return "msedge";
            if (baseName == "brave") return "brave";
            if (baseName == "firefox") return "firefox";
            return baseName;
        }

        private static bool IsBrowserKey(string key)
        {
            return key == "ego" || key == "chrome" || key == "msedge" || key == "brave" || key == "firefox";
        }

        private static GroupIconKind ResolveIconKind(string key)
        {
            if (IsBrowserKey(key)) return GroupIconKind.Browser;
            if (key == "node" || key == "python" || key == "pythonw" || key == "pwsh" || key == "powershell" || key == "cmd" || key == "bun" || key == "deno" || key == "git")
                return GroupIconKind.TerminalNode;
            if (key == "photoshop" || key == "blender" || key == "premiere" || key == "afterfx" || key == "figma" || key == "figma_agent" || key == "paper" || key == "spotify" || key == "obs64" || key == "vlc")
                return GroupIconKind.CreativeMedia;
            return GroupIconKind.AppCube;
        }

        private static string FriendlyFamilyName(string key, string exeFile)
        {
            switch (key)
            {
                case "ego": return "Ego Browser";
                case "chrome": return "Google Chrome";
                case "msedge": return "Microsoft Edge";
                case "brave": return "Brave Browser";
                case "firefox": return "Mozilla Firefox";
                case "node": return "Node.js Runtime";
                case "python":
                case "pythonw": return "Python Runtime";
                case "pwsh":
                case "powershell": return "PowerShell";
                case "explorer": return "Windows Explorer";
                case "paper": return "Paper Design";
                case "figma_agent":
                case "figma": return "Figma Desktop";
                case "photoshop": return "Adobe Photoshop";
                case "blender": return "Blender 3D";
                case "discord": return "Discord";
                case "spotify": return "Spotify";
                case "telegram": return "Telegram";
                case "whatsapp": return "WhatsApp";
                default:
                    string clean = Path.GetFileNameWithoutExtension(exeFile ?? key);
                    if (string.IsNullOrEmpty(clean)) return key;
                    return char.ToUpperInvariant(clean[0]) + clean.Substring(1);
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
                            int rawPort = Marshal.ReadInt32(rowPtr, 8);
                            int port = ((rawPort & 0xFF) << 8) | ((rawPort >> 8) & 0xFF);
                            int pid = Marshal.ReadInt32(rowPtr, 20);
                            if (pid > 4)
                            {
                                res.TcpListenPids.Add(pid);
                                if (port == 27124) res.Obsidian27124Active = true;
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
                            if (pid > 4) res.TcpListenPids.Add(pid);
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
