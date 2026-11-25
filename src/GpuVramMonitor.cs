using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Legram
{
    internal static class GpuVramMonitor
    {
        private static bool nvmlChecked;
        private static bool nvmlReady;
        private static IntPtr nvmlDevice = IntPtr.Zero;
        private static DateTime nextNvmlRetryUtc = DateTime.MinValue;
        private static bool wddmAdapterChecked;
        private static long wddmFallbackTotalBytes;
        private static string wddmFallbackFullName = "Windows WDDM GPU";
        private static string wddmFallbackShortName = "GPU";
        private static string cachedFullName = "Windows WDDM GPU";
        private static string cachedShortName = "GPU";
        private static readonly object Sync = new object();

        // Persistent PDH query handle (opened lazily only when Popover / VRAM refresh needs per-PID breakdown)
        private static IntPtr persistentPdhQuery = IntPtr.Zero;
        private static IntPtr persistentPdhCounter = IntPtr.Zero;
        private static IntPtr persistentPdhSharedCounter = IntPtr.Zero;

        private static readonly Dictionary<int, bool> KnownBrowserGpuPidCache = new Dictionary<int, bool>();

        public static void NotifyDisplayOrPowerReset()
        {
            lock (Sync)
            {
                if (persistentPdhQuery != IntPtr.Zero)
                {
                    try { NativeApi.PdhCloseQuery(persistentPdhQuery); } catch { }
                    persistentPdhQuery = IntPtr.Zero;
                    persistentPdhCounter = IntPtr.Zero;
                    persistentPdhSharedCounter = IntPtr.Zero;
                }

                if (nvmlReady)
                {
                    try { NativeApi.NvmlShutdown(); } catch { }
                }

                nvmlReady = false;
                nvmlChecked = false;
                nvmlDevice = IntPtr.Zero;
                nextNvmlRetryUtc = DateTime.MinValue;
                wddmAdapterChecked = false;

                lock (KnownBrowserGpuPidCache)
                {
                    KnownBrowserGpuPidCache.Clear();
                }
            }
        }

        private static void EnsureNvml()
        {
            if (nvmlChecked) return;
            if (DateTime.UtcNow < nextNvmlRetryUtc) return;
            nvmlChecked = true;
            try
            {
                if (NativeApi.NvmlInit() == 0)
                {
                    IntPtr dev;
                    if (NativeApi.NvmlDeviceGetHandleByIndex(0, out dev) == 0 && dev != IntPtr.Zero)
                    {
                        nvmlDevice = dev;
                        nvmlReady = true;
                        nextNvmlRetryUtc = DateTime.MinValue;
                        StringBuilder sb = new StringBuilder(96);
                        if (NativeApi.NvmlDeviceGetName(dev, sb, (uint)sb.Capacity) == 0)
                        {
                            string raw = sb.ToString().Trim();
                            if (!string.IsNullOrEmpty(raw))
                            {
                                cachedFullName = raw;
                                cachedShortName = ShortenGpuName(raw);
                            }
                        }
                    }
                }
            }
            catch
            {
                nvmlReady = false;
            }
        }

        private static string ShortenGpuName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "GPU";
            string s = raw
                .Replace("NVIDIA ", "")
                .Replace("GeForce ", "")
                .Replace("AMD ", "")
                .Replace("Radeon(TM) ", "")
                .Replace("Radeon ", "")
                .Replace("Intel(R) ", "")
                .Replace("(TM)", "")
                .Replace("(R)", "")
                .Trim();
            if (s.EndsWith(" Graphics", StringComparison.OrdinalIgnoreCase) && s.Length > 10)
            {
                s = s.Substring(0, s.Length - 9).Trim();
            }
            return string.IsNullOrEmpty(s) ? raw : s;
        }

        private static void EnsureWddmAdapterFallback()
        {
            if (wddmAdapterChecked) return;
            wddmAdapterChecked = true;

            long bestVram = 0;
            string bestName = "";
            try
            {
                const uint DISPLAY_DEVICE_ACTIVE = 0x00000001u;
                const uint DISPLAY_DEVICE_PRIMARY_DEVICE = 0x00000004u;
                const uint DISPLAY_DEVICE_MIRRORING_DRIVER = 0x00000008u;

                for (uint idx = 0; idx < 16; idx++)
                {
                    NativeApi.DISPLAY_DEVICEW dd = new NativeApi.DISPLAY_DEVICEW();
                    dd.cb = Marshal.SizeOf(typeof(NativeApi.DISPLAY_DEVICEW));
                    if (!NativeApi.EnumDisplayDevicesW(null, idx, ref dd, 0)) break;
                    if ((dd.StateFlags & DISPLAY_DEVICE_MIRRORING_DRIVER) != 0) continue;
                    if ((dd.StateFlags & DISPLAY_DEVICE_ACTIVE) == 0) continue;

                    string desc = (dd.DeviceString ?? "").Trim();
                    if (desc.IndexOf("Basic Render", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                    long vram = ReadWddmRegistryVramBytes(dd.DeviceKey);
                    bool isPrimary = (dd.StateFlags & DISPLAY_DEVICE_PRIMARY_DEVICE) != 0;
                    if (vram > bestVram || (bestVram == 0 && isPrimary && !string.IsNullOrEmpty(desc)) || string.IsNullOrEmpty(bestName))
                    {
                        if (vram > 0) bestVram = vram;
                        if (!string.IsNullOrEmpty(desc)) bestName = desc;
                    }
                }
            }
            catch { }

            if (!string.IsNullOrEmpty(bestName))
            {
                wddmFallbackFullName = bestName;
                wddmFallbackShortName = ShortenGpuName(bestName);
            }
            wddmFallbackTotalBytes = bestVram;
        }

        private static long ReadWddmRegistryVramBytes(string deviceKey)
        {
            if (string.IsNullOrEmpty(deviceKey)) return 0;
            const string prefix = @"\Registry\Machine\";
            string subPath = deviceKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? deviceKey.Substring(prefix.Length)
                : deviceKey.TrimStart('\\');
            if (string.IsNullOrEmpty(subPath)) return 0;

            try
            {
                using (Microsoft.Win32.RegistryKey hklm = Microsoft.Win32.RegistryKey.OpenBaseKey(
                    Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64))
                using (Microsoft.Win32.RegistryKey k = hklm.OpenSubKey(subPath))
                {
                    if (k == null) return 0;
                    object qw = k.GetValue("HardwareInformation.qwMemorySize");
                    if (qw is long) return Math.Max(0L, (long)qw);
                    if (qw is ulong) return (long)Math.Min((ulong)long.MaxValue, (ulong)qw);
                    byte[] qwBytes = qw as byte[];
                    if (qwBytes != null && qwBytes.Length >= 8)
                    {
                        return Math.Max(0L, BitConverter.ToInt64(qwBytes, 0));
                    }

                    object dw = k.GetValue("HardwareInformation.MemorySize");
                    if (dw is int) return (long)(uint)(int)dw;
                    if (dw is long) return Math.Max(0L, (long)dw);
                    byte[] dwBytes = dw as byte[];
                    if (dwBytes != null && dwBytes.Length >= 4)
                    {
                        return (long)BitConverter.ToUInt32(dwBytes, 0);
                    }
                }
            }
            catch { }
            return 0;
        }

        public static GpuSnapshot SampleLightweight()
        {
            Dictionary<int, long> dummy;
            return SampleCore(false, out dummy);
        }

        public static GpuSnapshot Sample(out Dictionary<int, long> perPidDedicatedVram)
        {
            return SampleCore(true, out perPidDedicatedVram);
        }

        private static GpuSnapshot SampleCore(bool includePerPidPdh, out Dictionary<int, long> perPidDedicatedVram)
        {
            Dictionary<int, long> perPidSharedGpu;
            if (includePerPidPdh)
            {
                perPidDedicatedVram = QueryPerPidGpuMemoryPdh(out perPidSharedGpu);
            }
            else
            {
                perPidDedicatedVram = new Dictionary<int, long>();
                perPidSharedGpu = new Dictionary<int, long>();
            }

            GpuSnapshot snap = new GpuSnapshot();
            if (includePerPidPdh && perPidSharedGpu.Count > 0)
            {
                long sharedSum = 0;
                foreach (long sv in perPidSharedGpu.Values) sharedSum += sv;
                snap.SharedGpuUsedBytes = sharedSum;
            }

            lock (Sync)
            {
                EnsureNvml();
                if (nvmlReady && nvmlDevice != IntPtr.Zero)
                {
                    snap.Available = true;
                    snap.Name = cachedFullName;
                    snap.ShortName = cachedShortName;

                    try
                    {
                        bool memOk = false;
                        NativeApi.nvmlMemory_v2_t mem2 = new NativeApi.nvmlMemory_v2_t();
                        mem2.version = (uint)(Marshal.SizeOf(typeof(NativeApi.nvmlMemory_v2_t)) | (2 << 24));
                        if (NativeApi.NvmlDeviceGetMemoryInfoV2(nvmlDevice, ref mem2) == 0)
                        {
                            snap.VramTotalBytes = (long)mem2.total;
                            snap.VramUsedBytes = (long)mem2.used;
                            snap.VramReservedBytes = (long)mem2.reserved;
                            memOk = true;
                        }
                        else
                        {
                            NativeApi.nvmlMemory_t mem1;
                            if (NativeApi.NvmlDeviceGetMemoryInfo(nvmlDevice, out mem1) == 0)
                            {
                                snap.VramTotalBytes = (long)mem1.total;
                                snap.VramUsedBytes = (long)mem1.used;
                                memOk = true;
                            }
                        }

                        if (!memOk)
                        {
                            try { NativeApi.NvmlShutdown(); } catch { }
                            nvmlReady = false;
                            nvmlChecked = false;
                            nvmlDevice = IntPtr.Zero;
                            nextNvmlRetryUtc = DateTime.UtcNow.AddSeconds(3);
                            snap.Available = false;
                        }
                        else
                        {
                            uint temp;
                            if (NativeApi.NvmlDeviceGetTemperature(nvmlDevice, 0, out temp) == 0)
                                snap.TemperatureC = (int)temp;

                            NativeApi.nvmlUtilization_t util;
                            if (NativeApi.NvmlDeviceGetUtilizationRates(nvmlDevice, out util) == 0)
                                snap.GpuUtilPercent = (int)util.gpu;

                            uint mw;
                            if (NativeApi.NvmlDeviceGetPowerUsage(nvmlDevice, out mw) == 0)
                                snap.PowerWatts = mw / 1000.0;

                            uint decUtil, decPer, encUtil, encPer;
                            if (NativeApi.NvmlDeviceGetDecoderUtilization(nvmlDevice, out decUtil, out decPer) == 0)
                                snap.DecoderUtilPercent = (int)decUtil;
                            if (NativeApi.NvmlDeviceGetEncoderUtilization(nvmlDevice, out encUtil, out encPer) == 0)
                                snap.EncoderUtilPercent = (int)encUtil;
                        }
                    }
                    catch
                    {
                    }
                }
            }

            if (!includePerPidPdh)
            {
                if (!snap.Available)
                {
                    lock (Sync)
                    {
                        EnsureWddmAdapterFallback();
                        if (wddmFallbackTotalBytes > 0)
                        {
                            snap.Available = true;
                            snap.Name = wddmFallbackFullName;
                            snap.ShortName = wddmFallbackShortName;
                            snap.VramTotalBytes = wddmFallbackTotalBytes;
                        }
                    }
                }
                snap.TopVramSummary = snap.Available
                    ? (nvmlReady
                        ? ("NVML Direct \u00B7 Decoder " + snap.DecoderUtilPercent + "% \u00B7 Encoder " + snap.EncoderUtilPercent + "%")
                        : "WDDM Adapter Telemetry Active")
                    : "WDDM GPU Active";
                return snap;
            }

            if (!snap.Available && (perPidDedicatedVram.Count > 0 || wddmFallbackTotalBytes > 0))
            {
                long sum = 0;
                foreach (long v in perPidDedicatedVram.Values) sum += v;
                lock (Sync)
                {
                    EnsureWddmAdapterFallback();
                    snap.Name = wddmFallbackFullName;
                    snap.ShortName = wddmFallbackShortName;
                    snap.VramTotalBytes = wddmFallbackTotalBytes;
                }
                snap.Available = sum > 0 || snap.VramTotalBytes > 0;
                snap.VramUsedBytes = sum;
            }

            List<KeyValuePair<int, long>> topVram = new List<KeyValuePair<int, long>>(perPidDedicatedVram);
            topVram.Sort(delegate(KeyValuePair<int, long> a, KeyValuePair<int, long> b) { return b.Value.CompareTo(a.Value); });

            List<string> summaryParts = new List<string>();
            for (int i = 0; i < topVram.Count; i++)
            {
                int pid = topVram[i].Key;
                long bytes = topVram[i].Value;
                if (bytes <= 0) continue;

                string pname = TryGetProcessNameFast(pid);
                string low = (pname ?? "").ToLowerInvariant();

                bool isGpuSubproc = bytes >= 64L * 1024 * 1024
                    && !IsCompositorOrShellProcess(low)
                    && !IsAiEngineProcess(low)
                    && IsBrowserGpuSubprocessZeroWmi(pid);

                if (IsCompositorOrShellProcess(low))
                {
                    snap.VramCompositorBytes += bytes;
                }
                else if (IsBrowserProcess(low) || isGpuSubproc)
                {
                    snap.VramBrowserBytes += bytes;
                }
                else if (IsAiEngineProcess(low))
                {
                    snap.VramAiBytes += bytes;
                }
                else
                {
                    snap.VramAppsAndGamesBytes += bytes;
                }

                if (bytes < 25L * 1024 * 1024) continue;

                if (summaryParts.Count < 4 && !string.IsNullOrEmpty(pname))
                {
                    summaryParts.Add(pname + " " + Fmt.BytesShort(bytes));
                }

                if (bytes >= 180L * 1024 * 1024 && (isGpuSubproc || (IsBrowserProcess(low) && IsBrowserGpuSubprocessZeroWmi(pid))))
                {
                    snap.RefreshableBrowserGpuPids.Add(pid);
                    snap.ReclaimableVramBytes += (long)(bytes * 0.45);
                }
            }

            List<string> catParts = new List<string>();
            if (snap.VramAiBytes > 0) catParts.Add("AI " + Fmt.BytesShort(snap.VramAiBytes));
            if (snap.VramAppsAndGamesBytes > 0) catParts.Add("Apps/3D " + Fmt.BytesShort(snap.VramAppsAndGamesBytes));
            if (snap.VramBrowserBytes > 0) catParts.Add("Browser " + Fmt.BytesShort(snap.VramBrowserBytes));
            if (snap.VramCompositorBytes > 0) catParts.Add("DWM/Shell " + Fmt.BytesShort(snap.VramCompositorBytes));
            if (snap.SharedGpuUsedBytes > 0) catParts.Add("Shared RAM " + Fmt.BytesShort(snap.SharedGpuUsedBytes));
            snap.CategoryBreakdownSummary = catParts.Count > 0
                ? ("Categories: " + string.Join(" \u00B7 ", catParts.ToArray()))
                : "";

            // Probe idle Local AI VRAM models (Ollama :11434, LM Studio :1234, ComfyUI :8188) ONLY when GPU is idle
            if (!snap.IsRenderingOrDecoding && snap.GpuUtilPercent < 10)
            {
                List<string> aiNames = new List<string>();
                int aiTargetCount = 0;
                long aiVramSum = 0;

                if (NativeApi.IsLoopbackTcpPortListening(11434) || IsOllamaDaemonPresent())
                {
                    List<string> idleModels;
                    long ollamaVram;
                    ProbeOllamaLoadedModels("http://127.0.0.1:11434", out idleModels, out ollamaVram);
                    if (idleModels.Count > 0)
                    {
                        snap.IdleOllamaModelCount = idleModels.Count;
                        snap.IdleOllamaVramBytes = ollamaVram > 0 ? ollamaVram : (512L * 1048576L);
                        snap.IdleOllamaSummary = "Idle Ollama: " + string.Join(", ", idleModels.ToArray())
                            + " (" + Fmt.BytesShort(snap.IdleOllamaVramBytes) + ")";
                        for (int i = 0; i < idleModels.Count; i++)
                        {
                            aiNames.Add(idleModels[i]);
                        }
                        aiTargetCount += idleModels.Count;
                        aiVramSum += snap.IdleOllamaVramBytes;
                    }
                }

                if (IsLmStudioActive())
                {
                    List<string> lmsModels;
                    ProbeLmStudioLoadedModels("http://127.0.0.1:1234", out lmsModels);
                    if (lmsModels.Count > 0)
                    {
                        aiNames.Add("LM Studio (" + string.Join(", ", lmsModels.ToArray()) + ")");
                        aiTargetCount += lmsModels.Count;
                        if (snap.VramAiBytes > aiVramSum)
                            aiVramSum = snap.VramAiBytes;
                        else
                            aiVramSum += 512L * 1048576L * lmsModels.Count;
                    }
                }

                if (IsComfyUiActive() && snap.VramAiBytes >= 384L * 1048576L && ProbeComfyUiIdle("http://127.0.0.1:8188"))
                {
                    aiNames.Add("ComfyUI");
                    aiTargetCount += 1;
                    if (snap.VramAiBytes > aiVramSum)
                        aiVramSum = snap.VramAiBytes;
                    else
                        aiVramSum += 512L * 1048576L;
                }

                if (aiTargetCount > 0)
                {
                    if (snap.VramAiBytes > aiVramSum) aiVramSum = snap.VramAiBytes;
                    snap.IdleAiEngineCount = aiTargetCount;
                    snap.IdleAiVramBytes = aiVramSum > 0 ? aiVramSum : (512L * 1048576L);
                    snap.IdleAiSummary = "Idle AI: " + string.Join(", ", aiNames.ToArray())
                        + " (" + Fmt.BytesShort(snap.IdleAiVramBytes) + ")";
                }
            }

            snap.TopVramSummary = summaryParts.Count > 0
                ? "Top VRAM: " + string.Join(" \u00B7 ", summaryParts.ToArray())
                : "VRAM healthy";

            string idleLine = !string.IsNullOrEmpty(snap.IdleAiSummary) ? snap.IdleAiSummary : snap.IdleOllamaSummary;
            if (!string.IsNullOrEmpty(idleLine))
            {
                snap.TopVramSummary += "\n" + idleLine;
            }

            return snap;
        }

        private static bool IsCompositorOrShellProcess(string low)
        {
            return low == "dwm"
                || low == "csrss"
                || low == "explorer"
                || low == "shellexperiencehost"
                || low == "searchhost"
                || low == "startmenuexperiencehost"
                || low == "applicationframehost";
        }

        internal static bool IsBrowserProcess(string low)
        {
            return low == "chrome"
                || low == "msedge"
                || low == "brave"
                || low == "firefox"
                || low == "vivaldi"
                || low == "opera"
                || low == "arc"
                || low == "chromium"
                || low == "thorium"
                || low == "waterfox"
                || low == "librewolf"
                || low == "zen"
                || low == "floorp"
                || low == "msedgewebview2";
        }

        private static bool IsAiEngineProcess(string low)
        {
            return low == "ollama"
                || low == "ollama_llama_server"
                || low == "lm studio"
                || low == "lms"
                || low == "llama-server"
                || low == "python"
                || low == "pythonw";
        }

        public static bool IsOllamaDaemonPresent()
        {
            try
            {
                Process[] procs = Process.GetProcessesByName("ollama");
                bool found = procs.Length > 0;
                for (int i = 0; i < procs.Length; i++) procs[i].Dispose();
                if (found) return true;

                Process[] runners = Process.GetProcessesByName("ollama_llama_server");
                found = runners.Length > 0;
                for (int i = 0; i < runners.Length; i++) runners[i].Dispose();
                return found;
            }
            catch
            {
                return false;
            }
        }

        public static void ProbeOllamaLoadedModels(string baseUrl, out List<string> modelNames, out long totalVramBytes)
        {
            modelNames = new List<string>();
            totalVramBytes = 0;
            try
            {
                System.Net.HttpWebRequest req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(baseUrl.TrimEnd('/') + "/api/ps");
                req.Method = "GET";
                req.Timeout = 450;
                req.ReadWriteTimeout = 450;
                req.Proxy = null;
                using (System.Net.HttpWebResponse resp = (System.Net.HttpWebResponse)req.GetResponse())
                using (System.IO.Stream st = resp.GetResponseStream())
                {
                    if (st == null) return;
                    using (System.IO.StreamReader sr = new System.IO.StreamReader(st, Encoding.UTF8))
                    {
                        string json = sr.ReadToEnd();
                        System.Text.RegularExpressions.MatchCollection names =
                            System.Text.RegularExpressions.Regex.Matches(json, "\"name\"\\s*:\\s*\"([^\"]+)\"");
                        for (int i = 0; i < names.Count; i++)
                        {
                            string mName = names[i].Groups[1].Value.Trim();
                            if (!string.IsNullOrEmpty(mName) && !modelNames.Contains(mName))
                            {
                                modelNames.Add(mName);
                            }
                        }
                        System.Text.RegularExpressions.MatchCollection sizes =
                            System.Text.RegularExpressions.Regex.Matches(json, "\"size_vram\"\\s*:\\s*([0-9]+)");
                        for (int i = 0; i < sizes.Count; i++)
                        {
                            long sz;
                            if (long.TryParse(sizes[i].Groups[1].Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out sz))
                            {
                                totalVramBytes += sz;
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }

        public static int EvictIdleOllamaModels(bool gpuBusy)
        {
            if (gpuBusy || !IsOllamaDaemonPresent()) return 0;
            return EvictOllamaModelsAtEndpoint("http://127.0.0.1:11434");
        }

        public static int EvictOllamaModelsAtEndpoint(string baseUrl)
        {
            List<string> models;
            long vram;
            ProbeOllamaLoadedModels(baseUrl, out models, out vram);
            if (models.Count == 0) return 0;

            int unloaded = 0;
            for (int i = 0; i < models.Count; i++)
            {
                try
                {
                    string safeModel = models[i].Replace("\\", "\\\\").Replace("\"", "\\\"");
                    byte[] payload = Encoding.UTF8.GetBytes("{\"model\":\"" + safeModel + "\",\"keep_alive\":0}");
                    System.Net.HttpWebRequest req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(baseUrl.TrimEnd('/') + "/api/generate");
                    req.Method = "POST";
                    req.ContentType = "application/json";
                    req.ContentLength = payload.Length;
                    req.Timeout = 1100;
                    req.ReadWriteTimeout = 1100;
                    req.Proxy = null;
                    req.ServicePoint.Expect100Continue = false;
                    using (System.IO.Stream reqSt = req.GetRequestStream())
                    {
                        reqSt.Write(payload, 0, payload.Length);
                    }
                    using (System.Net.HttpWebResponse resp = (System.Net.HttpWebResponse)req.GetResponse())
                    {
                        if ((int)resp.StatusCode >= 200 && (int)resp.StatusCode < 300) unloaded++;
                    }
                }
                catch
                {
                }
            }
            return unloaded;
        }

        public static bool IsLmStudioActive()
        {
            return NativeApi.IsLoopbackTcpPortListening(1234);
        }

        public static void ProbeLmStudioLoadedModels(string baseUrl, out List<string> instanceIds)
        {
            instanceIds = new List<string>();
            if (string.IsNullOrEmpty(baseUrl)) return;
            try
            {
                System.Net.HttpWebRequest req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(baseUrl.TrimEnd('/') + "/api/v1/models");
                req.Method = "GET";
                req.Timeout = 400;
                req.ReadWriteTimeout = 400;
                req.Proxy = null;
                using (System.Net.HttpWebResponse resp = (System.Net.HttpWebResponse)req.GetResponse())
                using (System.IO.Stream st = resp.GetResponseStream())
                {
                    if (st == null) return;
                    using (System.IO.StreamReader sr = new System.IO.StreamReader(st, Encoding.UTF8))
                    {
                        string json = sr.ReadToEnd();
                        // 1. Parse "loaded_instances": [ { "id": "..." } / { "instance_id": "..." } ]
                        System.Text.RegularExpressions.MatchCollection instArrays =
                            System.Text.RegularExpressions.Regex.Matches(
                                json,
                                "\"loaded_instances\"\\s*:\\s*\\[([^\\]]*)\\]",
                                System.Text.RegularExpressions.RegexOptions.Singleline);
                        for (int i = 0; i < instArrays.Count; i++)
                        {
                            string block = instArrays[i].Groups[1].Value;
                            System.Text.RegularExpressions.MatchCollection ids =
                                System.Text.RegularExpressions.Regex.Matches(block, "\"(?:instance_id|id)\"\\s*:\\s*\"([^\"]+)\"");
                            for (int k = 0; k < ids.Count; k++)
                            {
                                string val = ids[k].Groups[1].Value.Trim();
                                if (!string.IsNullOrEmpty(val) && !instanceIds.Contains(val))
                                {
                                    instanceIds.Add(val);
                                }
                            }
                        }

                        // 2. Parse objects where "id"/"instance_id" precedes "state":"loaded"
                        System.Text.RegularExpressions.MatchCollection forwardLoaded =
                            System.Text.RegularExpressions.Regex.Matches(
                                json,
                                "\"(?:instance_id|id)\"\\s*:\\s*\"([^\"]+)\"(?:(?!\"(?:instance_id|id)\"\\s*:).)*?\"state\"\\s*:\\s*\"loaded\"",
                                System.Text.RegularExpressions.RegexOptions.Singleline);
                        for (int i = 0; i < forwardLoaded.Count; i++)
                        {
                            string val = forwardLoaded[i].Groups[1].Value.Trim();
                            if (!string.IsNullOrEmpty(val) && !instanceIds.Contains(val))
                            {
                                instanceIds.Add(val);
                            }
                        }

                        // 3. Parse objects where "state":"loaded" precedes "id"/"instance_id"
                        System.Text.RegularExpressions.MatchCollection reverseLoaded =
                            System.Text.RegularExpressions.Regex.Matches(
                                json,
                                "\"state\"\\s*:\\s*\"loaded\"[^{}]*?\"(?:instance_id|id)\"\\s*:\\s*\"([^\"]+)\"",
                                System.Text.RegularExpressions.RegexOptions.Singleline);
                        for (int i = 0; i < reverseLoaded.Count; i++)
                        {
                            string val = reverseLoaded[i].Groups[1].Value.Trim();
                            if (!string.IsNullOrEmpty(val) && !instanceIds.Contains(val))
                            {
                                instanceIds.Add(val);
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }

        public static int EvictLmStudioModelsAtEndpoint(string baseUrl)
        {
            List<string> ids;
            ProbeLmStudioLoadedModels(baseUrl, out ids);
            if (ids.Count == 0) return 0;

            int unloaded = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                try
                {
                    string safeId = ids[i].Replace("\\", "\\\\").Replace("\"", "\\\"");
                    byte[] payload = Encoding.UTF8.GetBytes("{\"instance_id\":\"" + safeId + "\"}");
                    System.Net.HttpWebRequest req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(baseUrl.TrimEnd('/') + "/api/v1/models/unload");
                    req.Method = "POST";
                    req.ContentType = "application/json";
                    req.ContentLength = payload.Length;
                    req.Timeout = 1100;
                    req.ReadWriteTimeout = 1100;
                    req.Proxy = null;
                    req.ServicePoint.Expect100Continue = false;
                    using (System.IO.Stream reqSt = req.GetRequestStream())
                    {
                        reqSt.Write(payload, 0, payload.Length);
                    }
                    using (System.Net.HttpWebResponse resp = (System.Net.HttpWebResponse)req.GetResponse())
                    {
                        if ((int)resp.StatusCode >= 200 && (int)resp.StatusCode < 300) unloaded++;
                    }
                }
                catch
                {
                }
            }
            return unloaded;
        }

        public static bool IsComfyUiActive()
        {
            return NativeApi.IsLoopbackTcpPortListening(8188);
        }

        public static bool ProbeComfyUiIdle(string baseUrl)
        {
            if (string.IsNullOrEmpty(baseUrl)) return false;
            try
            {
                System.Net.HttpWebRequest req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(baseUrl.TrimEnd('/') + "/queue");
                req.Method = "GET";
                req.Timeout = 350;
                req.ReadWriteTimeout = 350;
                req.Proxy = null;
                using (System.Net.HttpWebResponse resp = (System.Net.HttpWebResponse)req.GetResponse())
                {
                    if ((int)resp.StatusCode < 200 || (int)resp.StatusCode >= 300) return false;
                    using (System.IO.Stream st = resp.GetResponseStream())
                    {
                        if (st == null) return false;
                        using (System.IO.StreamReader sr = new System.IO.StreamReader(st, Encoding.UTF8))
                        {
                            string json = sr.ReadToEnd();
                            bool runningEmpty = System.Text.RegularExpressions.Regex.IsMatch(json, "\"queue_running\"\\s*:\\s*\\[\\s*\\]");
                            bool pendingEmpty = System.Text.RegularExpressions.Regex.IsMatch(json, "\"queue_pending\"\\s*:\\s*\\[\\s*\\]");
                            return runningEmpty && pendingEmpty;
                        }
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool FreeComfyUiMemoryAtEndpoint(string baseUrl)
        {
            if (!ProbeComfyUiIdle(baseUrl)) return false;
            try
            {
                byte[] payload = Encoding.UTF8.GetBytes("{\"unload_models\":true,\"free_memory\":true}");
                System.Net.HttpWebRequest req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(baseUrl.TrimEnd('/') + "/free");
                req.Method = "POST";
                req.ContentType = "application/json";
                req.ContentLength = payload.Length;
                req.Timeout = 1100;
                req.ReadWriteTimeout = 1100;
                req.Proxy = null;
                req.ServicePoint.Expect100Continue = false;
                using (System.IO.Stream reqSt = req.GetRequestStream())
                {
                    reqSt.Write(payload, 0, payload.Length);
                }
                using (System.Net.HttpWebResponse resp = (System.Net.HttpWebResponse)req.GetResponse())
                {
                    return (int)resp.StatusCode >= 200 && (int)resp.StatusCode < 300;
                }
            }
            catch
            {
                return false;
            }
        }

        public static int EvictAllIdleAiModels(bool gpuBusy)
        {
            if (gpuBusy) return 0;
            int totalUnloaded = 0;

            if (NativeApi.IsLoopbackTcpPortListening(11434) || IsOllamaDaemonPresent())
            {
                totalUnloaded += EvictOllamaModelsAtEndpoint("http://127.0.0.1:11434");
            }

            if (IsLmStudioActive())
            {
                totalUnloaded += EvictLmStudioModelsAtEndpoint("http://127.0.0.1:1234");
            }

            if (IsComfyUiActive())
            {
                if (FreeComfyUiMemoryAtEndpoint("http://127.0.0.1:8188"))
                {
                    totalUnloaded += 1;
                }
            }

            return totalUnloaded;
        }

        public static Dictionary<int, long> QueryPerPidDedicatedVramPdh()
        {
            Dictionary<int, long> dummyShared;
            return QueryPerPidGpuMemoryPdh(out dummyShared);
        }

        public static Dictionary<int, long> QueryPerPidGpuMemoryPdh(out Dictionary<int, long> sharedByPid)
        {
            Dictionary<int, long> vramByPid = new Dictionary<int, long>();
            sharedByPid = new Dictionary<int, long>();
            lock (Sync)
            {
                try
                {
                    if (persistentPdhQuery == IntPtr.Zero)
                    {
                        IntPtr hQ, hC;
                        if (NativeApi.PdhOpenQueryW(IntPtr.Zero, IntPtr.Zero, out hQ) == 0 && hQ != IntPtr.Zero)
                        {
                            if (NativeApi.PdhAddEnglishCounterW(hQ, @"\GPU Process Memory(*)\Dedicated Usage", IntPtr.Zero, out hC) == 0)
                            {
                                persistentPdhQuery = hQ;
                                persistentPdhCounter = hC;

                                IntPtr hShared;
                                if (NativeApi.PdhAddEnglishCounterW(hQ, @"\GPU Process Memory(*)\Shared Usage", IntPtr.Zero, out hShared) == 0)
                                {
                                    persistentPdhSharedCounter = hShared;
                                }
                            }
                            else
                            {
                                NativeApi.PdhCloseQuery(hQ);
                            }
                        }
                    }

                    if (persistentPdhQuery == IntPtr.Zero || persistentPdhCounter == IntPtr.Zero)
                        return vramByPid;

                    if (NativeApi.PdhCollectQueryData(persistentPdhQuery) != 0)
                        return vramByPid;

                    ReadPdhCounterByPid(persistentPdhCounter, vramByPid);
                    if (persistentPdhSharedCounter != IntPtr.Zero)
                    {
                        ReadPdhCounterByPid(persistentPdhSharedCounter, sharedByPid);
                    }
                }
                catch
                {
                }
            }
            return vramByPid;
        }

        private static void ReadPdhCounterByPid(IntPtr counter, Dictionary<int, long> targetByPid)
        {
            if (counter == IntPtr.Zero) return;
            uint bufSize = 0;
            uint itemCount = 0;
            uint st = NativeApi.PdhGetFormattedCounterArrayW(counter, NativeApi.PDH_FMT_LARGE, ref bufSize, out itemCount, IntPtr.Zero);
            if (st == NativeApi.PDH_MORE_DATA && bufSize > 0)
            {
                IntPtr pItems = Marshal.AllocHGlobal((int)bufSize);
                try
                {
                    if (NativeApi.PdhGetFormattedCounterArrayW(counter, NativeApi.PDH_FMT_LARGE, ref bufSize, out itemCount, pItems) == 0)
                    {
                        int itemSz = Marshal.SizeOf(typeof(NativeApi.PDH_FMT_COUNTERVALUE_ITEM_W));
                        for (int i = 0; i < (int)itemCount; i++)
                        {
                            IntPtr ptr = new IntPtr(pItems.ToInt64() + (long)(i * itemSz));
                            NativeApi.PDH_FMT_COUNTERVALUE_ITEM_W item =
                                (NativeApi.PDH_FMT_COUNTERVALUE_ITEM_W)Marshal.PtrToStructure(ptr, typeof(NativeApi.PDH_FMT_COUNTERVALUE_ITEM_W));
                            if (item.FmtValue.largeValue <= 0) continue;
                            string inst = Marshal.PtrToStringUni(item.szName);
                            if (inst != null && inst.StartsWith("pid_", StringComparison.OrdinalIgnoreCase))
                            {
                                int us = inst.IndexOf('_', 4);
                                int pidVal;
                                if (us > 4 && int.TryParse(inst.Substring(4, us - 4), out pidVal))
                                {
                                    long cur;
                                    if (!targetByPid.TryGetValue(pidVal, out cur)) cur = 0;
                                    targetByPid[pidVal] = cur + item.FmtValue.largeValue;
                                }
                            }
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(pItems);
                }
            }
        }

        private static bool IsBrowserGpuSubprocessZeroWmi(int pid)
        {
            lock (KnownBrowserGpuPidCache)
            {
                bool cached;
                if (KnownBrowserGpuPidCache.TryGetValue(pid, out cached)) return cached;
            }

            string cmd = NativeApi.QueryProcessCommandLineFast(pid);
            bool isGpuProc = cmd.IndexOf("--type=gpu-process", StringComparison.OrdinalIgnoreCase) >= 0;

            lock (KnownBrowserGpuPidCache)
            {
                if (KnownBrowserGpuPidCache.Count > 256) KnownBrowserGpuPidCache.Clear();
                KnownBrowserGpuPidCache[pid] = isGpuProc;
            }
            return isGpuProc;
        }

        private static string TryGetProcessNameFast(int pid)
        {
            IntPtr h = NativeApi.OpenProcess(NativeApi.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero) return "";
            try
            {
                StringBuilder sb = new StringBuilder(260);
                uint cap = (uint)sb.Capacity;
                if (NativeApi.QueryFullProcessImageNameW(h, 0, sb, ref cap))
                {
                    return System.IO.Path.GetFileNameWithoutExtension(sb.ToString());
                }
            }
            catch { }
            finally
            {
                NativeApi.CloseHandle(h);
            }
            return "";
        }

        public static void ClosePdhIfIdle()
        {
            lock (Sync)
            {
                if (persistentPdhQuery != IntPtr.Zero)
                {
                    try { NativeApi.PdhCloseQuery(persistentPdhQuery); } catch { }
                    persistentPdhQuery = IntPtr.Zero;
                    persistentPdhCounter = IntPtr.Zero;
                    persistentPdhSharedCounter = IntPtr.Zero;
                }
            }
        }

        public static void Shutdown()
        {
            ClosePdhIfIdle();
            lock (Sync)
            {
                if (nvmlReady)
                {
                    try { NativeApi.NvmlShutdown(); } catch { }
                    nvmlReady = false;
                    nvmlDevice = IntPtr.Zero;
                }
            }
        }
    }
}
