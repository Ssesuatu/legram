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
        private static string cachedFullName = "Windows WDDM GPU";
        private static string cachedShortName = "GPU";
        private static readonly object Sync = new object();

        // Persistent PDH query handle (opened lazily only when Popover / VRAM refresh needs per-PID breakdown)
        private static IntPtr persistentPdhQuery = IntPtr.Zero;
        private static IntPtr persistentPdhCounter = IntPtr.Zero;

        private static readonly Dictionary<int, bool> KnownBrowserGpuPidCache = new Dictionary<int, bool>();

        private static void EnsureNvml()
        {
            if (nvmlChecked) return;
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
            string s = raw.Replace("NVIDIA ", "").Replace("GeForce ", "").Trim();
            return string.IsNullOrEmpty(s) ? raw : s;
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
            perPidDedicatedVram = includePerPidPdh ? QueryPerPidDedicatedVramPdh() : new Dictionary<int, long>();
            GpuSnapshot snap = new GpuSnapshot();

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
                        NativeApi.nvmlMemory_v2_t mem2 = new NativeApi.nvmlMemory_v2_t();
                        mem2.version = (uint)(Marshal.SizeOf(typeof(NativeApi.nvmlMemory_v2_t)) | (2 << 24));
                        if (NativeApi.NvmlDeviceGetMemoryInfoV2(nvmlDevice, ref mem2) == 0)
                        {
                            snap.VramTotalBytes = (long)mem2.total;
                            snap.VramUsedBytes = (long)mem2.used;
                            snap.VramReservedBytes = (long)mem2.reserved;
                        }
                        else
                        {
                            NativeApi.nvmlMemory_t mem1;
                            if (NativeApi.NvmlDeviceGetMemoryInfo(nvmlDevice, out mem1) == 0)
                            {
                                snap.VramTotalBytes = (long)mem1.total;
                                snap.VramUsedBytes = (long)mem1.used;
                            }
                        }

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
                    catch
                    {
                    }
                }
            }

            if (!includePerPidPdh)
            {
                snap.TopVramSummary = snap.Available
                    ? ("NVML Direct \u00B7 Decoder " + snap.DecoderUtilPercent + "% \u00B7 Encoder " + snap.EncoderUtilPercent + "%")
                    : "WDDM GPU Active";
                return snap;
            }

            // Probe idle Ollama VRAM models ONLY if ollama process is actually present and GPU is idle
            if (!snap.IsRenderingOrDecoding && snap.GpuUtilPercent < 10 && IsOllamaDaemonPresent())
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
                }
            }

            if (!snap.Available && perPidDedicatedVram.Count > 0)
            {
                long sum = 0;
                foreach (long v in perPidDedicatedVram.Values) sum += v;
                snap.Available = true;
                snap.ShortName = "GPU";
                snap.Name = "Windows WDDM GPU";
                snap.VramUsedBytes = sum;
            }

            List<KeyValuePair<int, long>> topVram = new List<KeyValuePair<int, long>>(perPidDedicatedVram);
            topVram.Sort(delegate(KeyValuePair<int, long> a, KeyValuePair<int, long> b) { return b.Value.CompareTo(a.Value); });

            List<string> summaryParts = new List<string>();
            for (int i = 0; i < topVram.Count; i++)
            {
                int pid = topVram[i].Key;
                long bytes = topVram[i].Value;
                if (bytes < 25L * 1024 * 1024) continue;

                string pname = TryGetProcessNameFast(pid);
                if (summaryParts.Count < 4 && !string.IsNullOrEmpty(pname))
                {
                    summaryParts.Add(pname + " " + Fmt.BytesShort(bytes));
                }

                string low = (pname ?? "").ToLowerInvariant();
                if ((low == "ego" || low == "chrome" || low == "msedge" || low == "brave") && bytes >= 180L * 1024 * 1024)
                {
                    if (IsBrowserGpuSubprocessZeroWmi(pid))
                    {
                        snap.RefreshableBrowserGpuPids.Add(pid);
                        snap.ReclaimableVramBytes += (long)(bytes * 0.45);
                    }
                }
            }

            snap.TopVramSummary = summaryParts.Count > 0
                ? "Top VRAM: " + string.Join(" \u00B7 ", summaryParts.ToArray())
                : "VRAM healthy";
            if (!string.IsNullOrEmpty(snap.IdleOllamaSummary))
            {
                snap.TopVramSummary += "\n" + snap.IdleOllamaSummary;
            }

            return snap;
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

        public static Dictionary<int, long> QueryPerPidDedicatedVramPdh()
        {
            Dictionary<int, long> vramByPid = new Dictionary<int, long>();
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

                    uint bufSize = 0;
                    uint itemCount = 0;
                    uint st = NativeApi.PdhGetFormattedCounterArrayW(persistentPdhCounter, NativeApi.PDH_FMT_LARGE, ref bufSize, out itemCount, IntPtr.Zero);
                    if (st == NativeApi.PDH_MORE_DATA && bufSize > 0)
                    {
                        IntPtr pItems = Marshal.AllocHGlobal((int)bufSize);
                        try
                        {
                            if (NativeApi.PdhGetFormattedCounterArrayW(persistentPdhCounter, NativeApi.PDH_FMT_LARGE, ref bufSize, out itemCount, pItems) == 0)
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
                                            if (!vramByPid.TryGetValue(pidVal, out cur)) cur = 0;
                                            vramByPid[pidVal] = cur + item.FmtValue.largeValue;
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
                catch
                {
                }
            }
            return vramByPid;
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
