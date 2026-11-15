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

        private static readonly HashSet<string> ThrottleTargetFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ego", "chrome", "msedge", "msedgewebview2", "brave", "firefox",
            "adobecollabsync", "creative cloud helper", "onedrive",
            "steamwebhelper", "epicwebhelper", "widgets", "widgetservice", "searchhost",
            "discord", "spotify", "slack", "teams"
        };

        private readonly object sync = new object();
        private readonly HashSet<int> throttledPids = new HashSet<int>();
        private NativeApi.WinEventDelegate hookDelegate;
        private IntPtr hookHandle = IntPtr.Zero;
        private bool enabled = true;
        private int currentForegroundPid;

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
            if (fgPid <= 4 || fgPid == currentForegroundPid) return;
            currentForegroundPid = fgPid;

            // Immediately unthrottle the newly focused process (< 1 ms)
            bool wasThrottled = false;
            lock (sync)
            {
                if (throttledPids.Remove(fgPid)) wasThrottled = true;
            }
            if (wasThrottled || enabled)
            {
                ApplyProcessQos(fgPid, false);
            }
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
            List<int> toRestore = new List<int>();

            lock (sync)
            {
                // Prune exited processes or newly protected / foreground processes
                List<int> deadOrProtected = new List<int>();
                foreach (int pid in throttledPids)
                {
                    ProcNode n;
                    if (!guardScan.Nodes.TryGetValue(pid, out n))
                    {
                        deadOrProtected.Add(pid);
                    }
                    else if (n.Protection != ProtectionReason.None || pid == guardScan.ForegroundPid)
                    {
                        deadOrProtected.Add(pid);
                        toRestore.Add(pid);
                    }
                }
                for (int i = 0; i < deadOrProtected.Count; i++)
                {
                    throttledPids.Remove(deadOrProtected[i]);
                }

                // Apply EcoQoS to background heavy target processes
                foreach (ProcNode n in guardScan.Nodes.Values)
                {
                    if (n.Pid <= 4 || n.Protection != ProtectionReason.None) continue;
                    if (n.Pid == guardScan.ForegroundPid) continue;
                    if (!ThrottleTargetFamilies.Contains(n.BaseName)) continue;

                    // If this browser/app is currently the foreground window, keep its main parent process responsive!
                    if (!string.IsNullOrEmpty(fgBaseName) && n.BaseName == fgBaseName)
                    {
                        ProcNode parentNode;
                        bool isRootProc = !guardScan.Nodes.TryGetValue(n.ParentPid, out parentNode) || parentNode.BaseName != n.BaseName;
                        if (isRootProc)
                        {
                            if (throttledPids.Remove(n.Pid)) toRestore.Add(n.Pid);
                            continue;
                        }
                    }

                    if (!throttledPids.Contains(n.Pid))
                    {
                        throttledPids.Add(n.Pid);
                        toThrottle.Add(n.Pid);
                    }
                }

                foreach (int pid in throttledPids) copy.Add(pid);
            }

            for (int i = 0; i < toRestore.Count; i++) ApplyProcessQos(toRestore[i], false);
            for (int i = 0; i < toThrottle.Count; i++) ApplyProcessQos(toThrottle[i], true);

            return copy;
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
            List<int> list;
            lock (sync)
            {
                list = new List<int>(throttledPids);
                throttledPids.Clear();
            }
            for (int i = 0; i < list.Count; i++)
            {
                ApplyProcessQos(list[i], false);
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
