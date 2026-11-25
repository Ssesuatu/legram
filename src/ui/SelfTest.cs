using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Legram
{
    internal static class SelfTest
    {
        public static int Run(string outDir)
        {
            Directory.CreateDirectory(outDir);
            int failures = 0;

            Console.WriteLine("=== LEGRAM v2.7.0 — AUTOMATED SELFTEST ===");
            Stopwatch swTotal = Stopwatch.StartNew();

            AppSettings settings = AppSettings.Load();
            using (EcoQosManager eco = new EcoQosManager())
            using (RamEngine engine = new RamEngine(settings, eco))
            {
                eco.Enabled = true;
                eco.Start();
                engine.Start();
                SystemSnapshot snap = engine.RefreshSnapshot(false);

                // =========================================================================
                // FEATURE #1: Commit Charge & Pagefile OOM Shield
                // =========================================================================
                SystemSnapshot simCommitOom = new SystemSnapshot
                {
                    TotalRamBytes = 16L * 1024 * 1024 * 1024,
                    AvailableRamBytes = 5600L * 1024 * 1024,
                    UsedRamPercent = 65,
                    CommitTotalBytes = 36400L * 1024 * 1024,
                    CommitLimitBytes = 40000L * 1024 * 1024, // 91% Commit Charge!
                    RenderShieldActive = false
                };
                string oomReason = RamEngine.EvaluateAutoGuardTrigger(simCommitOom);
                bool f1Pass = snap.TotalRamBytes > 0
                    && snap.CommitTotalBytes > 0
                    && snap.CommitLimitBytes >= snap.TotalRamBytes
                    && oomReason != null
                    && oomReason.IndexOf("Commit OOM Shield", StringComparison.Ordinal) >= 0;
                if (!f1Pass) failures++;

                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "[F1] Commit Charge & OOM Shield -> PASS={0} | Live RAM: {1}/{2} ({3}% used, {4} free) | Live Commit: {5}/{6} ({7}%, Peak {8}) | Sim 91% Trigger=\"{9}\"",
                    f1Pass,
                    Fmt.BytesShort(snap.TotalRamBytes - snap.AvailableRamBytes),
                    Fmt.BytesShort(snap.TotalRamBytes),
                    snap.UsedRamPercent,
                    Fmt.BytesShort(snap.AvailableRamBytes),
                    Fmt.BytesShort(snap.CommitTotalBytes),
                    Fmt.BytesShort(snap.CommitLimitBytes),
                    snap.CommitUsedPercent,
                    Fmt.BytesShort(snap.CommitPeakBytes),
                    oomReason ?? "null"));

                // =========================================================================
                // FEATURE #2: Windows 11 Kernel Registry Cache (155) + System File Cache
                // =========================================================================
                bool regFlushed;
                bool sysCacheTrimmed;
                int rawRegNtStatus = -1;
                Stopwatch swF2 = Stopwatch.StartNew();
                RamEngine.TryFlushRegistryAndSystemFileCache(out regFlushed, out sysCacheTrimmed);
                try
                {
                    rawRegNtStatus = NativeApi.NtSetSystemInformation(NativeApi.SystemRegistryReconciliationInformation, IntPtr.Zero, 0);
                }
                catch { }
                swF2.Stop();
                // Without elevation NTSTATUS is typically STATUS_PRIVILEGE_NOT_HELD (0xC0000061) or STATUS_ACCESS_DENIED (0xC0000022), zero crash/zero hang
                bool f2Safe = swF2.ElapsedMilliseconds < (sysCacheTrimmed ? 10000 : 2500);
                if (!f2Safe) failures++;

                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "[F2] Kernel Registry(155) & SysFileCache -> PASS={0} ({1} ms, 0-Stutter) | RegistryFlushed={2} (NTSTATUS=0x{3:X8}), SysFileCacheTrimmed={4}, HelperTask0UAC={5}",
                    f2Safe,
                    swF2.ElapsedMilliseconds,
                    regFlushed,
                    (uint)rawRegNtStatus,
                    sysCacheTrimmed,
                    snap.KernelPurgeTaskReady));

                // =========================================================================
                // FEATURE #3: Idle Ollama VRAM Model Evictor + Expanded EcoQoS (IoPriorityLow)
                // =========================================================================
                string capturedPostBody = "";
                int mockUnloaded = 0;
                long mockDetectedVram = 0;
                string mockDetectedModel = "";
                bool ollamaLivePresent = GpuVramMonitor.IsOllamaDaemonPresent();

                TcpListener mockServer = new TcpListener(IPAddress.Loopback, 0);
                mockServer.Start();
                int port = ((IPEndPoint)mockServer.LocalEndpoint).Port;
                string mockBaseUrl = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);

                Thread serverThread = new Thread(delegate()
                {
                    for (int reqIdx = 0; reqIdx < 3; reqIdx++)
                    {
                        try
                        {
                            using (TcpClient client = mockServer.AcceptTcpClient())
                            using (NetworkStream ns = client.GetStream())
                            {
                                client.ReceiveTimeout = 1500;
                                client.SendTimeout = 1500;
                                MemoryStream reqMs = new MemoryStream();
                                byte[] buf = new byte[1024];
                                string rawReq = "";
                                int bodySep = -1;
                                int expectedBodyLen = 0;

                                while (true)
                                {
                                    int n = ns.Read(buf, 0, buf.Length);
                                    if (n <= 0) break;
                                    reqMs.Write(buf, 0, n);
                                    rawReq = Encoding.UTF8.GetString(reqMs.ToArray());
                                    bodySep = rawReq.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                                    if (bodySep >= 0)
                                    {
                                        string hdrPart = rawReq.Substring(0, bodySep);
                                        int clIdx = hdrPart.IndexOf("Content-Length:", StringComparison.OrdinalIgnoreCase);
                                        if (clIdx >= 0)
                                        {
                                            int clEnd = hdrPart.IndexOf("\r\n", clIdx, StringComparison.Ordinal);
                                            string clVal = (clEnd >= 0 ? hdrPart.Substring(clIdx + 15, clEnd - (clIdx + 15)) : hdrPart.Substring(clIdx + 15)).Trim();
                                            int.TryParse(clVal, NumberStyles.Integer, CultureInfo.InvariantCulture, out expectedBodyLen);
                                        }
                                        int bodyBytesHave = Encoding.UTF8.GetByteCount(rawReq.Substring(bodySep + 4));
                                        if (bodyBytesHave >= expectedBodyLen) break;
                                    }
                                }
                                if (rawReq.Length == 0) break;
                                string reqBody = bodySep >= 0 ? rawReq.Substring(bodySep + 4) : "";

                                string jsonResp;
                                if (rawReq.StartsWith("GET /api/ps", StringComparison.OrdinalIgnoreCase))
                                {
                                    jsonResp = "{\"models\":[{\"name\":\"qwen3:8b\",\"size_vram\":5368709120}]}";
                                }
                                else
                                {
                                    capturedPostBody = reqBody;
                                    jsonResp = "{\"model\":\"qwen3:8b\",\"done\":true}";
                                }

                                byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonResp);
                                string hdr = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: "
                                    + bodyBytes.Length.ToString(CultureInfo.InvariantCulture)
                                    + "\r\nConnection: close\r\n\r\n";
                                byte[] hdrBytes = Encoding.ASCII.GetBytes(hdr);
                                ns.Write(hdrBytes, 0, hdrBytes.Length);
                                ns.Write(bodyBytes, 0, bodyBytes.Length);
                                ns.Flush();
                            }
                        }
                        catch
                        {
                            break;
                        }
                    }
                });
                serverThread.IsBackground = true;
                serverThread.Start();

                try
                {
                    List<string> probedModels;
                    GpuVramMonitor.ProbeOllamaLoadedModels(mockBaseUrl, out probedModels, out mockDetectedVram);
                    if (probedModels.Count > 0) mockDetectedModel = probedModels[0];
                    mockUnloaded = GpuVramMonitor.EvictOllamaModelsAtEndpoint(mockBaseUrl);
                }
                finally
                {
                    try { mockServer.Stop(); } catch { }
                }

                // Verify live EcoQoS + NT Kernel ProcessIoPriority (IoPriorityLow = 1) on live background processes
                int selfPid = Process.GetCurrentProcess().Id;
                int initialSelfIoPrio = EcoQosManager.QueryProcessIoPriority(selfPid);
                GuardScanResult liveGuard = ProcessGuard.Scan(null, null, false);
                HashSet<int> throttledSet = eco.ReconcileAndSnapshot(liveGuard);
                int verifiedLowIoCount = 0;
                int sampleLowPid = 0;
                string sampleLowName = "";
                foreach (int tPid in throttledSet)
                {
                    int ioPrio = EcoQosManager.QueryProcessIoPriority(tPid);
                    if (ioPrio == NativeApi.IoPriorityLow)
                    {
                        verifiedLowIoCount++;
                        if (sampleLowPid == 0)
                        {
                            sampleLowPid = tPid;
                            try { sampleLowName = Process.GetProcessById(tPid).ProcessName; } catch { sampleLowName = "pid:" + tPid; }
                        }
                    }
                }
                int selfIoPrio = EcoQosManager.QueryProcessIoPriority(selfPid);

                bool f3OllamaPass = mockDetectedModel == "qwen3:8b"
                    && mockDetectedVram == 5368709120L
                    && mockUnloaded == 1
                    && capturedPostBody.IndexOf("\"keep_alive\":0", StringComparison.Ordinal) >= 0;
                bool f3EcoPass = !throttledSet.Contains(selfPid)
                    && selfIoPrio == initialSelfIoPrio
                    && (throttledSet.Count == 0 || verifiedLowIoCount > 0);
                if (!f3OllamaPass || !f3EcoPass) failures++;

                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "[F3a] Idle Ollama VRAM Evictor -> PASS={0} | LiveDaemon={1}, LiveIdleModels={2} | MockProbe={3} ({4}), Evicted={5}, Payload={6}",
                    f3OllamaPass,
                    ollamaLivePresent,
                    snap.Gpu.IdleOllamaModelCount,
                    mockDetectedModel,
                    Fmt.BytesShort(mockDetectedVram),
                    mockUnloaded,
                    capturedPostBody));
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "[F3b] Expanded EcoQoS + Kernel IoPriority -> PASS={0} | Throttled={1} BG PIDs, KernelVerified IoPriorityLow(1)={2} PIDs (e.g. {3}#{4}), ProtectedSelf IoPriority={5}",
                    f3EcoPass,
                    throttledSet.Count,
                    verifiedLowIoCount,
                    sampleLowName,
                    sampleLowPid,
                    selfIoPrio));

                // =========================================================================
                // FEATURE #4: Dynamic Numeric Tray Icon + ISLC Dual-Threshold Anti-Stutter
                // =========================================================================
                SystemSnapshot simIslc = new SystemSnapshot
                {
                    TotalRamBytes = 16L * 1024 * 1024 * 1024,
                    AvailableRamBytes = 5100L * 1024 * 1024,
                    UsedRamPercent = 68,
                    CommitTotalBytes = 18L * 1024 * 1024 * 1024,
                    CommitLimitBytes = 32L * 1024 * 1024 * 1024,
                    StandbyTotalBytes = 4600L * 1024 * 1024, // >= 4.0 GB Standby
                    FreeZeroBytes = 620L * 1024 * 1024,      // < 1.0 GB True Free+Zero!
                    RenderShieldActive = false
                };
                string islcReason = RamEngine.EvaluateAutoGuardTrigger(simIslc);
                string trayStripPath = Path.Combine(outDir, "legram-tray.png");
                RenderTrayNumericStripPng(42, trayStripPath);

                bool iconHandleOk = false;
                System.Drawing.Icon testIcon = TrayGlyph.Make(32, snap.UsedRamPercent, snap.Pressure, false, false);
                if (testIcon != null && testIcon.Handle != IntPtr.Zero)
                {
                    iconHandleOk = true;
                    NativeApi.DestroyIcon(testIcon.Handle);
                    testIcon.Dispose();
                }

                bool f4Pass = islcReason != null
                    && islcReason.IndexOf("ISLC Standby", StringComparison.Ordinal) >= 0
                    && iconHandleOk
                    && File.Exists(trayStripPath);
                if (!f4Pass) failures++;

                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "[F4] Numeric Tray + ISLC Anti-Stutter -> PASS={0} | Live Standby={1}, Live Free+Zero={2}, SaturatedNow={3} | SimTrigger=\"{4}\" | TrayPNG={5}",
                    f4Pass,
                    Fmt.BytesShort(snap.StandbyTotalBytes),
                    Fmt.BytesShort(snap.FreeZeroBytes),
                    snap.IsIslcStandbySaturated,
                    islcReason ?? "null",
                    Path.GetFileName(trayStripPath)));

                // =========================================================================
                // FEATURE #5: dwm.exe Leak Detector & Safe Working-Set Deflator
                // =========================================================================
                bool dwmDetected = snap.DwmPid > 4 && snap.DwmWorkingSetBytes > 0;
                bool dwmProtectedFromKillAndEco = liveGuard.ProtectedPids.Contains(snap.DwmPid) && !throttledSet.Contains(snap.DwmPid);

                // Prove gentle K32EmptyWorkingSet deflation on a touched 24 MB buffer in our own process
                byte[] tempTouch = new byte[24 * 1024 * 1024];
                for (int i = 0; i < tempTouch.Length; i += 4096)
                {
                    tempTouch[i] = (byte)((i >> 12) & 0xFF);
                }
                long wsBeforeDeflate;
                using (Process selfBefore = Process.GetCurrentProcess())
                {
                    wsBeforeDeflate = selfBefore.WorkingSet64;
                }
                long selfDeflatedBytes;
                bool deflateCallOk = RamEngine.TryDeflateDwmWorkingSet(selfPid, wsBeforeDeflate, 8L * 1024 * 1024, out selfDeflatedBytes);
                GC.KeepAlive(tempTouch);
                tempTouch = null;
                int selfIoAfterDeflate = EcoQosManager.QueryProcessIoPriority(selfPid);

                bool f5Pass = dwmDetected && dwmProtectedFromKillAndEco && deflateCallOk && selfDeflatedBytes > 0 && selfIoAfterDeflate == initialSelfIoPrio;
                if (!f5Pass) failures++;

                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "[F5] dwm.exe Leak Detector & Safe Deflator -> PASS={0} | Live dwm#{1}: WS={2}, VRAM={3}, Bloated={4}, GuardProtected={5} | DeflateProof Freed={6} (IoPrio preserved={7})",
                    f5Pass,
                    snap.DwmPid,
                    Fmt.BytesShort(snap.DwmWorkingSetBytes),
                    Fmt.BytesShort(snap.DwmVramBytes),
                    snap.IsDwmBloated,
                    dwmProtectedFromKillAndEco,
                    Fmt.BytesShort(selfDeflatedBytes),
                    selfIoAfterDeflate));

                // =========================================================================
                // FEATURE #6: Game Mode Auto-Pause (Anti Micro-Stutter) & 1-Click Whitelist Shield
                // =========================================================================
                SystemSnapshot simGameActive = new SystemSnapshot
                {
                    TotalRamBytes = 16L * 1024 * 1024 * 1024,
                    AvailableRamBytes = 1800L * 1024 * 1024,
                    UsedRamPercent = 89,
                    CommitTotalBytes = 36400L * 1024 * 1024,
                    CommitLimitBytes = 40000L * 1024 * 1024,
                    StandbyTotalBytes = 5200L * 1024 * 1024,
                    FreeZeroBytes = 420L * 1024 * 1024,
                    GameShieldActive = true,
                    ActiveGameName = "FullscreenGame"
                };
                string gamePausedReason = RamEngine.EvaluateAutoGuardTrigger(simGameActive);
                bool d3dExcl;
                bool fgFullscreenNow = NativeApi.IsForegroundWindowFullscreen(NativeApi.GetForegroundWindow(), out d3dExcl);

                // Pick a live non-protected process group from TopGroups (or fallback "chrome") to test 1-Click Immune Shield
                bool foundLiveCandidate = false;
                string testGroupKey = "chrome";
                for (int i = 0; i < snap.TopGroups.Count; i++)
                {
                    ProcessGroupItem pg = snap.TopGroups[i];
                    if (pg != null && !pg.IsProtected && !pg.IsOrphanGroup && !string.IsNullOrEmpty(pg.GroupKey))
                    {
                        testGroupKey = pg.GroupKey;
                        foundLiveCandidate = true;
                        break;
                    }
                }
                bool hadImmuneBefore = settings.IsGroupImmune(testGroupKey);
                settings.SetGroupImmune(testGroupKey, true);
                HashSet<string> testImmuneSet = settings.SnapshotImmuneGroups();
                GuardScanResult immuneScan = ProcessGuard.Scan(null, null, false, testImmuneSet, true);
                HashSet<int> throttledAfterImmune = eco.ReconcileAndSnapshot(immuneScan);

                bool groupMarkedImmune = !foundLiveCandidate && immuneScan.UserImmuneGroupCount >= 1;
                bool allGroupPidsProtectedAndUnthrottled = true;
                for (int i = 0; i < immuneScan.TopGroups.Count; i++)
                {
                    ProcessGroupItem pg = immuneScan.TopGroups[i];
                    if (pg != null && string.Equals(pg.GroupKey, testGroupKey, StringComparison.OrdinalIgnoreCase))
                    {
                        groupMarkedImmune = pg.IsUserWhitelisted;
                        for (int k = 0; k < pg.Pids.Count; k++)
                        {
                            int gPid = pg.Pids[k];
                            if (!immuneScan.ProtectedPids.Contains(gPid) || throttledAfterImmune.Contains(gPid))
                            {
                                allGroupPidsProtectedAndUnthrottled = false;
                            }
                        }
                        break;
                    }
                }
                settings.SetGroupImmune(testGroupKey, hadImmuneBefore);

                bool f6Pass = (gamePausedReason == null)
                    && testImmuneSet.Contains(testGroupKey)
                    && groupMarkedImmune
                    && allGroupPidsProtectedAndUnthrottled;
                if (!f6Pass) failures++;

                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "[F6] Game Mode Auto-Pause & 1-Click Immune Shield -> PASS={0} | SimGame89%RAM Trigger={1} (Paused!) | LiveFullscreen={2} (D3D={3}) | ImmuneTest({4}): MarkedImmune={5}, AllPidsProtectedAndUnthrottled={6}",
                    f6Pass,
                    gamePausedReason ?? "null",
                    fgFullscreenNow,
                    d3dExcl,
                    testGroupKey,
                    groupMarkedImmune,
                    allGroupPidsProtectedAndUnthrottled));

                // =========================================================================
                // FEATURE #7: Virtual Desktop Auto-Sleep & Active Desktop Indicator (Desk 1/2)
                // =========================================================================
                VirtualDesktopState liveVd = VirtualDesktopShield.SampleAndFollow(IntPtr.Zero, false);
                bool vdLiveValid = liveVd != null
                    && liveVd.ActiveDesktopNumber >= 1
                    && liveVd.TotalDesktopCount >= 1
                    && liveVd.ActiveDesktopNumber <= liveVd.TotalDesktopCount;

                // Simulate a secondary desktop where an unprotected process group lives on Desktop 1 while user is on Desktop 2
                int simSleepPid = 0;
                for (int i = 0; i < snap.TopGroups.Count; i++)
                {
                    ProcessGroupItem pg = snap.TopGroups[i];
                    if (pg != null && !pg.IsProtected && pg.Pids != null && pg.Pids.Count > 0)
                    {
                        simSleepPid = pg.Pids[0];
                        break;
                    }
                }
                bool simDesktopSleepOk = true;
                if (simSleepPid > 0)
                {
                    VirtualDesktopState simVdAway = new VirtualDesktopState
                    {
                        ActiveDesktopNumber = 2,
                        TotalDesktopCount = Math.Max(2, liveVd != null ? liveVd.TotalDesktopCount : 2),
                        ActiveDesktopName = "Desktop 2"
                    };
                    simVdAway.PidsOnInactiveDesktops.Add(simSleepPid);
                    simVdAway.PidPrimaryDesktopNumber[simSleepPid] = 1;

                    GuardScanResult vdAwayScan = ProcessGuard.Scan(null, null, false, null, true, simVdAway);
                    HashSet<int> vdAwayThrottled = eco.ReconcileAndSnapshot(vdAwayScan);
                    bool markedInactiveSleep = vdAwayScan.InactiveDesktopSleepPids.Contains(simSleepPid)
                        && !vdAwayScan.ProtectedPids.Contains(simSleepPid);

                    // Now simulate switching back to Desktop 1 -> Instant Wake when active on Desktop 1
                    VirtualDesktopState simVdReturn = new VirtualDesktopState
                    {
                        ActiveDesktopNumber = 1,
                        TotalDesktopCount = Math.Max(2, liveVd != null ? liveVd.TotalDesktopCount : 2),
                        ActiveDesktopName = "Desktop 1"
                    };
                    simVdReturn.PidsOnActiveDesktop.Add(simSleepPid);
                    simVdReturn.PidPrimaryDesktopNumber[simSleepPid] = 1;

                    GuardScanResult vdReturnScan = ProcessGuard.Scan(null, null, false, null, true, simVdReturn);
                    bool clearedInactiveOnReturn = !vdReturnScan.InactiveDesktopSleepPids.Contains(simSleepPid);
                    simDesktopSleepOk = markedInactiveSleep && vdAwayThrottled.Contains(simSleepPid) && clearedInactiveOnReturn;
                }

                bool f7Pass = vdLiveValid && simDesktopSleepOk;
                if (!f7Pass) failures++;

                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "[F7] Virtual Desktop Auto-Sleep & Indicator -> PASS={0} | LiveDesk=Desk {1}/{2} (\"{3}\"), ActivePids={4}, InactivePids={5} | SimAwayWakeOk={6}",
                    f7Pass,
                    liveVd != null ? liveVd.ActiveDesktopNumber : 1,
                    liveVd != null ? liveVd.TotalDesktopCount : 1,
                    liveVd != null ? liveVd.ActiveDesktopName : "Desktop 1",
                    liveVd != null ? liveVd.PidsOnActiveDesktop.Count : 0,
                    liveVd != null ? liveVd.PidsOnInactiveDesktops.Count : 0,
                    simDesktopSleepOk));

                // =========================================================================
                // FEATURE #8: Paket A+B+C Verification (MemoryPriority Restore, Multi-AI LM Studio + ComfyUI, Port Probe)
                // =========================================================================
                EcoQosManager.ApplyProcessQos(selfPid, false);
                uint memPrioBefore = EcoQosManager.QueryProcessMemoryPriority(selfPid);
                bool trimSafeOk = RamEngine.TrimSingleProcessSafe(selfPid, true, false);
                uint memPrioAfter = EcoQosManager.QueryProcessMemoryPriority(selfPid);
                bool f8MemPrioPass = trimSafeOk && memPrioBefore >= 3 && memPrioAfter == memPrioBefore;

                string capturedLmsUnload = "";
                string capturedComfyFree = "";
                int mockLmsUnloaded = 0;
                bool mockComfyFreed = false;
                bool portListenDetected = false;

                TcpListener aiMock = new TcpListener(IPAddress.Loopback, 0);
                aiMock.Start();
                int aiPort = ((IPEndPoint)aiMock.LocalEndpoint).Port;
                portListenDetected = NativeApi.IsLoopbackTcpPortListening(aiPort);
                string aiBaseUrl = "http://127.0.0.1:" + aiPort.ToString(CultureInfo.InvariantCulture);

                Thread aiServerThread = new Thread(delegate()
                {
                    for (int reqIdx = 0; reqIdx < 4; reqIdx++)
                    {
                        try
                        {
                            using (TcpClient client = aiMock.AcceptTcpClient())
                            using (NetworkStream ns = client.GetStream())
                            {
                                client.ReceiveTimeout = 1500;
                                client.SendTimeout = 1500;
                                MemoryStream reqMs = new MemoryStream();
                                byte[] buf = new byte[1024];
                                string rawReq = "";
                                int bodySep = -1;
                                int expectedBodyLen = 0;

                                while (true)
                                {
                                    int n = ns.Read(buf, 0, buf.Length);
                                    if (n <= 0) break;
                                    reqMs.Write(buf, 0, n);
                                    rawReq = Encoding.UTF8.GetString(reqMs.ToArray());
                                    bodySep = rawReq.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                                    if (bodySep >= 0)
                                    {
                                        string hdrPart = rawReq.Substring(0, bodySep);
                                        int clIdx = hdrPart.IndexOf("Content-Length:", StringComparison.OrdinalIgnoreCase);
                                        if (clIdx >= 0)
                                        {
                                            int clEnd = hdrPart.IndexOf("\r\n", clIdx, StringComparison.Ordinal);
                                            string clVal = (clEnd >= 0 ? hdrPart.Substring(clIdx + 15, clEnd - (clIdx + 15)) : hdrPart.Substring(clIdx + 15)).Trim();
                                            int.TryParse(clVal, NumberStyles.Integer, CultureInfo.InvariantCulture, out expectedBodyLen);
                                        }
                                        int bodyBytesHave = Encoding.UTF8.GetByteCount(rawReq.Substring(bodySep + 4));
                                        if (bodyBytesHave >= expectedBodyLen) break;
                                    }
                                }
                                if (rawReq.Length == 0) break;
                                string reqBody = bodySep >= 0 ? rawReq.Substring(bodySep + 4) : "";

                                string jsonResp = "{}";
                                if (rawReq.StartsWith("GET /api/v1/models", StringComparison.OrdinalIgnoreCase))
                                {
                                    jsonResp = "{\"data\":[{\"id\":\"qwen2.5-coder-14b\",\"state\":\"loaded\"}]}";
                                }
                                else if (rawReq.StartsWith("POST /api/v1/models/unload", StringComparison.OrdinalIgnoreCase))
                                {
                                    capturedLmsUnload = reqBody;
                                    jsonResp = "{\"status\":\"unloaded\"}";
                                }
                                else if (rawReq.StartsWith("GET /queue", StringComparison.OrdinalIgnoreCase))
                                {
                                    jsonResp = "{\"queue_running\":[],\"queue_pending\":[]}";
                                }
                                else if (rawReq.StartsWith("POST /free", StringComparison.OrdinalIgnoreCase))
                                {
                                    capturedComfyFree = reqBody;
                                    jsonResp = "{\"status\":\"ok\"}";
                                }

                                byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonResp);
                                string hdr = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: "
                                    + bodyBytes.Length.ToString(CultureInfo.InvariantCulture)
                                    + "\r\nConnection: close\r\n\r\n";
                                byte[] hdrBytes = Encoding.ASCII.GetBytes(hdr);
                                ns.Write(hdrBytes, 0, hdrBytes.Length);
                                ns.Write(bodyBytes, 0, bodyBytes.Length);
                                ns.Flush();
                            }
                        }
                        catch
                        {
                            break;
                        }
                    }
                });
                aiServerThread.IsBackground = true;
                aiServerThread.Start();

                try
                {
                    mockLmsUnloaded = GpuVramMonitor.EvictLmStudioModelsAtEndpoint(aiBaseUrl);
                    mockComfyFreed = GpuVramMonitor.FreeComfyUiMemoryAtEndpoint(aiBaseUrl);
                }
                finally
                {
                    try { aiMock.Stop(); } catch { }
                }

                bool f8MultiAiPass = portListenDetected
                    && mockLmsUnloaded == 1
                    && capturedLmsUnload.IndexOf("qwen2.5-coder-14b", StringComparison.Ordinal) >= 0
                    && mockComfyFreed
                    && capturedComfyFree.IndexOf("\"unload_models\":true", StringComparison.Ordinal) >= 0;

                if (!f8MemPrioPass || !f8MultiAiPass) failures++;

                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "[F8] MemoryPriority Restore & Multi-AI (LM Studio + ComfyUI) -> PASS={0} | MemPrio {1}->{2} | TcpProbe(:{3})={4} | LMS Unload={5} ({6}) | ComfyUI Free={7} ({8})",
                    f8MemPrioPass && f8MultiAiPass,
                    memPrioBefore,
                    memPrioAfter,
                    aiPort,
                    portListenDetected,
                    mockLmsUnloaded,
                    capturedLmsUnload,
                    mockComfyFreed,
                    capturedComfyFree));

                // =========================================================================
                // FEATURE #9: Commit Anti-Loop Hysteresis Latch & Kernel Pool Telemetry
                // =========================================================================
                SystemSnapshot simHighCommitHealthyRam = new SystemSnapshot
                {
                    TotalRamBytes = 16L * 1073741824L,
                    AvailableRamBytes = 6800L * 1048576L,
                    UsedRamPercent = 58,
                    CommitTotalBytes = 30940L * 1048576L,
                    CommitLimitBytes = 34000L * 1048576L, // 91% Commit while Physical RAM is 58%!
                    RenderShieldActive = false
                };
                engine.SetCommitLatchForSelfTest(false, 0, DateTime.MinValue);
                string f9Reason1;
                bool f9CommitOnly1;
                bool f9InitialFire = engine.ShouldFireAutoGuard(simHighCommitHealthyRam, out f9Reason1, out f9CommitOnly1);

                engine.SetCommitLatchForSelfTest(true, 91, DateTime.UtcNow.AddMinutes(-3));
                string f9Reason2;
                bool f9CommitOnly2;
                bool f9BlockedWhileLatched = !engine.ShouldFireAutoGuard(simHighCommitHealthyRam, out f9Reason2, out f9CommitOnly2);

                SystemSnapshot simRecoveredCommit = new SystemSnapshot
                {
                    TotalRamBytes = 16L * 1073741824L,
                    AvailableRamBytes = 7400L * 1048576L,
                    UsedRamPercent = 54,
                    CommitTotalBytes = 27880L * 1048576L,
                    CommitLimitBytes = 34000L * 1048576L // 82% <= 84% hysteresis reset!
                };
                engine.UpdateAutoGuardHysteresis(simRecoveredCommit);
                string f9Reason3;
                bool f9CommitOnly3;
                bool f9RearmedAfterDrop = engine.ShouldFireAutoGuard(simHighCommitHealthyRam, out f9Reason3, out f9CommitOnly3);
                engine.SetCommitLatchForSelfTest(false, 0, DateTime.MinValue);

                bool f9PoolTelemetry = snap.KernelNonpagedBytes > 0 && snap.KernelPagedBytes > 0;
                bool f9Pass = f9InitialFire && f9CommitOnly1 && f9BlockedWhileLatched && f9RearmedAfterDrop && f9PoolTelemetry;
                if (!f9Pass) failures++;

                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "[F9] Commit Hysteresis Anti-Loop & Kernel Pool -> PASS={0} | CommitOnlyWhenRamFree={1}, LatchedBlock={2}, RearmAt82Pct={3} | Live Pool NP={4}, Paged={5}",
                    f9Pass,
                    f9CommitOnly1,
                    f9BlockedWhileLatched,
                    f9RearmedAfterDrop,
                    Fmt.BytesShort(snap.KernelNonpagedBytes),
                    Fmt.BytesShort(snap.KernelPagedBytes)));

                // =========================================================================
                // UI Audit + Deterministic 2x HiDPI Render Proofs + Live 1-Button Unified Harmony
                // =========================================================================
                Application app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                MonolithWindow testWin = new MonolithWindow(settings, engine, eco);
                bool hasMinClose = testWin.MainSurface != null
                    && testWin.MainSurface.MinimizeButton != null
                    && testWin.MainSurface.CloseButton != null;
                bool hoverImmune = testWin.VerifyHoverImmunityForSelfTest();
                testWin.Close();
                if (!hasMinClose || !hoverImmune) failures++;

                SystemSnapshot showcase = BuildShowcaseSnapshot();
                RenderMonolithPng(showcase, Path.Combine(outDir, "legram-monolith.png"));
                RenderMonolithClickMotionPng(showcase, Path.Combine(outDir, "legram-motion.png"));
                RenderCapsuleMiniPng(showcase, Path.Combine(outDir, "legram-capsule.png"));

                PurgeResult harmony = engine.RunUnifiedHarmonyAsync().GetAwaiter().GetResult();
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "[HARMONY] 1-Click Live PC -> Success={0}, FreedRAM={1}, FreedVRAM={2}, FreedSSD={3}, Trimmed={4} procs, Duration={5} ms -> \"{6}\"",
                    harmony.Success,
                    Fmt.BytesShort(harmony.FreedRamBytes),
                    Fmt.BytesShort(harmony.FreedVramBytes),
                    Fmt.BytesShort(harmony.FreedDiskBytes),
                    harmony.TrimmedProcessCount,
                    harmony.DurationMs,
                    harmony.SummaryText));
                if (!harmony.Success) failures++;

                // Restore all background QoS before exiting selftest cleanly
                eco.RestoreAll();
                GpuVramMonitor.ClosePdhIfIdle();
                GC.Collect(2, GCCollectionMode.Forced, true);
                NativeApi.SelfTrimWorkingSet();
                using (Process self = Process.GetCurrentProcess())
                {
                    double wsMb = self.WorkingSet64 / 1048576.0;
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "[FOOTPRINT] Post Self-Trim Working Set: {0:F2} MB | Protected PIDs: {1} | Total Elapsed: {2} ms | Failures: {3}",
                        wsMb, snap.ProtectedProcessCount, swTotal.ElapsedMilliseconds, failures));
                }
            }

            return failures == 0 ? 0 : 1;
        }

        private static void RenderTrayNumericStripPng(int livePct, string path)
        {
            int tileSz = 72;
            int pad = 16;
            int gap = 14;
            int count = 5;
            int width = pad * 2 + count * tileSz + (count - 1) * gap;
            int height = pad * 2 + tileSz + 24;

            int[] pcts = new int[] { livePct, 52, 76, 91, livePct };
            MemoryPressure[] states = new MemoryPressure[]
            {
                livePct >= 85 ? MemoryPressure.Critical : (livePct >= 70 ? MemoryPressure.Warning : MemoryPressure.Healthy),
                MemoryPressure.Healthy,
                MemoryPressure.Warning,
                MemoryPressure.Critical,
                MemoryPressure.Boosting
            };
            bool[] working = new bool[] { false, false, false, false, true };
            string[] labels = new string[] { "IDLE", "CLEAR", "BUSY", "TIGHT", "TUNING" };

            using (System.Drawing.Bitmap canvas = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(canvas))
            using (System.Drawing.Font lblFont = new System.Drawing.Font("Segoe UI", 10.5f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel))
            using (System.Drawing.Brush lblBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(210, 167, 173, 186)))
            using (System.Drawing.StringFormat sf = new System.Drawing.StringFormat())
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.Clear(System.Drawing.Color.FromArgb(255, 11, 13, 18));
                sf.Alignment = System.Drawing.StringAlignment.Center;

                for (int i = 0; i < count; i++)
                {
                    int x = pad + i * (tileSz + gap);
                    using (System.Drawing.Bitmap iconBmp = TrayGlyph.MakeBitmap(tileSz, pcts[i], states[i], false, working[i]))
                    {
                        g.DrawImage(iconBmp, x, pad, tileSz, tileSz);
                    }
                    g.DrawString(labels[i], lblFont, lblBrush, new System.Drawing.RectangleF(x - 4, pad + tileSz + 5, tileSz + 8, 18), sf);
                }

                canvas.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        private static SystemSnapshot BuildShowcaseSnapshot()
        {
            DriveSnapshot cDrive = new DriveSnapshot
            {
                Root = @"C:\",
                Letter = "C:",
                Label = "System",
                HardwareModel = "NVMe SSD",
                FileSystem = "NTFS",
                BusType = 17,
                FreeBytes = 284L * 1073741824L,
                TotalBytes = 512L * 1073741824L,
                IsVirtualOrCloud = false
            };
            DriveSnapshot dDrive = new DriveSnapshot
            {
                Root = @"D:\",
                Letter = "D:",
                Label = "Workspace",
                HardwareModel = "NVMe SSD",
                FileSystem = "NTFS",
                BusType = 17,
                FreeBytes = 612L * 1073741824L,
                TotalBytes = 1024L * 1073741824L,
                IsVirtualOrCloud = false
            };

            SystemSnapshot s = new SystemSnapshot
            {
                InstalledDimmBytes = 16L * 1073741824L,
                TotalRamBytes = (long)(15.8 * 1073741824.0),
                HardwareReservedBytes = 200L * 1048576L,
                AvailableRamBytes = (long)(9.2 * 1073741824.0),
                CommitTotalBytes = (long)(12.4 * 1073741824.0),
                CommitLimitBytes = 24L * 1073741824L,
                CommitPeakBytes = 14L * 1073741824L,
                KernelNonpagedBytes = 420L * 1048576L,
                KernelPagedBytes = 610L * 1048576L,
                ModifiedBytes = 96L * 1048576L,
                FreeZeroBytes = 5L * 1073741824L,
                StandbyTotalBytes = (long)(4.2 * 1073741824.0),
                StandbyLowPrioBytes = (long)(2.6 * 1073741824.0),
                StandbyP0Bytes = 920L * 1048576L,
                WorkingSetTrimmableBytes = 940L * 1048576L,
                UsedRamPercent = 42,
                Pressure = MemoryPressure.Healthy,
                ActiveDesktopNumber = 1,
                TotalDesktopCount = 2,
                ActiveDesktopName = "Desktop 1",
                KernelPurgeTaskReady = true,
                ProtectedProcessCount = 148,
                TcpListenerCount = 12,
                EcoQosThrottledCount = 18,
                DwmPid = 920,
                DwmWorkingSetBytes = 142L * 1048576L,
                GuardTooltipSummary = "Auto-Protected: Dev Toolchain \u00B7 12 TCP Listen \u00B7 1 Active Audio",
                PrimaryDisplayDrive = cDrive
            };

            s.Gpu = new GpuSnapshot
            {
                Available = true,
                Name = "NVIDIA GeForce RTX 4070",
                ShortName = "RTX 4070",
                VramUsedBytes = (long)(2.4 * 1073741824.0),
                VramTotalBytes = 12L * 1073741824L,
                TemperatureC = 41,
                GpuUtilPercent = 4,
                ReclaimableVramBytes = 320L * 1048576L
            };

            s.Drives.Add(cDrive);
            s.Drives.Add(dDrive);
            s.Junk = new SweepEstimate { LocalTempBytes = 180L * 1048576L, StaleShaderAndUpdaterBytes = 140L * 1048576L };

            s.TopGroups.Add(new ProcessGroupItem
            {
                GroupKey = "chrome",
                DisplayName = "Google Chrome",
                Subtitle = "18 procs \u00B7 +720M trimmable \u00B7 Click for Shield/Trim",
                IconKind = GroupIconKind.Browser,
                ProcessCount = 18,
                WorkingSetBytes = (long)(1.84 * 1073741824.0),
                EstimTrimmableBytes = 720L * 1048576L,
                IsEcoQosActive = true,
                PrimaryDesktopNumber = 1
            });
            s.TopGroups.Add(new ProcessGroupItem
            {
                GroupKey = "code",
                DisplayName = "Visual Studio Code",
                Subtitle = "11 procs \u00B7 Protected \u00B7 Click to manage",
                IconKind = GroupIconKind.ProtectedShield,
                ProcessCount = 11,
                WorkingSetBytes = (long)(1.12 * 1073741824.0),
                IsProtected = true,
                IsForeground = true,
                PrimaryDesktopNumber = 1
            });
            s.TopGroups.Add(new ProcessGroupItem
            {
                GroupKey = "ollama",
                DisplayName = "Ollama",
                Subtitle = "3 procs \u00B7 +220M trimmable \u00B7 Click for Shield/Trim",
                IconKind = GroupIconKind.AiSparkle,
                ProcessCount = 3,
                WorkingSetBytes = 860L * 1048576L,
                EstimTrimmableBytes = 220L * 1048576L,
                PrimaryDesktopNumber = 2,
                IsOnInactiveDesktop = true
            });
            s.TopGroups.Add(new ProcessGroupItem
            {
                GroupKey = "protected-dev",
                DisplayName = "Dev \u00B7 AI \u00B7 Servers",
                Subtitle = "Dev Tools \u00B7 12 TCP",
                IconKind = GroupIconKind.ProtectedShield,
                ProcessCount = 14,
                WorkingSetBytes = 640L * 1048576L,
                IsProtected = true
            });
            return s;
        }

        private static void RenderMonolithPng(SystemSnapshot snap, string path)
        {
            MonolithView mv = new MonolithView();
            mv.Bind(snap, false, false, "");

            Border bg = new Border
            {
                Background = Theme.BrushHex(0xFF090A0Eu),
                Padding = new Thickness(22),
                Child = mv
            };
            SaveElementPng(bg, path, 192.0);
        }

        private static void RenderMonolithClickMotionPng(SystemSnapshot snap, string path)
        {
            MonolithView mIdle = new MonolithView { Margin = new Thickness(0, 0, 18, 0) };
            mIdle.Bind(snap, false, false, "");

            MonolithView mSweep = new MonolithView { Margin = new Thickness(0, 0, 18, 0) };
            mSweep.Bind(snap, false, true, "");
            mSweep.SetPreviewClickState(0.62, false);

            MonolithView mDone = new MonolithView();
            mDone.Bind(snap, false, false, "Harmonized \u00B7 +1.2 GB");
            mDone.SetPreviewClickState(1.0, true);

            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(mIdle);
            row.Children.Add(mSweep);
            row.Children.Add(mDone);

            Border stage = new Border
            {
                Background = Theme.Grad(0xFF090A0F, 0xFF141824, 135),
                Padding = new Thickness(24),
                Child = row
            };
            SaveElementPng(stage, path, 192.0);
        }

        private static void RenderCapsuleMiniPng(SystemSnapshot snap, string path)
        {
            OpticalLensView cIdle = new OpticalLensView { Margin = new Thickness(0, 0, 16, 0) };
            cIdle.Bind(snap, false, false, "");

            OpticalLensView cDone = new OpticalLensView();
            cDone.Bind(snap, false, false, "+1.2 GB Freed");

            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(cIdle);
            row.Children.Add(cDone);

            Border stage = new Border
            {
                Background = Theme.Grad(0xFF0B0C10, 0xFF171921, 135),
                Padding = new Thickness(22),
                Child = row
            };
            SaveElementPng(stage, path, 192.0);
        }

        private static void SaveElementPng(FrameworkElement el, string filePath, double dpi)
        {
            el.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Size sz = el.DesiredSize;
            el.Arrange(new Rect(0, 0, sz.Width, sz.Height));
            el.UpdateLayout();

            int pxW = Math.Max(1, (int)Math.Ceiling(sz.Width * dpi / 96.0));
            int pxH = Math.Max(1, (int)Math.Ceiling(sz.Height * dpi / 96.0));

            RenderTargetBitmap rtb = new RenderTargetBitmap(pxW, pxH, dpi, dpi, PixelFormats.Pbgra32);
            rtb.Render(el);

            PngBitmapEncoder enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (FileStream fs = File.Create(filePath))
            {
                enc.Save(fs);
            }
        }
    }
}
