using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Legram
{
    internal static class StorageSweeper
    {
        private sealed class CachedDriveHw
        {
            public string Label = "";
            public string HardwareModel = "";
            public string FileSystem = "";
            public int BusType = -1;
            public bool IsVirtualOrCloud;
        }

        private static readonly object CacheSync = new object();
        private static readonly Dictionary<string, CachedDriveHw> HwCache = new Dictionary<string, CachedDriveHw>(StringComparer.OrdinalIgnoreCase);
        private static SweepEstimate cachedEstimate = new SweepEstimate();
        private static DateTime lastEstimateUtc = DateTime.MinValue;

        public static List<DriveSnapshot> ScanDrives(out DriveSnapshot primaryDisplayDrive)
        {
            List<DriveSnapshot> list = new List<DriveSnapshot>();
            primaryDisplayDrive = null;

            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                for (int i = 0; i < drives.Length; i++)
                {
                    DriveInfo d = drives[i];
                    if (!d.IsReady) continue;

                    string root = d.Name;
                    string letter = root.Length >= 2 ? root.Substring(0, 2) : root;

                    CachedDriveHw hw;
                    lock (CacheSync)
                    {
                        HwCache.TryGetValue(letter, out hw);
                    }

                    if (hw == null)
                    {
                        StringBuilder volName = new StringBuilder(260);
                        StringBuilder fsName = new StringBuilder(260);
                        uint s1, s2, s3;
                        NativeApi.GetVolumeInformationW(root, volName, volName.Capacity, out s1, out s2, out s3, fsName, fsName.Capacity);

                        int busType = -1;
                        string hwModel = "";
                        QueryStorageBusAndModel(letter, out busType, out hwModel);

                        string fs = fsName.ToString();
                        bool isRealHardware = (busType == 3 || busType == 7 || busType == 8 || busType == 11 || busType == 17)
                            && fs.IndexOf("Google", StringComparison.OrdinalIgnoreCase) < 0
                            && fs.IndexOf("Wigram", StringComparison.OrdinalIgnoreCase) < 0
                            && !string.Equals(letter, "V:", StringComparison.OrdinalIgnoreCase);

                        hw = new CachedDriveHw
                        {
                            Label = volName.ToString(),
                            HardwareModel = hwModel,
                            FileSystem = fs,
                            BusType = busType,
                            IsVirtualOrCloud = !isRealHardware
                        };
                        lock (CacheSync)
                        {
                            HwCache[letter] = hw;
                        }
                    }

                    DriveSnapshot snap = new DriveSnapshot
                    {
                        Root = root,
                        Letter = letter,
                        Label = hw.Label,
                        HardwareModel = hw.HardwareModel,
                        FileSystem = hw.FileSystem,
                        BusType = hw.BusType,
                        FreeBytes = d.AvailableFreeSpace,
                        TotalBytes = d.TotalSize,
                        IsVirtualOrCloud = hw.IsVirtualOrCloud
                    };
                    list.Add(snap);
                }
            }
            catch (Exception ex)
            {
                Log.Error("StorageSweeper.ScanDrives", ex);
            }

            // Pick the most critical physical drive (lowest FreePercent if < 12%, or lowest FreeBytes, fallback C:)
            DriveSnapshot best = null;
            for (int i = 0; i < list.Count; i++)
            {
                DriveSnapshot ds = list[i];
                if (ds.IsVirtualOrCloud) continue;
                if (best == null)
                {
                    best = ds;
                    continue;
                }
                if (ds.IsCritical && !best.IsCritical)
                {
                    best = ds;
                }
                else if (ds.IsCritical == best.IsCritical && ds.FreePercent < best.FreePercent)
                {
                    best = ds;
                }
            }

            primaryDisplayDrive = best;
            return list;
        }

        private static void QueryStorageBusAndModel(string letter, out int busType, out string hwModel)
        {
            busType = -1;
            hwModel = "";
            IntPtr hVol = NativeApi.CreateFileW(
                @"\\.\" + letter, 0,
                NativeApi.FILE_SHARE_READ | NativeApi.FILE_SHARE_WRITE,
                IntPtr.Zero, NativeApi.OPEN_EXISTING, 0, IntPtr.Zero);

            if (hVol == IntPtr.Zero || hVol == new IntPtr(-1)) return;
            IntPtr outBuf = Marshal.AllocHGlobal(1024);
            try
            {
                NativeApi.STORAGE_PROPERTY_QUERY q = new NativeApi.STORAGE_PROPERTY_QUERY
                {
                    PropertyId = 0,
                    QueryType = 0,
                    AdditionalParameters = 0
                };
                uint retBytes;
                if (NativeApi.DeviceIoControlStorage(
                    hVol, NativeApi.IOCTL_STORAGE_QUERY_PROPERTY,
                    ref q, (uint)Marshal.SizeOf(typeof(NativeApi.STORAGE_PROPERTY_QUERY)),
                    outBuf, 1024, out retBytes, IntPtr.Zero)
                    && retBytes >= Marshal.SizeOf(typeof(NativeApi.STORAGE_DEVICE_DESCRIPTOR_HEADER)))
                {
                    NativeApi.STORAGE_DEVICE_DESCRIPTOR_HEADER hdr =
                        (NativeApi.STORAGE_DEVICE_DESCRIPTOR_HEADER)Marshal.PtrToStructure(outBuf, typeof(NativeApi.STORAGE_DEVICE_DESCRIPTOR_HEADER));
                    busType = hdr.BusType;
                    string vendor = ReadAsciiAt(outBuf, hdr.VendorIdOffset, retBytes);
                    string product = ReadAsciiAt(outBuf, hdr.ProductIdOffset, retBytes);
                    hwModel = (vendor + " " + product).Trim();
                }
            }
            catch
            {
            }
            finally
            {
                Marshal.FreeHGlobal(outBuf);
                NativeApi.CloseHandle(hVol);
            }
        }

        private static string ReadAsciiAt(IntPtr buf, uint offset, uint maxLen)
        {
            if (offset == 0 || offset >= maxLen) return "";
            StringBuilder sb = new StringBuilder();
            for (uint i = offset; i < maxLen; i++)
            {
                byte b = Marshal.ReadByte(buf, (int)i);
                if (b == 0) break;
                if (b >= 32 && b <= 126) sb.Append((char)b);
            }
            return sb.ToString().Trim();
        }

        public static SweepEstimate GetJunkEstimate(List<DriveSnapshot> drives, bool forceRefresh)
        {
            lock (CacheSync)
            {
                if (!forceRefresh && (DateTime.UtcNow - lastEstimateUtc).TotalSeconds < 60)
                {
                    return cachedEstimate;
                }
            }

            SweepEstimate est = ComputeJunkEstimate(drives);
            lock (CacheSync)
            {
                cachedEstimate = est;
                lastEstimateUtc = DateTime.UtcNow;
            }
            return est;
        }

        private static SweepEstimate ComputeJunkEstimate(List<DriveSnapshot> drives)
        {
            SweepEstimate est = new SweepEstimate();
            est.PhotoshopRunning = Process.GetProcessesByName("Photoshop").Length > 0;

            // 1. Recycle Bin across all volumes
            try
            {
                NativeApi.SHQUERYRBINFO rbi = new NativeApi.SHQUERYRBINFO();
                rbi.cbSize = Marshal.SizeOf(typeof(NativeApi.SHQUERYRBINFO));
                if (NativeApi.SHQueryRecycleBinW(null, ref rbi) == 0 && rbi.i64Size > 0)
                {
                    est.RecycleBinBytes = rbi.i64Size;
                }
            }
            catch { }

            // 2. %LOCALAPPDATA%\Temp files older than 24h
            DateTime cutoffUtc = DateTime.UtcNow.AddHours(-24);
            string tempDir = Path.GetTempPath();
            est.LocalTempBytes = SumDirectoryFilesOlderThan(tempDir, cutoffUtc, 2);

            // 3. CrashDumps & RdClientAutoTrace
            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            est.CrashDumpBytes += SumDirectoryFilesOlderThan(Path.Combine(localApp, "CrashDumps"), DateTime.UtcNow.AddHours(-2), 1);
            est.CrashDumpBytes += SumDirectoryFilesOlderThan(Path.Combine(localApp, @"Temp\DiagOutputDir\RdClientAutoTrace"), cutoffUtc, 1);

            // 4. Orphaned Photoshop Temp* scratch files on real physical drives ONLY
            if (drives != null)
            {
                for (int i = 0; i < drives.Count; i++)
                {
                    DriveSnapshot d = drives[i];
                    if (d.IsVirtualOrCloud) continue;
                    try
                    {
                        string[] psFiles = Directory.GetFiles(d.Root, "Photoshop Temp*");
                        for (int k = 0; k < psFiles.Length; k++)
                        {
                            try { est.PhotoshopScratchBytes += new FileInfo(psFiles[k]).Length; } catch { }
                        }
                    }
                    catch { }
                }
            }

            return est;
        }

        private static long SumDirectoryFilesOlderThan(string rootDir, DateTime cutoffUtc, int maxDepth)
        {
            if (string.IsNullOrEmpty(rootDir) || !Directory.Exists(rootDir)) return 0;
            long sum = 0;
            try
            {
                string[] files = Directory.GetFiles(rootDir);
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        FileInfo fi = new FileInfo(files[i]);
                        if (fi.LastWriteTimeUtc < cutoffUtc) sum += fi.Length;
                    }
                    catch { }
                }

                if (maxDepth > 0)
                {
                    string[] subDirs = Directory.GetDirectories(rootDir);
                    for (int i = 0; i < subDirs.Length; i++)
                    {
                        string name = Path.GetFileName(subDirs[i]).ToLowerInvariant();
                        if (name.Contains("wigram") || name.Contains("t3") || name.Contains("cbm") || name.Contains("antigravity"))
                            continue;
                        sum += SumDirectoryFilesOlderThan(subDirs[i], cutoffUtc, maxDepth - 1);
                    }
                }
            }
            catch { }
            return sum;
        }

        public static long ExecuteSafeSweep(List<DriveSnapshot> drives)
        {
            long freedBytes = 0;
            bool photoshopRunning = Process.GetProcessesByName("Photoshop").Length > 0;

            // 1. Empty Recycle Bin silently (SHERB_NOCONFIRMATION=1 | SHERB_NOPROGRESSUI=2 | SHERB_NOSOUND=4 = 7)
            try
            {
                NativeApi.SHQUERYRBINFO rbi = new NativeApi.SHQUERYRBINFO();
                rbi.cbSize = Marshal.SizeOf(typeof(NativeApi.SHQUERYRBINFO));
                if (NativeApi.SHQueryRecycleBinW(null, ref rbi) == 0 && rbi.i64Size > 0)
                {
                    long before = rbi.i64Size;
                    if (NativeApi.SHEmptyRecycleBinW(IntPtr.Zero, null, 7u) == 0)
                    {
                        freedBytes += before;
                    }
                }
            }
            catch { }

            // 2. Delete unlocked %TEMP% files older than 24h
            DateTime cutoffUtc = DateTime.UtcNow.AddHours(-24);
            freedBytes += DeleteFilesOlderThan(Path.GetTempPath(), cutoffUtc, 2);

            // 3. Delete CrashDumps & RdClientAutoTrace
            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            freedBytes += DeleteFilesOlderThan(Path.Combine(localApp, "CrashDumps"), DateTime.UtcNow.AddHours(-2), 1);
            freedBytes += DeleteFilesOlderThan(Path.Combine(localApp, @"Temp\DiagOutputDir\RdClientAutoTrace"), cutoffUtc, 1);

            // 4. Delete orphaned Photoshop Temp* ONLY if Photoshop is NOT running
            if (!photoshopRunning && drives != null)
            {
                for (int i = 0; i < drives.Count; i++)
                {
                    DriveSnapshot d = drives[i];
                    if (d.IsVirtualOrCloud) continue;
                    try
                    {
                        string[] psFiles = Directory.GetFiles(d.Root, "Photoshop Temp*");
                        for (int k = 0; k < psFiles.Length; k++)
                        {
                            try
                            {
                                FileInfo fi = new FileInfo(psFiles[k]);
                                long len = fi.Length;
                                fi.Delete();
                                freedBytes += len;
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }

            lock (CacheSync)
            {
                lastEstimateUtc = DateTime.MinValue;
            }
            return freedBytes;
        }

        private static long DeleteFilesOlderThan(string rootDir, DateTime cutoffUtc, int maxDepth)
        {
            if (string.IsNullOrEmpty(rootDir) || !Directory.Exists(rootDir)) return 0;
            long freed = 0;
            try
            {
                string[] files = Directory.GetFiles(rootDir);
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        FileInfo fi = new FileInfo(files[i]);
                        if (fi.LastWriteTimeUtc < cutoffUtc)
                        {
                            long len = fi.Length;
                            fi.Delete();
                            freed += len;
                        }
                    }
                    catch { }
                }

                if (maxDepth > 0)
                {
                    string[] subDirs = Directory.GetDirectories(rootDir);
                    for (int i = 0; i < subDirs.Length; i++)
                    {
                        string name = Path.GetFileName(subDirs[i]).ToLowerInvariant();
                        if (name.Contains("wigram") || name.Contains("t3") || name.Contains("cbm") || name.Contains("antigravity"))
                            continue;
                        freed += DeleteFilesOlderThan(subDirs[i], cutoffUtc, maxDepth - 1);
                        try
                        {
                            if (Directory.GetFiles(subDirs[i]).Length == 0 && Directory.GetDirectories(subDirs[i]).Length == 0)
                                Directory.Delete(subDirs[i], false);
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return freed;
        }
    }
}
