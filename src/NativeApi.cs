using System;
using System.Collections.Generic;
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
        internal const int WM_DISPLAYCHANGE = 0x007E;
        internal const int WM_POWERBROADCAST = 0x0218;
        internal const int WM_DPICHANGED = 0x02E0;
        internal const int PBT_APMSUSPEND = 0x0004;
        internal const int PBT_APMRESUMESUSPEND = 0x0007;
        internal const int PBT_APMRESUMEAUTOMATIC = 0x0012;
        internal const int MA_NOACTIVATE = 3;
        internal const uint CREATE_BREAKAWAY_FROM_JOB = 0x01000000;
        internal const uint CREATE_NO_WINDOW = 0x08000000;
        internal const uint DETACHED_PROCESS = 0x00000008;
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
        internal static extern bool GetProcessInformation(IntPtr hProcess, int ProcessInformationClass, out MEMORY_PRIORITY_INFORMATION Info, uint Size);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetProcessInformation(IntPtr hProcess, int ProcessInformationClass, ref PROCESS_POWER_THROTTLING_STATE State, uint Size);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint GetPriorityClass(IntPtr hProcess);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern uint RegisterWindowMessageW(string lpString);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct STARTUPINFOW
        {
            public uint cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public ushort wShowWindow;
            public ushort cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool CreateProcessW(
            string lpApplicationName,
            StringBuilder lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFOW lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        // Zero-System.Management detached process breakaway helper:
        // 1. Tries direct Win32 CREATE_BREAKAWAY_FROM_JOB (<1 ms).
        // 2. If the parent terminal Job Object forbids breakaway (KILL_ON_JOB_CLOSE without BREAKAWAY_OK),
        //    delegates creation out-of-process via late-bound WbemScripting.SWbemLocator in the short-lived stub
        //    so the long-running --detached Legram process stays 100% outside the job and never loads WMI!
        internal static bool TrySpawnDetachedBreakaway(string commandLine, string workingDirectory)
        {
            if (string.IsNullOrEmpty(commandLine)) return false;

            STARTUPINFOW si = new STARTUPINFOW();
            si.cb = (uint)Marshal.SizeOf(typeof(STARTUPINFOW));
            PROCESS_INFORMATION pi;
            StringBuilder cmdBuf = new StringBuilder(commandLine);
            if (CreateProcessW(
                null, cmdBuf, IntPtr.Zero, IntPtr.Zero, false,
                CREATE_BREAKAWAY_FROM_JOB | DETACHED_PROCESS | CREATE_NO_WINDOW,
                IntPtr.Zero, workingDirectory, ref si, out pi))
            {
                if (pi.hThread != IntPtr.Zero) CloseHandle(pi.hThread);
                if (pi.hProcess != IntPtr.Zero) CloseHandle(pi.hProcess);
                return true;
            }

            object locator = null;
            object svc = null;
            object procClass = null;
            try
            {
                Type locType = Type.GetTypeFromProgID("WbemScripting.SWbemLocator");
                if (locType != null)
                {
                    locator = Activator.CreateInstance(locType);
                    svc = locType.InvokeMember(
                        "ConnectServer",
                        System.Reflection.BindingFlags.InvokeMethod,
                        null,
                        locator,
                        new object[] { ".", @"root\cimv2" });
                    if (svc != null)
                    {
                        procClass = svc.GetType().InvokeMember(
                            "Get",
                            System.Reflection.BindingFlags.InvokeMethod,
                            null,
                            svc,
                            new object[] { "Win32_Process" });
                        if (procClass != null)
                        {
                            object[] wmiArgs = new object[] { commandLine, workingDirectory, null, null };
                            object res = procClass.GetType().InvokeMember(
                                "Create",
                                System.Reflection.BindingFlags.InvokeMethod,
                                null,
                                procClass,
                                wmiArgs);
                            if (res != null && Convert.ToInt32(res) == 0)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            catch { }
            finally
            {
                if (procClass != null && Marshal.IsComObject(procClass)) { try { Marshal.FinalReleaseComObject(procClass); } catch { } }
                if (svc != null && Marshal.IsComObject(svc)) { try { Marshal.FinalReleaseComObject(svc); } catch { } }
                if (locator != null && Marshal.IsComObject(locator)) { try { Marshal.FinalReleaseComObject(locator); } catch { } }
            }
            return false;
        }

        // Ultra-fast (<0.1 ms) zero-socket TCP LISTEN port check (IPv4 + IPv6) so AI/ComfyUI/LM Studio probes never hang on closed ports
        internal static bool IsLoopbackTcpPortListening(int targetPort)
        {
            if (targetPort <= 0 || targetPort > 65535) return false;
            int size = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref size, false, 2, 3, 0);
            if (size > 0)
            {
                IntPtr buf = Marshal.AllocHGlobal(size);
                try
                {
                    if (GetExtendedTcpTable(buf, ref size, false, 2, 3, 0) == 0)
                    {
                        int count = Marshal.ReadInt32(buf);
                        IntPtr rowPtr = new IntPtr(buf.ToInt64() + 4);
                        for (int i = 0; i < count; i++)
                        {
                            int rawPort = Marshal.ReadInt32(rowPtr, 8);
                            int port = ((rawPort & 0xFF) << 8) | ((rawPort >> 8) & 0xFF);
                            if (port == targetPort) return true;
                            rowPtr = new IntPtr(rowPtr.ToInt64() + 24);
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buf);
                }
            }

            int size6 = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref size6, false, 23, 3, 0);
            if (size6 > 0)
            {
                IntPtr buf6 = Marshal.AllocHGlobal(size6);
                try
                {
                    if (GetExtendedTcpTable(buf6, ref size6, false, 23, 3, 0) == 0)
                    {
                        int count = Marshal.ReadInt32(buf6);
                        IntPtr rowPtr = new IntPtr(buf6.ToInt64() + 4);
                        for (int i = 0; i < count; i++)
                        {
                            int rawPort = Marshal.ReadInt32(rowPtr, 20);
                            int port = ((rawPort & 0xFF) << 8) | ((rawPort >> 8) & 0xFF);
                            if (port == targetPort) return true;
                            rowPtr = new IntPtr(rowPtr.ToInt64() + 56);
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buf6);
                }
            }
            return false;
        }

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
        internal static extern uint GetWindowLongW(IntPtr hWnd, int nIndex);

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

        [DllImport("user32.dll")]
        internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetClassNameW(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct DISPLAY_DEVICEW
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public uint StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern bool EnumDisplayDevicesW(string lpDevice, uint iDevNum, ref DISPLAY_DEVICEW lpDisplayDevice, uint dwFlags);

        [DllImport("shell32.dll")]
        internal static extern int SHQueryUserNotificationState(out int pquns);

        // Passive, 100% Anti-Cheat safe fullscreen window / D3D game detector (never opens game process handle!)
        internal static bool IsForegroundWindowFullscreen(IntPtr fgHwnd, out bool isD3dExclusive)
        {
            isD3dExclusive = false;
            try
            {
                int quns = 0;
                if (SHQueryUserNotificationState(out quns) == 0 && quns == 3) // QUNS_RUNNING_D3D_FULL_SCREEN = 3
                {
                    isD3dExclusive = true;
                    return true;
                }
            }
            catch { }

            if (fgHwnd == IntPtr.Zero) return false;
            try
            {
                StringBuilder clsSb = new StringBuilder(64);
                if (GetClassNameW(fgHwnd, clsSb, clsSb.Capacity) > 0)
                {
                    string cls = clsSb.ToString();
                    if (string.Equals(cls, "Progman", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(cls, "WorkerW", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(cls, "Shell_TrayWnd", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(cls, "XamlExplorerHostIslandWindow", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                RECT wr;
                if (!GetWindowRect(fgHwnd, out wr)) return false;
                int ww = wr.R - wr.L;
                int wh = wr.B - wr.T;
                if (ww < 640 || wh < 480) return false;

                IntPtr hMon = MonitorFromWindow(fgHwnd, 2); // MONITOR_DEFAULTTONEAREST
                if (hMon == IntPtr.Zero) return false;
                MONITORINFO mi = new MONITORINFO { Size = Marshal.SizeOf(typeof(MONITORINFO)) };
                if (!GetMonitorInfo(hMon, ref mi)) return false;

                int mw = mi.Monitor.R - mi.Monitor.L;
                int mh = mi.Monitor.B - mi.Monitor.T;
                if (mw <= 0 || mh <= 0) return false;

                // Covers entire physical monitor rectangle (including taskbar area -> borderless fullscreen / fullscreen game)
                bool coversMonitor = wr.L <= mi.Monitor.L + 2
                    && wr.T <= mi.Monitor.T + 2
                    && wr.R >= mi.Monitor.R - 2
                    && wr.B >= mi.Monitor.B - 2;
                return coversMonitor;
            }
            catch
            {
                return false;
            }
        }

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

    // --- 8. Windows 10/11 Virtual Desktop Manager (IVirtualDesktopManager COM + Ordered Desktop Index & Auto-Follow) ---
    internal sealed class VirtualDesktopState
    {
        public int ActiveDesktopNumber = 1;
        public int TotalDesktopCount = 1;
        public string ActiveDesktopName = "";
        public Guid ActiveDesktopId = Guid.Empty;
        public Dictionary<int, int> PidPrimaryDesktopNumber = new Dictionary<int, int>();
        public HashSet<int> PidsOnActiveDesktop = new HashSet<int>();
        public HashSet<int> PidsOnInactiveDesktops = new HashSet<int>();
        public Dictionary<int, string> DesktopNamesByNumber = new Dictionary<int, string>();
    }

    internal static class VirtualDesktopShield
    {
        [ComImport, Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a")]
        private class VirtualDesktopManagerComObject { }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
        private interface IVirtualDesktopManager
        {
            [PreserveSig]
            int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, [MarshalAs(UnmanagedType.Bool)] out bool onCurrentDesktop);
            [PreserveSig]
            int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);
            [PreserveSig]
            int MoveWindowToDesktop(IntPtr topLevelWindow, [In] ref Guid desktopId);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLengthW(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
            public uint pad1;
            public uint pad2;
        }

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const ushort VK_CONTROL = 0x11;
        private const ushort VK_LWIN = 0x5B;
        private const ushort VK_LEFT = 0x25;
        private const ushort VK_RIGHT = 0x27;
        private const ushort VK_D = 0x44;
        private const ushort VK_F4 = 0x73;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKeyW(uint uCode, uint uMapType);

        private static readonly object Sync = new object();
        private static readonly List<Guid> KnownDesktopOrder = new List<Guid>();
        private static Guid LastKnownActiveGuid = Guid.Empty;
        private static VirtualDesktopState CachedLightweightState;
        private static DateTime CachedLightweightUtc = DateTime.MinValue;

        public static void InvalidateCache()
        {
            lock (Sync)
            {
                CachedLightweightUtc = DateTime.MinValue;
            }
        }

        // Event-driven cached lightweight Virtual Desktop state (avoids EnumWindows + COM + Registry every 4.5s when idle!)
        public static VirtualDesktopState SampleLightweightCached(IntPtr legramHwnd)
        {
            lock (Sync)
            {
                if (CachedLightweightState != null && (DateTime.UtcNow - CachedLightweightUtc).TotalSeconds < 25.0)
                {
                    return CachedLightweightState;
                }
            }
            VirtualDesktopState fresh = SampleAndFollow(legramHwnd, true);
            lock (Sync)
            {
                CachedLightweightState = fresh;
                CachedLightweightUtc = DateTime.UtcNow;
            }
            return fresh;
        }

        private static INPUT MakeKeyInput(ushort vk, bool keyUp)
        {
            uint flags = keyUp ? KEYEVENTF_KEYUP : 0u;
            if (vk == VK_LWIN || vk == VK_LEFT || vk == VK_RIGHT)
            {
                flags |= KEYEVENTF_EXTENDEDKEY;
            }
            ushort scan = (ushort)(MapVirtualKeyW(vk, 0) & 0xFFFF);
            INPUT inp = new INPUT();
            inp.type = INPUT_KEYBOARD;
            inp.ki.wVk = vk;
            inp.ki.wScan = scan;
            inp.ki.dwFlags = flags;
            return inp;
        }

        private static void SendCtrlWinKey(ushort vkKey)
        {
            INPUT[] inputs = new INPUT[6];
            int sz = Marshal.SizeOf(typeof(INPUT));
            inputs[0] = MakeKeyInput(VK_CONTROL, false);
            inputs[1] = MakeKeyInput(VK_LWIN, false);
            inputs[2] = MakeKeyInput(vkKey, false);
            inputs[3] = MakeKeyInput(vkKey, true);
            inputs[4] = MakeKeyInput(VK_LWIN, true);
            inputs[5] = MakeKeyInput(VK_CONTROL, true);
            SendInput((uint)inputs.Length, inputs, sz);
        }

        // Creates a new Windows Virtual Desktop (Ctrl + Win + D) and switches to it smoothly
        public static void CreateNewDesktopAndSwitch(IntPtr legramHwnd, Action onCompleted)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    SendCtrlWinKey(VK_D);
                    System.Threading.Thread.Sleep(240);
                    SampleAndFollow(legramHwnd, true);
                }
                catch { }
                if (onCompleted != null)
                {
                    try { onCompleted(); } catch { }
                }
            });
        }

        // Switches from the current Virtual Desktop to targetDesktopNumber (1-based) using smooth native Ctrl+Win+Left/Right
        public static void SwitchToDesktopNumber(int targetDesktopNumber, IntPtr legramHwnd, Action onCompleted)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    VirtualDesktopState cur = SampleAndFollow(legramHwnd, false);
                    int currentNum = Math.Max(1, cur.ActiveDesktopNumber);
                    int targetNum = Math.Max(1, Math.Min(cur.TotalDesktopCount, targetDesktopNumber));
                    int delta = targetNum - currentNum;
                    if (delta != 0)
                    {
                        ushort dirVk = delta > 0 ? VK_RIGHT : VK_LEFT;
                        int steps = Math.Min(8, Math.Abs(delta));
                        for (int s = 0; s < steps; s++)
                        {
                            SendCtrlWinKey(dirVk);
                            System.Threading.Thread.Sleep(160);
                        }
                        System.Threading.Thread.Sleep(140);
                    }
                    SampleAndFollow(legramHwnd, true);
                }
                catch { }
                if (onCompleted != null)
                {
                    try { onCompleted(); } catch { }
                }
            });
        }

        // Closes the current Virtual Desktop (Ctrl + Win + F4) and removes its GUID from KnownDesktopOrder
        public static void CloseCurrentDesktop(IntPtr legramHwnd, Action onCompleted)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    VirtualDesktopState cur = SampleAndFollow(legramHwnd, false);
                    if (cur.TotalDesktopCount > 1)
                    {
                        Guid closingGuid = cur.ActiveDesktopId;
                        SendCtrlWinKey(VK_F4);
                        System.Threading.Thread.Sleep(260);
                        lock (Sync)
                        {
                            if (closingGuid != Guid.Empty && KnownDesktopOrder.Count > 1)
                            {
                                KnownDesktopOrder.Remove(closingGuid);
                            }
                        }
                        SampleAndFollow(legramHwnd, true);
                    }
                }
                catch { }
                if (onCompleted != null)
                {
                    try { onCompleted(); } catch { }
                }
            });
        }

        public static VirtualDesktopState SampleAndFollow(IntPtr legramHwnd, bool autoFollowLegramWindow)
        {
            VirtualDesktopState state = new VirtualDesktopState();
            IVirtualDesktopManager vdm = null;
            try
            {
                vdm = (IVirtualDesktopManager)(new VirtualDesktopManagerComObject());
            }
            catch
            {
                return state;
            }

            try
            {
                int selfPid = System.Diagnostics.Process.GetCurrentProcess().Id;
                Dictionary<Guid, string> regNames = new Dictionary<Guid, string>();
                Guid regCurrentGuid;
                List<Guid> orderedGuids = ReadRegistryDesktopOrder(regNames, out regCurrentGuid);

                lock (Sync)
                {
                    // Always synchronize KnownDesktopOrder with Explorer's authoritative VirtualDesktopIDs binary array when present
                    if (orderedGuids.Count > 0)
                    {
                        KnownDesktopOrder.Clear();
                        for (int i = 0; i < orderedGuids.Count; i++)
                        {
                            KnownDesktopOrder.Add(orderedGuids[i]);
                        }
                    }

                    // 1. Primary source of truth for active desktop GUID: Explorer's live CurrentVirtualDesktop registry value
                    Guid activeGuid = regCurrentGuid;

                    // 2. Fallback: Foreground window on current desktop (excluding Legram itself)
                    if (activeGuid == Guid.Empty)
                    {
                        IntPtr fg = NativeApi.GetForegroundWindow();
                        if (fg != IntPtr.Zero && fg != legramHwnd)
                        {
                            bool onCur;
                            Guid fgDesk;
                            if (vdm.IsWindowOnCurrentVirtualDesktop(fg, out onCur) == 0 && onCur
                                && vdm.GetWindowDesktopId(fg, out fgDesk) == 0 && fgDesk != Guid.Empty
                                && !IsPinnedToAllDesktopsGuid(fgDesk))
                            {
                                activeGuid = fgDesk;
                            }
                        }
                    }

                    List<IntPtr> candidateHwnds = new List<IntPtr>(64);
                    EnumWindowsProc callback = delegate(IntPtr hWnd, IntPtr lParam)
                    {
                        if (hWnd == IntPtr.Zero || hWnd == legramHwnd) return true;
                        if (!IsWindowVisible(hWnd)) return true;

                        uint exStyle = NativeApi.GetWindowLongW(hWnd, NativeApi.GWL_EXSTYLE);
                        if ((exStyle & NativeApi.WS_EX_TOOLWINDOW) != 0) return true;

                        candidateHwnds.Add(hWnd);
                        return true;
                    };
                    EnumWindows(callback, IntPtr.Zero);
                    GC.KeepAlive(callback);

                    // First pass: find activeGuid from any window on the current virtual desktop if still empty
                    for (int i = 0; i < candidateHwnds.Count; i++)
                    {
                        IntPtr hWnd = candidateHwnds[i];
                        if (GetWindowTextLengthW(hWnd) <= 0) continue;

                        bool onCur = false;
                        Guid deskId = Guid.Empty;
                        int hr1 = vdm.IsWindowOnCurrentVirtualDesktop(hWnd, out onCur);
                        int hr2 = vdm.GetWindowDesktopId(hWnd, out deskId);
                        if (hr1 == 0 && hr2 == 0 && deskId != Guid.Empty && !IsPinnedToAllDesktopsGuid(deskId))
                        {
                            if (onCur && activeGuid == Guid.Empty)
                            {
                                activeGuid = deskId;
                            }
                        }
                    }

                    // Ensure activeGuid is registered FIRST when starting up so current desktop is always Desktop 1!
                    if (activeGuid != Guid.Empty && !KnownDesktopOrder.Contains(activeGuid))
                    {
                        KnownDesktopOrder.Add(activeGuid);
                    }

                    // Second pass: classify active vs inactive desktop PIDs and assign desktop numbers
                    for (int i = 0; i < candidateHwnds.Count; i++)
                    {
                        IntPtr hWnd = candidateHwnds[i];
                        if (GetWindowTextLengthW(hWnd) <= 0) continue;

                        uint wPid;
                        NativeApi.GetWindowThreadProcessId(hWnd, out wPid);
                        int pid = (int)wPid;
                        if (pid <= 4 || pid == selfPid) continue;

                        bool onCur = false;
                        Guid deskId = Guid.Empty;
                        int hr1 = vdm.IsWindowOnCurrentVirtualDesktop(hWnd, out onCur);
                        int hr2 = vdm.GetWindowDesktopId(hWnd, out deskId);
                        if (hr1 != 0) continue;

                        if (hr2 == 0 && deskId != Guid.Empty && !IsPinnedToAllDesktopsGuid(deskId))
                        {
                            if (!KnownDesktopOrder.Contains(deskId))
                            {
                                KnownDesktopOrder.Add(deskId);
                            }
                        }

                        if (onCur)
                        {
                            state.PidsOnActiveDesktop.Add(pid);
                            state.PidsOnInactiveDesktops.Remove(pid);
                            int deskNum = (hr2 == 0) ? GetOrAssignDesktopNumber(deskId) : 0;
                            if (deskNum > 0) state.PidPrimaryDesktopNumber[pid] = deskNum;
                        }
                        else if (hr2 == 0 && deskId != Guid.Empty && !IsPinnedToAllDesktopsGuid(deskId))
                        {
                            // Window genuinely belongs to another Virtual Desktop GUID
                            if (!state.PidsOnActiveDesktop.Contains(pid))
                            {
                                state.PidsOnInactiveDesktops.Add(pid);
                            }
                            int deskNum = GetOrAssignDesktopNumber(deskId);
                            if (deskNum > 0 && !state.PidPrimaryDesktopNumber.ContainsKey(pid))
                            {
                                state.PidPrimaryDesktopNumber[pid] = deskNum;
                            }
                        }
                    }

                    if (activeGuid == Guid.Empty)
                    {
                        activeGuid = LastKnownActiveGuid;
                    }
                    if (activeGuid == Guid.Empty && KnownDesktopOrder.Count > 0)
                    {
                        activeGuid = KnownDesktopOrder[0];
                    }
                    if (activeGuid != Guid.Empty)
                    {
                        LastKnownActiveGuid = activeGuid;
                        if (!KnownDesktopOrder.Contains(activeGuid))
                        {
                            KnownDesktopOrder.Add(activeGuid);
                        }
                    }

                    state.TotalDesktopCount = Math.Max(1, KnownDesktopOrder.Count);
                    state.ActiveDesktopId = activeGuid;
                    int activeIdx = activeGuid != Guid.Empty ? KnownDesktopOrder.IndexOf(activeGuid) : 0;
                    state.ActiveDesktopNumber = activeIdx >= 0 ? (activeIdx + 1) : 1;

                    foreach (int actPid in state.PidsOnActiveDesktop)
                    {
                        if (!state.PidPrimaryDesktopNumber.ContainsKey(actPid))
                        {
                            state.PidPrimaryDesktopNumber[actPid] = state.ActiveDesktopNumber;
                        }
                    }
                    if (state.TotalDesktopCount == 2)
                    {
                        int otherDesk = state.ActiveDesktopNumber == 1 ? 2 : 1;
                        foreach (int inactPid in state.PidsOnInactiveDesktops)
                        {
                            if (!state.PidPrimaryDesktopNumber.ContainsKey(inactPid))
                            {
                                state.PidPrimaryDesktopNumber[inactPid] = otherDesk;
                            }
                        }
                    }

                    for (int i = 0; i < KnownDesktopOrder.Count; i++)
                    {
                        Guid g = KnownDesktopOrder[i];
                        string nm;
                        if (!regNames.TryGetValue(g, out nm) || string.IsNullOrEmpty(nm))
                        {
                            nm = "Desktop " + (i + 1);
                        }
                        state.DesktopNamesByNumber[i + 1] = nm;
                    }
                    string activeNm;
                    if (state.DesktopNamesByNumber.TryGetValue(state.ActiveDesktopNumber, out activeNm))
                    {
                        state.ActiveDesktopName = activeNm;
                    }
                    else
                    {
                        state.ActiveDesktopName = "Desktop " + state.ActiveDesktopNumber;
                    }

                    // Keep Legram window effortlessly on the current active desktop when switching desktops
                    if (autoFollowLegramWindow && legramHwnd != IntPtr.Zero && activeGuid != Guid.Empty)
                    {
                        bool legramOnCur = true;
                        if (vdm.IsWindowOnCurrentVirtualDesktop(legramHwnd, out legramOnCur) == 0 && !legramOnCur)
                        {
                            vdm.MoveWindowToDesktop(legramHwnd, ref activeGuid);
                        }
                    }

                    CachedLightweightState = state;
                    CachedLightweightUtc = DateTime.UtcNow;
                }
            }
            catch
            {
            }
            finally
            {
                if (vdm != null)
                {
                    try { Marshal.FinalReleaseComObject(vdm); } catch { }
                }
            }
            return state;
        }

        private static int GetOrAssignDesktopNumber(Guid deskId)
        {
            if (deskId == Guid.Empty || IsPinnedToAllDesktopsGuid(deskId)) return 0;
            int idx = KnownDesktopOrder.IndexOf(deskId);
            if (idx < 0)
            {
                KnownDesktopOrder.Add(deskId);
                idx = KnownDesktopOrder.Count - 1;
            }
            return idx + 1;
        }

        private static bool IsPinnedToAllDesktopsGuid(Guid g)
        {
            // Windows uses sentinel GUIDs for windows pinned across all desktops
            string s = g.ToString("N");
            return s.StartsWith("c2ddea68", StringComparison.OrdinalIgnoreCase)
                || s.StartsWith("bb64d5b7", StringComparison.OrdinalIgnoreCase);
        }

        private static List<Guid> ReadRegistryDesktopOrder(Dictionary<Guid, string> namesOut, out Guid currentGuidOut)
        {
            List<Guid> list = new List<Guid>();
            currentGuidOut = Guid.Empty;
            try
            {
                using (Microsoft.Win32.RegistryKey hkcu = Microsoft.Win32.RegistryKey.OpenBaseKey(
                    Microsoft.Win32.RegistryHive.CurrentUser, Microsoft.Win32.RegistryView.Registry64))
                using (Microsoft.Win32.RegistryKey vdKey = hkcu.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops"))
                {
                    if (vdKey == null) return list;

                    // 1. Read authoritative CurrentVirtualDesktop GUID from Explorer
                    byte[] rawCur = vdKey.GetValue("CurrentVirtualDesktop") as byte[];
                    if (rawCur != null && rawCur.Length == 16)
                    {
                        Guid curG = new Guid(rawCur);
                        if (curG != Guid.Empty) currentGuidOut = curG;
                    }

                    // 2. Read ordered desktop list from VirtualDesktopIDs if present (do NOT add orphaned Desktops\{GUID} wallpaper subkeys!)
                    byte[] rawIds = vdKey.GetValue("VirtualDesktopIDs") as byte[];
                    if (rawIds != null && rawIds.Length >= 16)
                    {
                        byte[] buf = new byte[16];
                        for (int offset = 0; offset + 16 <= rawIds.Length; offset += 16)
                        {
                            Buffer.BlockCopy(rawIds, offset, buf, 0, 16);
                            Guid g = new Guid(buf);
                            if (g != Guid.Empty && !list.Contains(g)) list.Add(g);
                        }
                    }

                    // 3. Read custom desktop names (if any) without polluting the active desktop list with stale subkeys
                    using (Microsoft.Win32.RegistryKey desktopsKey = vdKey.OpenSubKey("Desktops"))
                    {
                        if (desktopsKey != null)
                        {
                            string[] subNames = desktopsKey.GetSubKeyNames();
                            for (int i = 0; i < subNames.Length; i++)
                            {
                                Guid g;
                                if (Guid.TryParse(subNames[i], out g) && g != Guid.Empty)
                                {
                                    using (Microsoft.Win32.RegistryKey oneDesk = desktopsKey.OpenSubKey(subNames[i]))
                                    {
                                        if (oneDesk != null)
                                        {
                                            string customName = oneDesk.GetValue("Name") as string;
                                            if (!string.IsNullOrEmpty(customName))
                                            {
                                                namesOut[g] = customName;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            return list;
        }
    }
}
