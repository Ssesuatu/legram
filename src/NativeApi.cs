using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace Legram
{
    internal static class NativeApi
    {
        // --- Process Access Rights & Constants ---
        internal const uint PROCESS_TERMINATE = 0x0001;
        internal const uint PROCESS_SET_QUOTA = 0x0100;
        internal const uint PROCESS_SET_INFORMATION = 0x0200;
        internal const uint PROCESS_QUERY_INFORMATION = 0x0400;
        internal const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        internal const uint NORMAL_PRIORITY_CLASS = 0x00000020;
        internal const uint BELOW_NORMAL_PRIORITY_CLASS = 0x00004000;

        internal const int ProcessMemoryPriority = 0;
        internal const int ProcessPowerThrottling = 4;
        internal const int ProcessIoPriority = 33;
        internal const int IoPriorityLow = 1;
        internal const int IoPriorityNormal = 2;
        internal const uint MEMORY_PRIORITY_LOW = 2;
        internal const uint MEMORY_PRIORITY_NORMAL = 5;
        internal const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
        internal const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;

        internal const int SystemMemoryListInformation = 80;
        internal const int SystemCombinePhysicalMemoryInformation = 130;
        internal const int SystemRegistryReconciliationInformation = 155;
        internal const int MemoryPurgeStandbyList = 4;
        internal const int MemoryPurgeLowPriorityStandbyList = 5;

        // --- Window & DWM Constants ---
        internal const int GWL_STYLE = -16;
        internal const int GWL_EXSTYLE = -20;
        internal const int WS_MINIMIZEBOX = 0x00020000;
        internal const int WS_EX_TOOLWINDOW = 0x00000080;
        internal const int WS_EX_NOACTIVATE = 0x08000000;
        internal const int WM_MOUSEACTIVATE = 0x0021;
        internal const int WM_DPICHANGED = 0x02E0;
        internal const int MA_NOACTIVATE = 3;
        internal const uint SWP_NOSIZE = 0x0001;
        internal const uint SWP_NOMOVE = 0x0002;
        internal const uint SWP_NOZORDER = 0x0004;
        internal const uint SWP_NOACTIVATE = 0x0010;

        // --- 1. Memory & Kernel Telemetry (ntdll + kernel32) ---
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        internal sealed class MEMORYSTATUSEX
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct SYSTEM_MEMORY_LIST_INFORMATION
        {
            public UIntPtr ZeroPageCount;
            public UIntPtr FreePageCount;
            public UIntPtr ModifiedPageCount;
            public UIntPtr ModifiedNoWritePageCount;
            public UIntPtr BadPageCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public UIntPtr[] PageCountByPriority;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public UIntPtr[] RepurposedPagesByPriority;
            public UIntPtr ModifiedPageCountPageFile;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PERFORMANCE_INFORMATION
        {
            public uint cb;
            public UIntPtr CommitTotal;
            public UIntPtr CommitLimit;
            public UIntPtr CommitPeak;
            public UIntPtr PhysicalTotal;
            public UIntPtr PhysicalAvailable;
            public UIntPtr SystemCache;
            public UIntPtr KernelTotal;
            public UIntPtr KernelPaged;
            public UIntPtr KernelNonpaged;
            public UIntPtr PageSize;
            public uint HandleCount;
            public uint ProcessCount;
            public uint ThreadCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_MEMORY_COUNTERS
        {
            public uint cb;
            public uint PageFaultCount;
            public UIntPtr PeakWorkingSetSize;
            public UIntPtr WorkingSetSize;
            public UIntPtr QuotaPeakPagedPoolUsage;
            public UIntPtr QuotaPagedPoolUsage;
            public UIntPtr QuotaPeakNonPagedPoolUsage;
            public UIntPtr QuotaNonPagedPoolUsage;
            public UIntPtr PagefileUsage;
            public UIntPtr PeakPagefileUsage;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MEMORY_COMBINE_INFORMATION_EX
        {
            public IntPtr Handle;
            public UIntPtr PagesCombined;
            public ulong Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MEMORY_PRIORITY_INFORMATION
        {
            public uint MemoryPriority;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_POWER_THROTTLING_STATE
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GetPhysicallyInstalledSystemMemory(out ulong TotalMemoryInKilobytes);

        [DllImport("kernel32.dll", EntryPoint = "K32GetPerformanceInfo", SetLastError = true)]
        internal static extern bool K32GetPerformanceInfo(out PERFORMANCE_INFORMATION pPerformanceInformation, uint cb);

        [DllImport("kernel32.dll", EntryPoint = "K32GetProcessMemoryInfo", SetLastError = true)]
        internal static extern bool K32GetProcessMemoryInfo(IntPtr Process, out PROCESS_MEMORY_COUNTERS ppsmemCounters, uint cb);

        [DllImport("kernel32.dll", EntryPoint = "K32EmptyWorkingSet", SetLastError = true)]
        internal static extern bool K32EmptyWorkingSet(IntPtr hProcess);

        [DllImport("ntdll.dll")]
        internal static extern int NtQuerySystemInformation(int SystemInformationClass, IntPtr SystemInformation, int SystemInformationLength, out int ReturnLength);

        [DllImport("ntdll.dll")]
        internal static extern int NtSetSystemInformation(int InfoClass, ref int Command, int Length);

        [DllImport("ntdll.dll")]
        internal static extern int NtSetSystemInformation(int InfoClass, ref MEMORY_COMBINE_INFORMATION_EX Info, int Length);

        [DllImport("ntdll.dll")]
        internal static extern int NtSetSystemInformation(int InfoClass, IntPtr Info, int Length);

        [DllImport("ntdll.dll")]
        internal static extern int NtSetInformationProcess(IntPtr ProcessHandle, int ProcessInformationClass, ref int ProcessInformation, int ProcessInformationLength);

        [DllImport("ntdll.dll")]
        internal static extern int NtQueryInformationProcess(IntPtr ProcessHandle, int ProcessInformationClass, out int ProcessInformation, int ProcessInformationLength, out int ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetSystemFileCacheSize(IntPtr MinimumFileCacheSize, IntPtr MaximumFileCacheSize, int Flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr CreateMemoryResourceNotification(int NotificationType);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool QueryMemoryResourceNotification(IntPtr ResourceNotificationHandle, out bool ResourceState);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool QueryFullProcessImageNameW(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetProcessInformation(IntPtr hProcess, int ProcessInformationClass, ref MEMORY_PRIORITY_INFORMATION Info, uint Size);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetProcessInformation(IntPtr hProcess, int ProcessInformationClass, ref PROCESS_POWER_THROTTLING_STATE State, uint Size);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);

        [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
        internal static extern uint DnsFlushResolverCache();

        // --- 2. Token Privileges for Elevated Helper (advapi32) ---
        [StructLayout(LayoutKind.Sequential)]
        internal struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID Luid;
            public uint Attributes;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool LookupPrivilegeValueW(string lpSystemName, string lpName, out LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, uint BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

        // --- 3. Toolhelp32 Snapshot & TCP Table (kernel32 + iphlpapi) ---
        internal const uint TH32CS_SNAPPROCESS = 0x00000002;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct PROCESSENTRY32W
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool Process32FirstW(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool Process32NextW(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        internal static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int dwOutBufLen, bool sort, int ipVersion, int tblClass, uint reserved);

        // --- 4. Storage Bus Property & Recycle Bin (kernel32 + shell32) ---
        internal const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x002D1400;
        internal const uint FILE_SHARE_READ = 0x00000001;
        internal const uint FILE_SHARE_WRITE = 0x00000002;
        internal const uint OPEN_EXISTING = 3;

        [StructLayout(LayoutKind.Sequential)]
        internal struct STORAGE_PROPERTY_QUERY
        {
            public int PropertyId;
            public int QueryType;
            public byte AdditionalParameters;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct STORAGE_DEVICE_DESCRIPTOR_HEADER
        {
            public uint Version;
            public uint Size;
            public byte DeviceType;
            public byte DeviceTypeModifier;
            [MarshalAs(UnmanagedType.U1)]
            public bool RemovableMedia;
            [MarshalAs(UnmanagedType.U1)]
            public bool CommandQueueing;
            public uint VendorIdOffset;
            public uint ProductIdOffset;
            public uint ProductRevisionOffset;
            public uint SerialNumberOffset;
            public int BusType;
            public uint RawPropertiesLength;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr CreateFileW(
            string lpFileName, uint dwDesiredAccess, uint dwShareMode,
            IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)]
        internal static extern bool DeviceIoControlStorage(
            IntPtr hDevice, uint dwIoControlCode, ref STORAGE_PROPERTY_QUERY lpInBuffer, uint nInBufferSize,
            IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool GetVolumeInformationW(
            string lpRootPathName, StringBuilder lpVolumeNameBuffer, int nVolumeNameSize,
            out uint lpVolumeSerialNumber, out uint lpMaximumComponentLength, out uint lpFileSystemFlags,
            StringBuilder lpFileSystemNameBuffer, int nFileSystemNameSize);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        internal struct SHQUERYRBINFO
        {
            public int cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        internal static extern int SHQueryRecycleBinW(string pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        internal static extern int SHEmptyRecycleBinW(IntPtr hwnd, string pszRootPath, uint dwFlags);

        // --- 5. NVIDIA NVML v2 + Taskmgr PDH GPU Memory (nvml.dll + pdh.dll) ---
        [StructLayout(LayoutKind.Sequential)]
        internal struct nvmlMemory_v2_t
        {
            public uint version;
            public ulong total;
            public ulong reserved;
            public ulong free;
            public ulong used;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct nvmlMemory_t
        {
            public ulong total;
            public ulong free;
            public ulong used;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct nvmlUtilization_t
        {
            public uint gpu;
            public uint memory;
        }

        [DllImport("nvml.dll", EntryPoint = "nvmlInit_v2")]
        internal static extern int NvmlInit();

        [DllImport("nvml.dll", EntryPoint = "nvmlShutdown")]
        internal static extern int NvmlShutdown();

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetHandleByIndex_v2")]
        internal static extern int NvmlDeviceGetHandleByIndex(uint index, out IntPtr device);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetName", CharSet = CharSet.Ansi)]
        internal static extern int NvmlDeviceGetName(IntPtr device, StringBuilder name, uint length);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetMemoryInfo_v2")]
        internal static extern int NvmlDeviceGetMemoryInfoV2(IntPtr device, ref nvmlMemory_v2_t memory);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetMemoryInfo")]
        internal static extern int NvmlDeviceGetMemoryInfo(IntPtr device, out nvmlMemory_t memory);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetTemperature")]
        internal static extern int NvmlDeviceGetTemperature(IntPtr device, int sensorType, out uint temp);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetUtilizationRates")]
        internal static extern int NvmlDeviceGetUtilizationRates(IntPtr device, out nvmlUtilization_t util);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetPowerUsage")]
        internal static extern int NvmlDeviceGetPowerUsage(IntPtr device, out uint powerMilliwatts);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetDecoderUtilization")]
        internal static extern int NvmlDeviceGetDecoderUtilization(IntPtr device, out uint utilization, out uint samplingPeriodUs);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetEncoderUtilization")]
        internal static extern int NvmlDeviceGetEncoderUtilization(IntPtr device, out uint utilization, out uint samplingPeriodUs);

        [StructLayout(LayoutKind.Sequential)]
        internal struct PDH_FMT_COUNTERVALUE
        {
            public uint CStatus;
            public long largeValue;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PDH_FMT_COUNTERVALUE_ITEM_W
        {
            public IntPtr szName;
            public PDH_FMT_COUNTERVALUE FmtValue;
        }

        internal const uint PDH_FMT_LARGE = 0x00000400;
        internal const uint PDH_MORE_DATA = 0x800007D2;

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        internal static extern uint PdhOpenQueryW(IntPtr szDataSource, IntPtr dwUserData, out IntPtr phQuery);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        internal static extern uint PdhAddEnglishCounterW(IntPtr hQuery, string szFullCounterPath, IntPtr dwUserData, out IntPtr phCounter);

        [DllImport("pdh.dll")]
        internal static extern uint PdhCollectQueryData(IntPtr hQuery);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        internal static extern uint PdhGetFormattedCounterArrayW(IntPtr hCounter, uint dwFormat, ref uint lpdwBufferSize, out uint lpdwItemCount, IntPtr ItemBuffer);

        [DllImport("pdh.dll")]
        internal static extern uint PdhCloseQuery(IntPtr hQuery);

        // --- 6. User32, WinEventHook, DWM & Per-Monitor DPI ---
        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT { public int L, T, R, B; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MONITORINFO
        {
            public int Size;
            public RECT Monitor;
            public RECT Work;
            public uint Flags;
        }

        internal delegate void WinEventDelegate(
            IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject,
            int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        internal static extern IntPtr SetWinEventHook(
            uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc,
            uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        internal static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        internal static extern bool GetCursorPos(out POINT pt);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        internal static extern IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        internal static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        internal static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll")]
        internal static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("shcore.dll")]
        internal static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        [DllImport("user32.dll")]
        internal static extern uint GetDpiForSystem();

        [DllImport("user32.dll")]
        internal static extern int GetSystemMetricsForDpi(int nIndex, uint dpi);

        [DllImport("dwmapi.dll")]
        internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll")]
        internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref uint pvAttribute, int cbAttribute);

        internal static Int32Rect WorkAreaAt(int x, int y, out uint dpi)
        {
            POINT p = new POINT { X = x, Y = y };
            IntPtr m = MonitorFromPoint(p, 2);
            MONITORINFO mi = new MONITORINFO { Size = Marshal.SizeOf(typeof(MONITORINFO)) };
            GetMonitorInfo(m, ref mi);
            uint dx, dy;
            dpi = GetDpiForMonitor(m, 0, out dx, out dy) == 0 ? dx : 96u;
            return new Int32Rect(mi.Work.L, mi.Work.T, mi.Work.R - mi.Work.L, mi.Work.B - mi.Work.T);
        }

        internal static Int32Rect WorkAreaForWindow(IntPtr h, out uint dpi)
        {
            IntPtr m = MonitorFromWindow(h, 2);
            MONITORINFO mi = new MONITORINFO { Size = Marshal.SizeOf(typeof(MONITORINFO)) };
            GetMonitorInfo(m, ref mi);
            uint dx, dy;
            dpi = GetDpiForMonitor(m, 0, out dx, out dy) == 0 ? dx : 96u;
            return new Int32Rect(mi.Work.L, mi.Work.T, mi.Work.R - mi.Work.L, mi.Work.B - mi.Work.T);
        }

        internal static int SmallIconSize()
        {
            try { return GetSystemMetricsForDpi(49, GetDpiForSystem()); }
            catch { return 16; }
        }

        internal static void SelfTrimWorkingSet()
        {
            try { SetProcessWorkingSetSize(GetCurrentProcess(), new IntPtr(-1), new IntPtr(-1)); }
            catch { }
        }

        [DllImport("ntdll.dll")]
        internal static extern int NtQueryInformationProcess(
            IntPtr ProcessHandle, int ProcessInformationClass,
            IntPtr ProcessInformation, int ProcessInformationLength, out int ReturnLength);

        [StructLayout(LayoutKind.Sequential)]
        private struct UNICODE_STRING
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;
        }

        // ProcessCommandLineInformation = 60 (Windows 8.1+ / Win11 native zero-WMI command line query!)
        internal static string QueryProcessCommandLineFast(int pid)
        {
            IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero) return "";
            try
            {
                int retLen = 0;
                NtQueryInformationProcess(h, 60, IntPtr.Zero, 0, out retLen);
                if (retLen <= 0 || retLen > 65536) return "";
                IntPtr buf = Marshal.AllocHGlobal(retLen);
                try
                {
                    if (NtQueryInformationProcess(h, 60, buf, retLen, out retLen) == 0)
                    {
                        UNICODE_STRING us = (UNICODE_STRING)Marshal.PtrToStructure(buf, typeof(UNICODE_STRING));
                        if (us.Buffer != IntPtr.Zero && us.Length > 0)
                        {
                            return Marshal.PtrToStringUni(us.Buffer, us.Length / 2) ?? "";
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buf);
                }
            }
            catch { }
            finally
            {
                CloseHandle(h);
            }
            return "";
        }

        internal static void ApplyWin11PopoverAttributes(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            try
            {
                int dark = 1;
                DwmSetWindowAttribute(hwnd, 20, ref dark, 4); // DWMWA_USE_IMMERSIVE_DARK_MODE
                int round = 2; // DWMWCP_ROUND
                DwmSetWindowAttribute(hwnd, 33, ref round, 4); // DWMWA_WINDOW_CORNER_PREFERENCE
                uint borderColor = 0x003A3636; // COLORREF BGR for #36363A
                DwmSetWindowAttribute(hwnd, 34, ref borderColor, 4); // DWMWA_BORDER_COLOR
            }
            catch { }
        }
    }

    // --- 7. CoreAudio Active Playback PID Shield (COM with deterministic RCW cleanup) ---
    internal static class CoreAudioShield
    {
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorComObject { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        }

        [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionManager2
        {
            int GetAudioSessionControl(IntPtr AudioSessionGuid, uint StreamFlags, IntPtr SessionControl);
            int GetSimpleAudioVolume(IntPtr AudioSessionGuid, uint StreamFlags, IntPtr AudioVolume);
            int GetSessionEnumerator(out IAudioSessionEnumerator SessionEnum);
        }

        [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionEnumerator
        {
            int GetCount(out int SessionCount);
            int GetSession(int SessionCount, out IAudioSessionControl Session);
        }

        [ComImport, Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionControl
        {
            int GetState(out int pRetVal);
        }

        [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionControl2
        {
            int GetState(out int pRetVal);
            int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
            int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string Value, Guid EventContext);
            int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
            int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string Value, Guid EventContext);
            int GetGroupingParam(out Guid pRetVal);
            int SetGroupingParam(Guid Override, Guid EventContext);
            int RegisterAudioSessionNotification(IntPtr NewNotifications);
            int UnregisterAudioSessionNotification(IntPtr NewNotifications);
            int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
            int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
            int GetProcessId(out uint pRetVal);
        }

        internal static void CollectActiveAudioPids(System.Collections.Generic.HashSet<int> activePids)
        {
            IMMDeviceEnumerator enumerator = null;
            try
            {
                enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
                for (int dataFlow = 0; dataFlow <= 1; dataFlow++)
                {
                    IMMDevice endpoint = null;
                    object objMgr = null;
                    IAudioSessionEnumerator sesEnum = null;
                    try
                    {
                        if (enumerator.GetDefaultAudioEndpoint(dataFlow, 1, out endpoint) == 0 && endpoint != null)
                        {
                            Guid iidMgr = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
                            if (endpoint.Activate(ref iidMgr, 23, IntPtr.Zero, out objMgr) == 0 && objMgr != null)
                            {
                                IAudioSessionManager2 mgr = (IAudioSessionManager2)objMgr;
                                if (mgr.GetSessionEnumerator(out sesEnum) == 0 && sesEnum != null)
                                {
                                    int count = 0;
                                    sesEnum.GetCount(out count);
                                    for (int i = 0; i < count; i++)
                                    {
                                        IAudioSessionControl ctl = null;
                                        try
                                        {
                                            if (sesEnum.GetSession(i, out ctl) == 0 && ctl != null)
                                            {
                                                IAudioSessionControl2 ctl2 = ctl as IAudioSessionControl2;
                                                if (ctl2 != null)
                                                {
                                                    int st = 0;
                                                    uint pid = 0;
                                                    ctl2.GetState(out st);
                                                    ctl2.GetProcessId(out pid);
                                                    if (st == 1 && pid > 4) activePids.Add((int)pid);
                                                }
                                            }
                                        }
                                        finally
                                        {
                                            if (ctl != null) Marshal.FinalReleaseComObject(ctl);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        if (sesEnum != null) Marshal.FinalReleaseComObject(sesEnum);
                        if (objMgr != null) Marshal.FinalReleaseComObject(objMgr);
                        if (endpoint != null) Marshal.FinalReleaseComObject(endpoint);
                    }
                }
            }
            catch
            {
            }
            finally
            {
                if (enumerator != null) Marshal.FinalReleaseComObject(enumerator);
            }
        }
    }
}
