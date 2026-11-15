using System;
using System.Collections.Generic;
using System.Globalization;

namespace Legram
{
    public enum MemoryPressure
    {
        Healthy,
        Warning,
        Critical,
        Boosting
    }

    [Flags]
    public enum ProtectionReason
    {
        None = 0,
        SystemCore = 1 << 0,
        WigramMountOrNode = 1 << 1,
        ObsidianLocalRest = 1 << 2,
        BatereaiSibling = 1 << 3,
        HermesGateway = 1 << 4,
        T3CodeActive = 1 << 5,
        LocalTcpListener = 1 << 6,
        ForegroundWindow = 1 << 7,
        ActiveAudioStream = 1 << 8,
        CreativeRender = 1 << 9
    }

    public enum GroupIconKind
    {
        Browser,
        ProtectedShield,
        AiSparkle,
        TerminalNode,
        CreativeMedia,
        AppCube
    }

    public sealed class GpuSnapshot
    {
        public bool Available;
        public string Name = "Windows WDDM GPU";
        public string ShortName = "GPU";
        public long VramUsedBytes;
        public long VramReservedBytes;
        public long VramTotalBytes = 12L * 1024 * 1024 * 1024;
        public int TemperatureC;
        public int GpuUtilPercent;
        public int DecoderUtilPercent;
        public int EncoderUtilPercent;
        public double PowerWatts;
        public long ReclaimableVramBytes;
        public List<int> RefreshableBrowserGpuPids = new List<int>();
        public int IdleOllamaModelCount;
        public long IdleOllamaVramBytes;
        public string IdleOllamaSummary = "";
        public string TopVramSummary = "";

        public double VramUsedPercent
        {
            get { return VramTotalBytes > 0 ? (VramUsedBytes * 100.0 / VramTotalBytes) : 0.0; }
        }

        public bool IsRenderingOrDecoding
        {
            get { return GpuUtilPercent >= 25 || DecoderUtilPercent >= 15 || EncoderUtilPercent >= 10; }
        }

        public bool IsHighAlert
        {
            get { return Available && (VramUsedPercent >= 80.0 || TemperatureC >= 78); }
        }
    }

    public sealed class DriveSnapshot
    {
        public string Root = "";
        public string Letter = "";
        public string Label = "";
        public string HardwareModel = "";
        public string FileSystem = "";
        public int BusType;
        public long FreeBytes;
        public long TotalBytes;
        public bool IsVirtualOrCloud;

        public long UsedBytes
        {
            get { return Math.Max(0L, TotalBytes - FreeBytes); }
        }

        public double FreePercent
        {
            get { return TotalBytes > 0 ? (FreeBytes * 100.0 / TotalBytes) : 100.0; }
        }

        public bool IsCritical
        {
            get { return !IsVirtualOrCloud && TotalBytes > 0 && (FreePercent < 6.0 || FreeBytes < 18L * 1024 * 1024 * 1024); }
        }

        public bool IsTight
        {
            get { return !IsVirtualOrCloud && TotalBytes > 0 && (FreePercent < 15.0 || FreeBytes < 35L * 1024 * 1024 * 1024); }
        }
    }

    public sealed class ProcessGroupItem
    {
        public string GroupKey = "";
        public string DisplayName = "";
        public string Subtitle = "";
        public string RepresentativeExePath = "";
        public GroupIconKind IconKind = GroupIconKind.AppCube;
        public int ProcessCount;
        public long WorkingSetBytes;
        public long DedicatedVramBytes;
        public long EstimTrimmableBytes;
        public bool IsProtected;
        public ProtectionReason Reason;
        public bool IsEcoQosActive;
        public bool IsForeground;
        public bool IsOrphanGroup;
        public List<int> Pids = new List<int>();
    }

    public sealed class SweepEstimate
    {
        public long RecycleBinBytes;
        public long LocalTempBytes;
        public long CrashDumpBytes;
        public long PhotoshopScratchBytes;
        public bool PhotoshopRunning;

        public long TotalBytes
        {
            get
            {
                return RecycleBinBytes + LocalTempBytes + CrashDumpBytes + (PhotoshopRunning ? 0 : PhotoshopScratchBytes);
            }
        }
    }

    public sealed class SystemSnapshot
    {
        public DateTime TimestampUtc = DateTime.UtcNow;
        public long InstalledDimmBytes;
        public long TotalRamBytes;
        public long HardwareReservedBytes;
        public long AvailableRamBytes;
        public long CommitTotalBytes;
        public long CommitLimitBytes;
        public long CommitPeakBytes;
        public long ModifiedBytes;
        public long FreeZeroBytes;
        public long StandbyTotalBytes;
        public long StandbyLowPrioBytes; // P0..P4
        public long StandbyP0Bytes;
        public long WorkingSetTrimmableBytes;
        public int UsedRamPercent;
        public MemoryPressure Pressure;
        public bool RenderShieldActive;
        public bool KernelPurgeTaskReady;
        public int ProtectedProcessCount;
        public int TcpListenerCount;
        public int ActiveAudioCount;
        public int EcoQosThrottledCount;
        public int OrphanAiCount;
        public long OrphanAiBytes;
        public int DwmPid;
        public long DwmWorkingSetBytes;
        public long DwmVramBytes;
        public string GuardTooltipSummary = "";
        public GpuSnapshot Gpu = new GpuSnapshot();
        public List<DriveSnapshot> Drives = new List<DriveSnapshot>();
        public DriveSnapshot PrimaryDisplayDrive;
        public SweepEstimate Junk = new SweepEstimate();
        public List<ProcessGroupItem> TopGroups = new List<ProcessGroupItem>();

        public int CommitUsedPercent
        {
            get
            {
                if (CommitLimitBytes <= 0) return 0;
                int pct = (int)Math.Round(CommitTotalBytes * 100.0 / CommitLimitBytes);
                return Math.Max(0, Math.Min(100, pct));
            }
        }

        public bool IsCommitWarning
        {
            get { return CommitUsedPercent >= 80; }
        }

        public bool IsCommitCritical
        {
            get { return CommitUsedPercent >= 88; }
        }

        // ISLC Dual-Threshold Anti-Stutter condition: Standby >= 4.0 GB while true Free+Zeroed < 1.0 GB
        public bool IsIslcStandbySaturated
        {
            get
            {
                return StandbyTotalBytes >= 4096L * 1048576L
                    && FreeZeroBytes > 0
                    && FreeZeroBytes < 1024L * 1048576L;
            }
        }

        public bool IsDwmBloated
        {
            get { return DwmWorkingSetBytes >= 650L * 1048576L; }
        }

        public long UsedRamBytes
        {
            get { return Math.Max(0L, TotalRamBytes - AvailableRamBytes); }
        }

        public long ActiveLockedBytes
        {
            get { return Math.Max(0L, UsedRamBytes - WorkingSetTrimmableBytes); }
        }

        public int PhysicalDriveCount
        {
            get
            {
                int c = 0;
                if (Drives != null)
                {
                    for (int i = 0; i < Drives.Count; i++)
                        if (!Drives[i].IsVirtualOrCloud) c++;
                }
                return c;
            }
        }

        public DriveSnapshot SystemDrive
        {
            get
            {
                if (Drives != null)
                {
                    for (int i = 0; i < Drives.Count; i++)
                    {
                        if (!Drives[i].IsVirtualOrCloud && string.Equals(Drives[i].Letter, "C:", StringComparison.OrdinalIgnoreCase))
                            return Drives[i];
                    }
                }
                return PrimaryDisplayDrive;
            }
        }

        public DriveSnapshot TightestNonSystemDrive
        {
            get
            {
                DriveSnapshot tightest = null;
                if (Drives != null)
                {
                    for (int i = 0; i < Drives.Count; i++)
                    {
                        DriveSnapshot d = Drives[i];
                        if (d.IsVirtualOrCloud || string.Equals(d.Letter, "C:", StringComparison.OrdinalIgnoreCase)) continue;
                        if (tightest == null)
                        {
                            tightest = d;
                            continue;
                        }
                        // Prioritize lowest absolute free bytes when < 25 GB, otherwise lowest percentage
                        bool dLowAbs = d.FreeBytes < 25L * 1073741824L;
                        bool tLowAbs = tightest.FreeBytes < 25L * 1073741824L;
                        if (dLowAbs && !tLowAbs) tightest = d;
                        else if (dLowAbs && tLowAbs && d.FreeBytes < tightest.FreeBytes) tightest = d;
                        else if (!dLowAbs && !tLowAbs && d.FreePercent < tightest.FreePercent) tightest = d;
                    }
                }
                return tightest;
            }
        }

        // Honest, un-inflated reclaimable estimate (zero artificial floor!)
        public long EstimatedPurgeGainBytes
        {
            get
            {
                long standbyEst = KernelPurgeTaskReady ? (long)(StandbyLowPrioBytes * 0.72) : (long)(StandbyP0Bytes * 0.50);
                long dwmEst = IsDwmBloated ? Math.Max(0L, DwmWorkingSetBytes - 220L * 1048576L) : 0L;
                return Math.Max(0L, WorkingSetTrimmableBytes + standbyEst + dwmEst);
            }
        }

        public long EstimatedVramAndAiGainBytes
        {
            get { return Math.Max(0L, Gpu.ReclaimableVramBytes + Gpu.IdleOllamaVramBytes + OrphanAiBytes); }
        }

        public long EstimatedHarmonyGainBytes
        {
            get
            {
                long diskPart = (Junk != null && Junk.TotalBytes >= 24L * 1024 * 1024) ? Junk.TotalBytes : 0L;
                return EstimatedPurgeGainBytes + EstimatedVramAndAiGainBytes + diskPart;
            }
        }
    }

    public sealed class PurgeResult
    {
        public bool Success;
        public long FreedRamBytes;
        public long FreedVramBytes;
        public long FreedDiskBytes;
        public int TrimmedProcessCount;
        public int ReapedOrphanCount;
        public int UnloadedOllamaModels;
        public bool KernelStandbyPurged;
        public bool RegistryCacheFlushed;
        public bool SystemCacheTrimmed;
        public bool DwmDeflated;
        public long DwmFreedBytes;
        public string TriggerReason = "Manual";
        public int DurationMs;
        public string SummaryText = "";
        public string PillToastText = "";
    }

    public static class Fmt
    {
        public static string BytesShort(long bytes)
        {
            if (bytes < 0) bytes = 0;
            double gb = bytes / 1073741824.0;
            if (gb >= 100.0) return gb.ToString("F0", CultureInfo.InvariantCulture) + " GB";
            if (gb >= 1.0) return gb.ToString("F1", CultureInfo.InvariantCulture) + " GB";
            double mb = bytes / 1048576.0;
            if (mb >= 1.0) return mb.ToString("F0", CultureInfo.InvariantCulture) + " MB";
            return (bytes / 1024.0).ToString("F0", CultureInfo.InvariantCulture) + " KB";
        }

        public static string BytesCompactG(long bytes)
        {
            if (bytes < 0) bytes = 0;
            double gb = bytes / 1073741824.0;
            if (gb >= 100.0) return gb.ToString("F0", CultureInfo.InvariantCulture) + "G";
            if (gb >= 0.95) return gb.ToString("F1", CultureInfo.InvariantCulture) + "G";
            double mb = Math.Max(1.0, bytes / 1048576.0);
            return mb.ToString("F0", CultureInfo.InvariantCulture) + "M";
        }

        public static string Gb2(long bytes)
        {
            double gb = Math.Max(0.0, bytes / 1073741824.0);
            if (gb >= 1.0) return gb.ToString("F2", CultureInfo.InvariantCulture) + " GB";
            double mb = Math.Max(1.0, bytes / 1048576.0);
            return mb.ToString("F0", CultureInfo.InvariantCulture) + " MB";
        }

        public static string Gb1(long bytes)
        {
            double gb = Math.Max(0.0, bytes / 1073741824.0);
            return gb.ToString("F1", CultureInfo.InvariantCulture);
        }

        public static string PressureWord(MemoryPressure p)
        {
            switch (p)
            {
                case MemoryPressure.Healthy: return "Clear";
                case MemoryPressure.Warning: return "Busy";
                case MemoryPressure.Critical: return "Tight";
                case MemoryPressure.Boosting: return "Tuned";
                default: return "Clear";
            }
        }
    }
}
