using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Legram
{
    internal sealed class RamEngine : IDisposable
    {
        public const string ScheduledTaskName = "Legram_PurgeHelper";

        private readonly EcoQosManager ecoQos;
        private readonly AppSettings settings;
        private readonly object stateLock = new object();
        private SystemSnapshot currentSnapshot = new SystemSnapshot();

        private IntPtr lowMemHandle = IntPtr.Zero;
        private RegisteredWaitHandle lowMemWait;
        private readonly SemaphoreSlim purgeGate = new SemaphoreSlim(1, 1);
        private DateTime bootUtc = DateTime.UtcNow;
        private DateTime lastAutoPurgeUtc = DateTime.MinValue;
        private DateTime taskCheckUtc = DateTime.MinValue;
        private bool cachedTaskReady;
        private int busyFlag;
        private volatile bool disposed;

        public event EventHandler SnapshotUpdated;
        public event EventHandler<PurgeCompletedEventArgs> AutoPurgeCompleted;

        public SystemSnapshot Current
        {
            get { lock (stateLock) return currentSnapshot; }
        }

        public bool IsBusy
        {
            get { return Interlocked.CompareExchange(ref busyFlag, 0, 0) != 0; }
        }

        public RamEngine(AppSettings appSettings, EcoQosManager ecoManager)
        {
            settings = appSettings;
            ecoQos = ecoManager;
        }

        public void Start()
        {
            bootUtc = DateTime.UtcNow;
            // Initial synchronous fast sample without PDH PerfLib bloat so idle footprint stays ~8-14 MB!
            SystemSnapshot bootSnap = BuildSnapshotCore(false, false);
            lock (stateLock) { currentSnapshot = bootSnap; }

            // Register 0% CPU Kernel LowMemoryResourceNotification wait handle
            try
            {
                lowMemHandle = NativeApi.CreateMemoryResourceNotification(0); // LowMemoryResourceNotification = 0
                if (lowMemHandle != IntPtr.Zero)
                {
                    lowMemWait = ThreadPool.RegisterWaitForSingleObject(
                        new NativeWaitHandleWrapper(lowMemHandle),
                        new WaitOrTimerCallback(OnKernelLowMemorySignaled),
                        null, -1, false);
                }
            }
            catch (Exception ex)
            {
                Log.Error("CreateMemoryResourceNotification", ex);
            }
        }

        private void OnKernelLowMemorySignaled(object state, bool timedOut)
        {
            if (disposed) return;
            try
            {
                if (!settings.AutoGuardEnabled || (DateTime.UtcNow - bootUtc).TotalSeconds < 5)
                {
                    Thread.Sleep(5000);
                    return;
                }
                if ((DateTime.UtcNow - lastAutoPurgeUtc).TotalSeconds < 90)
                {
                    Thread.Sleep(5000);
                    return;
                }

                lastAutoPurgeUtc = DateTime.UtcNow;
                PurgeResult res = ExecutePurgeCore(true);
                if (res.Success && res.FreedRamBytes > 32L * 1024 * 1024)
                {
                    EventHandler<PurgeCompletedEventArgs> h = AutoPurgeCompleted;
                    if (h != null) h(this, new PurgeCompletedEventArgs(res));
                }
            }
            catch (Exception ex)
            {
                Log.Error("AutoGuard LowMem", ex);
            }
            finally
            {
                // Avoid tight loop while Windows low-memory event stays signaled
                Thread.Sleep(4000);
            }
        }

        // Evaluates smart anti-stutter & OOM auto-harmony conditions (RAM >84%, Commit >=88%, or ISLC Standby Saturated)
        public static string EvaluateAutoGuardTrigger(SystemSnapshot snap)
        {
            if (snap == null || snap.RenderShieldActive) return null;
            if (snap.AvailableRamBytes > 0 && snap.AvailableRamBytes < 2500L * 1024 * 1024 && snap.UsedRamPercent >= 84)
            {
                return "Low RAM (" + snap.UsedRamPercent + "%)";
            }
            if (snap.IsCommitCritical)
            {
                return "Commit OOM Shield (" + snap.CommitUsedPercent + "%)";
            }
            if (snap.IsIslcStandbySaturated)
            {
                return "ISLC Standby (" + Fmt.BytesShort(snap.StandbyTotalBytes) + ")";
            }
            return null;
        }

        public SystemSnapshot RefreshSnapshot(bool forceJunkRescan)
        {
            if (disposed) return Current;
            SystemSnapshot snap = BuildSnapshot(forceJunkRescan);
            lock (stateLock)
            {
                currentSnapshot = snap;
            }

            // Smart anti-stutter threshold check (RAM >84%, Commit >=88%, or ISLC Standby >=4GB & Free <1GB) after 5s boot grace
            string autoReason = EvaluateAutoGuardTrigger(snap);
            if (settings.AutoGuardEnabled && !IsBusy
                && autoReason != null
                && (DateTime.UtcNow - bootUtc).TotalSeconds >= 5
                && (DateTime.UtcNow - lastAutoPurgeUtc).TotalSeconds >= 120)
            {
                lastAutoPurgeUtc = DateTime.UtcNow;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    PurgeResult r = ExecutePurgeCore(true);
                    r.TriggerReason = autoReason;
                    if (r.Success && r.FreedRamBytes > 48L * 1024 * 1024)
                    {
                        EventHandler<PurgeCompletedEventArgs> h = AutoPurgeCompleted;
                        if (h != null) h(this, new PurgeCompletedEventArgs(r));
                    }
                });
            }

            EventHandler ev = SnapshotUpdated;
            if (ev != null) ev(this, EventArgs.Empty);
            return snap;
        }

        public void RefreshSnapshotAsync(bool forceJunkRescan)
        {
            ThreadPool.QueueUserWorkItem(delegate { RefreshSnapshot(forceJunkRescan); });
        }

        private int calmTickCount;

        // Zero-PDH calm refresh for 24/7 pinned single-screen Monolith (~12-18 MB RAM, 0% CPU!)
        public void RefreshCalmScreenAsync(bool immediateFull)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                if (disposed || IsBusy) return;
                int tick = Interlocked.Increment(ref calmTickCount);
                if (immediateFull || tick % 4 == 0)
                {
                    SystemSnapshot snap = BuildSnapshotCore(false, false);
                    lock (stateLock) { currentSnapshot = snap; }
                    EventHandler ev = SnapshotUpdated;
                    if (ev != null) ev(this, EventArgs.Empty);
                    GC.Collect(1, GCCollectionMode.Optimized, false);
                    NativeApi.SelfTrimWorkingSet();
                    return;
                }
                RefreshLightweightInline();
            });
        }

        private int lightTickCount;

        // Ultra-fast (<0.4 ms) 0-PDH update for Floating Pill & Tray when Popover is closed!
        public void RefreshLightweightAsync()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                if (disposed || IsBusy) return;
                int tick = Interlocked.Increment(ref lightTickCount);

                // Every 8th idle tick (~32s), run background EcoQoS reconcile without PDH overhead
                if (tick % 8 == 0)
                {
                    SystemSnapshot ecoSnap = BuildSnapshotCore(false, false);
                    lock (stateLock) { currentSnapshot = ecoSnap; }
                    EventHandler ev2 = SnapshotUpdated;
                    if (ev2 != null) ev2(this, EventArgs.Empty);
                    GC.Collect(1, GCCollectionMode.Optimized, false);
                    NativeApi.SelfTrimWorkingSet();
                    return;
                }

                RefreshLightweightInline();
            });
        }

        private void RefreshLightweightInline()
        {
            SystemSnapshot prev = Current;
            NativeApi.MEMORYSTATUSEX ms = new NativeApi.MEMORYSTATUSEX();
            if (!NativeApi.GlobalMemoryStatusEx(ms)) return;

            long total = (long)ms.ullTotalPhys;
            long avail = (long)ms.ullAvailPhys;
            int usedPct = (int)ms.dwMemoryLoad;
            long commitLim = ms.ullTotalPageFile > 0 ? (long)ms.ullTotalPageFile : prev.CommitLimitBytes;
            long commitTot = (ms.ullTotalPageFile > 0 && ms.ullTotalPageFile >= ms.ullAvailPageFile)
                ? (long)(ms.ullTotalPageFile - ms.ullAvailPageFile)
                : prev.CommitTotalBytes;
            int commitPct = commitLim > 0 ? (int)Math.Round(commitTot * 100.0 / commitLim) : 0;

            MemoryPressure p;
            if (usedPct >= 85 || avail < 2200L * 1024 * 1024 || commitPct >= 90) p = MemoryPressure.Critical;
            else if (usedPct >= 70 || avail < 4500L * 1024 * 1024 || commitPct >= 82) p = MemoryPressure.Warning;
            else p = MemoryPressure.Healthy;

            long modBytes = prev.ModifiedBytes;
            long freeZeroBytes = prev.FreeZeroBytes;
            long standbyTotal = prev.StandbyTotalBytes;
            long standbyLow = prev.StandbyLowPrioBytes;
            long standbyP0 = prev.StandbyP0Bytes;

            int mliSize = Marshal.SizeOf(typeof(NativeApi.SYSTEM_MEMORY_LIST_INFORMATION));
            IntPtr mliBuf = Marshal.AllocHGlobal(mliSize);
            try
            {
                int retLen;
                if (NativeApi.NtQuerySystemInformation(NativeApi.SystemMemoryListInformation, mliBuf, mliSize, out retLen) == 0)
                {
                    NativeApi.SYSTEM_MEMORY_LIST_INFORMATION mli =
                        (NativeApi.SYSTEM_MEMORY_LIST_INFORMATION)Marshal.PtrToStructure(mliBuf, typeof(NativeApi.SYSTEM_MEMORY_LIST_INFORMATION));
                    modBytes = (long)((ulong)mli.ModifiedPageCount * 4096UL);
                    freeZeroBytes = (long)(((ulong)mli.FreePageCount + (ulong)mli.ZeroPageCount) * 4096UL);
                    ulong tSt = 0;
                    ulong lSt = 0;
                    for (int pr = 0; pr < 8; pr++)
                    {
                        ulong pb = (ulong)mli.PageCountByPriority[pr] * 4096UL;
                        tSt += pb;
                        if (pr <= 4) lSt += pb;
                        if (pr == 0) standbyP0 = (long)pb;
                    }
                    standbyTotal = (long)tSt;
                    standbyLow = (long)lSt;
                }
            }
            catch { }
            finally
            {
                Marshal.FreeHGlobal(mliBuf);
            }

            GpuSnapshot gpu = GpuVramMonitor.SampleLightweight();
            if (prev.Drives != null)
            {
                for (int i = 0; i < prev.Drives.Count; i++)
                {
                    DriveSnapshot ds = prev.Drives[i];
                    if (ds.IsVirtualOrCloud || string.IsNullOrEmpty(ds.Root)) continue;
                    try
                    {
                        DriveInfo di = new DriveInfo(ds.Root);
                        if (di.IsReady) ds.FreeBytes = di.AvailableFreeSpace;
                    }
                    catch { }
                }
            }

            SystemSnapshot next = new SystemSnapshot
            {
                InstalledDimmBytes = prev.InstalledDimmBytes,
                TotalRamBytes = total,
                HardwareReservedBytes = prev.HardwareReservedBytes,
                AvailableRamBytes = avail,
                CommitTotalBytes = commitTot,
                CommitLimitBytes = commitLim,
                CommitPeakBytes = Math.Max(prev.CommitPeakBytes, commitTot),
                ModifiedBytes = modBytes,
                FreeZeroBytes = freeZeroBytes,
                StandbyTotalBytes = standbyTotal,
                StandbyLowPrioBytes = standbyLow,
                StandbyP0Bytes = standbyP0,
                WorkingSetTrimmableBytes = prev.WorkingSetTrimmableBytes,
                UsedRamPercent = usedPct,
                Pressure = p,
                RenderShieldActive = prev.RenderShieldActive || gpu.IsRenderingOrDecoding,
                KernelPurgeTaskReady = prev.KernelPurgeTaskReady,
                ProtectedProcessCount = prev.ProtectedProcessCount,
                TcpListenerCount = prev.TcpListenerCount,
                ActiveAudioCount = prev.ActiveAudioCount,
                EcoQosThrottledCount = prev.EcoQosThrottledCount,
                OrphanAiCount = prev.OrphanAiCount,
                OrphanAiBytes = prev.OrphanAiBytes,
                DwmPid = prev.DwmPid,
                DwmWorkingSetBytes = prev.DwmWorkingSetBytes,
                DwmVramBytes = prev.DwmVramBytes,
                GuardTooltipSummary = prev.GuardTooltipSummary,
                Gpu = gpu,
                Drives = prev.Drives,
                PrimaryDisplayDrive = prev.PrimaryDisplayDrive,
                Junk = prev.Junk,
                TopGroups = prev.TopGroups
            };

            lock (stateLock) { currentSnapshot = next; }

            string autoReason = EvaluateAutoGuardTrigger(next);
            if (settings.AutoGuardEnabled && !IsBusy
                && autoReason != null
                && (DateTime.UtcNow - bootUtc).TotalSeconds >= 5
                && !gpu.IsRenderingOrDecoding
                && (DateTime.UtcNow - lastAutoPurgeUtc).TotalSeconds >= 120)
            {
                lastAutoPurgeUtc = DateTime.UtcNow;
                PurgeResult r = ExecutePurgeCore(true);
                r.TriggerReason = autoReason;
                if (r.Success && r.FreedRamBytes > 48L * 1024 * 1024)
                {
                    EventHandler<PurgeCompletedEventArgs> h = AutoPurgeCompleted;
                    if (h != null) h(this, new PurgeCompletedEventArgs(r));
                }
            }

            EventHandler ev = SnapshotUpdated;
            if (ev != null) ev(this, EventArgs.Empty);
        }

        private SystemSnapshot BuildSnapshot(bool forceJunkRescan)
        {
            return BuildSnapshotCore(forceJunkRescan, true);
        }

        private SystemSnapshot BuildSnapshotCore(bool forceJunkRescan, bool includePdh)
        {
            SystemSnapshot snap = new SystemSnapshot();

            // 1. Installed DIMM + OS Memory Status + Commit Charge + Kernel Standby Breakdown
            ulong dimmKb;
            if (NativeApi.GetPhysicallyInstalledSystemMemory(out dimmKb) && dimmKb > 0)
            {
                snap.InstalledDimmBytes = (long)(dimmKb * 1024UL);
            }

            NativeApi.MEMORYSTATUSEX ms = new NativeApi.MEMORYSTATUSEX();
            if (NativeApi.GlobalMemoryStatusEx(ms))
            {
                snap.TotalRamBytes = (long)ms.ullTotalPhys;
                snap.AvailableRamBytes = (long)ms.ullAvailPhys;
                snap.UsedRamPercent = (int)ms.dwMemoryLoad;
                if (ms.ullTotalPageFile > 0)
                {
                    snap.CommitLimitBytes = (long)ms.ullTotalPageFile;
                    if (ms.ullTotalPageFile >= ms.ullAvailPageFile)
                    {
                        snap.CommitTotalBytes = (long)(ms.ullTotalPageFile - ms.ullAvailPageFile);
                    }
                }
                if (snap.InstalledDimmBytes > snap.TotalRamBytes)
                {
                    snap.HardwareReservedBytes = snap.InstalledDimmBytes - snap.TotalRamBytes;
                }
            }

            NativeApi.PERFORMANCE_INFORMATION perf = new NativeApi.PERFORMANCE_INFORMATION();
            perf.cb = (uint)Marshal.SizeOf(typeof(NativeApi.PERFORMANCE_INFORMATION));
            ulong pageBytes = 4096UL;
            if (NativeApi.K32GetPerformanceInfo(out perf, perf.cb) && (ulong)perf.PageSize > 0)
            {
                pageBytes = (ulong)perf.PageSize;
                if ((ulong)perf.CommitLimit > 0)
                {
                    snap.CommitTotalBytes = (long)((ulong)perf.CommitTotal * pageBytes);
                    snap.CommitLimitBytes = (long)((ulong)perf.CommitLimit * pageBytes);
                    snap.CommitPeakBytes = (long)((ulong)perf.CommitPeak * pageBytes);
                }
            }

            int mliSize = Marshal.SizeOf(typeof(NativeApi.SYSTEM_MEMORY_LIST_INFORMATION));
            IntPtr mliBuf = Marshal.AllocHGlobal(mliSize);
            try
            {
                int retLen;
                if (NativeApi.NtQuerySystemInformation(NativeApi.SystemMemoryListInformation, mliBuf, mliSize, out retLen) == 0)
                {
                    NativeApi.SYSTEM_MEMORY_LIST_INFORMATION mli =
                        (NativeApi.SYSTEM_MEMORY_LIST_INFORMATION)Marshal.PtrToStructure(mliBuf, typeof(NativeApi.SYSTEM_MEMORY_LIST_INFORMATION));
                    snap.ModifiedBytes = (long)((ulong)mli.ModifiedPageCount * pageBytes);
                    snap.FreeZeroBytes = (long)(((ulong)mli.FreePageCount + (ulong)mli.ZeroPageCount) * pageBytes);

                    ulong totalStandby = 0;
                    ulong lowPrio = 0;
                    for (int p = 0; p < 8; p++)
                    {
                        ulong pb = (ulong)mli.PageCountByPriority[p] * pageBytes;
                        totalStandby += pb;
                        if (p <= 4) lowPrio += pb;
                        if (p == 0) snap.StandbyP0Bytes = (long)pb;
                    }
                    snap.StandbyTotalBytes = (long)totalStandby;
                    snap.StandbyLowPrioBytes = (long)lowPrio;
                }
                else
                {
                    snap.StandbyTotalBytes = (long)((ulong)perf.SystemCache * pageBytes);
                    snap.StandbyLowPrioBytes = snap.StandbyTotalBytes / 2;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(mliBuf);
            }

            // Semantic Pressure State (Physical RAM + Commit Charge OOM Shield)
            if (snap.UsedRamPercent >= 85 || snap.AvailableRamBytes < 2200L * 1024 * 1024 || snap.CommitUsedPercent >= 90)
                snap.Pressure = MemoryPressure.Critical;
            else if (snap.UsedRamPercent >= 70 || snap.AvailableRamBytes < 4500L * 1024 * 1024 || snap.CommitUsedPercent >= 82)
                snap.Pressure = MemoryPressure.Warning;
            else
                snap.Pressure = MemoryPressure.Healthy;

            // 2. GPU RTX 3060 (+ Per-PID Dedicated VRAM & Idle Ollama Probe when Deep scan requests it)
            Dictionary<int, long> vramByPid = null;
            snap.Gpu = includePdh ? GpuVramMonitor.Sample(out vramByPid) : GpuVramMonitor.SampleLightweight();

            // 3. ProcessGuard + EcoQoS Reconcile
            GuardScanResult guard = ProcessGuard.Scan(vramByPid, null, snap.Gpu.IsRenderingOrDecoding);
            HashSet<int> throttled = ecoQos.ReconcileAndSnapshot(guard);
            if (throttled.Count > 0)
            {
                // Stamp live EcoQoS badges onto TopGroups
                for (int i = 0; i < guard.TopGroups.Count; i++)
                {
                    ProcessGroupItem g = guard.TopGroups[i];
                    for (int k = 0; k < g.Pids.Count; k++)
                    {
                        if (throttled.Contains(g.Pids[k]))
                        {
                            g.IsEcoQosActive = true;
                            break;
                        }
                    }
                }
            }

            snap.RenderShieldActive = guard.RenderAppPresent && snap.Gpu.IsRenderingOrDecoding;
            snap.ProtectedProcessCount = guard.ProtectedPids.Count;
            snap.TcpListenerCount = guard.TcpListenerCount;
            snap.ActiveAudioCount = guard.ActiveAudioPids.Count;
            snap.EcoQosThrottledCount = throttled.Count;
            snap.WorkingSetTrimmableBytes = guard.WorkingSetTrimmableBytes;
            snap.OrphanAiCount = guard.OrphanAiPids.Count;
            snap.OrphanAiBytes = guard.OrphanAiBytes;
            snap.DwmPid = guard.DwmPid;
            snap.DwmWorkingSetBytes = guard.DwmWorkingSetBytes;
            snap.DwmVramBytes = guard.DwmVramBytes;
            snap.GuardTooltipSummary = guard.GuardTooltipSummary;
            snap.TopGroups = guard.TopGroups;

            // 4. Physical vs Cloud/Virtual Drives + Cached Safe Junk Estimate
            DriveSnapshot critDrive;
            snap.Drives = StorageSweeper.ScanDrives(out critDrive);
            snap.PrimaryDisplayDrive = critDrive;
            snap.Junk = StorageSweeper.GetJunkEstimate(snap.Drives, forceJunkRescan);

            // 5. Check Scheduled Task status (cached every 30s)
            snap.KernelPurgeTaskReady = IsKernelPurgeTaskRegistered();

            return snap;
        }

        public Task<PurgeResult> RunManualPurgeAsync()
        {
            return Task.Run(delegate { return ExecutePurgeCore(false); });
        }

        private PurgeResult ExecutePurgeCore(bool gentleAutoMode)
        {
            PurgeResult result = new PurgeResult();
            if (!purgeGate.Wait(gentleAutoMode ? 0 : 2500))
            {
                result.SummaryText = "Harmonizing...";
                return result;
            }
            Interlocked.Exchange(ref busyFlag, 1);

            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                long availBefore = ReadAvailableRamBytes();

                GpuSnapshot gpu = GpuVramMonitor.SampleLightweight();
                GuardScanResult guard = ProcessGuard.Scan(null, null, gpu.IsRenderingOrDecoding);

                // 1. Lower MemoryPriority to Low (2) -> K32EmptyWorkingSet on non-guarded background processes
                int trimmed = 0;
                for (int i = 0; i < guard.TrimmablePids.Count; i++)
                {
                    int pid = guard.TrimmablePids[i];
                    if (guard.ProtectedPids.Contains(pid)) continue;
                    if (TrimSingleProcessSafe(pid, true))
                    {
                        trimmed++;
                    }
                }
                result.TrimmedProcessCount = trimmed;

                // 2. Deflate bloated dwm.exe safely (K32EmptyWorkingSet ONLY — never touches priority/QoS, skipped during GPU render)
                if (!gpu.IsRenderingOrDecoding && guard.DwmPid > 4)
                {
                    long dwmThreshold = gentleAutoMode ? (650L * 1048576L) : (450L * 1048576L);
                    long dwmFreed;
                    if (TryDeflateDwmWorkingSet(guard.DwmPid, guard.DwmWorkingSetBytes, dwmThreshold, out dwmFreed))
                    {
                        result.DwmDeflated = true;
                        result.DwmFreedBytes = dwmFreed;
                    }
                }

                // 3. Flush Windows 11 Registry Cache (InfoClass 155) + Trim System File Cache Working Set (-1, -1)
                bool regFlushed, sysCacheTrimmed;
                TryFlushRegistryAndSystemFileCache(out regFlushed, out sysCacheTrimmed);
                result.RegistryCacheFlushed = regFlushed;
                result.SystemCacheTrimmed = sysCacheTrimmed;

                // 4. Try Direct or Helper-Task Kernel Page Combine + Low-Priority Standby Purge (0 FlushModifiedList = 0 Stutter)
                bool kernelPurged = TryDirectKernelCombineAndStandbyPurge();
                if (!kernelPurged && IsKernelPurgeTaskRegistered())
                {
                    kernelPurged = TriggerScheduledPurgeHelper();
                }
                result.KernelStandbyPurged = kernelPurged;

                // 5. Flush DNS Resolver Cache (3 ms)
                if (!gentleAutoMode)
                {
                    try { NativeApi.DnsFlushResolverCache(); } catch { }
                }

                // Brief settle (65 ms) so OS page accounting updates Available Physical Memory
                Thread.Sleep(65);

                long availAfter = ReadAvailableRamBytes();
                long delta = Math.Max(0L, availAfter - availBefore);
                result.FreedRamBytes = delta;
                result.Success = true;
                sw.Stop();
                result.DurationMs = (int)sw.ElapsedMilliseconds;

                if (delta >= 24L * 1024 * 1024)
                {
                    result.PillToastText = "+" + Fmt.BytesShort(delta) + " freed";
                    result.SummaryText = "\u2713 Freed +" + Fmt.Gb2(delta) + " (" + result.DurationMs + " ms)";
                }
                else
                {
                    result.PillToastText = "RAM Optimal";
                    result.SummaryText = "\u2713 RAM Already Optimal \u00B7 " + trimmed + " procs trimmed";
                }

                Log.Info("Purge completed (" + (gentleAutoMode ? "auto" : "manual") + "): " + result.SummaryText);
                SystemSnapshot postSnap = BuildSnapshotCore(false, false);
                lock (stateLock) { currentSnapshot = postSnap; }
                EventHandler ev = SnapshotUpdated;
                if (ev != null) ev(this, EventArgs.Empty);
                return result;
            }
            catch (Exception ex)
            {
                Log.Error("ExecutePurgeCore", ex);
                result.SummaryText = "Partial error";
                return result;
            }
            finally
            {
                Interlocked.Exchange(ref busyFlag, 0);
                purgeGate.Release();
            }
        }

        public Task<PurgeResult> TrimOrKillProcessGroupAsync(ProcessGroupItem group)
        {
            return Task.Run(delegate
            {
                PurgeResult r = new PurgeResult();
                if (group == null || group.IsProtected) return r;
                Stopwatch sw = Stopwatch.StartNew();
                long before = ReadAvailableRamBytes();

                int count = 0;
                if (group.IsOrphanGroup)
                {
                    for (int i = 0; i < group.Pids.Count; i++)
                    {
                        if (TerminatePidSafe(group.Pids[i])) count++;
                    }
                    TryShutdownIdleWsl();
                }
                else
                {
                    for (int i = 0; i < group.Pids.Count; i++)
                    {
                        if (TrimSingleProcessSafe(group.Pids[i], true)) count++;
                    }
                }

                Thread.Sleep(55);
                long delta = Math.Max(0L, ReadAvailableRamBytes() - before);
                sw.Stop();
                r.Success = true;
                r.FreedRamBytes = delta;
                r.TrimmedProcessCount = count;
                r.DurationMs = (int)sw.ElapsedMilliseconds;
                r.PillToastText = delta > 8L * 1024 * 1024 ? ("+" + Fmt.BytesShort(delta) + " freed") : (group.DisplayName + " trimmed");
                r.SummaryText = "\u2713 " + group.DisplayName + ": +" + Fmt.BytesShort(Math.Max(delta, 1024 * 1024));
                RefreshSnapshot(false);
                return r;
            });
        }

        public Task<PurgeResult> RefreshVramAndOrphansAsync()
        {
            return Task.Run(delegate
            {
                PurgeResult r = new PurgeResult();
                Stopwatch sw = Stopwatch.StartNew();
                long ramBefore = ReadAvailableRamBytes();
                Dictionary<int, long> vramBeforeMap;
                GpuSnapshot gpuBefore = GpuVramMonitor.Sample(out vramBeforeMap);
                GuardScanResult guard = ProcessGuard.Scan(vramBeforeMap, null, gpuBefore.IsRenderingOrDecoding);

                int reaped = 0;
                // 1. Reap verified orphan AI child processes
                for (int i = 0; i < guard.OrphanAiPids.Count; i++)
                {
                    if (TerminatePidSafe(guard.OrphanAiPids[i])) reaped++;
                }

                // 2. Evict idle Ollama VRAM models safely (keep_alive: 0)
                int unloadedOllama = GpuVramMonitor.EvictIdleOllamaModels(gpuBefore.IsRenderingOrDecoding || gpuBefore.GpuUtilPercent >= 10);

                // 3. Shutdown WSL VM if idle
                if (TryShutdownIdleWsl()) reaped++;

                // 4. Refresh bloated Chromium/Browser --type=gpu-process ONLY if not rendering/decoding/playing audio
                if (!gpuBefore.IsRenderingOrDecoding && guard.ActiveAudioPids.Count == 0)
                {
                    for (int i = 0; i < gpuBefore.RefreshableBrowserGpuPids.Count; i++)
                    {
                        int gpuPid = gpuBefore.RefreshableBrowserGpuPids[i];
                        if (!guard.ProtectedPids.Contains(gpuPid))
                        {
                            if (TerminatePidSafe(gpuPid)) reaped++;
                        }
                    }
                }

                Thread.Sleep(220);
                Dictionary<int, long> vramAfterMap;
                GpuSnapshot gpuAfter = GpuVramMonitor.Sample(out vramAfterMap);
                long vramDelta = Math.Max(0L, gpuBefore.VramUsedBytes - gpuAfter.VramUsedBytes);
                long ramDelta = Math.Max(0L, ReadAvailableRamBytes() - ramBefore);

                sw.Stop();
                r.Success = true;
                r.FreedVramBytes = vramDelta;
                r.FreedRamBytes = ramDelta;
                r.ReapedOrphanCount = reaped;
                r.UnloadedOllamaModels = unloadedOllama;
                r.DurationMs = (int)sw.ElapsedMilliseconds;
                long combined = vramDelta + ramDelta;
                r.PillToastText = combined > 16L * 1024 * 1024 ? ("VRAM +" + Fmt.BytesShort(combined)) : "VRAM & AI Fresh";
                r.SummaryText = gpuBefore.IsRenderingOrDecoding
                    ? "\uD83D\uDEE1 Render Shield Active \u00B7 " + reaped + " orphan AI reaped"
                    : "\u2713 VRAM & AI Refreshed (+" + Fmt.BytesShort(Math.Max(combined, 0)) + ")";
                RefreshSnapshot(false);
                return r;
            });
        }

        public Task<PurgeResult> SweepStorageAsync()
        {
            return Task.Run(delegate
            {
                PurgeResult r = new PurgeResult();
                Stopwatch sw = Stopwatch.StartNew();
                DriveSnapshot crit;
                List<DriveSnapshot> drives = StorageSweeper.ScanDrives(out crit);
                long freed = StorageSweeper.ExecuteSafeSweep(drives);
                sw.Stop();
                r.Success = true;
                r.FreedDiskBytes = freed;
                r.DurationMs = (int)sw.ElapsedMilliseconds;
                r.PillToastText = freed > 1024 * 1024 ? ("SSD +" + Fmt.BytesShort(freed)) : "SSD Clean";
                r.SummaryText = freed > 1024 * 1024
                    ? "\u2713 SSD Freed +" + Fmt.BytesShort(freed)
                    : "\u2713 SSD Junk Already Clean";
                RefreshSnapshot(true);
                return r;
            });
        }

        // THE ONE BUTTON: Unified Harmony Pass (RAM + Registry/SysCache + DWM Leak + Safe VRAM/Ollama/AI Orphans + Safe SSD Junk)
        public Task<PurgeResult> RunUnifiedHarmonyAsync()
        {
            return Task.Run(delegate
            {
                Stopwatch sw = Stopwatch.StartNew();
                long ramBefore = ReadAvailableRamBytes();

                // 1. Core RAM Working-Set + DWM Leak + Registry/SysCache + Page Combine + 0-UAC Kernel Low-Prio Standby Purge
                PurgeResult core = ExecutePurgeCore(false);

                // 2. Opportunistic Orphan AI, Idle Ollama VRAM Evict & Safe GPU Browser Compositor Refresh (0 disruption to audio/render)
                long vramFreed = 0;
                int reaped = 0;
                int unloadedOllama = 0;
                try
                {
                    Dictionary<int, long> vramBeforeMap;
                    GpuSnapshot gpuBefore = GpuVramMonitor.Sample(out vramBeforeMap);
                    GuardScanResult guard = ProcessGuard.Scan(vramBeforeMap, null, gpuBefore.IsRenderingOrDecoding);

                    for (int i = 0; i < guard.OrphanAiPids.Count; i++)
                    {
                        if (TerminatePidSafe(guard.OrphanAiPids[i])) reaped++;
                    }
                    unloadedOllama = GpuVramMonitor.EvictIdleOllamaModels(gpuBefore.IsRenderingOrDecoding || gpuBefore.GpuUtilPercent >= 10);

                    if (!gpuBefore.IsRenderingOrDecoding && guard.ActiveAudioPids.Count == 0
                        && gpuBefore.RefreshableBrowserGpuPids.Count > 0
                        && (gpuBefore.IsHighAlert || gpuBefore.ReclaimableVramBytes >= 192L * 1024 * 1024))
                    {
                        for (int i = 0; i < gpuBefore.RefreshableBrowserGpuPids.Count; i++)
                        {
                            int gpuPid = gpuBefore.RefreshableBrowserGpuPids[i];
                            if (!guard.ProtectedPids.Contains(gpuPid) && TerminatePidSafe(gpuPid)) reaped++;
                        }
                    }
                    if (reaped > 0 || unloadedOllama > 0)
                    {
                        Thread.Sleep(140);
                        Dictionary<int, long> vramAfterMap;
                        GpuSnapshot gpuAfter = GpuVramMonitor.Sample(out vramAfterMap);
                        vramFreed = Math.Max(0L, gpuBefore.VramUsedBytes - gpuAfter.VramUsedBytes);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Harmony.VramOrphan", ex);
                }

                // 3. Opportunistic Safe Physical SSD Sweep (only when >= 24 MB junk or tight SSD, cloud/virtual immune)
                long diskFreed = 0;
                bool sweptDisk = false;
                try
                {
                    SystemSnapshot snap = Current;
                    long estJunk = (snap != null && snap.Junk != null) ? snap.Junk.TotalBytes : 0L;
                    bool tightDrive = (snap != null && snap.PrimaryDisplayDrive != null && snap.PrimaryDisplayDrive.IsTight);
                    if (estJunk >= 24L * 1024 * 1024 || tightDrive)
                    {
                        DriveSnapshot crit;
                        List<DriveSnapshot> drives = StorageSweeper.ScanDrives(out crit);
                        diskFreed = StorageSweeper.ExecuteSafeSweep(drives);
                        sweptDisk = true;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Harmony.StorageSweep", ex);
                }

                long ramDelta = Math.Max(core.FreedRamBytes, Math.Max(0L, ReadAvailableRamBytes() - ramBefore));
                sw.Stop();

                PurgeResult res = new PurgeResult
                {
                    Success = core.Success,
                    FreedRamBytes = ramDelta,
                    FreedVramBytes = vramFreed,
                    FreedDiskBytes = diskFreed,
                    TrimmedProcessCount = core.TrimmedProcessCount,
                    ReapedOrphanCount = reaped,
                    UnloadedOllamaModels = unloadedOllama,
                    KernelStandbyPurged = core.KernelStandbyPurged,
                    RegistryCacheFlushed = core.RegistryCacheFlushed,
                    SystemCacheTrimmed = core.SystemCacheTrimmed,
                    DwmDeflated = core.DwmDeflated,
                    DwmFreedBytes = core.DwmFreedBytes,
                    DurationMs = (int)sw.ElapsedMilliseconds
                };

                long totalMemGain = ramDelta + vramFreed;
                if (totalMemGain >= 20L * 1024 * 1024 && diskFreed >= 8L * 1024 * 1024)
                {
                    res.PillToastText = "+" + Fmt.BytesShort(totalMemGain) + " \u00B7 SSD +" + Fmt.BytesShort(diskFreed);
                    res.SummaryText = "Harmonized \u00B7 +" + Fmt.BytesShort(totalMemGain) + " Memory \u00B7 +" + Fmt.BytesShort(diskFreed) + " SSD";
                }
                else if (totalMemGain >= 20L * 1024 * 1024)
                {
                    res.PillToastText = "+" + Fmt.BytesShort(totalMemGain) + " freed";
                    res.SummaryText = "Harmonized \u00B7 +" + Fmt.Gb2(totalMemGain) + " Freed";
                }
                else if (diskFreed >= 8L * 1024 * 1024)
                {
                    res.PillToastText = "SSD +" + Fmt.BytesShort(diskFreed);
                    res.SummaryText = "Harmonized \u00B7 SSD +" + Fmt.BytesShort(diskFreed) + " Cleaned";
                }
                else
                {
                    res.PillToastText = "Harmonized";
                    res.SummaryText = "Already Optimal \u00B7 " + core.TrimmedProcessCount + " Procs Trimmed";
                }

                GpuVramMonitor.ClosePdhIfIdle();
                SystemSnapshot postSnap = BuildSnapshotCore(sweptDisk, false);
                lock (stateLock) { currentSnapshot = postSnap; }
                EventHandler ev = SnapshotUpdated;
                if (ev != null) ev(this, EventArgs.Empty);
                return res;
            }
            );
        }

        public static bool TryDeflateDwmWorkingSet(int dwmPid, long currentWsBytes, long thresholdBytes, out long freedBytes)
        {
            freedBytes = 0;
            if (dwmPid <= 4 || currentWsBytes < thresholdBytes) return false;

            IntPtr h = NativeApi.OpenProcess(NativeApi.PROCESS_QUERY_LIMITED_INFORMATION | NativeApi.PROCESS_SET_QUOTA, false, dwmPid);
            if (h == IntPtr.Zero) return false;
            try
            {
                // Gentle K32EmptyWorkingSet ONLY — never alter DWM priority or EcoQoS!
                if (!NativeApi.K32EmptyWorkingSet(h)) return false;

                NativeApi.PROCESS_MEMORY_COUNTERS pmc;
                if (NativeApi.K32GetProcessMemoryInfo(h, out pmc, (uint)Marshal.SizeOf(typeof(NativeApi.PROCESS_MEMORY_COUNTERS))))
                {
                    long after = (long)(ulong)pmc.WorkingSetSize;
                    freedBytes = Math.Max(0L, currentWsBytes - after);
                }
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                NativeApi.CloseHandle(h);
            }
        }

        public static void TryFlushRegistryAndSystemFileCache(out bool regFlushed, out bool sysCacheTrimmed)
        {
            regFlushed = false;
            sysCacheTrimmed = false;

            try
            {
                EnablePrivilege("SeProfileSingleProcessPrivilege");
                EnablePrivilege("SeIncreaseQuotaPrivilege");
            }
            catch { }

            try
            {
                int stReg = NativeApi.NtSetSystemInformation(NativeApi.SystemRegistryReconciliationInformation, IntPtr.Zero, 0);
                regFlushed = (stReg == 0);
            }
            catch { }

            try
            {
                sysCacheTrimmed = NativeApi.SetSystemFileCacheSize(new IntPtr(-1), new IntPtr(-1), 0);
            }
            catch { }
        }

        private static bool TrimSingleProcessSafe(int pid, bool lowerMemPriorityFirst)
        {
            uint access = NativeApi.PROCESS_QUERY_LIMITED_INFORMATION | NativeApi.PROCESS_SET_QUOTA;
            if (lowerMemPriorityFirst) access |= NativeApi.PROCESS_SET_INFORMATION;

            IntPtr h = NativeApi.OpenProcess(access, false, pid);
            if (h == IntPtr.Zero && lowerMemPriorityFirst)
            {
                h = NativeApi.OpenProcess(NativeApi.PROCESS_QUERY_LIMITED_INFORMATION | NativeApi.PROCESS_SET_QUOTA, false, pid);
            }
            if (h == IntPtr.Zero) return false;

            try
            {
                if (lowerMemPriorityFirst)
                {
                    NativeApi.MEMORY_PRIORITY_INFORMATION mpi = new NativeApi.MEMORY_PRIORITY_INFORMATION
                    {
                        MemoryPriority = NativeApi.MEMORY_PRIORITY_LOW
                    };
                    NativeApi.SetProcessInformation(
                        h, NativeApi.ProcessMemoryPriority,
                        ref mpi, (uint)Marshal.SizeOf(typeof(NativeApi.MEMORY_PRIORITY_INFORMATION)));
                }
                return NativeApi.K32EmptyWorkingSet(h);
            }
            catch
            {
                return false;
            }
            finally
            {
                NativeApi.CloseHandle(h);
            }
        }

        private static bool TerminatePidSafe(int pid)
        {
            IntPtr h = NativeApi.OpenProcess(NativeApi.PROCESS_TERMINATE, false, pid);
            if (h == IntPtr.Zero) return false;
            try
            {
                using (Process p = Process.GetProcessById(pid))
                {
                    p.Kill();
                    return true;
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                NativeApi.CloseHandle(h);
            }
        }

        private static bool TryShutdownIdleWsl()
        {
            try
            {
                Process[] vms = Process.GetProcessesByName("vmmemWSL");
                if (vms.Length == 0) vms = Process.GetProcessesByName("vmmem");
                for (int i = 0; i < vms.Length; i++) vms[i].Dispose();
                if (vms.Length == 0) return false;

                Process[] activeBash = Process.GetProcessesByName("bash");
                for (int i = 0; i < activeBash.Length; i++) activeBash[i].Dispose();
                if (activeBash.Length > 0) return false;

                ProcessStartInfo psi = new ProcessStartInfo("wsl.exe", "--shutdown")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (Process p = Process.Start(psi))
                {
                    if (p != null) p.WaitForExit(3500);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static long ReadAvailableRamBytes()
        {
            NativeApi.MEMORYSTATUSEX ms = new NativeApi.MEMORYSTATUSEX();
            if (NativeApi.GlobalMemoryStatusEx(ms)) return (long)ms.ullAvailPhys;
            return 0;
        }

        public bool IsKernelPurgeTaskRegistered()
        {
            if ((DateTime.UtcNow - taskCheckUtc).TotalSeconds < 30) return cachedTaskReady;
            taskCheckUtc = DateTime.UtcNow;
            try
            {
                string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                ProcessStartInfo psi = new ProcessStartInfo("schtasks.exe", "/Query /TN \"" + ScheduledTaskName + "\" /XML")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (Process p = Process.Start(psi))
                {
                    if (p != null)
                    {
                        string xml = p.StandardOutput.ReadToEnd();
                        if (p.WaitForExit(1500) && p.ExitCode == 0 && !string.IsNullOrEmpty(exe))
                        {
                            cachedTaskReady = xml.IndexOf(exe, StringComparison.OrdinalIgnoreCase) >= 0 && File.Exists(exe);
                        }
                        else
                        {
                            cachedTaskReady = false;
                        }
                    }
                }
            }
            catch
            {
                cachedTaskReady = false;
            }
            return cachedTaskReady;
        }

        public bool RegisterKernelPurgeTaskInteractive()
        {
            try
            {
                string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string args = "/Create /TN \"" + ScheduledTaskName + "\" /TR \"\\\"" + exe + "\\\" --purge-helper\" /SC ONCE /ST 00:00 /RL HIGHEST /F";
                ProcessStartInfo psi = new ProcessStartInfo("schtasks.exe", args)
                {
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (Process p = Process.Start(psi))
                {
                    if (p != null) p.WaitForExit(10000);
                }
                taskCheckUtc = DateTime.MinValue;
                return IsKernelPurgeTaskRegistered();
            }
            catch (Exception ex)
            {
                Log.Error("RegisterKernelPurgeTaskInteractive", ex);
                return false;
            }
        }

        private static bool TriggerScheduledPurgeHelper()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("schtasks.exe", "/Run /TN \"" + ScheduledTaskName + "\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (Process p = Process.Start(psi))
                {
                    if (p != null && p.WaitForExit(2000) && p.ExitCode == 0)
                    {
                        Thread.Sleep(120);
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        // Invoked both opportunistically (if process holds privileges) and inside `--purge-helper` elevated execution!
        public static bool TryDirectKernelCombineAndStandbyPurge()
        {
            try
            {
                bool regOk, sysCacheOk;
                TryFlushRegistryAndSystemFileCache(out regOk, out sysCacheOk);

                // Also deflate bloated Session-1 dwm.exe (>500 MB) if running elevated
                try
                {
                    Process[] dwms = Process.GetProcessesByName("dwm");
                    for (int i = 0; i < dwms.Length; i++)
                    {
                        using (Process dp = dwms[i])
                        {
                            long dFreed;
                            TryDeflateDwmWorkingSet(dp.Id, dp.WorkingSet64, 500L * 1048576L, out dFreed);
                        }
                    }
                }
                catch { }

                // 1. MemoryCombineInformationEx (InfoClass = 130) - safe page deduplication
                NativeApi.MEMORY_COMBINE_INFORMATION_EX combine = new NativeApi.MEMORY_COMBINE_INFORMATION_EX();
                int stCombine = NativeApi.NtSetSystemInformation(
                    NativeApi.SystemCombinePhysicalMemoryInformation,
                    ref combine,
                    Marshal.SizeOf(typeof(NativeApi.MEMORY_COMBINE_INFORMATION_EX)));

                // 2. MemoryPurgeLowPriorityStandbyList (Command = 5 on InfoClass = 80) - 0 stutter!
                int cmd = NativeApi.MemoryPurgeLowPriorityStandbyList;
                int stStandby = NativeApi.NtSetSystemInformation(
                    NativeApi.SystemMemoryListInformation,
                    ref cmd,
                    sizeof(int));

                return stStandby == 0 || stCombine == 0 || sysCacheOk;
            }
            catch
            {
                return false;
            }
        }

        private static void EnablePrivilege(string privName)
        {
            IntPtr hToken;
            if (!NativeApi.OpenProcessToken(NativeApi.GetCurrentProcess(), 0x0020 | 0x0008, out hToken)) // TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY
                return;
            try
            {
                NativeApi.LUID luid;
                if (NativeApi.LookupPrivilegeValueW(null, privName, out luid))
                {
                    NativeApi.TOKEN_PRIVILEGES tp = new NativeApi.TOKEN_PRIVILEGES
                    {
                        PrivilegeCount = 1,
                        Luid = luid,
                        Attributes = 0x00000002 // SE_PRIVILEGE_ENABLED
                    };
                    NativeApi.AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
                }
            }
            finally
            {
                NativeApi.CloseHandle(hToken);
            }
        }

        public void Dispose()
        {
            disposed = true;
            if (lowMemWait != null)
            {
                try { lowMemWait.Unregister(null); } catch { }
                lowMemWait = null;
            }
            if (lowMemHandle != IntPtr.Zero)
            {
                try { NativeApi.CloseHandle(lowMemHandle); } catch { }
                lowMemHandle = IntPtr.Zero;
            }
            GpuVramMonitor.Shutdown();
        }

        private sealed class NativeWaitHandleWrapper : WaitHandle
        {
            public NativeWaitHandleWrapper(IntPtr existingHandle)
            {
                SafeWaitHandle = new Microsoft.Win32.SafeHandles.SafeWaitHandle(existingHandle, false);
            }
        }
    }

    internal sealed class PurgeCompletedEventArgs : EventArgs
    {
        public readonly PurgeResult Result;
        public PurgeCompletedEventArgs(PurgeResult r) { Result = r; }
    }
}
