using System;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Legram
{
    // ONE SCREEN, ONE BUTTON — Sculpted Double-Shell Hardware Instrument (Gentle Monster × Google)
    public sealed class MonolithView : Border
    {
        private readonly Border outerShell;
        private readonly Border innerChassis;

        // Zone 1: Recessed Display Well + Optical Refraction Aura
        private readonly Border heroWell;
        private readonly Ellipse auraOrb;
        private readonly PrismaticLensMedallion brandLogo;
        private readonly TextBlock brandTitle;

        private readonly Border guardPill;
        private readonly System.Windows.Shapes.Path guardShieldIcon;
        private readonly TextBlock guardStatusText;
        private readonly TextBlock guardTipBody;

        private readonly Border deskPill;
        private readonly Border[] deskNumberBtns = new Border[5];
        private readonly TextBlock[] deskNumberTexts = new TextBlock[5];
        private readonly Border deskAddBtn;
        private readonly TextBlock deskAddText;
        private readonly TextBlock deskTipBody;
        private int currentActiveDeskNum = 1;

        private readonly WindowChromeButton minimizeBtn;
        private readonly WindowChromeButton closeBtn;

        private readonly TextBlock heroOverline;
        private readonly Border statePill;
        private readonly Ellipse stateDot;
        private readonly TextBlock stateWord;

        private readonly TextBlock heroLeadZero;
        private readonly TextBlock heroNumber;
        private readonly TextBlock heroUnit;

        private readonly PrecisionRulerGauge rulerGauge;
        private readonly TextBlock splitLeftText;
        private readonly TextBlock splitRightText;

        // Zone 2: 3 Tactile Hardware Telemetry Tiles
        private readonly HardwareSpecTile ramTile;
        private readonly HardwareSpecTile gpuTile;
        private readonly HardwareSpecTile ssdTile;

        // Zone 3: Recessed Process Usage Ledger Well
        private readonly Border ledgerWell;
        private readonly TextBlock ledgerHeadLeft;
        private readonly TextBlock ledgerHeadRight;
        private readonly EditorialLedgerRow[] ledgerRows = new EditorialLedgerRow[4];

        // Zone 4: THE ONE BUTTON
        private readonly ChromeActuatorButton oneButton;

        public event EventHandler OneButtonInvoked;
        public event EventHandler MinimizeRequested;
        public event Action<bool> CloseRequested;
        public event Action<ProcessGroupItem> ProcessRowClicked;
        public event Action<int> DesktopSwitchRequested;
        public event EventHandler DesktopCreateRequested;
        public event EventHandler DesktopCloseRequested;
        public event Action<MouseButtonEventArgs> SurfaceMouseDown;
        public event Action<MouseEventArgs> SurfaceMouseMove;
        public event Action<MouseButtonEventArgs> SurfaceMouseUp;
        public event EventHandler HeaderDoubleClicked;

        public WindowChromeButton MinimizeButton { get { return minimizeBtn; } }
        public WindowChromeButton CloseButton { get { return closeBtn; } }

        public MonolithView()
        {
            Width = 356;
            CornerRadius = new CornerRadius(32);
            Padding = new Thickness(1);

            outerShell = new Border
            {
                CornerRadius = new CornerRadius(31),
                Padding = new Thickness(5)
            };

            innerChassis = new Border
            {
                CornerRadius = new CornerRadius(26),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12)
            };

            StackPanel root = new StackPanel { Background = Brushes.Transparent };

            // =================================================================
            // ZONE 1: RECESSED UPPER OPTICAL DISPLAY WELL
            // =================================================================
            heroWell = new Border
            {
                CornerRadius = new CornerRadius(20),
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
                Margin = new Thickness(0, 0, 0, 8)
            };

            Grid wellHost = new Grid();

            // Soft Top-Right Optical Refraction Aura (kept inside rounded corner radius)
            auraOrb = new Ellipse
            {
                Width = 176,
                Height = 116,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 3, 5, 0),
                IsHitTestVisible = false
            };
            wellHost.Children.Add(auraOrb);

            StackPanel wellStack = new StackPanel
            {
                Margin = new Thickness(15, 13, 15, 13),
                Background = Brushes.Transparent
            };

            // 1A. Brand Emblem + Autonomous Guard Pill + Interactive Virtual Desktop Bar [1][2][+] + Minimize & Close
            Grid topBar = new Grid { Margin = new Thickness(0, 0, 0, 12), Background = Brushes.Transparent };
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel brandRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            brandLogo = new PrismaticLensMedallion(22);
            brandTitle = Theme.Text("LEGRAM", 10.5, FontWeights.Bold, Theme.Obsidian.InkPrimary);
            brandTitle.Margin = new Thickness(6.5, 0, 6.5, 0);

            StackPanel guardInner = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            guardShieldIcon = Theme.StrokeIcon(Theme.ShieldGuardGeom, 11.0, Theme.Obsidian.Mint, 1.8);
            guardShieldIcon.Margin = new Thickness(0, 0, 3.8, 0);
            guardStatusText = Theme.Text("167 Safe", 8.8, FontWeights.SemiBold, Theme.Obsidian.InkSecondary);
            guardInner.Children.Add(guardShieldIcon);
            guardInner.Children.Add(guardStatusText);

            guardPill = new Border
            {
                CornerRadius = new CornerRadius(9),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6.0, 2.5, 6.0, 2.5),
                Margin = new Thickness(0, 0, 4.5, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = guardInner
            };
            guardTipBody = Theme.Text("", 11, FontWeights.Normal, Theme.Obsidian.InkPrimary);
            guardTipBody.TextWrapping = TextWrapping.Wrap;
            guardTipBody.MaxWidth = 295;
            guardPill.ToolTip = Chrome.Tip(guardTipBody);

            StackPanel deskInner = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            for (int i = 0; i < deskNumberBtns.Length; i++)
            {
                int deskIndex = i + 1;
                TextBlock numTxt = Theme.Text(deskIndex.ToString(CultureInfo.InvariantCulture), 8.6, FontWeights.Bold, Theme.Obsidian.InkSecondary);
                numTxt.HorizontalAlignment = HorizontalAlignment.Center;
                numTxt.VerticalAlignment = VerticalAlignment.Center;
                deskNumberTexts[i] = numTxt;

                Border numBtn = new Border
                {
                    Width = 17,
                    Height = 16,
                    CornerRadius = new CornerRadius(5.5),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 2.0, 0),
                    Background = Brushes.Transparent,
                    BorderBrush = Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    Child = numTxt,
                    Visibility = i == 0 ? Visibility.Visible : Visibility.Collapsed
                };

                int capturedIdx = i;
                numBtn.MouseEnter += delegate
                {
                    if (capturedIdx + 1 != currentActiveDeskNum)
                    {
                        numBtn.Background = Theme.BrushHex(0x22FFFFFFu);
                    }
                };
                numBtn.MouseLeave += delegate
                {
                    if (capturedIdx + 1 != currentActiveDeskNum)
                    {
                        numBtn.Background = Brushes.Transparent;
                    }
                };
                numBtn.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
                {
                    e.Handled = true;
                };
                numBtn.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
                {
                    e.Handled = true;
                    Action<int> sw = DesktopSwitchRequested;
                    if (sw != null) sw(deskIndex);
                };
                numBtn.MouseRightButtonUp += delegate(object s, MouseButtonEventArgs e)
                {
                    e.Handled = true;
                    EventHandler cl = DesktopCloseRequested;
                    if (cl != null) cl(this, EventArgs.Empty);
                };

                deskNumberBtns[i] = numBtn;
                deskInner.Children.Add(numBtn);
            }

            deskAddText = Theme.Text("+", 9.5, FontWeights.Bold, Theme.Obsidian.InkSecondary);
            deskAddText.HorizontalAlignment = HorizontalAlignment.Center;
            deskAddText.VerticalAlignment = VerticalAlignment.Center;
            deskAddText.Margin = new Thickness(0, -1, 0, 0);

            deskAddBtn = new Border
            {
                Width = 17,
                Height = 16,
                CornerRadius = new CornerRadius(5.5),
                BorderThickness = new Thickness(1),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Child = deskAddText
            };
            deskAddBtn.MouseEnter += delegate
            {
                deskAddBtn.Background = Theme.BrushHex(0x22FFFFFFu);
                Theme.Set(deskAddText, Theme.Obsidian.Mint);
            };
            deskAddBtn.MouseLeave += delegate
            {
                deskAddBtn.Background = Brushes.Transparent;
                Theme.Set(deskAddText, Theme.Obsidian.InkSecondary);
            };
            deskAddBtn.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
            };
            deskAddBtn.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                EventHandler cr = DesktopCreateRequested;
                if (cr != null) cr(this, EventArgs.Empty);
            };
            deskInner.Children.Add(deskAddBtn);

            deskPill = new Border
            {
                CornerRadius = new CornerRadius(8.5),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(2.5, 2.0, 2.5, 2.0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = deskInner
            };
            deskTipBody = Theme.Text("", 11, FontWeights.Normal, Theme.Obsidian.InkPrimary);
            deskTipBody.TextWrapping = TextWrapping.Wrap;
            deskTipBody.MaxWidth = 295;
            deskPill.ToolTip = Chrome.Tip(deskTipBody);

            brandRow.Children.Add(brandLogo);
            brandRow.Children.Add(brandTitle);
            brandRow.Children.Add(guardPill);
            brandRow.Children.Add(deskPill);

            StackPanel chromeBtns = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            minimizeBtn = new WindowChromeButton(
                Theme.MinimizeLineGeom,
                false,
                "Minimize to Taskbar (Double-click header: Mini Capsule)")
            {
                Margin = new Thickness(0, 0, 4, 0)
            };
            minimizeBtn.Invoked += delegate
            {
                EventHandler m = MinimizeRequested;
                if (m != null) m(this, EventArgs.Empty);
            };

            closeBtn = new WindowChromeButton(
                Theme.CloseCrossGeom,
                true,
                "Hide to System Tray (Shift+Click: Quit Legram)");
            closeBtn.Invoked += delegate(bool shiftHeld)
            {
                Action<bool> c = CloseRequested;
                if (c != null) c(shiftHeld);
            };

            chromeBtns.Children.Add(minimizeBtn);
            chromeBtns.Children.Add(closeBtn);
            Grid.SetColumn(chromeBtns, 1);

            topBar.Children.Add(brandRow);
            topBar.Children.Add(chromeBtns);
            topBar.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                if (e.ClickCount == 2)
                {
                    e.Handled = true;
                    EventHandler dc = HeaderDoubleClicked;
                    if (dc != null) dc(this, EventArgs.Empty);
                }
            };
            wellStack.Children.Add(topBar);

            // 1B. Overline + Status Badge Pill
            Grid overlineRow = new Grid { Margin = new Thickness(0, 0, 0, 1) };
            overlineRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            overlineRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            heroOverline = Theme.Text("AVAILABLE MEMORY", 8.5, FontWeights.Bold, Theme.Obsidian.InkMuted);

            StackPanel stRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            stateDot = new Ellipse { Width = 5.5, Height = 5.5, Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
            stateWord = Theme.Text("CLEAR", 9.0, FontWeights.Bold, Theme.Obsidian.Mint);
            stRow.Children.Add(stateDot);
            stRow.Children.Add(stateWord);

            statePill = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(7, 2, 7, 2),
                Child = stRow
            };
            Grid.SetColumn(statePill, 1);

            overlineRow.Children.Add(heroOverline);
            overlineRow.Children.Add(statePill);
            wellStack.Children.Add(overlineRow);

            // 1C. Giant Braun/Google Tabular Readout (Leading Muted Zero + Value + GB)
            StackPanel numRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(-1, -3, 0, 6)
            };
            heroLeadZero = Theme.Text("0", 38, FontWeights.Light, Theme.Obsidian.LeadingZeroInk);
            heroNumber = Theme.Text("6.4", 38, FontWeights.SemiBold, Theme.Obsidian.InkPrimary);
            heroUnit = Theme.Text("GB", 13.5, FontWeights.SemiBold, Theme.Obsidian.InkSecondary);
            heroUnit.VerticalAlignment = VerticalAlignment.Bottom;
            heroUnit.Margin = new Thickness(5, 0, 0, 7);

            numRow.Children.Add(heroLeadZero);
            numRow.Children.Add(heroNumber);
            numRow.Children.Add(heroUnit);
            wellStack.Children.Add(numRow);

            // 1D. Sleek Modern Optical Liquid Pill Bar + Unambiguous Split Math
            rulerGauge = new PrecisionRulerGauge { Margin = new Thickness(0, 2, 0, 7) };
            wellStack.Children.Add(rulerGauge);

            Grid splitRow = new Grid();
            splitRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            splitRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            splitLeftText = Theme.Text("Used 9.5 GB (60%)", 10, FontWeights.SemiBold, Theme.Obsidian.InkSecondary);
            splitRightText = Theme.Text("Standby 5.1 GB \u00B7 Total 15.9G", 9.8, FontWeights.Normal, Theme.Obsidian.InkMuted);
            Grid.SetColumn(splitRightText, 1);

            splitRow.Children.Add(splitLeftText);
            splitRow.Children.Add(splitRightText);
            wellStack.Children.Add(splitRow);

            wellHost.Children.Add(wellStack);
            heroWell.Child = wellHost;
            root.Children.Add(heroWell);

            // =================================================================
            // ZONE 2: 3 RECESSED HARDWARE TELEMETRY TILES (RAM · GPU · SSD)
            // =================================================================
            Grid tilesGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            tilesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tilesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            tilesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tilesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            tilesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            ramTile = new HardwareSpecTile(Theme.RamLayersGeom, "SYSTEM RAM", "9.5 / 15.9G", "Ready +1.4G");
            Grid.SetColumn(ramTile, 0);

            gpuTile = new HardwareSpecTile(Theme.GpuDieGeom, "GPU VRAM", "1.4 / 12.0G", "45\u00B0C \u00B7 GPU 4%");
            Grid.SetColumn(gpuTile, 2);

            ssdTile = new HardwareSpecTile(Theme.SsdVaultGeom, "LOCAL SSD", "C: 104G free", "D: 13.7G");
            Grid.SetColumn(ssdTile, 4);

            tilesGrid.Children.Add(ramTile);
            tilesGrid.Children.Add(gpuTile);
            tilesGrid.Children.Add(ssdTile);
            root.Children.Add(tilesGrid);

            // =================================================================
            // ZONE 3: RECESSED PROCESS USAGE LEDGER WELL (Real App Logos)
            // =================================================================
            ledgerWell = new Border
            {
                CornerRadius = new CornerRadius(16),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(11, 9, 11, 7),
                Margin = new Thickness(0, 0, 0, 10)
            };

            StackPanel ledgerStack = new StackPanel();
            Grid ledgerHead = new Grid { Margin = new Thickness(2, 0, 2, 5) };
            ledgerHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ledgerHead.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            ledgerHeadLeft = Theme.Text("ACTIVE MEMORY LEDGER", 8.5, FontWeights.Bold, Theme.Obsidian.InkMuted);
            ledgerHeadRight = Theme.Text("CLICK ROW: SHIELD", 8.5, FontWeights.SemiBold, Theme.Obsidian.InkMuted);
            Grid.SetColumn(ledgerHeadRight, 1);
            ledgerHead.Children.Add(ledgerHeadLeft);
            ledgerHead.Children.Add(ledgerHeadRight);
            ledgerStack.Children.Add(ledgerHead);

            for (int i = 0; i < 4; i++)
            {
                EditorialLedgerRow r = new EditorialLedgerRow();
                r.RowClicked += delegate(ProcessGroupItem clickedItem)
                {
                    Action<ProcessGroupItem> h = ProcessRowClicked;
                    if (h != null) h(clickedItem);
                };
                ledgerRows[i] = r;
                ledgerStack.Children.Add(r);
            }
            ledgerWell.Child = ledgerStack;
            root.Children.Add(ledgerWell);

            // =================================================================
            // ZONE 4: THE ONE BUTTON (Prismatic Medallion + Tactile Capsule)
            // =================================================================
            oneButton = new ChromeActuatorButton();
            oneButton.Invoked += delegate
            {
                EventHandler h = OneButtonInvoked;
                if (h != null) h(this, EventArgs.Empty);
            };
            root.Children.Add(oneButton);

            innerChassis.Child = root;
            outerShell.Child = innerChassis;
            Child = outerShell;

            innerChassis.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                Action<MouseButtonEventArgs> h = SurfaceMouseDown;
                if (h != null) h(e);
            };
            innerChassis.MouseMove += delegate(object s, MouseEventArgs e)
            {
                Action<MouseEventArgs> h = SurfaceMouseMove;
                if (h != null) h(e);
            };
            innerChassis.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                Action<MouseButtonEventArgs> h = SurfaceMouseUp;
                if (h != null) h(e);
            };

            Theme.Crisp(this);
        }

        public void Bind(SystemSnapshot s, bool porcelain, bool isWorking, string celebrationText)
        {
            HausPalette pal = Theme.Palette(porcelain);

            Background = pal.OuterShellRim;
            outerShell.Background = pal.OuterShellFill;
            innerChassis.Background = pal.InnerChassisFill;
            innerChassis.BorderBrush = pal.InnerChassisRim;

            heroWell.Background = pal.WellFill;
            heroWell.BorderBrush = pal.WellBorder;
            ledgerWell.Background = pal.WellFill;
            ledgerWell.BorderBrush = pal.WellBorder;

            Theme.Set(brandTitle, pal.InkPrimary);

            guardPill.Background = pal.PillBadgeFill;
            guardPill.BorderBrush = pal.TileBorder;
            deskPill.Background = pal.PillBadgeFill;
            deskPill.BorderBrush = pal.TileBorder;

            Theme.Set(heroOverline, pal.InkMuted);
            Theme.Set(heroLeadZero, pal.LeadingZeroInk);
            Theme.Set(heroNumber, pal.InkPrimary);
            Theme.Set(heroUnit, pal.InkSecondary);

            Theme.Set(ledgerHeadLeft, pal.InkMuted);
            Theme.Set(ledgerHeadRight, pal.InkMuted);

            if (s == null)
            {
                brandLogo.SetState(MemoryPressure.Healthy, false);
                rulerGauge.SetState(pal, 0.58, 0.20, MemoryPressure.Healthy);
                oneButton.SetSessionTelemetry("", 0, 0L);
                oneButton.Apply(pal, MemoryPressure.Healthy, 0L, isWorking, celebrationText);
                return;
            }

            MemoryPressure visPressure = (!string.IsNullOrEmpty(celebrationText) || isWorking)
                ? MemoryPressure.Boosting
                : s.Pressure;

            // Soft Optical Refraction Aura
            Color auraCol = pal.SignalAura(visPressure);
            RadialGradientBrush auraBrush = new RadialGradientBrush(
                auraCol,
                Color.FromArgb(0, auraCol.R, auraCol.G, auraCol.B))
            {
                Center = new Point(0.58, 0.42),
                GradientOrigin = new Point(0.64, 0.34),
                RadiusX = 0.48,
                RadiusY = 0.48
            };
            auraBrush.Freeze();
            auraOrb.Fill = auraBrush;

            brandLogo.SetState(visPressure, !string.IsNullOrEmpty(celebrationText));

            // 1. Header Guard Pill + Interactive Virtual Desktop Bar ([1] [2] [3] [+])
            if (s.GameShieldActive)
            {
                guardShieldIcon.Stroke = pal.Ice;
                Theme.Set(guardStatusText, "Game Shield");
                Theme.Set(guardStatusText, pal.Ice);
            }
            else if (s.RenderShieldActive)
            {
                guardShieldIcon.Stroke = pal.Amber;
                Theme.Set(guardStatusText, "Render Shield");
                Theme.Set(guardStatusText, pal.Amber);
            }
            else
            {
                guardShieldIcon.Stroke = pal.Mint;
                Theme.Set(guardStatusText, s.ProtectedProcessCount + " Safe");
                Theme.Set(guardStatusText, pal.InkSecondary);
            }
            Theme.Set(guardTipBody, s.GuardTooltipSummary);

            int totalDesks = Math.Max(1, Math.Min(deskNumberBtns.Length, s.TotalDesktopCount));
            int activeDesk = Math.Max(1, Math.Min(totalDesks, s.ActiveDesktopNumber));
            currentActiveDeskNum = activeDesk;

            for (int i = 0; i < deskNumberBtns.Length; i++)
            {
                int dNum = i + 1;
                if (dNum <= totalDesks)
                {
                    deskNumberBtns[i].Visibility = Visibility.Visible;
                    if (dNum == activeDesk)
                    {
                        deskNumberBtns[i].Background = Theme.BrushHex(0x2E7CE3A1u);
                        deskNumberBtns[i].BorderBrush = Theme.BrushHex(0x917CE3A1u);
                        Theme.Set(deskNumberTexts[i], pal.Mint);
                    }
                    else
                    {
                        deskNumberBtns[i].Background = Brushes.Transparent;
                        deskNumberBtns[i].BorderBrush = Brushes.Transparent;
                        Theme.Set(deskNumberTexts[i], s.SleepingDesktopAppCount > 0 ? pal.Ice : pal.InkSecondary);
                    }
                }
                else
                {
                    deskNumberBtns[i].Visibility = Visibility.Collapsed;
                }
            }
            deskAddBtn.Visibility = totalDesks < deskNumberBtns.Length ? Visibility.Visible : Visibility.Collapsed;
            Theme.Set(deskAddText, pal.InkSecondary);

            string baseDeskTip = !string.IsNullOrEmpty(s.VirtualDesktopTooltipSummary)
                ? s.VirtualDesktopTooltipSummary
                : ("Active Workspace: Desktop " + activeDesk + " of " + totalDesks);
            Theme.Set(deskTipBody, baseDeskTip + "\n\u2022 Click [1], [2], [3] to switch desktop \u00B7 Click [+] to create new desktop"
                + (totalDesks > 1 ? "\n\u2022 Right-click number to close current desktop" : ""));

            // 2. Hero Numeral + Leading Zero + Ruler + Unambiguous Split Math
            Brush sigBrush = pal.SignalBrush(s.Pressure);
            statePill.Background = pal.SignalBgBrush(s.Pressure);
            stateDot.Fill = sigBrush;
            Theme.Set(stateWord, Fmt.PressureWord(s.Pressure).ToUpperInvariant());
            Theme.Set(stateWord, sigBrush);

            double availGb = Math.Max(0.0, s.AvailableRamBytes / 1073741824.0);
            heroLeadZero.Visibility = availGb < 9.95 ? Visibility.Visible : Visibility.Collapsed;
            Theme.Set(heroNumber, availGb.ToString("F1", CultureInfo.InvariantCulture));

            double total = Math.Max(1.0, (double)s.TotalRamBytes);
            double usedFrac = s.UsedRamBytes / total;
            double standbyFrac = s.StandbyTotalBytes / total;
            rulerGauge.SetState(pal, usedFrac, standbyFrac, visPressure);

            Theme.Set(splitLeftText, string.Format(CultureInfo.InvariantCulture,
                "Used {0} GB ({1}%)", Fmt.Gb1(s.UsedRamBytes), s.UsedRamPercent));
            Theme.Set(splitLeftText, pal.InkSecondary);

            Theme.Set(splitRightText, string.Format(CultureInfo.InvariantCulture,
                "Standby {0} \u00B7 Total {1}G", Fmt.BytesShort(s.StandbyTotalBytes), Fmt.Gb1(s.TotalRamBytes)));
            Theme.Set(splitRightText, pal.InkMuted);

            // 3A. RAM Tile (with Commit Charge OOM Shield + DWM Leak awareness)
            long ramEst = s.EstimatedPurgeGainBytes;
            string ramSub;
            Brush ramSubCol;
            if (s.IsCommitCritical)
            {
                ramSub = "Commit " + s.CommitUsedPercent + "%!";
                ramSubCol = pal.Coral;
            }
            else if (s.IsCommitWarning)
            {
                ramSub = "Commit " + s.CommitUsedPercent + "%";
                ramSubCol = pal.Amber;
            }
            else if (s.IsDwmBloated)
            {
                ramSub = "DWM " + Fmt.BytesCompactG(s.DwmWorkingSetBytes);
                ramSubCol = pal.Amber;
            }
            else if (ramEst >= 64L * 1048576L)
            {
                ramSub = "Ready +" + Fmt.BytesCompactG(ramEst);
                ramSubCol = ramEst >= 512L * 1048576L ? pal.Mint : pal.InkSecondary;
            }
            else
            {
                ramSub = s.KernelPurgeTaskReady ? "0-UAC Ready" : ("Commit " + s.CommitUsedPercent + "%");
                ramSubCol = pal.InkSecondary;
            }

            ramTile.Apply(
                pal,
                "SYSTEM RAM",
                Fmt.Gb1(s.UsedRamBytes) + " / " + Fmt.Gb1(s.TotalRamBytes) + "G",
                s.IsCommitCritical ? pal.Coral : pal.InkPrimary,
                ramSub,
                ramSubCol,
                s.IsCommitCritical ? pal.Coral : pal.Ice);

            string standbyP0Line = s.EstimatedStandbyZeroBytes > 0
                ? ("\nStandby P0 Ready: " + Fmt.BytesShort(s.EstimatedStandbyZeroBytes))
                : "";
            Theme.Set(ramTile.TipBody, string.Format(CultureInfo.InvariantCulture,
                "Installed DIMM: {0} (Usable OS: {1}, HW Reserved: {2})\n" +
                "Physical Used: {3} ({4}%) \u00B7 Available: {5}\n" +
                "Commit Charge (RAM+Pagefile): {6} / {7} ({8}%)\n" +
                "Kernel Standby: {9} (Low-Prio: {10}) \u00B7 Free+Zero: {11}{15}\n" +
                "Trimmable Working Set: {12} \u00B7 DWM: {13}\n" +
                "Kernel Helper: {14}",
                Fmt.BytesShort(s.InstalledDimmBytes > 0 ? s.InstalledDimmBytes : s.TotalRamBytes),
                Fmt.BytesShort(s.TotalRamBytes),
                Fmt.BytesShort(s.HardwareReservedBytes),
                Fmt.BytesShort(s.UsedRamBytes),
                s.UsedRamPercent,
                Fmt.BytesShort(s.AvailableRamBytes),
                Fmt.BytesShort(s.CommitTotalBytes),
                Fmt.BytesShort(s.CommitLimitBytes),
                s.CommitUsedPercent,
                Fmt.BytesShort(s.StandbyTotalBytes),
                Fmt.BytesShort(s.StandbyLowPrioBytes),
                Fmt.BytesShort(s.FreeZeroBytes),
                Fmt.BytesShort(s.WorkingSetTrimmableBytes),
                s.DwmWorkingSetBytes > 0
                    ? (Fmt.BytesShort(s.DwmWorkingSetBytes)
                        + (s.DwmVramBytes > 0 ? (" RAM / " + Fmt.BytesShort(s.DwmVramBytes) + " VRAM") : "")
                        + (s.IsDwmBloated ? " [Bloated]" : " [Healthy]"))
                    : "Healthy",
                s.KernelPurgeTaskReady ? "Active (0-UAC)" : "User-Space Mode (Right-click to enable 0-UAC Kernel Purge)",
                standbyP0Line));

            // 3B. GPU Tile (Live NVML VRAM + Temperature + Idle AI Engine Detector)
            if (s.Gpu != null && s.Gpu.Available)
            {
                int aiCount = s.Gpu.IdleAiEngineCount > 0 ? s.Gpu.IdleAiEngineCount : s.Gpu.IdleOllamaModelCount;
                long aiVram = s.Gpu.IdleAiVramBytes > 0 ? s.Gpu.IdleAiVramBytes : s.Gpu.IdleOllamaVramBytes;
                string gpuSub;
                if (aiCount > 0)
                {
                    gpuSub = aiVram > 0 ? ("AI Idle +" + Fmt.BytesCompactG(aiVram)) : "AI Ready";
                }
                else
                {
                    gpuSub = s.Gpu.TemperatureC + "\u00B0C \u00B7 GPU " + s.Gpu.GpuUtilPercent + "%";
                }
                bool gpuAlert = s.Gpu.IsHighAlert || aiCount > 0;

                gpuTile.Apply(
                    pal,
                    s.Gpu.ShortName.ToUpperInvariant(),
                    Fmt.Gb1(s.Gpu.VramUsedBytes) + " / " + Fmt.Gb1(s.Gpu.VramTotalBytes) + "G",
                    s.Gpu.IsHighAlert ? pal.Amber : pal.InkPrimary,
                    gpuSub,
                    gpuAlert ? pal.Amber : pal.InkSecondary,
                    gpuAlert ? pal.Amber : pal.Mint);

                string catLine = !string.IsNullOrEmpty(s.Gpu.CategoryBreakdownSummary)
                    ? ("\n" + s.Gpu.CategoryBreakdownSummary)
                    : "";
                string topLine = !string.IsNullOrEmpty(s.Gpu.TopVramSummary)
                    ? ("\n" + s.Gpu.TopVramSummary)
                    : "";
                Theme.Set(gpuTile.TipBody, string.Format(CultureInfo.InvariantCulture,
                    "{0}\nVRAM Used: {1} / {2} ({3:F0}%) \u00B7 WDDM Reserved: {4} \u00B7 Shared GPU RAM: {5}\n" +
                    "Temp: {6}\u00B0C \u00B7 Power: {7:F1} W \u00B7 Core Util: {8}%{9}{10}",
                    s.Gpu.Name,
                    Fmt.BytesShort(s.Gpu.VramUsedBytes),
                    Fmt.BytesShort(s.Gpu.VramTotalBytes),
                    s.Gpu.VramUsedPercent,
                    Fmt.BytesShort(s.Gpu.VramReservedBytes),
                    Fmt.BytesShort(s.Gpu.SharedGpuUsedBytes),
                    s.Gpu.TemperatureC,
                    s.Gpu.PowerWatts,
                    s.Gpu.GpuUtilPercent,
                    catLine,
                    topLine));
            }
            else
            {
                gpuTile.Apply(pal, "WDDM GPU", "Active", pal.InkPrimary, "Protected", pal.InkSecondary, pal.InkSecondary);
                Theme.Set(gpuTile.TipBody, "Windows WDDM GPU is active and automatically protected.");
            }

            // 3C. Physical SSD Tile (Surfaces BOTH System C: AND Tightest Physical Drive D:/E:!)
            DriveSnapshot sysDrive = s.SystemDrive;
            DriveSnapshot tightOther = s.TightestNonSystemDrive;
            int physCount = Math.Max(1, s.PhysicalDriveCount);

            if (sysDrive != null)
            {
                string ssdTitle = physCount == 1 ? "LOCAL SSD" : ("SSD \u00B7 " + physCount + "x");
                string ssdMain = sysDrive.Letter + " " + Fmt.BytesCompactG(sysDrive.FreeBytes) + " free";
                string ssdSub;
                Brush ssdSubBrush = pal.InkSecondary;
                Brush ssdIconBrush = pal.InkSecondary;

                long junkBytes = s.Junk != null ? s.Junk.TotalBytes : 0L;
                if (tightOther != null && (tightOther.IsCritical || tightOther.IsTight))
                {
                    ssdSub = tightOther.Letter + " " + Fmt.BytesCompactG(tightOther.FreeBytes) + " (" + tightOther.FreePercent.ToString("F0", CultureInfo.InvariantCulture) + "%)";
                    ssdSubBrush = tightOther.IsCritical ? pal.Coral : pal.Amber;
                    ssdIconBrush = ssdSubBrush;
                }
                else if (junkBytes >= 32L * 1048576L)
                {
                    ssdSub = "+" + Fmt.BytesCompactG(junkBytes) + " junk";
                    ssdSubBrush = pal.Amber;
                    ssdIconBrush = pal.Amber;
                }
                else if (tightOther != null)
                {
                    ssdSub = tightOther.Letter + " " + Fmt.BytesCompactG(tightOther.FreeBytes) + " free";
                }
                else
                {
                    ssdSub = sysDrive.FreePercent.ToString("F0", CultureInfo.InvariantCulture) + "% free space";
                }

                ssdTile.Apply(
                    pal,
                    ssdTitle,
                    ssdMain,
                    sysDrive.IsCritical ? pal.Coral : pal.InkPrimary,
                    ssdSub,
                    ssdSubBrush,
                    ssdIconBrush);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Local Physical Storage (IOCTL Bus Verified):");
                for (int i = 0; i < s.Drives.Count; i++)
                {
                    DriveSnapshot ds = s.Drives[i];
                    if (ds.IsVirtualOrCloud) continue;
                    string model = string.IsNullOrEmpty(ds.HardwareModel) ? ds.FileSystem : ds.HardwareModel;
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "\u2022 {0} [{1}] \u2014 {2} free of {3} ({4:F1}% free)\n",
                        ds.Letter, model, Fmt.BytesShort(ds.FreeBytes), Fmt.BytesShort(ds.TotalBytes), ds.FreePercent);
                }
                if (junkBytes > 0)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "Safe Reclaimable Junk: {0} (Temp + Recycle Bin + CrashDumps/WER + Cache)\n",
                        Fmt.BytesShort(junkBytes));
                }
                bool headerVirtual = false;
                for (int i = 0; i < s.Drives.Count; i++)
                {
                    DriveSnapshot ds = s.Drives[i];
                    if (!ds.IsVirtualOrCloud) continue;
                    if (!headerVirtual)
                    {
                        sb.AppendLine("Cloud & Virtual Drives (100% Untouched):");
                        headerVirtual = true;
                    }
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "\u2022 {0} ({1}) \u2014 Skipped automatically\n",
                        ds.Letter, string.IsNullOrEmpty(ds.Label) ? ds.FileSystem : ds.Label);
                }
                Theme.Set(ssdTile.TipBody, sb.ToString().TrimEnd());
            }

            // 4. Top 4 Process Usage Rows (with Real App Logos!)
            for (int i = 0; i < 4; i++)
            {
                ProcessGroupItem item = (s.TopGroups != null && i < s.TopGroups.Count) ? s.TopGroups[i] : null;
                ledgerRows[i].Bind(item, pal);
            }

            // 5. The One Button
            oneButton.SetSessionTelemetry(s.LastHarmonizeSummary, s.SessionHarmonizeCount, s.SessionTotalFreedBytes);
            oneButton.Apply(pal, visPressure, s.EstimatedHarmonyGainBytes, isWorking, celebrationText);
        }

        public void SetPreviewClickState(double sweepFrac, bool celebrated)
        {
            oneButton.SetPreviewClickState(sweepFrac, celebrated);
        }
    }

    // Folded Mini Capsule Mode: Recessed Hardware Recorder Pill + Prismatic Ring Trigger
    public sealed class OpticalLensView : Border
    {
        private readonly Border outerCapsule;
        private readonly Border leftButtonWell;
        private readonly PrismaticLensMedallion lensMedallion;
        private readonly Border rightTelemetryWell;
        private readonly MiniWaveformCanvas miniWave;
        private readonly TextBlock leadZeroText;
        private readonly TextBlock mainText;
        private readonly TextBlock stateWord;

        public event EventHandler OneButtonInvoked;
        public event EventHandler UnfoldRequested;
        public event Action<MouseButtonEventArgs> DragMouseDown;
        public event Action<MouseEventArgs> DragMouseMove;
        public event Action<MouseButtonEventArgs> DragMouseUp;

        public OpticalLensView()
        {
            Height = 46;
            CornerRadius = new CornerRadius(23);
            Padding = new Thickness(1);

            outerCapsule = new Border
            {
                CornerRadius = new CornerRadius(22),
                Padding = new Thickness(4)
            };

            StackPanel row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            lensMedallion = new PrismaticLensMedallion(34);
            leftButtonWell = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(18),
                Margin = new Thickness(0, 0, 5, 0),
                Cursor = Cursors.Hand,
                Child = lensMedallion,
                ToolTip = Chrome.Tip(Theme.Text("Click: Harmonize System (1-Button)", 11, FontWeights.Medium, Theme.Obsidian.InkPrimary))
            };
            leftButtonWell.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { e.Handled = true; };
            leftButtonWell.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                EventHandler h = OneButtonInvoked;
                if (h != null) h(this, EventArgs.Empty);
            };

            StackPanel teleRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            miniWave = new MiniWaveformCanvas { Margin = new Thickness(0, 0, 9, 0) };
            leadZeroText = Theme.Text("0", 12.5, FontWeights.Normal, Theme.Obsidian.LeadingZeroInk);
            mainText = Theme.Text("6.4 GB", 12.5, FontWeights.Bold, Theme.Obsidian.InkPrimary);
            stateWord = Theme.Text("Clear", 10.5, FontWeights.SemiBold, Theme.Obsidian.Mint);
            stateWord.Margin = new Thickness(7, 0, 0, 0);

            teleRow.Children.Add(miniWave);
            teleRow.Children.Add(leadZeroText);
            teleRow.Children.Add(mainText);
            teleRow.Children.Add(stateWord);

            rightTelemetryWell = new Border
            {
                Height = 36,
                CornerRadius = new CornerRadius(18),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(11, 0, 13, 0),
                Cursor = Cursors.Hand,
                Child = teleRow,
                ToolTip = Chrome.Tip(Theme.Text("Click: Expand Full View \u00B7 Drag: Move", 11, FontWeights.Medium, Theme.Obsidian.InkPrimary))
            };

            rightTelemetryWell.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                if (e.ClickCount == 2)
                {
                    e.Handled = true;
                    EventHandler u = UnfoldRequested;
                    if (u != null) u(this, EventArgs.Empty);
                    return;
                }
                Action<MouseButtonEventArgs> d = DragMouseDown;
                if (d != null) d(e);
            };
            rightTelemetryWell.MouseMove += delegate(object s, MouseEventArgs e)
            {
                Action<MouseEventArgs> m = DragMouseMove;
                if (m != null) m(e);
            };
            rightTelemetryWell.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                Action<MouseButtonEventArgs> u = DragMouseUp;
                if (u != null) u(e);
            };

            row.Children.Add(leftButtonWell);
            row.Children.Add(rightTelemetryWell);
            outerCapsule.Child = row;
            Child = outerCapsule;

            Theme.Crisp(this);
        }

        public void Bind(SystemSnapshot s, bool porcelain, bool isWorking, string toastText)
        {
            HausPalette pal = Theme.Palette(porcelain);
            Background = pal.OuterShellRim;
            outerCapsule.Background = pal.OuterShellFill;
            leftButtonWell.Background = pal.InnerChassisFill;
            rightTelemetryWell.Background = pal.WellFill;
            rightTelemetryWell.BorderBrush = pal.WellBorder;

            MemoryPressure p = s != null ? s.Pressure : MemoryPressure.Healthy;
            double usedFrac = (s != null && s.TotalRamBytes > 0) ? ((double)s.UsedRamBytes / s.TotalRamBytes) : 0.58;
            bool cel = !string.IsNullOrEmpty(toastText);

            lensMedallion.SetState(isWorking || cel ? MemoryPressure.Boosting : p, cel);
            miniWave.SetState(pal, usedFrac, isWorking || cel ? MemoryPressure.Boosting : p);

            if (isWorking)
            {
                leadZeroText.Visibility = Visibility.Collapsed;
                Theme.Set(mainText, "Harmonizing\u2026");
                Theme.Set(mainText, pal.InkPrimary);
                stateWord.Visibility = Visibility.Collapsed;
                return;
            }
            if (cel)
            {
                leadZeroText.Visibility = Visibility.Collapsed;
                Theme.Set(mainText, toastText);
                Theme.Set(mainText, pal.Mint);
                stateWord.Visibility = Visibility.Collapsed;
                return;
            }

            stateWord.Visibility = Visibility.Visible;
            double availGb = (s != null ? s.AvailableRamBytes : 6800L * 1048576L) / 1073741824.0;
            leadZeroText.Visibility = availGb < 9.95 ? Visibility.Visible : Visibility.Collapsed;
            Theme.Set(leadZeroText, pal.LeadingZeroInk);
            Theme.Set(mainText, availGb.ToString("F1", CultureInfo.InvariantCulture) + " GB");
            Theme.Set(mainText, pal.InkPrimary);
            Theme.Set(stateWord, Fmt.PressureWord(p));
            Theme.Set(stateWord, pal.SignalBrush(p));
        }
    }

    internal sealed class MonolithWindow : Window
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "Legram";

        private readonly AppSettings settings;
        private readonly RamEngine engine;
        private readonly EcoQosManager ecoQos;
        private readonly Grid rootHost;
        private readonly MonolithView monolithView;
        private readonly OpticalLensView lensView;
        private ContextMenu contextMenu;

        private Forms.NotifyIcon tray;
        private System.Drawing.Icon currentTrayIcon;
        private string lastTrayKey = "";

        private IntPtr hwnd = IntPtr.Zero;
        private int taskbarCreatedMsg;
        private DispatcherTimer telemetryTimer;
        private DispatcherTimer celebrationTimer;
        private DispatcherTimer selfTrimTimer;

        private bool isHarmonizing;
        private string celebrationSummary = "";
        private string celebrationShort = "";

        private bool dragArmed;
        private bool lensClickArmed;
        private NativeApi.POINT dragStartCursorPx;

        public MonolithView MainSurface { get { return monolithView; } }

        public MonolithWindow(AppSettings appSettings, RamEngine ramEngine, EcoQosManager ecoManager)
        {
            settings = appSettings;
            engine = ramEngine;
            ecoQos = ecoManager;

            Title = "Legram";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanMinimize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = !settings.FoldedLens;
            Topmost = settings.Pinned;
            SizeToContent = SizeToContent.WidthAndHeight;
            TryApplyExecutableIcon();

            rootHost = new Grid();
            monolithView = new MonolithView();
            lensView = new OpticalLensView();

            monolithView.OneButtonInvoked += delegate { TriggerUnifiedHarmony(); };
            monolithView.HeaderDoubleClicked += delegate { SetFoldedLens(true); };
            monolithView.MinimizeRequested += delegate { MinimizeWindow(); };
            monolithView.CloseRequested += delegate(bool forceExit) { CloseFromHeaderButton(forceExit); };
            monolithView.ProcessRowClicked += ShowProcessRowMenu;
            monolithView.DesktopSwitchRequested += delegate(int targetDesk) { engine.SwitchToDesktop(targetDesk); };
            monolithView.DesktopCreateRequested += delegate { engine.CreateNewDesktop(); };
            monolithView.DesktopCloseRequested += delegate { engine.CloseCurrentDesktop(); };
            monolithView.SurfaceMouseDown += delegate(MouseButtonEventArgs e) { OnSurfaceMouseDown(e, false); };
            monolithView.SurfaceMouseMove += OnSurfaceMouseMove;
            monolithView.SurfaceMouseUp += OnSurfaceMouseUpMonolith;

            lensView.OneButtonInvoked += delegate { TriggerUnifiedHarmony(); };
            lensView.UnfoldRequested += delegate { SetFoldedLens(false); };
            lensView.DragMouseDown += delegate(MouseButtonEventArgs e) { OnSurfaceMouseDown(e, true); };
            lensView.DragMouseMove += OnSurfaceMouseMove;
            lensView.DragMouseUp += OnSurfaceMouseUpLens;

            rootHost.Children.Add(monolithView);
            rootHost.Children.Add(lensView);
            Content = rootHost;

            MouseRightButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                dragArmed = false;
                lensClickArmed = false;
                e.Handled = true;
                ShowMonolithMenu();
            };

            Deactivated += delegate
            {
                dragArmed = false;
                lensClickArmed = false;
            };

            LostMouseCapture += delegate
            {
                dragArmed = false;
                lensClickArmed = false;
            };

            StateChanged += delegate
            {
                dragArmed = false;
                lensClickArmed = false;
                if (WindowState == WindowState.Normal)
                {
                    ClampInsideWorkArea(false);
                    engine.RefreshCalmScreenAsync(false);
                }
            };

            PreviewKeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Escape && !settings.Pinned)
                {
                    e.Handled = true;
                    MinimizeWindow();
                }
                else if (e.Key == Key.Space || e.Key == Key.Return)
                {
                    e.Handled = true;
                    TriggerUnifiedHarmony();
                }
                else if (e.Key == Key.M && Keyboard.Modifiers == ModifierKeys.None)
                {
                    e.Handled = true;
                    SetFoldedLens(!settings.FoldedLens);
                }
                else if (e.Key == Key.F5)
                {
                    e.Handled = true;
                    engine.RefreshSnapshotAsync(true);
                }
                else if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control
                    && e.Key >= Key.D1 && e.Key <= Key.D5)
                {
                    int idx = (int)(e.Key - Key.D1) + 1;
                    e.Handled = true;
                    engine.SwitchToDesktop(idx);
                }
            };

            engine.SnapshotUpdated += OnSnapshotUpdatedBackground;
            engine.AutoPurgeCompleted += delegate(object s, PurgeCompletedEventArgs e)
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (e != null && e.Result != null)
                    {
                        ShowCelebration("Auto \u00B7 " + e.Result.PillToastText, e.Result.PillToastText);
                    }
                }));
            };

            telemetryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
            telemetryTimer.Tick += delegate
            {
                bool fullOpen = IsVisible && WindowState != WindowState.Minimized && !settings.FoldedLens;
                telemetryTimer.Interval = fullOpen ? TimeSpan.FromSeconds(3.5) : TimeSpan.FromSeconds(4.5);
                if (fullOpen) engine.RefreshCalmScreenAsync(false);
                else engine.RefreshLightweightAsync();
            };
            telemetryTimer.Start();

            SyncModeVisibility();
        }

        private void TryApplyExecutableIcon()
        {
            try
            {
                string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrEmpty(exe)) return;
                using (System.Drawing.Icon ico = System.Drawing.Icon.ExtractAssociatedIcon(exe))
                {
                    if (ico != null)
                    {
                        System.Windows.Media.Imaging.BitmapSource bs = Imaging.CreateBitmapSourceFromHIcon(
                            ico.Handle,
                            Int32Rect.Empty,
                            System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                        bs.Freeze();
                        Icon = bs;
                    }
                }
            }
            catch { }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                taskbarCreatedMsg = (int)NativeApi.RegisterWindowMessageW("TaskbarCreated");
                engine.AttachWindowHwnd(hwnd);
                SyncWindowChromeStyle();
                HwndSource src = HwndSource.FromHwnd(hwnd);
                if (src != null) src.AddHook(WndProc);
            }

            PositionInitial();
            RefreshUi();
            ScheduleSelfTrim(2);
        }

        private void SyncWindowChromeStyle()
        {
            bool showTaskbar = !settings.FoldedLens;
            if (ShowInTaskbar != showTaskbar)
            {
                ShowInTaskbar = showTaskbar;
            }
            hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            long ex = NativeApi.GetWindowLongPtrW(hwnd, NativeApi.GWL_EXSTYLE).ToInt64();
            if (settings.FoldedLens)
            {
                ex |= (long)NativeApi.WS_EX_TOOLWINDOW;
            }
            else
            {
                ex &= ~(long)NativeApi.WS_EX_TOOLWINDOW;
            }
            NativeApi.SetWindowLongPtrW(hwnd, NativeApi.GWL_EXSTYLE, new IntPtr(ex));

            long style = NativeApi.GetWindowLongPtrW(hwnd, NativeApi.GWL_STYLE).ToInt64();
            style |= (long)NativeApi.WS_MINIMIZEBOX;
            NativeApi.SetWindowLongPtrW(hwnd, NativeApi.GWL_STYLE, new IntPtr(style));
        }

        public void SyncPinTopmost()
        {
            if (Topmost != settings.Pinned)
            {
                Topmost = settings.Pinned;
            }
        }

        public void MinimizeWindow()
        {
            dragArmed = false;
            lensClickArmed = false;
            if (settings.FoldedLens)
            {
                SetFoldedLens(false);
            }
            SyncWindowChromeStyle();
            WindowState = WindowState.Minimized;
            ScheduleSelfTrim(2);
        }

        public void CloseFromHeaderButton(bool forceExit)
        {
            dragArmed = false;
            lensClickArmed = false;
            if (forceExit)
            {
                Application.Current.Shutdown();
                return;
            }
            settings.ShowWindow = false;
            settings.Save();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Hide();
            ScheduleSelfTrim(2);
        }

        private IntPtr WndProc(IntPtr h, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (taskbarCreatedMsg != 0 && msg == taskbarCreatedMsg)
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (tray != null)
                    {
                        lastTrayKey = "";
                        tray.Visible = false;
                        tray.Visible = true;
                        UpdateTrayIconAndTooltip(engine.Current);
                    }
                }));
            }
            else if (msg == NativeApi.WM_POWERBROADCAST)
            {
                int evt = wParam.ToInt32();
                if (evt == NativeApi.PBT_APMRESUMEAUTOMATIC || evt == NativeApi.PBT_APMRESUMESUSPEND)
                {
                    engine.NotifySystemResume();
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        ClampInsideWorkArea(false);
                        RefreshUi();
                    }));
                }
            }
            else if (msg == NativeApi.WM_DISPLAYCHANGE || msg == NativeApi.WM_DPICHANGED)
            {
                Dispatcher.BeginInvoke(new Action(delegate { ClampInsideWorkArea(false); }));
            }
            return IntPtr.Zero;
        }

        public void AttachTray(Forms.NotifyIcon notifyIcon)
        {
            tray = notifyIcon;
            UpdateTrayIconAndTooltip(engine.Current);
        }

        private void SyncModeVisibility()
        {
            if (settings.FoldedLens)
            {
                monolithView.Visibility = Visibility.Collapsed;
                lensView.Visibility = Visibility.Visible;
                return;
            }
            lensView.Visibility = Visibility.Collapsed;
            monolithView.Visibility = Visibility.Visible;
        }

        public void SetFoldedLens(bool folded)
        {
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            double rightEdge = Left + (ActualWidth > 20 ? ActualWidth : (settings.FoldedLens ? 268 : 356));
            settings.FoldedLens = folded;
            settings.Save();
            SyncModeVisibility();
            SyncWindowChromeStyle();
            RefreshUi();
            UpdateLayout();
            double newW = ActualWidth > 20 ? ActualWidth : (folded ? 268 : 356);
            Left = Math.Round(rightEdge - newW);
            ClampInsideWorkArea(false);
            if (!folded) engine.RefreshCalmScreenAsync(true);
            ScheduleSelfTrim(3);
        }

        public void ToggleSurfaceFromTrayOrHotkey()
        {
            if (!IsVisible || WindowState == WindowState.Minimized)
            {
                OpenFullScreen();
                return;
            }
            if (settings.FoldedLens)
            {
                SetFoldedLens(false);
                Activate();
                return;
            }
            CloseFromHeaderButton(false);
        }

        public void OpenFullScreen()
        {
            settings.ShowWindow = true;
            if (settings.FoldedLens)
            {
                settings.FoldedLens = false;
            }
            settings.Save();
            SyncModeVisibility();
            SyncWindowChromeStyle();
            if (!IsVisible) Show();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            SyncPinTopmost();
            Activate();
            ClampInsideWorkArea(false);
            engine.RefreshCalmScreenAsync(true);
            ScheduleSelfTrim(3);
        }

        public void TriggerUnifiedHarmony()
        {
            RunHarmonyAsync("TriggerUnifiedHarmony");
        }

        public void TriggerSilentHarmonyFromHotkey()
        {
            RunHarmonyAsync("TriggerSilentHarmonyFromHotkey");
        }

        private async void RunHarmonyAsync(string logContext)
        {
            if (isHarmonizing) return;
            isHarmonizing = true;
            RefreshUi();
            try
            {
                PurgeResult res = await engine.RunUnifiedHarmonyAsync();
                isHarmonizing = false;
                if (res != null)
                {
                    ShowCelebration(res.SummaryText, res.PillToastText);
                }
                else
                {
                    RefreshUi();
                }
                ScheduleSelfTrim(3);
            }
            catch (Exception ex)
            {
                isHarmonizing = false;
                Log.Error(logContext, ex);
                RefreshUi();
            }
        }

        public void ShowCelebration(string fullSummary, string shortSummary)
        {
            celebrationSummary = fullSummary ?? "Harmonized";
            celebrationShort = shortSummary ?? "Harmonized";
            RefreshUi();

            if (celebrationTimer == null)
            {
                celebrationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2600) };
                celebrationTimer.Tick += delegate
                {
                    celebrationTimer.Stop();
                    celebrationSummary = "";
                    celebrationShort = "";
                    RefreshUi();
                };
            }
            celebrationTimer.Stop();
            celebrationTimer.Start();
        }

        // Hover-immune window drag: NEVER captures mouse and NEVER mutates Left/Top on hover.
        // Immediately disarms if e.LeftButton != Pressed, and delegates real drags to native Win32 DragMove().
        private void OnSurfaceMouseDown(MouseButtonEventArgs e, bool fromLens)
        {
            if (e.ChangedButton != MouseButton.Left || e.LeftButton != MouseButtonState.Pressed)
            {
                dragArmed = false;
                lensClickArmed = false;
                return;
            }
            dragArmed = true;
            lensClickArmed = fromLens;
            NativeApi.GetCursorPos(out dragStartCursorPx);
            e.Handled = true;
        }

        private void OnSurfaceMouseMove(MouseEventArgs e)
        {
            if (e == null || e.LeftButton != MouseButtonState.Pressed)
            {
                dragArmed = false;
                lensClickArmed = false;
                return;
            }
            if (!dragArmed) return;

            NativeApi.POINT curPx;
            NativeApi.GetCursorPos(out curPx);
            if (Math.Abs(curPx.X - dragStartCursorPx.X) <= 5 && Math.Abs(curPx.Y - dragStartCursorPx.Y) <= 5)
            {
                return;
            }

            dragArmed = false;
            lensClickArmed = false;
            try
            {
                DragMove();
            }
            catch
            {
                return;
            }

            if (WindowState == WindowState.Normal)
            {
                ClampInsideWorkArea(true);
                settings.WindowX = Left;
                settings.WindowY = Top;
                settings.Save();
            }
        }

        private void OnSurfaceMouseUpMonolith(MouseButtonEventArgs e)
        {
            dragArmed = false;
            lensClickArmed = false;
        }

        private void OnSurfaceMouseUpLens(MouseButtonEventArgs e)
        {
            bool shouldUnfold = lensClickArmed;
            dragArmed = false;
            lensClickArmed = false;
            if (shouldUnfold)
            {
                e.Handled = true;
                SetFoldedLens(false);
            }
        }

        internal bool VerifyHoverImmunityForSelfTest()
        {
            // Even if dragArmed were somehow stale, any hover with left button released must disarm and never move Left/Top.
            Left = 240.0;
            Top = 180.0;
            dragArmed = true;
            lensClickArmed = true;
            MouseEventArgs hoverArgs = Mouse.PrimaryDevice != null
                ? new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseMoveEvent }
                : null;
            OnSurfaceMouseMove(hoverArgs);
            return !dragArmed && !lensClickArmed && Math.Abs(Left - 240.0) < 0.01 && Math.Abs(Top - 180.0) < 0.01;
        }

        private void PositionInitial()
        {
            uint dpi;
            Int32Rect workPx = NativeApi.WorkAreaAt(100, 100, out dpi);
            double scale = (dpi == 0 ? 96.0 : dpi) / 96.0;
            double workLeft = workPx.X / scale;
            double workTop = workPx.Y / scale;
            double workW = workPx.Width / scale;
            double workH = workPx.Height / scale;

            if (!double.IsNaN(settings.WindowX) && !double.IsNaN(settings.WindowY))
            {
                Left = settings.WindowX;
                Top = settings.WindowY;
                ClampInsideWorkArea(false);
            }
            else
            {
                UpdateLayout();
                double estW = ActualWidth > 40 ? ActualWidth : (settings.FoldedLens ? 268 : 356);
                double estH = ActualHeight > 30 ? ActualHeight : (settings.FoldedLens ? 52 : 476);
                Left = Math.Round(workLeft + workW - estW - 20);
                Top = Math.Round(workTop + workH - estH - 20);
            }
        }

        private void ClampInsideWorkArea(bool magneticSnap)
        {
            if (WindowState == WindowState.Minimized) return;

            uint dpi;
            Int32Rect workPx = hwnd != IntPtr.Zero
                ? NativeApi.WorkAreaForWindow(hwnd, out dpi)
                : NativeApi.WorkAreaAt(100, 100, out dpi);
            double scale = (dpi == 0 ? 96.0 : dpi) / 96.0;

            double minX = workPx.X / scale + 14;
            double minY = workPx.Y / scale + 14;
            double w = ActualWidth > 20 ? ActualWidth : (settings.FoldedLens ? 268 : 356);
            double h = ActualHeight > 20 ? ActualHeight : (settings.FoldedLens ? 52 : 476);
            double maxX = (workPx.X + workPx.Width) / scale - w - 14;
            double maxY = (workPx.Y + workPx.Height) / scale - h - 14;

            double nx = Math.Max(minX, Math.Min(maxX, Left));
            double ny = Math.Max(minY, Math.Min(maxY, Top));

            if (magneticSnap)
            {
                const double snapDist = 26.0;
                if (Math.Abs(nx - minX) <= snapDist) nx = minX;
                else if (Math.Abs(maxX - nx) <= snapDist) nx = maxX;
                if (Math.Abs(ny - minY) <= snapDist) ny = minY;
                else if (Math.Abs(maxY - ny) <= snapDist) ny = maxY;
            }

            Left = Math.Round(nx);
            Top = Math.Round(ny);
        }

        private static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    return k != null && k.GetValue(RunValueName) != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void ToggleStartup()
        {
            try
            {
                string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                bool next = !IsStartupEnabled();
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (next) k.SetValue(RunValueName, "\"" + exe + "\" --background");
                    else k.DeleteValue(RunValueName, false);
                }
            }
            catch (Exception ex)
            {
                Log.Error("ToggleStartup", ex);
            }
        }

        private void ShowProcessRowMenu(ProcessGroupItem item)
        {
            if (item == null) return;
            if (contextMenu == null) contextMenu = Chrome.Menu();
            contextMenu.Items.Clear();

            HausPalette o = Theme.Obsidian;
            if (item.IsProtected || string.Equals(item.GroupKey, "__protected__", StringComparison.OrdinalIgnoreCase))
            {
                contextMenu.Items.Add(Chrome.IconItem(
                    Theme.ShieldGuardGeom, o.Mint,
                    "System & Active Guard Shield", "Protected", o.Mint,
                    delegate
                    {
                        ShowCelebration("Core OS & Active Apps are always immune", "Always Safe");
                    }));
            }
            else
            {
                bool currentlyImmune = settings.IsGroupImmune(item.GroupKey);
                contextMenu.Items.Add(Chrome.IconItem(
                    Theme.ShieldGuardGeom,
                    currentlyImmune ? o.Amber : o.Mint,
                    currentlyImmune
                        ? ("Remove Immune Shield (" + item.DisplayName + ")")
                        : ("Keep " + item.DisplayName + " Immune (1-Click Shield)"),
                    currentlyImmune ? "Unshield" : "Immune",
                    currentlyImmune ? o.Amber : o.Mint,
                    delegate
                    {
                        bool nowImmune = settings.ToggleGroupImmune(item.GroupKey);
                        engine.RefreshCalmScreenAsync(true);
                        ShowCelebration(
                            nowImmune
                                ? ("\uD83D\uDEE1 " + item.DisplayName + " is now Immune")
                                : (item.DisplayName + " returned to Auto-Guard"),
                            nowImmune ? "Immune On" : "Auto-Guard");
                    }));

                if (!currentlyImmune)
                {
                    contextMenu.Items.Add(Chrome.IconItem(
                        Theme.HausBarsGeom,
                        item.IsOrphanGroup ? o.Amber : o.Ice,
                        item.IsOrphanGroup
                            ? ("Reap " + item.DisplayName + " Processes")
                            : ("Trim " + item.DisplayName + " Memory Now"),
                        Fmt.Gb2(item.WorkingSetBytes),
                        item.IsOrphanGroup ? o.Amber : o.Ice,
                        async delegate
                        {
                            PurgeResult r = await engine.TrimOrKillProcessGroupAsync(item);
                            if (r != null && r.Success)
                            {
                                ShowCelebration(r.SummaryText, r.PillToastText);
                            }
                        }));
                }
            }

            contextMenu.Items.Add(Chrome.Line());
            contextMenu.Items.Add(Chrome.IconItem(
                Theme.BentoGridGeom, o.InkSecondary,
                "All Legram Controls\u2026", null, null,
                delegate { ShowMonolithMenu(); }));

            contextMenu.PlacementTarget = rootHost;
            contextMenu.Placement = PlacementMode.MousePoint;
            contextMenu.IsOpen = true;
        }

        private void ShowMonolithMenu()
        {
            if (contextMenu == null) contextMenu = Chrome.Menu();
            contextMenu.Items.Clear();

            HausPalette o = Theme.Obsidian;
            bool taskReady = engine.IsKernelPurgeTaskRegistered();
            bool startupOn = IsStartupEnabled();

            contextMenu.Items.Add(Chrome.IconItem(
                Theme.HausBarsGeom, o.Mint,
                "Harmonize System Now", "1-Click", o.Mint,
                delegate { TriggerUnifiedHarmony(); }));

            contextMenu.Items.Add(Chrome.Line());

            contextMenu.Items.Add(Chrome.IconItem(
                Theme.CapsuleMiniGeom, settings.FoldedLens ? o.Mint : o.InkSecondary,
                settings.FoldedLens ? "Expand Full View" : "Mini Capsule Mode", settings.FoldedLens ? "On" : "Fold", settings.FoldedLens ? o.Mint : o.InkMuted,
                delegate { SetFoldedLens(!settings.FoldedLens); }));

            contextMenu.Items.Add(Chrome.IconItem(
                Theme.PinGeom, settings.Pinned ? o.Mint : o.InkSecondary,
                "Always on Top", settings.Pinned ? "On" : "Off", settings.Pinned ? o.Mint : o.InkMuted,
                delegate
                {
                    settings.Pinned = !settings.Pinned;
                    settings.Save();
                    SyncPinTopmost();
                }));

            contextMenu.Items.Add(Chrome.Line());

            contextMenu.Items.Add(Chrome.IconItem(
                Theme.ShieldGuardGeom, settings.AutoGuardEnabled ? o.Mint : o.InkSecondary,
                "Auto-Harmonize (RAM/Commit/ISLC)", settings.AutoGuardEnabled ? "On" : "Off", settings.AutoGuardEnabled ? o.Mint : o.InkMuted,
                delegate
                {
                    settings.AutoGuardEnabled = !settings.AutoGuardEnabled;
                    settings.Save();
                }));

            contextMenu.Items.Add(Chrome.IconItem(
                Theme.GameControllerGeom, settings.GameModeAutoPause ? o.Ice : o.InkSecondary,
                "Game Mode Auto-Pause (Fullscreen)", settings.GameModeAutoPause ? "On" : "Off", settings.GameModeAutoPause ? o.Ice : o.InkMuted,
                delegate
                {
                    settings.GameModeAutoPause = !settings.GameModeAutoPause;
                    settings.Save();
                    engine.RefreshCalmScreenAsync(true);
                }));

            contextMenu.Items.Add(Chrome.IconItem(
                Theme.EcoLeafGeom, settings.EcoQosEnabled ? o.Ice : o.InkSecondary,
                "EcoQoS + Background I/O Saver", settings.EcoQosEnabled ? "On" : "Off", settings.EcoQosEnabled ? o.Ice : o.InkMuted,
                delegate
                {
                    settings.EcoQosEnabled = !settings.EcoQosEnabled;
                    ecoQos.Enabled = settings.EcoQosEnabled;
                    settings.Save();
                    engine.RefreshCalmScreenAsync(true);
                }));

            contextMenu.Items.Add(Chrome.IconItem(
                Theme.BentoGridGeom, settings.VirtualDesktopSleepEnabled ? o.Ice : o.InkSecondary,
                "Virtual Desktop Auto-Sleep", settings.VirtualDesktopSleepEnabled ? "On" : "Off", settings.VirtualDesktopSleepEnabled ? o.Ice : o.InkMuted,
                delegate
                {
                    settings.VirtualDesktopSleepEnabled = !settings.VirtualDesktopSleepEnabled;
                    ecoQos.VirtualDesktopSleep = settings.VirtualDesktopSleepEnabled;
                    settings.Save();
                    engine.RefreshCalmScreenAsync(true);
                }));

            System.Collections.Generic.HashSet<string> immuneSnap = settings.SnapshotImmuneGroups();
            if (immuneSnap.Count > 0)
            {
                contextMenu.Items.Add(Chrome.IconItem(
                    Theme.ShieldGuardGeom, o.Mint,
                    "Clear User Immune Shields", immuneSnap.Count + " apps", o.Mint,
                    delegate
                    {
                        foreach (string k in immuneSnap)
                        {
                            settings.SetGroupImmune(k, false);
                        }
                        engine.RefreshCalmScreenAsync(true);
                        ShowCelebration("Cleared User Immune Shields", "Shields Reset");
                    }));
            }

            contextMenu.Items.Add(Chrome.IconItem(
                Theme.KernelBoltGeom, taskReady ? o.Mint : o.Amber,
                "Kernel Standby Purge", taskReady ? "0-UAC" : "Enable", taskReady ? o.Mint : o.Amber,
                delegate
                {
                    if (!engine.IsKernelPurgeTaskRegistered())
                    {
                        if (engine.RegisterKernelPurgeTaskInteractive())
                        {
                            ShowCelebration("Kernel 0-UAC Active!", "0-UAC Ready");
                            engine.RefreshCalmScreenAsync(true);
                        }
                    }
                }));

            contextMenu.Items.Add(Chrome.IconItem(
                Theme.StartupWinGeom, startupOn ? o.Mint : o.InkSecondary,
                "Launch at Windows Sign-In", startupOn ? "On" : "Off", startupOn ? o.Mint : o.InkMuted,
                delegate { ToggleStartup(); }));

            contextMenu.Items.Add(Chrome.Line());

            contextMenu.Items.Add(Chrome.IconItem(
                Theme.MinimizeLineGeom, o.InkSecondary,
                "Minimize to Taskbar", null, null,
                delegate { MinimizeWindow(); }));

            contextMenu.Items.Add(Chrome.IconItem(
                Theme.TrayDownGeom, o.InkSecondary,
                "Hide to System Tray", null, null,
                delegate { CloseFromHeaderButton(false); }));

            contextMenu.Items.Add(Chrome.IconItem(
                Theme.PowerQuitGeom, o.Coral,
                "Quit Legram", null, null,
                delegate { Application.Current.Shutdown(); }));

            contextMenu.PlacementTarget = rootHost;
            contextMenu.Placement = PlacementMode.MousePoint;
            contextMenu.IsOpen = true;
        }

        private void OnSnapshotUpdatedBackground(object sender, EventArgs e)
        {
            if (Dispatcher.HasShutdownStarted) return;
            Dispatcher.BeginInvoke(new Action(RefreshUi), DispatcherPriority.Background);
        }

        public void RefreshUi()
        {
            SyncModeVisibility();
            SystemSnapshot snap = engine.Current;
            if (!settings.FoldedLens)
            {
                monolithView.Bind(snap, false, isHarmonizing, celebrationSummary);
            }
            else
            {
                lensView.Bind(snap, false, isHarmonizing, celebrationShort);
            }
            UpdateTrayIconAndTooltip(snap);
        }

        private void UpdateTrayIconAndTooltip(SystemSnapshot snap)
        {
            if (tray == null || snap == null) return;

            int iconSize = NativeApi.SmallIconSize();
            bool light = TrayGlyph.IsLightTaskbar();
            MemoryPressure p = (!string.IsNullOrEmpty(celebrationSummary) || isHarmonizing)
                ? MemoryPressure.Boosting
                : snap.Pressure;
            string key = iconSize + "|" + snap.UsedRamPercent + "|" + (int)p + "|" + (light ? "1" : "0") + "|" + (isHarmonizing ? "1" : "0");

            if (key != lastTrayKey)
            {
                System.Drawing.Icon old = currentTrayIcon;
                System.Drawing.Icon nextIcon = null;
                bool swapped = false;
                try
                {
                    nextIcon = TrayGlyph.Make(iconSize, snap.UsedRamPercent, p, light, isHarmonizing);
                    tray.Icon = nextIcon;
                    currentTrayIcon = nextIcon;
                    lastTrayKey = key;
                    swapped = true;
                }
                catch (Exception ex)
                {
                    Log.Error("TrayGlyph.Make", ex);
                    if (nextIcon != null && !ReferenceEquals(nextIcon, currentTrayIcon))
                    {
                        try { NativeApi.DestroyIcon(nextIcon.Handle); nextIcon.Dispose(); } catch { }
                    }
                }
                if (swapped && old != null && !ReferenceEquals(old, currentTrayIcon))
                {
                    try { NativeApi.DestroyIcon(old.Handle); old.Dispose(); } catch { }
                }
            }

            string rawTip = snap.CommitLimitBytes > 0
                ? string.Format(CultureInfo.InvariantCulture,
                    "Legram \u00B7 {0} Free ({1}% \u00B7 Cmt {2}%)",
                    Fmt.BytesShort(snap.AvailableRamBytes), snap.UsedRamPercent, snap.CommitUsedPercent)
                : string.Format(CultureInfo.InvariantCulture,
                    "Legram \u00B7 {0} Available ({1}%)",
                    Fmt.BytesShort(snap.AvailableRamBytes), snap.UsedRamPercent);
            if (rawTip.Length > 63) rawTip = rawTip.Substring(0, 62) + "\u2026";
            if (tray.Text != rawTip) tray.Text = rawTip;
        }

        private void ScheduleSelfTrim(int seconds)
        {
            if (selfTrimTimer == null)
            {
                selfTrimTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle);
                selfTrimTimer.Tick += delegate
                {
                    selfTrimTimer.Stop();
                    System.Threading.ThreadPool.QueueUserWorkItem(delegate
                    {
                        try
                        {
                            GpuVramMonitor.ClosePdhIfIdle();
                            GC.Collect(2, GCCollectionMode.Optimized, false);
                            NativeApi.SelfTrimWorkingSet();
                        }
                        catch { }
                    });
                };
            }
            selfTrimTimer.Stop();
            selfTrimTimer.Interval = TimeSpan.FromSeconds(seconds);
            selfTrimTimer.Start();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (telemetryTimer != null) telemetryTimer.Stop();
            if (celebrationTimer != null) celebrationTimer.Stop();
            if (selfTrimTimer != null) selfTrimTimer.Stop();
            engine.SnapshotUpdated -= OnSnapshotUpdatedBackground;
            if (currentTrayIcon != null)
            {
                if (tray != null) tray.Icon = null;
                try { NativeApi.DestroyIcon(currentTrayIcon.Handle); currentTrayIcon.Dispose(); } catch { }
                currentTrayIcon = null;
            }
            base.OnClosed(e);
        }
    }
}
