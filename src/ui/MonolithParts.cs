using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Legram
{
    // Bespoke Gentle Monster × Google Prismatic Dichroic Lens Medallion (legram.ico)
    public sealed class PrismaticLensMedallion : FrameworkElement
    {
        private readonly double diameter;
        private MemoryPressure pressure = MemoryPressure.Healthy;
        private bool isCelebrating;

        private static readonly Color[] PrismColors = new Color[]
        {
            Color.FromArgb(255, 255, 255, 255), // Specular Pearl
            Color.FromArgb(255, 255, 214, 122), // Liquid Gold Bevel
            Color.FromArgb(255, 246, 139, 130), // Dichroic Coral
            Color.FromArgb(255, 168, 199, 250), // Google Ice Blue
            Color.FromArgb(255, 124, 227, 161), // Calm Mint
            Color.FromArgb(255, 134, 232, 255)  // Cyan Refraction
        };

        public PrismaticLensMedallion(double size)
        {
            diameter = size;
            Width = size;
            Height = size;
            IsHitTestVisible = false;
        }

        public void SetState(MemoryPressure p, bool celebrating)
        {
            if (pressure != p || isCelebrating != celebrating)
            {
                pressure = p;
                isCelebrating = celebrating;
                InvalidateVisual();
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            double s = diameter;
            Point c = new Point(s / 2.0, s / 2.0);
            double outerR = s / 2.0 - 0.6;

            // 1. Dark Machined Seat Ring
            SolidColorBrush seatBrush = Theme.BrushHex(0xFF0C0E13);
            Pen outerHairline = new Pen(Theme.BrushHex(0x28FFFFFF), 0.65);
            outerHairline.Freeze();
            dc.DrawEllipse(seatBrush, outerHairline, c, outerR, outerR);

            // 2. Continuous Liquid-Smooth Prismatic Dichroic Bevel Ring (Inspirations #8 & #9)
            double ringThick = Math.Max(1.65, s * 0.088);
            double ringR = outerR - ringThick * 0.56;

            LinearGradientBrush prismSweep = new LinearGradientBrush
            {
                StartPoint = new Point(0.08, 0.06),
                EndPoint = new Point(0.92, 0.94)
            };
            prismSweep.GradientStops.Add(new GradientStop(Color.FromArgb(255, 255, 255, 255), 0.00)); // Specular Pearl
            prismSweep.GradientStops.Add(new GradientStop(Color.FromArgb(255, 255, 214, 122), 0.22)); // Liquid Gold Bevel
            prismSweep.GradientStops.Add(new GradientStop(Color.FromArgb(255, 246, 139, 130), 0.44)); // Dichroic Coral
            prismSweep.GradientStops.Add(new GradientStop(Color.FromArgb(255, 168, 199, 250), 0.70)); // Google Ice Blue
            prismSweep.GradientStops.Add(new GradientStop(Color.FromArgb(255, 124, 227, 161), 1.00)); // Calm Mint
            prismSweep.Freeze();

            Pen prismPen = new Pen(prismSweep, ringThick);
            prismPen.Freeze();
            dc.DrawEllipse(null, prismPen, c, ringR, ringR);

            // Secondary cross-axis cyan-gold optical glint arc for authentic dichroic depth
            LinearGradientBrush glintGrad = new LinearGradientBrush(
                Color.FromArgb(235, 134, 232, 255),
                Color.FromArgb(0, 255, 214, 122),
                new Point(0.0, 1.0),
                new Point(1.0, 0.0));
            glintGrad.Freeze();
            Pen glintPen = new Pen(glintGrad, ringThick * 0.85)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            glintPen.Freeze();
            DrawCircleArc(dc, glintPen, c, ringR, 188, 96);

            // 3. Recessed Smoked Glass Optical Lens Core
            double coreR = Math.Max(3.0, ringR - ringThick * 0.52);
            LinearGradientBrush coreFill = Theme.Grad(0xFF262934, 0xFF101218, 90);
            Pen innerRim = new Pen(Theme.BrushHex(0x30FFFFFF), 0.75);
            innerRim.Freeze();
            dc.DrawEllipse(coreFill, innerRim, c, coreR, coreR);

            // 4. Inner Precision Mark (Checkmark when celebrating, 3-Bar Optical Mark otherwise)
            if (isCelebrating && s >= 24)
            {
                Pen chkPen = new Pen(Theme.Obsidian.Mint, Math.Max(1.8, s * 0.068))
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                };
                chkPen.Freeze();
                StreamGeometry g = new StreamGeometry();
                using (StreamGeometryContext ctx = g.Open())
                {
                    ctx.BeginFigure(new Point(c.X - s * 0.16, c.Y + s * 0.01), false, false);
                    ctx.LineTo(new Point(c.X - s * 0.03, c.Y + s * 0.14), true, false);
                    ctx.LineTo(new Point(c.X + s * 0.18, c.Y - s * 0.11), true, false);
                }
                g.Freeze();
                dc.DrawGeometry(null, chkPen, g);
            }
            else
            {
                double barW = Math.Max(1.35, s * 0.062);
                double gap = s * 0.135;
                Brush sigBrush = Theme.Obsidian.SignalBrush(pressure);

                Pen sidePen = new Pen(Theme.Obsidian.InkPrimary, barW)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round
                };
                sidePen.Freeze();

                Pen midPen = new Pen(sigBrush, barW)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round
                };
                midPen.Freeze();

                dc.DrawLine(sidePen, new Point(c.X - gap, c.Y - s * 0.10), new Point(c.X - gap, c.Y + s * 0.10));
                dc.DrawLine(midPen, new Point(c.X, c.Y - s * 0.18), new Point(c.X, c.Y + s * 0.18));
                dc.DrawLine(sidePen, new Point(c.X + gap, c.Y - s * 0.12), new Point(c.X + gap, c.Y + s * 0.12));
            }
        }

        internal static void DrawCircleArc(DrawingContext dc, Pen pen, Point c, double r, double startDegTop, double sweepDeg)
        {
            if (sweepDeg <= 0.5 || r <= 1.0) return;
            Point p0 = CirclePt(c, r, startDegTop);
            Point p1 = CirclePt(c, r, startDegTop + sweepDeg);
            StreamGeometry geom = new StreamGeometry();
            using (StreamGeometryContext ctx = geom.Open())
            {
                ctx.BeginFigure(p0, false, false);
                ctx.ArcTo(p1, new Size(r, r), 0, sweepDeg > 180.0, SweepDirection.Clockwise, true, false);
            }
            geom.Freeze();
            dc.DrawGeometry(null, pen, geom);
        }

        private static Point CirclePt(Point c, double r, double degFromTop)
        {
            double rad = degFromTop * Math.PI / 180.0;
            return new Point(c.X + r * Math.Sin(rad), c.Y - r * Math.Cos(rad));
        }
    }

    // Sleek Modern Optical Liquid Pill Bar (Replaces busy 38-tick vertical comb)
    public sealed class PrecisionRulerGauge : FrameworkElement
    {
        private HausPalette palette = Theme.Obsidian;
        private double targetUsed = 0.60;
        private double targetStandby = 0.22;
        private double animUsed = 0.60;
        private double animStandby = 0.22;
        private bool hasInitialSample;
        private MemoryPressure pressure = MemoryPressure.Healthy;
        private System.Windows.Threading.DispatcherTimer animTimer;

        public PrecisionRulerGauge()
        {
            Height = 14;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            IsHitTestVisible = false;
        }

        public void SetState(HausPalette pal, double usedFrac, double standbyFrac, MemoryPressure p)
        {
            palette = pal ?? Theme.Obsidian;
            targetUsed = Math.Max(0.04, Math.Min(0.98, usedFrac));
            targetStandby = Math.Max(0.0, Math.Min(0.98 - targetUsed, standbyFrac));
            pressure = p;

            if (!hasInitialSample)
            {
                hasInitialSample = true;
                animUsed = targetUsed;
                animStandby = targetStandby;
                InvalidateVisual();
                return;
            }

            if (Math.Abs(animUsed - targetUsed) > 0.002 || Math.Abs(animStandby - targetStandby) > 0.002)
            {
                EnsureAnimTimer();
            }
            else
            {
                animUsed = targetUsed;
                animStandby = targetStandby;
                InvalidateVisual();
            }
        }

        private void EnsureAnimTimer()
        {
            if (animTimer == null)
            {
                animTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(16)
                };
                animTimer.Tick += delegate
                {
                    double du = targetUsed - animUsed;
                    double ds = targetStandby - animStandby;
                    if (Math.Abs(du) < 0.0015 && Math.Abs(ds) < 0.0015)
                    {
                        animUsed = targetUsed;
                        animStandby = targetStandby;
                        animTimer.Stop();
                    }
                    else
                    {
                        animUsed += du * 0.22;
                        animStandby += ds * 0.22;
                    }
                    InvalidateVisual();
                };
            }
            if (!animTimer.IsEnabled) animTimer.Start();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth > 20 ? ActualWidth : 284;
            double h = ActualHeight > 6 ? ActualHeight : 14;
            double barH = 6.5;
            double rad = barH * 0.5;
            double y = Math.Round((h - barH) * 0.5, 1);

            // 1. Recessed Seamless Matte Capsule Track
            SolidColorBrush trackFill = Theme.BrushHex(palette.IsPorcelain ? 0xFFDADDE5u : 0xFF222632u);
            Pen trackRim = new Pen(Theme.BrushHex(palette.IsPorcelain ? 0x16000000u : 0x18FFFFFFu), 0.7);
            trackRim.Freeze();
            dc.DrawRoundedRectangle(trackFill, trackRim, new Rect(0, y, w, barH), rad, rad);

            // 2. Soft Translucent Standby Cushion Segment (Reclaimable cache preview)
            double totalSpan = Math.Min(1.0, animUsed + animStandby);
            if (animStandby > 0.015)
            {
                double stbW = Math.Max(barH, w * totalSpan);
                SolidColorBrush stbFill = Theme.BrushHex(palette.IsPorcelain ? 0x2E5D79A6u : 0x2E8AD8FFu);
                dc.DrawRoundedRectangle(stbFill, null, new Rect(0, y, stbW, barH), rad, rad);
            }

            // 3. Luminous Gradient Active Pill Fill
            double activeW = Math.Max(barH, w * animUsed);
            Color c0;
            Color c1;
            Color cTip;

            if (pressure == MemoryPressure.Boosting)
            {
                c0 = Color.FromArgb(255, 37, 99, 235);    // Electric Cobalt
                c1 = Color.FromArgb(255, 16, 185, 129);   // Aurora Emerald
                cTip = Color.FromArgb(255, 167, 243, 208);
            }
            else if (pressure == MemoryPressure.Critical)
            {
                c0 = Color.FromArgb(255, 239, 83, 80);
                c1 = Color.FromArgb(255, 255, 138, 128);
                cTip = Color.FromArgb(255, 255, 226, 224);
            }
            else if (pressure == MemoryPressure.Warning)
            {
                c0 = Color.FromArgb(255, 245, 158, 11);
                c1 = Color.FromArgb(255, 251, 191, 36);
                cTip = Color.FromArgb(255, 254, 243, 199);
            }
            else
            {
                c0 = palette.IsPorcelain
                    ? Color.FromArgb(255, 16, 158, 108)
                    : Color.FromArgb(255, 62, 224, 174);  // Calm Mint
                c1 = palette.IsPorcelain
                    ? Color.FromArgb(255, 37, 99, 235)
                    : Color.FromArgb(255, 126, 228, 255); // Ice Cyan
                cTip = palette.IsPorcelain
                    ? Color.FromArgb(255, 96, 165, 250)
                    : Color.FromArgb(255, 245, 250, 255); // Optic Pearl Tip
            }

            LinearGradientBrush activeFill = new LinearGradientBrush
            {
                StartPoint = new Point(0.0, 0.5),
                EndPoint = new Point(1.0, 0.5)
            };
            activeFill.GradientStops.Add(new GradientStop(c0, 0.0));
            activeFill.GradientStops.Add(new GradientStop(c1, 0.72));
            activeFill.GradientStops.Add(new GradientStop(cTip, 1.0));
            activeFill.Freeze();

            // Subtle ambient glow under the leading edge
            double tipX = Math.Max(rad, activeW - rad);
            double cy = y + rad;
            RadialGradientBrush tipHalo = new RadialGradientBrush(
                Color.FromArgb(palette.IsPorcelain ? (byte)42 : (byte)72, c1.R, c1.G, c1.B),
                Color.FromArgb(0, c1.R, c1.G, c1.B));
            tipHalo.Freeze();
            dc.DrawEllipse(tipHalo, null, new Point(tipX, cy), 8.5, 6.5);

            dc.DrawRoundedRectangle(activeFill, null, new Rect(0, y, activeW, barH), rad, rad);
        }
    }

    // Sleek Mini Optical Liquid Pill Bar inside Folded Mini Capsule (Cohesive with MonolithView)
    public sealed class MiniWaveformCanvas : FrameworkElement
    {
        private HausPalette palette = Theme.Obsidian;
        private double usedFraction = 0.60;
        private MemoryPressure pressure = MemoryPressure.Healthy;

        public MiniWaveformCanvas()
        {
            Width = 46;
            Height = 14;
            IsHitTestVisible = false;
        }

        public void SetState(HausPalette pal, double usedFrac, MemoryPressure p)
        {
            palette = pal ?? Theme.Obsidian;
            usedFraction = Math.Max(0.08, Math.Min(0.98, usedFrac));
            pressure = p;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = 46.0;
            double barH = 5.5;
            double rad = barH * 0.5;
            double y = (14.0 - barH) * 0.5;

            SolidColorBrush track = Theme.BrushHex(0xFF222632u);
            Pen rim = new Pen(Theme.BrushHex(0x18FFFFFFu), 0.6);
            rim.Freeze();
            dc.DrawRoundedRectangle(track, rim, new Rect(0, y, w, barH), rad, rad);

            Color c0;
            Color c1;
            if (pressure == MemoryPressure.Boosting)
            {
                c0 = Color.FromArgb(255, 37, 99, 235);
                c1 = Color.FromArgb(255, 16, 185, 129);
            }
            else if (pressure == MemoryPressure.Critical)
            {
                c0 = Color.FromArgb(255, 239, 83, 80);
                c1 = Color.FromArgb(255, 255, 138, 128);
            }
            else if (pressure == MemoryPressure.Warning)
            {
                c0 = Color.FromArgb(255, 245, 158, 11);
                c1 = Color.FromArgb(255, 251, 191, 36);
            }
            else
            {
                c0 = Color.FromArgb(255, 62, 224, 174);
                c1 = Color.FromArgb(255, 186, 238, 255);
            }

            LinearGradientBrush fill = new LinearGradientBrush(c0, c1, new Point(0, 0.5), new Point(1, 0.5));
            fill.Freeze();
            double activeW = Math.Max(barH, w * usedFraction);
            dc.DrawRoundedRectangle(fill, null, new Rect(0, y, activeW, barH), rad, rad);
        }
    }

    // Recessed Tactile Hardware Spec Tile with Bespoke 24x24 Stroke Icon (Inspirations #1, #7 & #8)
    public sealed class HardwareSpecTile : Border
    {
        private readonly Border iconBox;
        private readonly System.Windows.Shapes.Path iconPath;
        private readonly TextBlock titleText;
        private readonly TextBlock mainText;
        private readonly TextBlock subText;
        public readonly TextBlock TipBody;

        public HardwareSpecTile(Geometry iconGeom, string title, string initialMain, string initialSub)
        {
            CornerRadius = new CornerRadius(15);
            BorderThickness = new Thickness(1);
            Padding = new Thickness(9, 8, 9, 8);

            StackPanel stack = new StackPanel();

            StackPanel topRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };

            iconPath = Theme.StrokeIcon(iconGeom, 12.5, Theme.Obsidian.InkSecondary, 1.75);
            iconBox = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 5, 0),
                Child = iconPath
            };

            titleText = Theme.Text(title, 8.5, FontWeights.Bold, Theme.Obsidian.InkMuted);
            topRow.Children.Add(iconBox);
            topRow.Children.Add(titleText);

            mainText = Theme.Text(initialMain, 11.5, FontWeights.SemiBold, Theme.Obsidian.InkPrimary);
            subText = Theme.Text(initialSub, 9.5, FontWeights.Medium, Theme.Obsidian.InkSecondary);
            subText.Margin = new Thickness(0, 1.5, 0, 0);

            stack.Children.Add(topRow);
            stack.Children.Add(mainText);
            stack.Children.Add(subText);
            Child = stack;

            TipBody = Theme.Text("", 11, FontWeights.Normal, Theme.Obsidian.InkPrimary);
            TipBody.TextWrapping = TextWrapping.Wrap;
            TipBody.MaxWidth = 310;
            ToolTip = Chrome.Tip(TipBody);
        }

        public void Apply(HausPalette pal, string title, string mainVal, Brush mainBrush, string subVal, Brush subBrush, Brush iconAccent)
        {
            Background = pal.TileFill;
            BorderBrush = pal.TileBorder;
            iconBox.Background = pal.IconWellFill;
            iconPath.Stroke = iconAccent ?? pal.InkSecondary;

            Theme.Set(titleText, title);
            Theme.Set(titleText, pal.InkMuted);
            Theme.Set(mainText, mainVal);
            Theme.Set(mainText, mainBrush ?? pal.InkPrimary);
            Theme.Set(subText, subVal);
            Theme.Set(subText, subBrush ?? pal.InkSecondary);
        }
    }

    // Authentic App Logo + Bespoke Vector Fallback Process Row (Inspirations #3, #4 & #8)
    // Click or Right-Click any process row to toggle 1-Click Immunity (Shield) or gently trim its working set.
    public sealed class EditorialLedgerRow : Border
    {
        private readonly Border appBadge;
        private readonly Image nativeAppImg;
        private readonly System.Windows.Shapes.Path fallbackVecIcon;
        private readonly TextBlock nameText;
        private readonly TextBlock countText;
        private readonly Border statePill;
        private readonly TextBlock stateText;
        private readonly TextBlock memText;
        private readonly TextBlock rowTip;

        private ProcessGroupItem boundItem;
        private bool isHover;

        public ProcessGroupItem BoundItem { get { return boundItem; } }
        public event Action<ProcessGroupItem> RowClicked;

        public EditorialLedgerRow()
        {
            Height = 31;
            CornerRadius = new CornerRadius(8);
            Padding = new Thickness(4, 0, 4, 0);
            Background = Brushes.Transparent;
            Cursor = Cursors.Hand;

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid badgeHost = new Grid();
            nativeAppImg = new Image
            {
                Width = 15,
                Height = 15,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            RenderOptions.SetBitmapScalingMode(nativeAppImg, BitmapScalingMode.HighQuality);

            fallbackVecIcon = Theme.StrokeIcon(Theme.BentoGridGeom, 13, Theme.Obsidian.InkSecondary, 1.7);
            badgeHost.Children.Add(nativeAppImg);
            badgeHost.Children.Add(fallbackVecIcon);

            appBadge = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(7),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 9, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = badgeHost
            };

            StackPanel titleStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            nameText = Theme.Text("", 11.5, FontWeights.Medium, Theme.Obsidian.InkPrimary);
            nameText.MaxWidth = 132;
            countText = Theme.Text("", 9.5, FontWeights.Normal, Theme.Obsidian.InkMuted);
            countText.Margin = new Thickness(4, 0, 0, 0);
            titleStack.Children.Add(nameText);
            titleStack.Children.Add(countText);
            Grid.SetColumn(titleStack, 1);

            stateText = Theme.Text("", 9.5, FontWeights.SemiBold, Theme.Obsidian.InkMuted);
            statePill = new Border
            {
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(6, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = stateText
            };
            Grid.SetColumn(statePill, 2);

            memText = Theme.Text("", 11.5, FontWeights.SemiBold, Theme.Obsidian.InkPrimary);
            memText.TextAlignment = TextAlignment.Right;
            memText.MinWidth = 54;
            Grid.SetColumn(memText, 3);

            g.Children.Add(appBadge);
            g.Children.Add(titleStack);
            g.Children.Add(statePill);
            g.Children.Add(memText);
            Child = g;

            rowTip = Theme.Text("", 11, FontWeights.Normal, Theme.Obsidian.InkPrimary);
            rowTip.TextWrapping = TextWrapping.Wrap;
            rowTip.MaxWidth = 290;
            ToolTip = Chrome.Tip(rowTip);

            MouseEnter += delegate
            {
                isHover = true;
                Background = Theme.BrushHex(0x14FFFFFFu);
            };
            MouseLeave += delegate
            {
                isHover = false;
                Background = Brushes.Transparent;
            };
            MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
            };
            MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                if (boundItem != null)
                {
                    Action<ProcessGroupItem> h = RowClicked;
                    if (h != null) h(boundItem);
                }
            };
            MouseRightButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                if (boundItem != null)
                {
                    Action<ProcessGroupItem> h = RowClicked;
                    if (h != null) h(boundItem);
                }
            };
        }

        public void Bind(ProcessGroupItem item, HausPalette pal)
        {
            boundItem = item;
            if (item == null)
            {
                Visibility = Visibility.Collapsed;
                return;
            }
            Visibility = Visibility.Visible;
            Background = isHover ? Theme.BrushHex(0x14FFFFFFu) : Brushes.Transparent;

            Theme.Set(nameText, item.DisplayName);
            Theme.Set(countText, item.ProcessCount > 1 ? ("\u00B7 " + item.ProcessCount) : "");
            Theme.Set(countText, pal.InkMuted);
            Theme.Set(memText, Fmt.Gb2(item.WorkingSetBytes));

            appBadge.Background = pal.IconWellFill;
            appBadge.BorderBrush = item.IsUserWhitelisted ? pal.Mint : pal.TileBorder;

            ImageSource realIcon = AppIconCache.TryGet(item);
            if (realIcon != null)
            {
                nativeAppImg.Source = realIcon;
                nativeAppImg.Visibility = Visibility.Visible;
                fallbackVecIcon.Visibility = Visibility.Collapsed;
            }
            else
            {
                nativeAppImg.Source = null;
                nativeAppImg.Visibility = Visibility.Collapsed;
                fallbackVecIcon.Visibility = Visibility.Visible;
                fallbackVecIcon.Data = Theme.FallbackGeometryForGroup(item.IconKind);
            }

            if (item.IsUserWhitelisted)
            {
                fallbackVecIcon.Stroke = pal.Mint;
                statePill.Background = pal.MintBg;
                Theme.Set(stateText, "Immune");
                Theme.Set(stateText, pal.Mint);
                Theme.Set(nameText, pal.InkPrimary);
                Theme.Set(memText, pal.Mint);
            }
            else if (item.IsProtected)
            {
                fallbackVecIcon.Stroke = pal.Mint;
                statePill.Background = pal.MintBg;
                Theme.Set(stateText, "Protected");
                Theme.Set(stateText, pal.Mint);
                Theme.Set(nameText, pal.InkPrimary);
                Theme.Set(memText, pal.InkSecondary);
            }
            else if (item.IsOrphanGroup)
            {
                fallbackVecIcon.Stroke = pal.Amber;
                statePill.Background = pal.AmberBg;
                Theme.Set(stateText, "Orphan");
                Theme.Set(stateText, pal.Amber);
                Theme.Set(nameText, pal.InkPrimary);
                Theme.Set(memText, pal.Amber);
            }
            else if (item.IsOnInactiveDesktop && item.PrimaryDesktopNumber > 0)
            {
                fallbackVecIcon.Stroke = pal.Ice;
                statePill.Background = pal.IceBg;
                Theme.Set(stateText, "D" + item.PrimaryDesktopNumber + " Sleep");
                Theme.Set(stateText, pal.Ice);
                Theme.Set(nameText, pal.InkPrimary);
                Theme.Set(memText, pal.InkSecondary);
            }
            else if (item.IsEcoQosActive)
            {
                fallbackVecIcon.Stroke = pal.Ice;
                statePill.Background = pal.IceBg;
                string ecoLbl = item.PrimaryDesktopNumber > 0
                    ? ("D" + item.PrimaryDesktopNumber + "\u00B7Eco")
                    : "EcoQoS";
                Theme.Set(stateText, ecoLbl);
                Theme.Set(stateText, pal.Ice);
                Theme.Set(nameText, pal.InkPrimary);
                Theme.Set(memText, pal.InkPrimary);
            }
            else
            {
                fallbackVecIcon.Stroke = pal.InkSecondary;
                statePill.Background = pal.PillBadgeFill;
                string bgLbl = item.IsForeground
                    ? "Focus"
                    : (item.PrimaryDesktopNumber > 0 ? ("Desk " + item.PrimaryDesktopNumber) : "Bg");
                Theme.Set(stateText, bgLbl);
                Theme.Set(stateText, item.IsForeground ? pal.InkPrimary : pal.InkMuted);
                Theme.Set(nameText, pal.InkPrimary);
                Theme.Set(memText, pal.InkPrimary);
            }

            string vramLine = item.DedicatedVramBytes >= 8L * 1048576L
                ? ("\nDedicated VRAM: " + Fmt.BytesShort(item.DedicatedVramBytes))
                : "";
            string actionHint = item.IsProtected
                ? "System/Active Shield (Immune automatically)"
                : (item.IsUserWhitelisted
                    ? "Click row: Remove 1-Click Immune Shield"
                    : "Click row: Toggle 1-Click Immune Shield or Trim Memory");
            Theme.Set(rowTip, string.Format(CultureInfo.InvariantCulture,
                "{0} ({1} {2})\nRAM Working Set: {3}{4}\nStatus: {5}\n\u2022 {6}",
                item.DisplayName,
                item.ProcessCount,
                item.ProcessCount == 1 ? "process" : "processes",
                Fmt.Gb2(item.WorkingSetBytes),
                vramLine,
                string.IsNullOrEmpty(item.Subtitle) ? "Auto-managed" : item.Subtitle,
                actionHint));
        }
    }

    // Tactile Machined Window Control Button (Minimize '—' and Close '×')
    public sealed class WindowChromeButton : Border
    {
        private readonly bool isCloseDanger;
        private readonly System.Windows.Shapes.Path iconPath;
        private readonly ScaleTransform pressScale = new ScaleTransform(1.0, 1.0);
        private bool isHover;
        private bool isPressed;

        public event Action<bool> Invoked; // arg: shiftHeld

        public WindowChromeButton(Geometry geom, bool dangerHover, string tooltipText)
        {
            isCloseDanger = dangerHover;
            Width = 24;
            Height = 24;
            CornerRadius = new CornerRadius(7.5);
            BorderThickness = new Thickness(1);
            Cursor = Cursors.Hand;
            RenderTransformOrigin = new Point(0.5, 0.5);
            RenderTransform = pressScale;

            iconPath = Theme.StrokeIcon(geom, dangerHover ? 9.5 : 10.5, Theme.BrushHex(0xFFB4BCCB), 1.7);
            iconPath.HorizontalAlignment = HorizontalAlignment.Center;
            iconPath.VerticalAlignment = VerticalAlignment.Center;
            Child = iconPath;

            if (!string.IsNullOrEmpty(tooltipText))
            {
                ToolTip = Chrome.Tip(Theme.Text(tooltipText, 11, FontWeights.Normal, Theme.Obsidian.InkPrimary));
            }

            MouseEnter += delegate
            {
                isHover = true;
                SyncLook();
            };
            MouseLeave += delegate
            {
                isHover = false;
                isPressed = false;
                SetScale(1.0);
                SyncLook();
            };
            MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                isPressed = true;
                SetScale(0.90);
                SyncLook();
            };
            MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                bool fire = isPressed && IsMouseOver;
                isPressed = false;
                SetScale(1.0);
                SyncLook();
                if (fire)
                {
                    bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
                    Action<bool> h = Invoked;
                    if (h != null) h(shift);
                }
            };

            SyncLook();
        }

        private void SetScale(double s)
        {
            pressScale.BeginAnimation(
                ScaleTransform.ScaleXProperty,
                new DoubleAnimation(s, new Duration(TimeSpan.FromMilliseconds(95))));
            pressScale.BeginAnimation(
                ScaleTransform.ScaleYProperty,
                new DoubleAnimation(s, new Duration(TimeSpan.FromMilliseconds(95))));
        }

        private void SyncLook()
        {
            if (isHover && isCloseDanger)
            {
                Background = Theme.Grad(0xFFE11D48, 0xFFBE123C, 90);
                BorderBrush = Theme.BrushHex(0x88FDA4AF);
                iconPath.Stroke = Brushes.White;
                return;
            }
            if (isHover)
            {
                Background = Theme.BrushHex(0xFF2D3342);
                BorderBrush = Theme.BrushHex(0x48FFFFFF);
                iconPath.Stroke = Brushes.White;
                return;
            }
            Background = Theme.BrushHex(0xFF1E222C);
            BorderBrush = Theme.BrushHex(0x22FFFFFF);
            iconPath.Stroke = Theme.BrushHex(0xFFB4BCCB);
        }
    }

    // Recessed Optical Jewel Glyph inside the 3D One-Button (Glowing Orb -> Spinning Arc -> Checkmark)
    internal sealed class KineticButtonGlyph : FrameworkElement
    {
        private bool isSweeping;
        private bool isCelebrating;
        private double spinAngle;
        private Color accentDot = Color.FromArgb(255, 62, 224, 174);

        public KineticButtonGlyph()
        {
            Width = 28;
            Height = 28;
            IsHitTestVisible = false;
        }

        public void SetState(Color dotColor, bool sweeping, bool celebrating, double angleDeg)
        {
            accentDot = dotColor;
            isSweeping = sweeping;
            isCelebrating = celebrating;
            spinAngle = angleDeg;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            Point c = new Point(14.0, 14.0);

            // Recessed Smoked Optical Socket Well
            SolidColorBrush wellFill = Theme.BrushHex(0x46060910u);
            Pen wellRim = new Pen(Theme.BrushHex(0x38FFFFFFu), 0.9);
            wellRim.Freeze();
            dc.DrawEllipse(wellFill, wellRim, c, 13.2, 13.2);

            if (isSweeping)
            {
                Pen trackPen = new Pen(Theme.BrushHex(0x36FFFFFFu), 1.7);
                trackPen.Freeze();
                dc.DrawEllipse(null, trackPen, c, 7.6, 7.6);

                Pen arcPen = new Pen(Brushes.White, 2.0)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round
                };
                arcPen.Freeze();
                PrismaticLensMedallion.DrawCircleArc(dc, arcPen, c, 7.6, spinAngle, 235.0);
                return;
            }

            if (isCelebrating)
            {
                Pen chkPen = new Pen(Brushes.White, 2.1)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                };
                chkPen.Freeze();
                StreamGeometry g = new StreamGeometry();
                using (StreamGeometryContext ctx = g.Open())
                {
                    ctx.BeginFigure(new Point(9.6, 14.2), false, false);
                    ctx.LineTo(new Point(12.7, 17.3), true, false);
                    ctx.LineTo(new Point(18.6, 10.9), true, false);
                }
                g.Freeze();
                dc.DrawGeometry(null, chkPen, g);
                return;
            }

            // Soft Radial Glow Halo + Crisp Concentric Ring + Luminous Core Dot
            RadialGradientBrush glow = new RadialGradientBrush(
                Color.FromArgb(95, accentDot.R, accentDot.G, accentDot.B),
                Color.FromArgb(0, accentDot.R, accentDot.G, accentDot.B));
            glow.Freeze();
            dc.DrawEllipse(glow, null, c, 9.5, 9.5);

            Pen haloPen = new Pen(new SolidColorBrush(Color.FromArgb(90, accentDot.R, accentDot.G, accentDot.B)), 1.1);
            haloPen.Freeze();
            dc.DrawEllipse(null, haloPen, c, 7.6, 7.6);

            SolidColorBrush dotBrush = new SolidColorBrush(accentDot);
            dotBrush.Freeze();
            dc.DrawEllipse(dotBrush, null, c, 3.6, 3.6);
        }
    }

    // Sculpted 3D Metallic-Glass Capsule Surface (Machined Socket + Convex Specular Dome + Liquid Wave)
    internal sealed class KineticCapsuleSurface : FrameworkElement
    {
        private bool isHover;
        private bool isSweeping;
        private bool isCelebrating;
        private double sweepProgress; // 0..1
        private Point rippleOrigin = new Point(140, 24);

        public KineticCapsuleSurface()
        {
            IsHitTestVisible = false;
        }

        public void SetVisualState(bool hover, bool sweeping, double progress, bool celebrating, Point clickPt)
        {
            isHover = hover;
            isSweeping = sweeping;
            sweepProgress = Math.Max(0.0, Math.Min(1.0, progress));
            isCelebrating = celebrating;
            rippleOrigin = clickPt;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth > 20 ? ActualWidth : 300;
            double h = ActualHeight > 16 ? ActualHeight : 48;
            double outerR = h * 0.5;

            // 1. Machined Recessed Chassis Socket Groove (gives physical 3D seat depth!)
            SolidColorBrush socketFill = Theme.BrushHex(0xFF07080Du);
            Pen socketLip = new Pen(Theme.BrushHex(0x22FFFFFFu), 0.8);
            socketLip.Freeze();
            dc.DrawRoundedRectangle(socketFill, socketLip, new Rect(0, 0, w, h), outerR, outerR);

            // 2. Inner Raised 3D Convex Capsule Body (inset by 2px inside the socket)
            const double inset = 2.0;
            double iw = Math.Max(10.0, w - inset * 2.0);
            double ih = Math.Max(10.0, h - inset * 2.0);
            double ir = ih * 0.5;
            Rect bodyRect = new Rect(inset, inset, iw, ih);

            RectangleGeometry clip = new RectangleGeometry(bodyRect, ir, ir);
            clip.Freeze();
            dc.PushClip(clip);

            if (isSweeping)
            {
                // Base shifts immediately to Deep Electric Sapphire
                LinearGradientBrush sapphireBase = new LinearGradientBrush(
                    Color.FromArgb(255, 29, 78, 216),
                    Color.FromArgb(255, 17, 45, 138),
                    new Point(0.5, 0.0),
                    new Point(0.5, 1.0));
                sapphireBase.Freeze();
                dc.DrawRectangle(sapphireBase, null, bodyRect);

                // Smooth Cubic-Out Electric Cyan -> Luminous Mint-Emerald Liquid Wave
                double eased = 1.0 - Math.Pow(1.0 - sweepProgress, 3.0);
                double waveW = Math.Max(ih, iw * eased);
                LinearGradientBrush waveGrad = new LinearGradientBrush
                {
                    StartPoint = new Point(0.0, 0.2),
                    EndPoint = new Point(1.0, 0.8)
                };
                waveGrad.GradientStops.Add(new GradientStop(Color.FromArgb(255, 2, 132, 199), 0.0));
                waveGrad.GradientStops.Add(new GradientStop(Color.FromArgb(255, 6, 182, 212), 0.48));
                waveGrad.GradientStops.Add(new GradientStop(Color.FromArgb(255, 16, 185, 129), 1.0));
                waveGrad.Freeze();
                dc.DrawRoundedRectangle(waveGrad, null, new Rect(inset, inset, waveW, ih), ir, ir);

                // Expanding Radial Click Shockwave Ripple
                double maxR = Math.Max(iw, 220.0) * 0.95;
                double ripR = 14.0 + maxR * eased;
                byte ripAlpha = (byte)Math.Round(96.0 * (1.0 - sweepProgress));
                if (ripAlpha > 4)
                {
                    SolidColorBrush ripFill = new SolidColorBrush(Color.FromArgb((byte)(ripAlpha / 2), 255, 255, 255));
                    ripFill.Freeze();
                    Pen ripPen = new Pen(new SolidColorBrush(Color.FromArgb(ripAlpha, 255, 255, 255)), 1.6);
                    ripPen.Freeze();
                    dc.DrawEllipse(ripFill, ripPen, rippleOrigin, ripR, ripR);
                }

                // Gliding Specular Sheen
                double sheenX = -60.0 + (iw + 120.0) * eased;
                LinearGradientBrush sheen = new LinearGradientBrush(
                    Color.FromArgb(0, 255, 255, 255),
                    Color.FromArgb(62, 255, 255, 255),
                    new Point(0.0, 0.0),
                    new Point(1.0, 0.0));
                sheen.Freeze();
                dc.DrawRectangle(sheen, null, new Rect(inset + sheenX - 38.0, inset, 38.0, ih));
            }
            else if (isCelebrating)
            {
                // 3D Sculpted Emerald-Jade Glass Cylinder
                LinearGradientBrush jadeBody = new LinearGradientBrush
                {
                    StartPoint = new Point(0.5, 0.0),
                    EndPoint = new Point(0.5, 1.0)
                };
                jadeBody.GradientStops.Add(new GradientStop(Color.FromArgb(255, 24, 176, 126), 0.0));
                jadeBody.GradientStops.Add(new GradientStop(Color.FromArgb(255, 15, 132, 93), 0.52));
                jadeBody.GradientStops.Add(new GradientStop(Color.FromArgb(255, 9, 88, 62), 1.0));
                jadeBody.Freeze();
                dc.DrawRectangle(jadeBody, null, bodyRect);
            }
            else
            {
                // 3D Sculpted Anodized Sapphire-Titanium Cylinder (Never Flat!)
                LinearGradientBrush titaniumBody = new LinearGradientBrush
                {
                    StartPoint = new Point(0.5, 0.0),
                    EndPoint = new Point(0.5, 1.0)
                };
                if (isHover)
                {
                    titaniumBody.GradientStops.Add(new GradientStop(Color.FromArgb(255, 62, 95, 148), 0.0));
                    titaniumBody.GradientStops.Add(new GradientStop(Color.FromArgb(255, 38, 62, 102), 0.48));
                    titaniumBody.GradientStops.Add(new GradientStop(Color.FromArgb(255, 21, 36, 62), 1.0));
                }
                else
                {
                    titaniumBody.GradientStops.Add(new GradientStop(Color.FromArgb(255, 50, 77, 122), 0.0));
                    titaniumBody.GradientStops.Add(new GradientStop(Color.FromArgb(255, 30, 49, 82), 0.48));
                    titaniumBody.GradientStops.Add(new GradientStop(Color.FromArgb(255, 17, 29, 50), 1.0));
                }
                titaniumBody.Freeze();
                dc.DrawRectangle(titaniumBody, null, bodyRect);

                // Subtle Internal Mint-Cyan Optical Refraction Glow radiating from the left jewel
                RadialGradientBrush auraLeft = new RadialGradientBrush(
                    Color.FromArgb(isHover ? (byte)86 : (byte)62, 56, 189, 248),
                    Color.FromArgb(0, 56, 189, 248))
                {
                    Center = new Point(0.14, 0.50),
                    GradientOrigin = new Point(0.12, 0.45),
                    RadiusX = 0.46,
                    RadiusY = 0.95
                };
                auraLeft.Freeze();
                dc.DrawRectangle(auraLeft, null, bodyRect);

                // Subtle Warm Mint-Teal Counter-Refraction on the right metric side
                RadialGradientBrush auraRight = new RadialGradientBrush(
                    Color.FromArgb(isHover ? (byte)54 : (byte)36, 52, 211, 153),
                    Color.FromArgb(0, 52, 211, 153))
                {
                    Center = new Point(0.86, 0.50),
                    GradientOrigin = new Point(0.88, 0.50),
                    RadiusX = 0.36,
                    RadiusY = 0.88
                };
                auraRight.Freeze();
                dc.DrawRectangle(auraRight, null, bodyRect);
            }

            // 3. Universal Top Convex Glass Specular Dome (gives curved tactile lens volume across ALL states!)
            LinearGradientBrush glassDome = new LinearGradientBrush(
                Color.FromArgb(isHover ? (byte)52 : (byte)40, 255, 255, 255),
                Color.FromArgb(0, 255, 255, 255),
                new Point(0.5, 0.0),
                new Point(0.5, 1.0));
            glassDome.Freeze();
            dc.DrawRectangle(glassDome, null, new Rect(inset, inset, iw, ih * 0.48));

            dc.Pop();

            // 4. Dichroic Top-Lit Specular Bezel Rim
            LinearGradientBrush rimGrad = new LinearGradientBrush
            {
                StartPoint = new Point(0.15, 0.0),
                EndPoint = new Point(0.85, 1.0)
            };
            if (isSweeping)
            {
                rimGrad.GradientStops.Add(new GradientStop(Color.FromArgb(210, 186, 230, 253), 0.0));
                rimGrad.GradientStops.Add(new GradientStop(Color.FromArgb(165, 110, 231, 183), 0.55));
                rimGrad.GradientStops.Add(new GradientStop(Color.FromArgb(70, 14, 116, 144), 1.0));
            }
            else if (isCelebrating)
            {
                rimGrad.GradientStops.Add(new GradientStop(Color.FromArgb(215, 167, 243, 208), 0.0));
                rimGrad.GradientStops.Add(new GradientStop(Color.FromArgb(140, 52, 211, 153), 0.55));
                rimGrad.GradientStops.Add(new GradientStop(Color.FromArgb(65, 6, 78, 59), 1.0));
            }
            else
            {
                rimGrad.GradientStops.Add(new GradientStop(Color.FromArgb(isHover ? (byte)195 : (byte)155, 186, 230, 253), 0.0));
                rimGrad.GradientStops.Add(new GradientStop(Color.FromArgb(isHover ? (byte)145 : (byte)110, 110, 231, 183), 0.48));
                rimGrad.GradientStops.Add(new GradientStop(Color.FromArgb(60, 30, 41, 59), 1.0));
            }
            rimGrad.Freeze();

            Pen bezelPen = new Pen(rimGrad, 1.05);
            bezelPen.Freeze();
            dc.DrawRoundedRectangle(
                null,
                bezelPen,
                new Rect(inset + 0.5, inset + 0.5, Math.Max(1.0, iw - 1.0), Math.Max(1.0, ih - 1.0)),
                ir,
                ir);
        }
    }

    // THE ONE BUTTON: Sculpted 3D Hardware Actuator with Spring Motion + Liquid Color Morph on Click
    public sealed class ChromeActuatorButton : Grid
    {
        private readonly ScaleTransform pressScale = new ScaleTransform(1.0, 1.0);
        private readonly TranslateTransform arrowShift = new TranslateTransform(0.0, 0.0);
        private readonly KineticCapsuleSurface surface;
        private readonly KineticButtonGlyph statusGlyph;
        private readonly TextBlock mainLabel;
        private readonly Border rightChipBorder;
        private readonly TextBlock rightMetricLabel;
        private readonly System.Windows.Shapes.Path arrowIcon;
        private readonly TextBlock tipBody;

        private HausPalette palette = Theme.Obsidian;
        private MemoryPressure currentPressure = MemoryPressure.Healthy;
        private long cachedGainBytes = 1200L * 1048576L;
        private string cachedCelebration = "";
        private string sessionLastSummary = "";
        private int sessionRunCount;
        private long sessionTotalFreedBytes;
        private bool isWorkingExternal;
        private bool isSweepAnimating;
        private bool isHover;

        private double sweepProgress;
        private double spinDeg;
        private DateTime sweepStartUtc;
        private Point lastClickPoint = new Point(150, 24);
        private System.Windows.Threading.DispatcherTimer motionTimer;

        public event EventHandler Invoked;

        public ChromeActuatorButton()
        {
            Height = 48;
            Cursor = Cursors.Hand;
            RenderTransformOrigin = new Point(0.5, 0.5);
            RenderTransform = pressScale;

            surface = new KineticCapsuleSurface();
            Children.Add(surface);

            Grid contentGrid = new Grid
            {
                Margin = new Thickness(11, 0, 11, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            statusGlyph = new KineticButtonGlyph
            {
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(statusGlyph, 0);

            mainLabel = Theme.Text("Harmonize", 13.0, FontWeights.SemiBold, Brushes.White);
            mainLabel.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(mainLabel, 1);

            StackPanel rightRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            rightMetricLabel = Theme.Text("+1.2 GB", 11.0, FontWeights.Bold, Theme.BrushHex(0xFFA7F3D0u));
            rightMetricLabel.VerticalAlignment = VerticalAlignment.Center;

            Geometry arrowGeom = Geometry.Parse("M 4,12 L 19,12 M 13,6 L 19,12 L 13,18");
            arrowGeom.Freeze();
            arrowIcon = Theme.StrokeIcon(arrowGeom, 11.5, Brushes.White, 2.0);
            arrowIcon.Margin = new Thickness(5, 0, 0, 0);
            arrowIcon.RenderTransform = arrowShift;

            rightRow.Children.Add(rightMetricLabel);
            rightRow.Children.Add(arrowIcon);

            rightChipBorder = new Border
            {
                CornerRadius = new CornerRadius(13),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 4.5, 10, 4.5),
                VerticalAlignment = VerticalAlignment.Center,
                Child = rightRow
            };
            Grid.SetColumn(rightChipBorder, 2);

            contentGrid.Children.Add(statusGlyph);
            contentGrid.Children.Add(mainLabel);
            contentGrid.Children.Add(rightChipBorder);
            Children.Add(contentGrid);

            tipBody = Theme.Text("1-Click Harmonize RAM, GPU VRAM, WSL2 & Physical SSD Junk (Shortcut: Ctrl+Alt+H)", 11, FontWeights.Normal, Theme.Obsidian.InkPrimary);
            tipBody.TextWrapping = TextWrapping.Wrap;
            tipBody.MaxWidth = 320;
            ToolTip = Chrome.Tip(tipBody);

            MouseEnter += delegate
            {
                isHover = true;
                AnimateArrow(2.8);
                SyncVisuals();
            };
            MouseLeave += delegate
            {
                isHover = false;
                AnimateArrow(0.0);
                AnimateScale(1.0, false);
                SyncVisuals();
            };
            MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                lastClickPoint = e.GetPosition(this);
                if (!isWorkingExternal && !isSweepAnimating)
                {
                    AnimateScale(0.962, false);
                }
            };
            MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                lastClickPoint = e.GetPosition(this);
                AnimateScale(1.0, true);
                if (IsMouseOver && !isWorkingExternal && !isSweepAnimating)
                {
                    StartClickSweep();
                    EventHandler h = Invoked;
                    if (h != null) h(this, EventArgs.Empty);
                }
            };

            SyncVisuals();
        }

        private void AnimateScale(double target, bool springBounce)
        {
            DoubleAnimation anim = new DoubleAnimation(
                target,
                new Duration(TimeSpan.FromMilliseconds(springBounce ? 310 : 85)));
            if (springBounce)
            {
                anim.EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.42 };
            }
            pressScale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            pressScale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        }

        private void AnimateArrow(double dx)
        {
            DoubleAnimation anim = new DoubleAnimation(dx, new Duration(TimeSpan.FromMilliseconds(150)));
            arrowShift.BeginAnimation(TranslateTransform.XProperty, anim);
        }

        private void StartClickSweep()
        {
            isSweepAnimating = true;
            sweepProgress = 0.04;
            spinDeg = 0.0;
            sweepStartUtc = DateTime.UtcNow;

            if (motionTimer == null)
            {
                motionTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(16)
                };
                motionTimer.Tick += delegate
                {
                    double elapsedMs = (DateTime.UtcNow - sweepStartUtc).TotalMilliseconds;
                    spinDeg = (spinDeg + 14.0) % 360.0;

                    if (elapsedMs < 640.0)
                    {
                        sweepProgress = Math.Min(1.0, elapsedMs / 640.0);
                    }
                    else if (isWorkingExternal)
                    {
                        sweepProgress = 0.94;
                    }
                    else
                    {
                        isSweepAnimating = false;
                        sweepProgress = 1.0;
                        motionTimer.Stop();
                    }
                    SyncVisuals();
                };
            }
            if (!motionTimer.IsEnabled) motionTimer.Start();
            SyncVisuals();
        }

        public void SetPreviewClickState(double sweepFrac, bool celebrated)
        {
            if (sweepFrac > 0.0 && sweepFrac < 1.0)
            {
                isSweepAnimating = true;
                sweepProgress = sweepFrac;
                spinDeg = 125.0;
                lastClickPoint = new Point(112, 24);
            }
            else
            {
                isSweepAnimating = false;
                sweepProgress = celebrated ? 1.0 : 0.0;
            }
            if (celebrated && string.IsNullOrEmpty(cachedCelebration))
            {
                cachedCelebration = "Harmonized \u00B7 +1.2 GB";
            }
            SyncVisuals();
        }

        public void SetSessionTelemetry(string lastSummary, int sessionCount, long sessionFreedBytes)
        {
            sessionLastSummary = lastSummary ?? "";
            sessionRunCount = Math.Max(0, sessionCount);
            sessionTotalFreedBytes = Math.Max(0L, sessionFreedBytes);
        }

        public void Apply(HausPalette pal, MemoryPressure pressure, long estimatedGainBytes, bool working, string celebrationText)
        {
            palette = pal ?? Theme.Obsidian;
            currentPressure = pressure;
            cachedGainBytes = estimatedGainBytes;
            isWorkingExternal = working;
            cachedCelebration = celebrationText ?? "";

            if (working && !isSweepAnimating)
            {
                StartClickSweep();
                return;
            }

            SyncVisuals();
        }

        private void SyncVisuals()
        {
            bool activeSweep = isSweepAnimating || isWorkingExternal;
            bool celebrating = !activeSweep && !string.IsNullOrEmpty(cachedCelebration);

            surface.SetVisualState(isHover, activeSweep, sweepProgress, celebrating, lastClickPoint);

            Color dotCol = currentPressure == MemoryPressure.Critical
                ? Color.FromArgb(255, 248, 113, 113)
                : (currentPressure == MemoryPressure.Warning
                    ? Color.FromArgb(255, 251, 191, 36)
                    : Color.FromArgb(255, 62, 224, 174));

            statusGlyph.SetState(dotCol, activeSweep, celebrating, spinDeg);

            if (activeSweep)
            {
                int pct = Math.Max(8, Math.Min(99, (int)Math.Round(sweepProgress * 100.0)));
                Theme.Set(mainLabel, "Harmonizing\u2026");
                Theme.Set(mainLabel, Brushes.White);
                rightChipBorder.Background = Theme.BrushHex(0x36040914u);
                rightChipBorder.BorderBrush = Theme.BrushHex(0x48FFFFFFu);
                Theme.Set(rightMetricLabel, pct.ToString(CultureInfo.InvariantCulture) + "%");
                Theme.Set(rightMetricLabel, Brushes.White);
                arrowIcon.Visibility = Visibility.Collapsed;
                return;
            }

            if (celebrating)
            {
                Theme.Set(mainLabel, "Harmonized");
                Theme.Set(mainLabel, Brushes.White);
                rightChipBorder.Background = Theme.BrushHex(0x38041A12u);
                rightChipBorder.BorderBrush = Theme.BrushHex(0x52A7F3D0u);
                string badge = ExtractGainChip(cachedCelebration, cachedGainBytes);
                Theme.Set(rightMetricLabel, badge);
                Theme.Set(rightMetricLabel, Theme.BrushHex(0xFFECFDF5u));
                arrowIcon.Visibility = Visibility.Collapsed;
                Theme.Set(tipBody, cachedCelebration + "\nAll active apps, audio & dev servers remain protected.");
                return;
            }

            // Sculpted 3D Idle State
            Theme.Set(mainLabel, "Harmonize");
            Theme.Set(mainLabel, Theme.BrushHex(0xFFF8FAFCu));

            rightChipBorder.Background = Theme.BrushHex(isHover ? 0x46060B16u : 0x3A060A14u);
            rightChipBorder.BorderBrush = Theme.BrushHex(isHover ? 0x5293C5FDu : 0x387DD3FCu);

            string gainStr = cachedGainBytes >= 48L * 1048576L
                ? ("+" + Fmt.BytesShort(cachedGainBytes))
                : "Optimal";
            Theme.Set(rightMetricLabel, gainStr);
            Theme.Set(rightMetricLabel, Theme.BrushHex(0xFFA7F3D0u));

            arrowIcon.Stroke = Theme.BrushHex(0xFFE2E8F0u);
            arrowIcon.Visibility = Visibility.Visible;
            string idleTip = "1-Click Harmonize RAM, GPU VRAM, WSL2 & Physical SSD Junk (Shortcut: Ctrl+Alt+H)";
            if (sessionRunCount > 0 || !string.IsNullOrEmpty(sessionLastSummary))
            {
                if (!string.IsNullOrEmpty(sessionLastSummary))
                {
                    idleTip += "\n" + sessionLastSummary;
                }
                if (sessionRunCount > 0)
                {
                    idleTip += "\nSession Total (" + sessionRunCount.ToString(CultureInfo.InvariantCulture) + "x): +" + Fmt.BytesShort(sessionTotalFreedBytes);
                }
            }
            Theme.Set(tipBody, idleTip);
        }

        private static string ExtractGainChip(string summary, long estBytes)
        {
            if (!string.IsNullOrEmpty(summary))
            {
                int plusIdx = summary.IndexOf('+');
                if (plusIdx >= 0)
                {
                    string sub = summary.Substring(plusIdx).Trim();
                    int dotIdx = sub.IndexOf('\u00B7');
                    if (dotIdx > 0) sub = sub.Substring(0, dotIdx).Trim();
                    if (sub.Length <= 14) return sub;
                }
            }
            if (estBytes >= 48L * 1048576L) return "+" + Fmt.BytesShort(estBytes);
            return "Optimal";
        }
    }
}
