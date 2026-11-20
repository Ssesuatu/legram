using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Legram
{
    internal sealed class EcoQosManager : IDisposable
    {
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

        // Real-time voice/chat apps (discord, slack, teams) are intentionally excluded so WebRTC/WebSocket heartbeats never lag
        private static readonly HashSet<string> ThrottleTargetFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ego", "chrome", "msedge", "msedgewebview2", "brave", "firefox",
            "adobecollabsync", "creative cloud helper", "onedrive",
            "steamwebhelper", "epicwebhelper", "widgets", "widgetservice", "searchhost"
        };

        private static readonly HashSet<string> RealtimeVoiceExclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "discord", "slack", "teams", "zoom", "obs64", "audiodg"
        };

        private struct RestoreItem
        {
            public int Pid;
            public uint PriorityClass;
            public uint MemoryPriority;
        }

        private readonly object sync = new object();
        private readonly HashSet<int> throttledPids = new HashSet<int>();
        private readonly Dictionary<int, uint> originalPriorityByPid = new Dictionary<int, uint>();
        private readonly Dictionary<int, uint> originalMemPriorityByPid = new Dictionary<int, uint>();
        private readonly Dictionary<int, int> lastParentByPid = new Dictionary<int, int>();
        private readonly Dictionary<int, string> lastBaseNameByPid = new Dictionary<int, string>();
        private NativeApi.WinEventDelegate hookDelegate;
        private IntPtr hookHandle = IntPtr.Zero;
        private bool enabled = true;
        private bool virtualDesktopSleep = true;
        private int currentForegroundPid;

        public event EventHandler ForegroundSwitched;

        public bool Enabled
        {
            get { return enabled; }
            set
            {
                if (enabled == value) return;
                enabled = value;
                if (!enabled) RestoreAll();
            }
        }

        public bool VirtualDesktopSleep
        {
            get { return virtualDesktopSleep; }
            set { virtualDesktopSleep = value; }
        }

        public bool IsPidThrottled(int pid)
        {
            lock (sync)
            {
                return throttledPids.Contains(pid);
            }
        }

        public void Start()
        {
            if (hookHandle != IntPtr.Zero) return;
            hookDelegate = new NativeApi.WinEventDelegate(OnWinEvent);
            hookHandle = NativeApi.SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, hookDelegate, 0, 0,
                WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

            IntPtr fg = NativeApi.GetForegroundWindow();
            if (fg != IntPtr.Zero)
            {
                uint pid;
                NativeApi.GetWindowThreadProcessId(fg, out pid);
                currentForegroundPid = (int)pid;
            }
        }

        private void OnWinEvent(
            IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject,
            int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (hwnd == IntPtr.Zero) return;
            uint pid;
            NativeApi.GetWindowThreadProcessId(hwnd, out pid);
            int fgPid = (int)pid;
            if (fgPid <= 4) return;

            bool changedPid = (fgPid != currentForegroundPid);
            currentForegroundPid = fgPid;

            // Immediately unthrottle ONLY previously throttled PIDs belonging to the newly focused process (< 1 ms).
            // Never open or modify unthrottled PIDs (100% Anti-Cheat & High-Priority Game safe).
            List<RestoreItem> toRestore = null;
            lock (sync)
            {
                bool wasThrottled = throttledPids.Contains(fgPid);
                if (wasThrottled)
                {
                    toRestore = new List<RestoreItem>();
                    toRestore.Add(PopRestoreItemLocked(fgPid));
                }

                if (changedPid && throttledPids.Count > 0)
                {
                    string fgBase;
                    lastBaseNameByPid.TryGetValue(fgPid, out fgBase);
                    int fgParent;
                    lastParentByPid.TryGetValue(fgPid, out fgParent);

                    List<int> relatedPids = null;
                    foreach (int tPid in throttledPids)
                    {
                        int tParent;
                        lastParentByPid.TryGetValue(tPid, out tParent);
                        string tBase;
                        lastBaseNameByPid.TryGetValue(tPid, out tBase);

                        bool match = false;
                        if (tParent == fgPid)
                        {
                            match = true;
                        }
                        else if (fgParent > 4 && tPid == fgParent)
                        {
                            match = true;
                        }
                        else if (!string.IsNullOrEmpty(fgBase)
                            && string.Equals(tBase, fgBase, StringComparison.OrdinalIgnoreCase))
                        {
                            match = true;
                        }
                        else if (HasAncestorPidLocked(tPid, fgPid))
                        {
                            match = true;
                        }

                        if (match)
                        {
                            if (relatedPids == null) relatedPids = new List<int>();
                            relatedPids.Add(tPid);
                        }
                    }

                    if (relatedPids != null)
                    {
                        if (toRestore == null) toRestore = new List<RestoreItem>(relatedPids.Count);
                        for (int i = 0; i < relatedPids.Count; i++)
                        {
                            toRestore.Add(PopRestoreItemLocked(relatedPids[i]));
                        }
                    }
                }
            }

            if (toRestore != null)
            {
                for (int i = 0; i < toRestore.Count; i++)
                {
                    RestoreProcessQos(toRestore[i].Pid, toRestore[i].PriorityClass, toRestore[i].MemoryPriority);
                }
            }

            EventHandler ev = ForegroundSwitched;
            if (ev != null) ev(this, EventArgs.Empty);
        }

        public HashSet<int> ReconcileAndSnapshot(GuardScanResult guardScan)
        {
            HashSet<int> copy = new HashSet<int>();
            if (!enabled || guardScan == null)
            {
                return copy;
            }

            string fgBaseName = "";
            ProcNode fgNode;
            if (guardScan.ForegroundPid > 4 && guardScan.Nodes.TryGetValue(guardScan.ForegroundPid, out fgNode))
            {
                fgBaseName = fgNode.BaseName;
            }

            List<int> toThrottle = new List<int>();
            List<RestoreItem> toRestore = new List<RestoreItem>();

            lock (sync)
            {
                // Refresh lightweight parent/name lookup for <1 ms OnWinEvent sibling/child wake
                lastParentByPid.Clear();
                lastBaseNameByPid.Clear();
                foreach (ProcNode node in guardScan.Nodes.Values)
                {
                    lastParentByPid[node.Pid] = node.ParentPid;
                    lastBaseNameByPid[node.Pid] = node.BaseName ?? "";
                }

                // Prune exited processes or newly protected / foreground / woken active-desktop processes
                List<int> deadPids = new List<int>();
                List<int> wokenPids = new List<int>();
                foreach (int pid in throttledPids)
                {
                    ProcNode n;
                    if (!guardScan.Nodes.TryGetValue(pid, out n))
                    {
                        deadPids.Add(pid);
                    }
                    else if (n.Protection != ProtectionReason.None || pid == guardScan.ForegroundPid)
                    {
                        wokenPids.Add(pid);
                    }
                    else if (n.IsOnActiveDesktop && !ThrottleTargetFamilies.Contains(n.BaseName))
                    {
                        // Instant Wake when user switches back to this app's Virtual Desktop!
                        wokenPids.Add(pid);
                    }
                }
                for (int i = 0; i < deadPids.Count; i++)
                {
                    int dp = deadPids[i];
                    throttledPids.Remove(dp);
                    originalPriorityByPid.Remove(dp);
                    originalMemPriorityByPid.Remove(dp);
                }
                for (int i = 0; i < wokenPids.Count; i++)
                {
                    toRestore.Add(PopRestoreItemLocked(wokenPids[i]));
                }

                // Apply EcoQoS to background heavy target processes + any unprotected app sleeping on an inactive Virtual Desktop
                foreach (ProcNode n in guardScan.Nodes.Values)
                {
                    if (n.Pid <= 4 || n.Protection != ProtectionReason.None) continue;
                    if (n.Pid == guardScan.ForegroundPid) continue;
                    if (RealtimeVoiceExclude.Contains(n.BaseName)) continue;

                    bool onOtherDesktop = virtualDesktopSleep
                        && guardScan.TotalDesktopCount > 1
                        && n.IsOnInactiveDesktop
                        && !n.IsOnActiveDesktop;

                    if (!ThrottleTargetFamilies.Contains(n.BaseName) && !onOtherDesktop) continue;

                    // Foreground Browser/App Tree Freedom: never throttle ANY process (root, GPU, renderer, utility, or child WebView2) of the active foreground browser/app
                    bool isForegroundFamily = !onOtherDesktop
                        && !string.IsNullOrEmpty(fgBaseName)
                        && (n.BaseName == fgBaseName
                            || (n.BaseName == "msedgewebview2"
                                && guardScan.ForegroundPid > 4
                                && HasAncestorPid(n, guardScan.Nodes, guardScan.ForegroundPid)));
                    if (isForegroundFamily)
                    {
                        if (throttledPids.Contains(n.Pid))
                        {
                            toRestore.Add(PopRestoreItemLocked(n.Pid));
                        }
                        continue;
                    }

                    if (!throttledPids.Contains(n.Pid))
                    {
                        throttledPids.Add(n.Pid);
                        toThrottle.Add(n.Pid);
                    }
                }

                foreach (int pid in throttledPids) copy.Add(pid);
            }

            for (int i = 0; i < toRestore.Count; i++)
            {
                RestoreProcessQos(toRestore[i].Pid, toRestore[i].PriorityClass, toRestore[i].MemoryPriority);
            }
            for (int i = 0; i < toThrottle.Count; i++)
            {
                CaptureOriginalAndThrottle(toThrottle[i]);
            }

            return copy;
        }

        private bool HasAncestorPidLocked(int pid, int targetPid)
        {
            int cur;
            if (!lastParentByPid.TryGetValue(pid, out cur)) return false;
            for (int depth = 0; depth < 6 && cur > 4; depth++)
            {
                if (cur == targetPid) return true;
                int nextParent;
                if (!lastParentByPid.TryGetValue(cur, out nextParent)) break;
                cur = nextParent;
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

        private RestoreItem PopRestoreItemLocked(int pid)
        {
            throttledPids.Remove(pid);
            uint prio;
            if (!originalPriorityByPid.TryGetValue(pid, out prio) || prio == 0)
            {
                prio = NativeApi.NORMAL_PRIORITY_CLASS;
            }
            originalPriorityByPid.Remove(pid);

            uint memPrio;
            if (!originalMemPriorityByPid.TryGetValue(pid, out memPrio) || memPrio == 0)
            {
                memPrio = NativeApi.MEMORY_PRIORITY_NORMAL;
            }
            originalMemPriorityByPid.Remove(pid);

            RestoreItem item = new RestoreItem();
            item.Pid = pid;
            item.PriorityClass = prio;
            item.MemoryPriority = memPrio;
            return item;
        }

        private void CaptureOriginalAndThrottle(int pid)
        {
            uint origPrio = NativeApi.NORMAL_PRIORITY_CLASS;
            uint origMemPrio = NativeApi.MEMORY_PRIORITY_NORMAL;

            IntPtr hQuery = NativeApi.OpenProcess(NativeApi.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hQuery != IntPtr.Zero)
            {
                try
                {
                    uint p = NativeApi.GetPriorityClass(hQuery);
                    if (p != 0) origPrio = p;

                    NativeApi.MEMORY_PRIORITY_INFORMATION mpi;
                    if (NativeApi.GetProcessInformation(
                        hQuery, NativeApi.ProcessMemoryPriority,
                        out mpi, (uint)Marshal.SizeOf(typeof(NativeApi.MEMORY_PRIORITY_INFORMATION)))
                        && mpi.MemoryPriority > 0)
                    {
                        origMemPrio = mpi.MemoryPriority;
                    }
                }
                catch { }
                finally
                {
                    NativeApi.CloseHandle(hQuery);
                }
            }

            bool stillThrottled;
            lock (sync)
            {
                stillThrottled = throttledPids.Contains(pid);
                if (stillThrottled)
                {
                    if (!originalPriorityByPid.ContainsKey(pid))
                    {
                        originalPriorityByPid[pid] = origPrio;
                    }
                    if (!originalMemPriorityByPid.ContainsKey(pid))
                    {
                        originalMemPriorityByPid[pid] = origMemPrio;
                    }
                }
            }

            if (stillThrottled)
            {
                ApplyProcessQos(pid, true);
            }
        }

        private static void RestoreProcessQos(int pid, uint priorityClass, uint memoryPriority)
        {
            if (priorityClass == 0) priorityClass = NativeApi.NORMAL_PRIORITY_CLASS;
            if (memoryPriority == 0) memoryPriority = NativeApi.MEMORY_PRIORITY_NORMAL;

            IntPtr h = NativeApi.OpenProcess(NativeApi.PROCESS_SET_INFORMATION, false, pid);
            if (h == IntPtr.Zero) return;
            try
            {
                NativeApi.PROCESS_POWER_THROTTLING_STATE pts = new NativeApi.PROCESS_POWER_THROTTLING_STATE
                {
                    Version = NativeApi.PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                    ControlMask = NativeApi.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                    StateMask = 0u
                };
                NativeApi.SetProcessInformation(
                    h, NativeApi.ProcessPowerThrottling,
                    ref pts, (uint)Marshal.SizeOf(typeof(NativeApi.PROCESS_POWER_THROTTLING_STATE)));

                NativeApi.MEMORY_PRIORITY_INFORMATION mpi = new NativeApi.MEMORY_PRIORITY_INFORMATION
                {
                    MemoryPriority = memoryPriority
                };
                NativeApi.SetProcessInformation(
                    h, NativeApi.ProcessMemoryPriority,
                    ref mpi, (uint)Marshal.SizeOf(typeof(NativeApi.MEMORY_PRIORITY_INFORMATION)));

                int ioPrio = NativeApi.IoPriorityNormal;
                NativeApi.NtSetInformationProcess(h, NativeApi.ProcessIoPriority, ref ioPrio, sizeof(int));

                NativeApi.SetPriorityClass(h, priorityClass);
            }
            catch
            {
            }
            finally
            {
                NativeApi.CloseHandle(h);
            }
        }

        public static void ApplyProcessQos(int pid, bool throttleBackground)
        {
            IntPtr h = NativeApi.OpenProcess(NativeApi.PROCESS_SET_INFORMATION, false, pid);
            if (h == IntPtr.Zero) return;
            try
            {
                NativeApi.PROCESS_POWER_THROTTLING_STATE pts = new NativeApi.PROCESS_POWER_THROTTLING_STATE
                {
                    Version = NativeApi.PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                    ControlMask = NativeApi.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                    StateMask = throttleBackground ? NativeApi.PROCESS_POWER_THROTTLING_EXECUTION_SPEED : 0u
                };
                NativeApi.SetProcessInformation(
                    h, NativeApi.ProcessPowerThrottling,
                    ref pts, (uint)Marshal.SizeOf(typeof(NativeApi.PROCESS_POWER_THROTTLING_STATE)));

                NativeApi.MEMORY_PRIORITY_INFORMATION mpi = new NativeApi.MEMORY_PRIORITY_INFORMATION
                {
                    MemoryPriority = throttleBackground ? NativeApi.MEMORY_PRIORITY_LOW : NativeApi.MEMORY_PRIORITY_NORMAL
                };
                NativeApi.SetProcessInformation(
                    h, NativeApi.ProcessMemoryPriority,
                    ref mpi, (uint)Marshal.SizeOf(typeof(NativeApi.MEMORY_PRIORITY_INFORMATION)));

                int ioPrio = throttleBackground ? NativeApi.IoPriorityLow : NativeApi.IoPriorityNormal;
                NativeApi.NtSetInformationProcess(h, NativeApi.ProcessIoPriority, ref ioPrio, sizeof(int));

                NativeApi.SetPriorityClass(
                    h, throttleBackground ? NativeApi.BELOW_NORMAL_PRIORITY_CLASS : NativeApi.NORMAL_PRIORITY_CLASS);
            }
            catch
            {
            }
            finally
            {
                NativeApi.CloseHandle(h);
            }
        }

        public static uint QueryProcessMemoryPriority(int pid)
        {
            IntPtr h = NativeApi.OpenProcess(NativeApi.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero) return 0u;
            try
            {
                NativeApi.MEMORY_PRIORITY_INFORMATION mpi;
                if (NativeApi.GetProcessInformation(
                    h, NativeApi.ProcessMemoryPriority,
                    out mpi, (uint)Marshal.SizeOf(typeof(NativeApi.MEMORY_PRIORITY_INFORMATION))))
                {
                    return mpi.MemoryPriority;
                }
            }
            catch { }
            finally
            {
                NativeApi.CloseHandle(h);
            }
            return 0u;
        }

        public static int QueryProcessIoPriority(int pid)
        {
            IntPtr h = NativeApi.OpenProcess(NativeApi.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero) return -1;
            try
            {
                int ioPrio = -1;
                int retLen = 0;
                if (NativeApi.NtQueryInformationProcess(h, NativeApi.ProcessIoPriority, out ioPrio, sizeof(int), out retLen) == 0)
                {
                    return ioPrio;
                }
            }
            catch { }
            finally
            {
                NativeApi.CloseHandle(h);
            }
            return -1;
        }

        public void RestoreAll()
        {
            List<RestoreItem> list = new List<RestoreItem>();
            lock (sync)
            {
                List<int> allPids = new List<int>(throttledPids);
                for (int i = 0; i < allPids.Count; i++)
                {
                    list.Add(PopRestoreItemLocked(allPids[i]));
                }
                throttledPids.Clear();
                originalPriorityByPid.Clear();
                originalMemPriorityByPid.Clear();
            }
            for (int i = 0; i < list.Count; i++)
            {
                RestoreProcessQos(list[i].Pid, list[i].PriorityClass, list[i].MemoryPriority);
            }
        }

        public void Dispose()
        {
            if (hookHandle != IntPtr.Zero)
            {
                NativeApi.UnhookWinEvent(hookHandle);
                hookHandle = IntPtr.Zero;
            }
            RestoreAll();
            GC.KeepAlive(hookDelegate);
        }
    }
}
