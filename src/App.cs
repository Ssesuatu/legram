using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Legram
{
    internal sealed class GlobalHotkey : Forms.NativeWindow, IDisposable
    {
        private const int HotkeyId = 827;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_NOREPEAT = 0x4000;
        private const uint VK_L = 0x4C;
        private const uint VK_H = 0x48;
        private const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private readonly Action callback;
        public readonly string BoundLabel;

        public GlobalHotkey(Action onTrigger)
        {
            callback = onTrigger;
            CreateHandle(new Forms.CreateParams { Caption = "Legram Hotkey" });
            if (RegisterHotKey(Handle, HotkeyId, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_L))
            {
                BoundLabel = "Ctrl+Alt+L";
            }
            else if (RegisterHotKey(Handle, HotkeyId, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_H))
            {
                BoundLabel = "Ctrl+Alt+H";
            }
            else
            {
                BoundLabel = "Tray";
            }
        }

        protected override void WndProc(ref Forms.Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            {
                try { if (callback != null) callback(); }
                catch (Exception ex) { Log.Error("Hotkey callback", ex); }
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            try { UnregisterHotKey(Handle, HotkeyId); } catch { }
            DestroyHandle();
        }
    }

    internal sealed class DarkTrayColorTable : Forms.ProfessionalColorTable
    {
        public override System.Drawing.Color ToolStripDropDownBackground { get { return System.Drawing.Color.FromArgb(18, 19, 23); } }
        public override System.Drawing.Color ImageMarginGradientBegin { get { return System.Drawing.Color.FromArgb(18, 19, 23); } }
        public override System.Drawing.Color ImageMarginGradientMiddle { get { return System.Drawing.Color.FromArgb(18, 19, 23); } }
        public override System.Drawing.Color ImageMarginGradientEnd { get { return System.Drawing.Color.FromArgb(18, 19, 23); } }
        public override System.Drawing.Color MenuBorder { get { return System.Drawing.Color.FromArgb(56, 58, 66); } }
        public override System.Drawing.Color MenuItemBorder { get { return System.Drawing.Color.FromArgb(46, 48, 56); } }
        public override System.Drawing.Color MenuItemSelected { get { return System.Drawing.Color.FromArgb(36, 38, 46); } }
        public override System.Drawing.Color MenuItemSelectedGradientBegin { get { return System.Drawing.Color.FromArgb(36, 38, 46); } }
        public override System.Drawing.Color MenuItemSelectedGradientEnd { get { return System.Drawing.Color.FromArgb(36, 38, 46); } }
        public override System.Drawing.Color SeparatorDark { get { return System.Drawing.Color.FromArgb(42, 44, 52); } }
        public override System.Drawing.Color SeparatorLight { get { return System.Drawing.Color.FromArgb(18, 19, 23); } }
        public override System.Drawing.Color CheckBackground { get { return System.Drawing.Color.FromArgb(129, 201, 149); } }
        public override System.Drawing.Color CheckSelectedBackground { get { return System.Drawing.Color.FromArgb(129, 201, 149); } }
    }

    public static class App
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "Legram";

        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool IsProcessInJob(IntPtr ProcessHandle, IntPtr JobHandle, out bool Result);

        private static bool DetachFromJobIfNeeded(string[] args)
        {
            try
            {
                if (HasArg(args, "--detached") || HasArg(args, "--quit") || HasArg(args, "--purge")
                    || HasArg(args, "--purge-helper") || HasArg(args, "--selftest") || HasArg(args, "--no-detach")
                    || Environment.GetEnvironmentVariable("LEGRAM_SESSION") != null)
                {
                    return false;
                }

                bool inJob;
                if (!IsProcessInJob(NativeApi.GetCurrentProcess(), IntPtr.Zero, out inJob) || !inJob)
                {
                    return false;
                }
                if (InstanceChannel.ElectionExists())
                {
                    return false;
                }

                string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string cmd = "\"" + exe + "\" --detached" + string.Concat(args.Select(a => " \"" + a.Replace("\"", "") + "\"").ToArray());
                using (ManagementClass cls = new ManagementClass("Win32_Process"))
                {
                    object res = cls.InvokeMethod("Create", new object[] { cmd, Path.GetDirectoryName(exe), null, null });
                    if (Convert.ToInt32(res) == 0)
                    {
                        Log.Info("Detached primary Legram instance outside parent job object");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("DetachFromJobIfNeeded", ex);
            }
            return false;
        }

        private static bool HasArg(string[] args, string flag)
        {
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        [STAThread]
        public static int Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                Log.Error("Unhandled", e.ExceptionObject as Exception);
            };
            Forms.Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
            {
                Log.Error("FormsThread", e.Exception);
            };
            TaskScheduler.UnobservedTaskException += delegate(object s, UnobservedTaskExceptionEventArgs e)
            {
                Log.Error("UnobservedTask", e.Exception);
                e.SetObserved();
            };

            if (HasArg(args, "--purge-helper"))
            {
                bool ok = RamEngine.TryDirectKernelCombineAndStandbyPurge();
                return ok ? 0 : 1;
            }

            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

            if (HasArg(args, "--selftest"))
            {
                string outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest");
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--selftest", StringComparison.OrdinalIgnoreCase))
                    {
                        outDir = Path.GetFullPath(args[i + 1]);
                        break;
                    }
                }
                return SelfTest.Run(outDir);
            }

            try
            {
                if (DetachFromJobIfNeeded(args)) return 0;

                using (InstanceChannel channel = new InstanceChannel())
                {
                    bool wantsQuit = HasArg(args, "--quit");
                    bool wantsPurge = HasArg(args, "--purge");
                    bool bg = HasArg(args, "--background");
                    bool wantsOpen = HasArg(args, "--open") || (!wantsQuit && !wantsPurge && !bg && !channel.Primary);

                    if (!channel.Primary)
                    {
                        AllowSetForegroundWindow(-1);
                        return channel.Send(wantsQuit, wantsPurge, wantsOpen);
                    }
                    if (wantsQuit) return 0;

                    FreeConsole();
                    Application app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    app.DispatcherUnhandledException += delegate(object s, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
                    {
                        Log.Error("Dispatcher", e.Exception);
                        e.Handled = true;
                    };

                    MonolithWindow winRef = null;

                    RegisteredWaitHandle openWait = ThreadPool.RegisterWaitForSingleObject(channel.Open, delegate
                    {
                        channel.Ack.Set();
                        app.Dispatcher.BeginInvoke(new Action(delegate
                        {
                            if (winRef != null) winRef.OpenFullScreen();
                        }));
                    }, null, -1, false);

                    RegisteredWaitHandle purgeWait = ThreadPool.RegisterWaitForSingleObject(channel.Purge, delegate
                    {
                        channel.Ack.Set();
                        app.Dispatcher.BeginInvoke(new Action(delegate
                        {
                            if (winRef != null) winRef.TriggerUnifiedHarmony();
                        }));
                    }, null, -1, false);

                    RegisteredWaitHandle quitWait = ThreadPool.RegisterWaitForSingleObject(channel.Quit, delegate
                    {
                        channel.Ack.Set();
                        app.Dispatcher.BeginInvoke(new Action(delegate { app.Shutdown(); }));
                        Thread killer = new Thread((ThreadStart)delegate
                        {
                            Thread.Sleep(4500);
                            Environment.Exit(0);
                        }) { IsBackground = true };
                        killer.Start();
                    }, null, -1, false);

                    channel.Listening();

                    AppSettings settings = AppSettings.Load();
                    using (EcoQosManager ecoQos = new EcoQosManager())
                    using (RamEngine engine = new RamEngine(settings, ecoQos))
                    using (Forms.NotifyIcon tray = new Forms.NotifyIcon())
                    using (GlobalHotkey hotkey = new GlobalHotkey(delegate
                    {
                        if (winRef != null) winRef.ToggleSurfaceFromTrayOrHotkey();
                    }))
                    {
                        ecoQos.Enabled = settings.EcoQosEnabled;
                        ecoQos.Start();
                        engine.Start();

                        MonolithWindow win = new MonolithWindow(settings, engine, ecoQos);
                        winRef = win;

                        Forms.ContextMenuStrip trayMenu = BuildTrayMenu(app, win, engine, ecoQos, settings, hotkey.BoundLabel);
                        string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        tray.Icon = Icon.ExtractAssociatedIcon(exePath) ?? SystemIcons.Application;
                        tray.Text = "Legram";
                        tray.ContextMenuStrip = trayMenu;
                        tray.MouseUp += delegate(object s, Forms.MouseEventArgs e)
                        {
                            if (e.Button == Forms.MouseButtons.Left)
                            {
                                win.ToggleSurfaceFromTrayOrHotkey();
                            }
                            else if (e.Button == Forms.MouseButtons.Middle)
                            {
                                win.TriggerUnifiedHarmony();
                            }
                        };
                        tray.Visible = true;
                        win.AttachTray(tray);

                        if (settings.ShowWindow && !bg)
                        {
                            win.Show();
                        }
                        else if (settings.ShowWindow && bg && settings.FoldedLens)
                        {
                            win.Show();
                        }
                        if (HasArg(args, "--open"))
                        {
                            win.OpenFullScreen();
                        }
                        if (wantsPurge)
                        {
                            win.TriggerUnifiedHarmony();
                        }

                        Log.Info("Legram started (PID " + Process.GetCurrentProcess().Id + ", Hotkey " + hotkey.BoundLabel + ")");
                        app.Run();

                        openWait.Unregister(null);
                        purgeWait.Unregister(null);
                        quitWait.Unregister(null);
                        tray.Visible = false;
                        win.Close();
                        trayMenu.Dispose();
                        Log.Info("Legram stopped cleanly");
                    }
                }
                return 0;
            }
            catch (Exception ex)
            {
                Log.Error("Fatal Main", ex);
                return 1;
            }
        }

        private static Forms.ContextMenuStrip BuildTrayMenu(
            Application app,
            MonolithWindow win,
            RamEngine engine,
            EcoQosManager ecoQos,
            AppSettings settings,
            string hotkeyLabel)
        {
            Forms.ContextMenuStrip menu = new Forms.ContextMenuStrip
            {
                Renderer = new Forms.ToolStripProfessionalRenderer(new DarkTrayColorTable()),
                ForeColor = System.Drawing.Color.FromArgb(245, 246, 249),
                BackColor = System.Drawing.Color.FromArgb(18, 19, 23),
                ShowImageMargin = true,
                Font = new System.Drawing.Font("Segoe UI", 9.2f, System.Drawing.FontStyle.Regular)
            };

            Forms.ToolStripMenuItem itemOpen = new Forms.ToolStripMenuItem("Open Legram (" + hotkeyLabel + ")");
            itemOpen.Font = new System.Drawing.Font("Segoe UI", 9.2f, System.Drawing.FontStyle.Bold);
            itemOpen.ForeColor = System.Drawing.Color.FromArgb(245, 246, 249);
            itemOpen.Click += delegate { win.OpenFullScreen(); };

            Forms.ToolStripMenuItem itemHarmony = new Forms.ToolStripMenuItem("Harmonize System (RAM \u00B7 VRAM \u00B7 SSD)");
            itemHarmony.ForeColor = System.Drawing.Color.FromArgb(124, 227, 161);
            itemHarmony.Click += delegate { win.TriggerUnifiedHarmony(); };

            Forms.ToolStripMenuItem itemFold = new Forms.ToolStripMenuItem("Mini Capsule Mode (Folded Lens)")
            {
                ForeColor = System.Drawing.Color.FromArgb(245, 246, 249)
            };
            itemFold.Click += delegate { win.SetFoldedLens(!settings.FoldedLens); };

            Forms.ToolStripMenuItem itemPin = new Forms.ToolStripMenuItem("Always on Top (Pin)")
            {
                ForeColor = System.Drawing.Color.FromArgb(245, 246, 249)
            };
            itemPin.Click += delegate
            {
                settings.Pinned = !settings.Pinned;
                settings.Save();
                win.SyncPinTopmost();
            };

            Forms.ToolStripMenuItem itemAutoGuard = new Forms.ToolStripMenuItem("Auto-Harmonize (RAM / Commit / ISLC)")
            {
                ForeColor = System.Drawing.Color.FromArgb(245, 246, 249)
            };
            itemAutoGuard.Click += delegate
            {
                settings.AutoGuardEnabled = !settings.AutoGuardEnabled;
                settings.Save();
            };

            Forms.ToolStripMenuItem itemEco = new Forms.ToolStripMenuItem("EcoQoS + Background I/O Saver")
            {
                ForeColor = System.Drawing.Color.FromArgb(245, 246, 249)
            };
            itemEco.Click += delegate
            {
                settings.EcoQosEnabled = !settings.EcoQosEnabled;
                ecoQos.Enabled = settings.EcoQosEnabled;
                settings.Save();
                engine.RefreshCalmScreenAsync(true);
            };

            Forms.ToolStripMenuItem itemKernelTask = new Forms.ToolStripMenuItem("Enable Kernel Standby Purge (1x UAC)")
            {
                ForeColor = System.Drawing.Color.FromArgb(245, 246, 249)
            };
            itemKernelTask.Click += delegate
            {
                if (!engine.IsKernelPurgeTaskRegistered())
                {
                    if (engine.RegisterKernelPurgeTaskInteractive())
                    {
                        win.ShowCelebration("Kernel 0-UAC Active!", "0-UAC Ready");
                        engine.RefreshCalmScreenAsync(true);
                    }
                }
            };

            string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
            Forms.ToolStripMenuItem itemStartup = new Forms.ToolStripMenuItem("Launch at Windows Sign-In")
            {
                ForeColor = System.Drawing.Color.FromArgb(245, 246, 249)
            };
            itemStartup.Click += delegate
            {
                try
                {
                    bool next = !itemStartup.Checked;
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                    {
                        if (next) k.SetValue(RunValueName, "\"" + exe + "\" --background");
                        else k.DeleteValue(RunValueName, false);
                    }
                    itemStartup.Checked = next;
                }
                catch (Exception ex)
                {
                    Log.Error("Startup Registry", ex);
                }
            };

            Forms.ToolStripMenuItem itemQuit = new Forms.ToolStripMenuItem("Quit Legram")
            {
                ForeColor = System.Drawing.Color.FromArgb(246, 139, 130)
            };
            itemQuit.Click += delegate { app.Shutdown(); };

            menu.Opening += delegate
            {
                itemFold.Checked = settings.FoldedLens;
                itemPin.Checked = settings.Pinned;
                itemAutoGuard.Checked = settings.AutoGuardEnabled;
                itemEco.Checked = settings.EcoQosEnabled;
                bool taskReady = engine.IsKernelPurgeTaskRegistered();
                itemKernelTask.Checked = taskReady;
                itemKernelTask.Text = taskReady
                    ? "Kernel Standby Purge Active (0-UAC)"
                    : "Enable Kernel Standby Purge (1x UAC)";
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                    {
                        itemStartup.Checked = (k != null && k.GetValue(RunValueName) != null);
                    }
                }
                catch { }
            };

            menu.Items.Add(itemOpen);
            menu.Items.Add(itemHarmony);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(itemFold);
            menu.Items.Add(itemPin);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(itemAutoGuard);
            menu.Items.Add(itemEco);
            menu.Items.Add(itemKernelTask);
            menu.Items.Add(itemStartup);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(itemQuit);
            return menu;
        }
    }
}
