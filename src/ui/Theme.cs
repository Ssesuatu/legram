using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Legram
{
    public sealed class HausPalette
    {
        public bool IsPorcelain;

        // Double-Shell Hardware Materials
        public Brush OuterShellFill;
        public Brush OuterShellRim;
        public Brush InnerChassisFill;
        public Brush InnerChassisRim;

        // Recessed Instrument Wells & Tiles
        public Brush WellFill;
        public SolidColorBrush WellBorder;
        public Brush TileFill;
        public SolidColorBrush TileBorder;
        public SolidColorBrush IconWellFill;
        public SolidColorBrush PillBadgeFill;
        public SolidColorBrush Hairline;
        public SolidColorBrush RulerTrack;

        // Typography Inks
        public SolidColorBrush InkPrimary;
        public SolidColorBrush InkSecondary;
        public SolidColorBrush InkMuted;
        public SolidColorBrush LeadingZeroInk;

        // The One Button (Tactile Sculptural Capsule)
        public Brush ButtonFill;
        public Brush ButtonHoverFill;
        public Brush ButtonPressedFill;
        public Brush ButtonCelebrationFill;
        public Brush ButtonBezel;
        public SolidColorBrush ButtonInk;
        public SolidColorBrush ButtonSubInk;
        public SolidColorBrush ButtonPillFill;
        public SolidColorBrush ButtonPillInk;

        // Semantic Calm Signal Pigments
        public SolidColorBrush Mint;
        public SolidColorBrush MintBg;
        public SolidColorBrush Amber;
        public SolidColorBrush AmberBg;
        public SolidColorBrush Coral;
        public SolidColorBrush CoralBg;
        public SolidColorBrush Ice;
        public SolidColorBrush IceBg;

        public Color AuraHealthy;
        public Color AuraWarning;
        public Color AuraCritical;
        public Color AuraBoosting;

        public SolidColorBrush SignalBrush(MemoryPressure p)
        {
            switch (p)
            {
                case MemoryPressure.Critical: return Coral;
                case MemoryPressure.Warning: return Amber;
                case MemoryPressure.Boosting: return Ice;
                default: return Mint;
            }
        }

        public SolidColorBrush SignalBgBrush(MemoryPressure p)
        {
            switch (p)
            {
                case MemoryPressure.Critical: return CoralBg;
                case MemoryPressure.Warning: return AmberBg;
                case MemoryPressure.Boosting: return IceBg;
                default: return MintBg;
            }
        }

        public Color SignalAura(MemoryPressure p)
        {
            switch (p)
            {
                case MemoryPressure.Critical: return AuraCritical;
                case MemoryPressure.Warning: return AuraWarning;
                case MemoryPressure.Boosting: return AuraBoosting;
                default: return AuraHealthy;
            }
        }
    }

    public static class Theme
    {
        public static readonly FontFamily Font = LoadFont();

        public static readonly HausPalette Obsidian = CreateObsidian();
        public static readonly HausPalette Porcelain = CreatePorcelain();

        // =====================================================================
        // UNIFIED 24x24 PRECISION GEOMETRIC ICON SYSTEM (1.7px Rounded Stroke)
        // =====================================================================

        // 1. Brand Optical Core (3-Bar Telemetry Aperture inside Lens)
        public static readonly Geometry HausBarsGeom = ParseGeom(
            "M7.5,9.2 L7.5,14.8 M12,6.2 L12,17.8 M16.5,8.4 L16.5,15.6");

        // 2. Hardware Telemetry Icons (RAM 3-Layers, GPU Silicon Die, SSD Storage Slot, Guard Shield)
        public static readonly Geometry RamLayersGeom = ParseGeom(
            "M12,3.2 L20.5,7.8 L12,12.4 L3.5,7.8 Z M3.5,12.1 L12,16.7 L20.5,12.1 M3.5,16.3 L12,20.9 L20.5,16.3");

        public static readonly Geometry GpuDieGeom = ParseGeom(
            "M6.5,6.5 L17.5,6.5 L17.5,17.5 L6.5,17.5 Z M9.8,9.8 L14.2,9.8 L14.2,14.2 L9.8,14.2 Z " +
            "M9.2,3.5 L9.2,6.5 M14.8,3.5 L14.8,6.5 M9.2,17.5 L9.2,20.5 M14.8,17.5 L14.8,20.5 " +
            "M3.5,9.2 L6.5,9.2 M3.5,14.8 L6.5,14.8 M17.5,9.2 L20.5,9.2 M17.5,14.8 L20.5,14.8");

        public static readonly Geometry SsdVaultGeom = ParseGeom(
            "M4.2,6.5 C4.2,5.3 5.2,4.3 6.4,4.3 L17.6,4.3 C18.8,4.3 19.8,5.3 19.8,6.5 L19.8,17.5 C19.8,18.7 18.8,19.7 17.6,19.7 L6.4,19.7 C5.2,19.7 4.2,18.7 4.2,17.5 Z " +
            "M4.2,14.2 L19.8,14.2 M8,17 L11.5,17 M16.2,17 L16.4,17");

        public static readonly Geometry ShieldGuardGeom = ParseGeom(
            "M12,2.8 L19.2,5.8 L19.2,11.6 C19.2,16.3 16.1,19.9 12,21.4 C7.9,19.9 4.8,16.3 4.8,11.6 L4.8,5.8 Z " +
            "M9.1,12.1 L11.1,14.1 L15.2,9.9");

        // 3. Process Category Vector Icons (2x2 Bento Grid from Inspiration #8, Globe, Terminal, Media, AI Core)
        public static readonly Geometry BentoGridGeom = ParseGeom(
            "M4.2,4.2 L10.3,4.2 L10.3,10.3 L4.2,10.3 Z M13.7,4.2 L19.8,4.2 L19.8,8.8 L13.7,8.8 Z " +
            "M4.2,13.7 L10.3,13.7 L10.3,19.8 L4.2,19.8 Z M13.7,12.2 L19.8,12.2 L19.8,19.8 L13.7,19.8 Z");

        public static readonly Geometry BrowserGlobeGeom = ParseGeom(
            "M12,3.5 A8.5,8.5 0 1,1 11.99,3.5 M3.8,12 L20.2,12 M12,3.5 C14.6,6.1 15.8,9.0 15.8,12 C15.8,15.0 14.6,17.9 12,20.5 C9.4,17.9 8.2,15.0 8.2,12 C8.2,9.0 9.4,6.1 12,3.5 Z");

        public static readonly Geometry TerminalNodeGeom = ParseGeom(
            "M4.2,5.2 L19.8,5.2 L19.8,18.8 L4.2,18.8 Z M7.8,9.5 L11.0,12.0 L7.8,14.5 M12.6,14.8 L16.2,14.8");

        public static readonly Geometry CreativeApertureGeom = ParseGeom(
            "M12,3.5 A8.5,8.5 0 1,1 11.99,3.5 M12,8.5 A3.5,3.5 0 1,1 11.99,8.5 M12,3.5 L14.5,8.8 M20.1,9.5 L15.2,12.6 M17.2,18.8 L12.2,15.5 M6.8,18.8 L9.4,13.8 M3.9,9.5 L9.2,10.2");

        public static readonly Geometry AiPulseCoreGeom = ParseGeom(
            "M12,3.2 L14.1,9.9 L20.8,12 L14.1,14.1 L12,20.8 L9.9,14.1 L3.2,12 L9.9,9.9 Z");

        // 4. Action & Menu Icons
        public static readonly Geometry CheckGeom = ParseGeom("M19.5,6.8 L9.8,16.8 L4.8,11.8");
        public static readonly Geometry PaletteLensGeom = ParseGeom(
            "M12,3.5 A8.5,8.5 0 1,1 11.99,3.5 M12,3.5 L12,20.5");
        public static readonly Geometry CapsuleMiniGeom = ParseGeom(
            "M3.5,8.2 C3.5,6.2 5.1,4.6 7.1,4.6 L16.9,4.6 C18.9,4.6 20.5,6.2 20.5,8.2 L20.5,15.8 C20.5,17.8 18.9,19.4 16.9,19.4 L7.1,19.4 C5.1,19.4 3.5,17.8 3.5,15.8 Z M8.2,12 L15.8,12");
        public static readonly Geometry PinGeom = ParseGeom(
            "M9,3.8 L15,3.8 L14.2,9.6 L17.2,12.4 L17.2,14.0 L6.8,14.0 L6.8,12.4 L9.8,9.6 Z M12,14.0 L12,20.5");
        public static readonly Geometry EcoLeafGeom = ParseGeom(
            "M19.5,4.5 C11.5,4.5 5.5,9.2 5.5,16.5 C5.5,17.9 6.1,19.0 7.2,19.5 C14.5,19.5 19.5,13.5 19.5,4.5 Z M5.5,19.5 L13.5,11.5");
        public static readonly Geometry KernelBoltGeom = ParseGeom(
            "M13.2,3.2 L5.8,13.2 L11.6,13.2 L10.8,20.8 L18.2,10.8 L12.4,10.8 Z");
        public static readonly Geometry StartupWinGeom = ParseGeom(
            "M4,5.5 L11,4.5 L11,11.3 L4,11.3 Z M12.5,4.3 L20,3.2 L20,11.3 L12.5,11.3 Z M4,12.7 L11,12.7 L11,19.5 L4,18.5 Z M12.5,12.7 L20,12.7 L20,20.8 L12.5,19.7 Z");
        public static readonly Geometry TrayDownGeom = ParseGeom(
            "M12,4.2 L12,15.2 M7.8,11.2 L12,15.5 L16.2,11.2 M4.5,19.2 L19.5,19.2");
        public static readonly Geometry PowerQuitGeom = ParseGeom(
            "M12,3.5 L12,11.5 M7.2,6.2 A7.8,7.8 0 1,0 16.8,6.2");
        public static readonly Geometry MinimizeLineGeom = ParseGeom(
            "M6.2,12.2 L17.8,12.2");
        public static readonly Geometry CloseCrossGeom = ParseGeom(
            "M7.4,7.4 L16.6,16.6 M16.6,7.4 L7.4,16.6");
        public static readonly Geometry GameControllerGeom = ParseGeom(
            "M6.5,8.2 L17.5,8.2 C19.5,8.2 21,9.9 20.6,12.1 L19.8,16.4 C19.5,17.9 18,18.6 16.8,17.6 L14.5,15.5 L9.5,15.5 L7.2,17.6 C6,18.6 4.5,17.9 4.2,16.4 L3.4,12.1 C3,9.9 4.5,8.2 6.5,8.2 Z " +
            "M7.2,11.8 L10.2,11.8 M8.7,10.3 L8.7,13.3 M15.4,11.2 L15.6,11.2 M17.2,12.6 L17.4,12.6");

        // 5. Three Layout Variant Icons (Monolith Vertical, Atelier Horizontal Deck, Optik Split Tuner)
        public static readonly Geometry LayoutMonolithGeom = ParseGeom(
            "M6.5,3.5 L17.5,3.5 C18.6,3.5 19.5,4.4 19.5,5.5 L19.5,18.5 C19.5,19.6 18.6,20.5 17.5,20.5 L6.5,20.5 C5.4,20.5 4.5,19.6 4.5,18.5 L4.5,5.5 C4.5,4.4 5.4,3.5 6.5,3.5 Z " +
            "M7.8,7.8 L16.2,7.8 M7.8,16.6 L16.2,16.6");
        public static readonly Geometry LayoutDeckGeom = ParseGeom(
            "M7.5,6.2 L16.5,6.2 C19.5,6.2 21.5,8.6 21.5,12.0 C21.5,15.4 19.5,17.8 16.5,17.8 L7.5,17.8 C4.5,17.8 2.5,15.4 2.5,12.0 C2.5,8.6 4.5,6.2 7.5,6.2 Z " +
            "M8.2,12 A2.6,2.6 0 1,1 8.19,12 M13.5,10.2 L18.2,10.2 M13.5,13.8 L17.2,13.8");
        public static readonly Geometry LayoutTunerGeom = ParseGeom(
            "M5.5,4.2 L18.5,4.2 C19.6,4.2 20.5,5.1 20.5,6.2 L20.5,17.8 C20.5,18.9 19.6,19.8 18.5,19.8 L5.5,19.8 C4.4,19.8 3.5,18.9 3.5,17.8 L3.5,6.2 C3.5,5.1 4.4,4.2 5.5,4.2 Z " +
            "M3.8,13.2 L20.2,13.2 M8.2,16.5 L15.8,16.5 M12,7.5 L12,10.5");

        private static HausPalette CreateObsidian()
        {
            HausPalette p = new HausPalette();
            p.IsPorcelain = false;

            // Double-Bezel Brushed Dark Titanium Casing
            p.OuterShellFill = Grad(0xFF252830, 0xFF13151A, 90);
            p.OuterShellRim = Grad3(0x58FFFFFF, 0x1CFFFFFF, 0x30C8D0E0, 125);
            p.InnerChassisFill = Grad(0xFF121318, 0xFF0B0C10, 90);
            p.InnerChassisRim = BrushHex(0x18FFFFFF);

            p.WellFill = Grad(0xFF181A21, 0xFF13151B, 90);
            p.WellBorder = BrushHex(0x22FFFFFF);
            p.TileFill = Grad(0xFF191B22, 0xFF14161C, 90);
            p.TileBorder = BrushHex(0x1EFFFFFF);
            p.IconWellFill = BrushHex(0xFF232630);
            p.PillBadgeFill = BrushHex(0xFF22252F);
            p.Hairline = BrushHex(0xFF232630);
            p.RulerTrack = BrushHex(0xFF2B2F3B);

            p.InkPrimary = BrushHex(0xFFF6F7FA);
            p.InkSecondary = BrushHex(0xFFA7ADBA);
            p.InkMuted = BrushHex(0xFF6E7484);
            p.LeadingZeroInk = BrushHex(0xFF343844);

            // Sculpted Anodized Sapphire-Titanium 3D Glass Actuator on Obsidian Chassis
            p.ButtonFill = Grad(0xFF2E4770, 0xFF152238, 90);
            p.ButtonHoverFill = Grad(0xFF395788, 0xFF1B2C47, 90);
            p.ButtonPressedFill = Grad(0xFF1B2B46, 0xFF0F192B, 90);
            p.ButtonCelebrationFill = Grad(0xFF16A374, 0xFF0A5C40, 90);
            p.ButtonBezel = BrushHex(0x7893C5FD);
            p.ButtonInk = BrushHex(0xFFF8FAFC);
            p.ButtonSubInk = BrushHex(0xFFBAE6FD);
            p.ButtonPillFill = BrushHex(0x38070B12);
            p.ButtonPillInk = BrushHex(0xFFA7F3D0);

            p.Mint = BrushHex(0xFF7CE3A1);
            p.MintBg = BrushHex(0x247CE3A1);
            p.Amber = BrushHex(0xFFFDD663);
            p.AmberBg = BrushHex(0x24FDD663);
            p.Coral = BrushHex(0xFFF68B82);
            p.CoralBg = BrushHex(0x26F68B82);
            p.Ice = BrushHex(0xFF9DC2FF);
            p.IceBg = BrushHex(0x249DC2FF);

            p.AuraHealthy = Color.FromArgb(44, 124, 227, 161);
            p.AuraWarning = Color.FromArgb(46, 253, 214, 99);
            p.AuraCritical = Color.FromArgb(50, 246, 139, 130);
            p.AuraBoosting = Color.FromArgb(56, 157, 194, 255);
            return p;
        }

        private static HausPalette CreatePorcelain()
        {
            HausPalette p = new HausPalette();
            p.IsPorcelain = true;

            // Sculpted Warm Ceramic Outer Shell + Pure Alabaster Inner Wells (Inspiration #3 & #4)
            p.OuterShellFill = Grad(0xFFE9E6DF, 0xFFD8D4CA, 90);
            p.OuterShellRim = Grad3(0xFFFFFFFF, 0xFFE5E2DA, 0xFFC5C0B5, 125);
            p.InnerChassisFill = Grad(0xFFF4F2EC, 0xFFECE9E1, 90);
            p.InnerChassisRim = BrushHex(0xB0FFFFFF);

            p.WellFill = Grad(0xFFFCFBF8, 0xFFF7F5F0, 90);
            p.WellBorder = BrushHex(0xFFE0DDD4);
            p.TileFill = Grad(0xFFFCFBF8, 0xFFF6F4EE, 90);
            p.TileBorder = BrushHex(0xFFE0DDD4);
            p.IconWellFill = BrushHex(0xFFEDEAE2);
            p.PillBadgeFill = BrushHex(0xFFECE9E0);
            p.Hairline = BrushHex(0xFFE0DDD4);
            p.RulerTrack = BrushHex(0xFFD5D1C6);

            p.InkPrimary = BrushHex(0xFF111216);
            p.InkSecondary = BrushHex(0xFF4E535E);
            p.InkMuted = BrushHex(0xFF818693);
            p.LeadingZeroInk = BrushHex(0xFFD2CEC3);

            // Deep Smoked Obsidian Capsule on Porcelain Ceramic (Inspiration #1 & #3)
            p.ButtonFill = Grad(0xFF20222A, 0xFF0E0F14, 90);
            p.ButtonHoverFill = Grad(0xFF2B2E38, 0xFF161820, 90);
            p.ButtonPressedFill = Grad(0xFF0A0B0E, 0xFF0A0B0E, 90);
            p.ButtonCelebrationFill = Grad(0xFF133A26, 0xFF0C2719, 90);
            p.ButtonBezel = BrushHex(0xFF3E4250);
            p.ButtonInk = BrushHex(0xFFF9F8F5);
            p.ButtonSubInk = BrushHex(0xFF9EA4B3);
            p.ButtonPillFill = BrushHex(0x22FFFFFF);
            p.ButtonPillInk = BrushHex(0xFFF4F5F8);

            p.Mint = BrushHex(0xFF15804E);
            p.MintBg = BrushHex(0x2015804E);
            p.Amber = BrushHex(0xFFB56A00);
            p.AmberBg = BrushHex(0x22B56A00);
            p.Coral = BrushHex(0xFFCE2C22);
            p.CoralBg = BrushHex(0x20CE2C22);
            p.Ice = BrushHex(0xFF1A63CC);
            p.IceBg = BrushHex(0x1E1A63CC);

            p.AuraHealthy = Color.FromArgb(38, 118, 198, 255);
            p.AuraWarning = Color.FromArgb(38, 245, 178, 72);
            p.AuraCritical = Color.FromArgb(40, 232, 92, 82);
            p.AuraBoosting = Color.FromArgb(46, 118, 168, 255);
            return p;
        }

        public static HausPalette Palette(bool porcelain)
        {
            return porcelain ? Porcelain : Obsidian;
        }

        public static SolidColorBrush BrushHex(uint argb)
        {
            SolidColorBrush b = new SolidColorBrush(Color.FromArgb(
                (byte)(argb >> 24),
                (byte)(argb >> 16),
                (byte)(argb >> 8),
                (byte)argb));
            b.Freeze();
            return b;
        }

        public static LinearGradientBrush Grad(uint c1, uint c2, double angleDeg)
        {
            double rad = angleDeg * Math.PI / 180.0;
            Point end = new Point(Math.Cos(rad), Math.Sin(rad));
            LinearGradientBrush g = new LinearGradientBrush(
                ToColor(c1), ToColor(c2), new Point(0, 0), end);
            g.Freeze();
            return g;
        }

        public static LinearGradientBrush Grad3(uint c1, uint c2, uint c3, double angleDeg)
        {
            double rad = angleDeg * Math.PI / 180.0;
            LinearGradientBrush g = new LinearGradientBrush();
            g.StartPoint = new Point(0, 0);
            g.EndPoint = new Point(Math.Max(0.1, Math.Abs(Math.Cos(rad))), Math.Max(0.1, Math.Abs(Math.Sin(rad))));
            g.GradientStops.Add(new GradientStop(ToColor(c1), 0.0));
            g.GradientStops.Add(new GradientStop(ToColor(c2), 0.52));
            g.GradientStops.Add(new GradientStop(ToColor(c3), 1.0));
            g.Freeze();
            return g;
        }

        private static Color ToColor(uint argb)
        {
            return Color.FromArgb(
                (byte)(argb >> 24),
                (byte)(argb >> 16),
                (byte)(argb >> 8),
                (byte)argb);
        }

        private static Geometry ParseGeom(string data)
        {
            Geometry g = Geometry.Parse(data);
            g.Freeze();
            return g;
        }

        private static FontFamily LoadFont()
        {
            try
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fonts");
                if (File.Exists(Path.Combine(dir, "Inter-Regular.ttf")))
                {
                    return new FontFamily(
                        new Uri(dir.TrimEnd('\\') + "\\"),
                        "./#Inter, Segoe UI Variable Display, Segoe UI");
                }
            }
            catch (Exception e)
            {
                Log.Error("Theme.LoadFont", e);
            }
            return new FontFamily("Segoe UI Variable Display, Segoe UI");
        }

        public static void Crisp(DependencyObject e)
        {
            TextOptions.SetTextFormattingMode(e, TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(e, TextRenderingMode.Grayscale);
            RenderOptions.SetClearTypeHint(e, ClearTypeHint.Auto);
            FrameworkElement fe = e as FrameworkElement;
            if (fe != null)
            {
                fe.UseLayoutRounding = true;
                fe.SnapsToDevicePixels = true;
            }
        }

        public static TextBlock Text(string s, double size, FontWeight weight, Brush fg)
        {
            TextBlock t = new TextBlock
            {
                Text = s ?? "",
                FontFamily = Font,
                FontSize = size,
                FontWeight = weight,
                Foreground = fg,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Typography.SetNumeralAlignment(t, FontNumeralAlignment.Tabular);
            return t;
        }

        public static void Set(TextBlock t, string text)
        {
            string v = text ?? "";
            if (t.Text != v) t.Text = v;
        }

        public static void Set(TextBlock t, Brush b)
        {
            if (!ReferenceEquals(t.Foreground, b)) t.Foreground = b;
        }

        public static System.Windows.Shapes.Path FillIcon(Geometry g, double size, Brush fill)
        {
            return new System.Windows.Shapes.Path
            {
                Data = g,
                Fill = fill,
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform,
                SnapsToDevicePixels = false,
                IsHitTestVisible = false
            };
        }

        public static System.Windows.Shapes.Path StrokeIcon(Geometry g, double size, Brush stroke, double thickness)
        {
            return new System.Windows.Shapes.Path
            {
                Data = g,
                Stroke = stroke,
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform,
                SnapsToDevicePixels = false,
                IsHitTestVisible = false
            };
        }

        public static Geometry FallbackGeometryForGroup(GroupIconKind kind)
        {
            switch (kind)
            {
                case GroupIconKind.ProtectedShield: return ShieldGuardGeom;
                case GroupIconKind.Browser: return BrowserGlobeGeom;
                case GroupIconKind.TerminalNode: return TerminalNodeGeom;
                case GroupIconKind.CreativeMedia: return CreativeApertureGeom;
                case GroupIconKind.AiSparkle: return AiPulseCoreGeom;
                default: return BentoGridGeom;
            }
        }
    }

    // Zero-Leak Cached Native Executable Logo Extractor (Frozen 32x32 ImageSource)
    public static class AppIconCache
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, ImageSource> Cache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        public static ImageSource TryGet(ProcessGroupItem item)
        {
            if (item == null || item.IsOrphanGroup || item.GroupKey == "protected-dev") return null;
            if (item.IsProtected && !item.IsUserWhitelisted && string.IsNullOrEmpty(item.RepresentativeExePath)) return null;
            if (string.IsNullOrEmpty(item.GroupKey) || string.IsNullOrEmpty(item.RepresentativeExePath)) return null;

            lock (Sync)
            {
                ImageSource cached;
                if (Cache.TryGetValue(item.GroupKey, out cached)) return cached;

                if (Cache.Count >= 32) Cache.Clear();

                ImageSource loaded = ExtractFrozenIcon(item.RepresentativeExePath);
                Cache[item.GroupKey] = loaded;
                return loaded;
            }
        }

        private static ImageSource ExtractFrozenIcon(string exePath)
        {
            try
            {
                if (!File.Exists(exePath)) return null;
                using (System.Drawing.Icon ico = System.Drawing.Icon.ExtractAssociatedIcon(exePath))
                {
                    if (ico == null) return null;
                    BitmapSource bs = Imaging.CreateBitmapSourceFromHIcon(
                        ico.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromWidthAndHeight(32, 32));
                    bs.Freeze();
                    return bs;
                }
            }
            catch
            {
                return null;
            }
        }
    }

    public static class Chrome
    {
        private const string Ns = "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'";

        private static T Parse<T>(string xaml)
        {
            return (T)XamlReader.Parse(xaml);
        }

        public static ToolTip Tip(object content)
        {
            ToolTip tip = new ToolTip
            {
                Content = content,
                HasDropShadow = false,
                Placement = PlacementMode.Top,
                VerticalOffset = -6,
                FontFamily = Theme.Font
            };
            tip.Template = Parse<ControlTemplate>(
                "<ControlTemplate " + Ns + " TargetType='ToolTip'>" +
                "<Border Background='#14161C' BorderBrush='#3AFFFFFF' BorderThickness='1' CornerRadius='13' Padding='12,9,12,9' SnapsToDevicePixels='True'>" +
                "<ContentPresenter/></Border></ControlTemplate>");
            Theme.Crisp(tip);
            return tip;
        }

        public static ContextMenu Menu()
        {
            ContextMenu menu = new ContextMenu
            {
                HasDropShadow = false,
                FontFamily = Theme.Font,
                FontSize = 12,
                Foreground = Theme.Obsidian.InkPrimary,
                MinWidth = 264
            };
            menu.Template = Parse<ControlTemplate>(
                "<ControlTemplate " + Ns + " TargetType='ContextMenu'>" +
                "<Border Background='#13151B' BorderBrush='#38FFFFFF' BorderThickness='1' CornerRadius='16' Padding='6' SnapsToDevicePixels='True'>" +
                "<ItemsPresenter/></Border></ControlTemplate>");
            Theme.Crisp(menu);
            return menu;
        }

        private static readonly ControlTemplate ItemTemplate = Parse<ControlTemplate>(
            "<ControlTemplate " + Ns + " TargetType='MenuItem'>" +
            "<Border x:Name='Bd' Height='32' CornerRadius='9' Padding='10,0,10,0' Background='Transparent' SnapsToDevicePixels='True'>" +
            "<ContentPresenter ContentSource='Header' VerticalAlignment='Center' RecognizesAccessKey='False'/></Border>" +
            "<ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'>" +
            "<Setter TargetName='Bd' Property='Background' Value='#1EFFFFFF'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");

        private static readonly ControlTemplate SeparatorTemplate = Parse<ControlTemplate>(
            "<ControlTemplate " + Ns + " TargetType='Separator'>" +
            "<Border Height='1' Margin='8,5,8,5' Background='#1AFFFFFF'/></ControlTemplate>");

        public static MenuItem IconItem(Geometry iconGeom, Brush iconBrush, string text, string rightTag, Brush rightBrush, Action click)
        {
            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            System.Windows.Shapes.Path ico = Theme.StrokeIcon(iconGeom, 14, iconBrush ?? Theme.Obsidian.InkSecondary, 1.7);
            ico.HorizontalAlignment = HorizontalAlignment.Left;
            ico.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(ico, 0);

            TextBlock lbl = Theme.Text(text, 12, FontWeights.Medium, Theme.Obsidian.InkPrimary);
            lbl.Margin = new Thickness(4, 0, 12, 0);
            Grid.SetColumn(lbl, 1);

            row.Children.Add(ico);
            row.Children.Add(lbl);

            if (!string.IsNullOrEmpty(rightTag))
            {
                TextBlock tag = Theme.Text(rightTag, 10.5, FontWeights.SemiBold, rightBrush ?? Theme.Obsidian.InkMuted);
                Grid.SetColumn(tag, 2);
                row.Children.Add(tag);
            }

            MenuItem mi = new MenuItem { Header = row, Template = ItemTemplate };
            mi.Click += delegate { click(); };
            return mi;
        }

        public static Separator Line()
        {
            return new Separator { Template = SeparatorTemplate };
        }
    }

    // Dynamic Numeric Hardware Micro-Badge for System Tray (1% live RAM tabular readout + semantic status rim + Working state)
    public static class TrayGlyph
    {
        private static readonly object GdiSync = new object();
        private static System.Drawing.StringFormat cachedFormat;
        private static readonly Dictionary<int, System.Drawing.Font> FontCacheByTenthPx = new Dictionary<int, System.Drawing.Font>();

        public static bool IsLightTaskbar()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = k == null ? null : k.GetValue("SystemUsesLightTheme");
                    return v is int && (int)v == 1;
                }
            }
            catch
            {
                return false;
            }
        }

        public static System.Drawing.Bitmap MakeBitmap(int size, int usedPercent, MemoryPressure pressure, bool lightTaskbar, bool isWorking)
        {
            if (size < 16) size = 16;
            usedPercent = Math.Max(0, Math.Min(99, usedPercent));

            System.Drawing.Color sig;
            if (isWorking || pressure == MemoryPressure.Boosting)
                sig = System.Drawing.Color.FromArgb(255, 86, 204, 242); // Dichroic Cyan
            else if (pressure == MemoryPressure.Critical)
                sig = System.Drawing.Color.FromArgb(255, 248, 113, 113); // Coral
            else if (pressure == MemoryPressure.Warning)
                sig = System.Drawing.Color.FromArgb(255, 251, 191, 36);  // Amber
            else
                sig = System.Drawing.Color.FromArgb(255, 110, 231, 183); // Mint

            System.Drawing.Color plateFill = lightTaskbar
                ? System.Drawing.Color.FromArgb(248, 20, 23, 31)
                : System.Drawing.Color.FromArgb(245, 15, 17, 24);

            System.Drawing.Color numColor = isWorking
                ? System.Drawing.Color.FromArgb(255, 165, 243, 252)
                : (pressure == MemoryPressure.Critical
                    ? System.Drawing.Color.FromArgb(255, 254, 202, 202)
                    : (pressure == MemoryPressure.Warning
                        ? System.Drawing.Color.FromArgb(255, 254, 240, 138)
                        : System.Drawing.Color.FromArgb(255, 244, 247, 252)));

            System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.Clear(System.Drawing.Color.Transparent);

                float pad = size >= 24 ? 1.0f : 0.6f;
                float side = size - pad * 2.0f;
                float radius = Math.Max(3.2f, size * 0.26f);

                using (System.Drawing.Drawing2D.GraphicsPath path = RoundedRect(pad, pad, side, side, radius))
                {
                    using (System.Drawing.Brush bgBrush = new System.Drawing.SolidBrush(plateFill))
                    {
                        g.FillPath(bgBrush, path);
                    }
                    float rimW = size >= 24 ? 1.45f : 1.15f;
                    using (System.Drawing.Pen rimPen = new System.Drawing.Pen(
                        System.Drawing.Color.FromArgb(isWorking ? 235 : 175, sig.R, sig.G, sig.B), rimW))
                    {
                        g.DrawPath(rimPen, path);
                    }
                }

                // Bottom semantic fill bar inside squircle showing exact memory fill proportion
                float barMargin = Math.Max(2.5f, size * 0.18f);
                float barMaxW = Math.Max(4.0f, size - barMargin * 2.0f);
                float barH = Math.Max(1.6f, size * 0.095f);
                float barY = size - pad - barH - (size >= 24 ? 2.1f : 1.4f);
                using (System.Drawing.Brush trackB = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(55, 255, 255, 255)))
                {
                    g.FillRectangle(trackB, barMargin, barY, barMaxW, barH);
                }
                float fillFrac = isWorking ? 1.0f : Math.Max(0.14f, usedPercent / 100.0f);
                using (System.Drawing.Brush fillB = new System.Drawing.SolidBrush(sig))
                {
                    g.FillRectangle(fillB, barMargin, barY, barMaxW * fillFrac, barH);
                }

                if (isWorking)
                {
                    // Sleek lightning bolt glyph during active harmonization
                    float cx = size * 0.5f;
                    float cy = size * 0.43f;
                    float s = size * 0.24f;
                    System.Drawing.PointF[] bolt = new System.Drawing.PointF[]
                    {
                        new System.Drawing.PointF(cx + s * 0.18f, cy - s * 1.05f),
                        new System.Drawing.PointF(cx - s * 0.72f, cy + s * 0.08f),
                        new System.Drawing.PointF(cx - s * 0.05f, cy + s * 0.08f),
                        new System.Drawing.PointF(cx - s * 0.22f, cy + s * 1.05f),
                        new System.Drawing.PointF(cx + s * 0.72f, cy - s * 0.10f),
                        new System.Drawing.PointF(cx + s * 0.06f, cy - s * 0.10f)
                    };
                    using (System.Drawing.Brush boltB = new System.Drawing.SolidBrush(sig))
                    {
                        g.FillPolygon(boltB, bolt);
                    }
                }
                else
                {
                    string txt = usedPercent.ToString(CultureInfo.InvariantCulture);
                    float emPx = Math.Max(7.2f, txt.Length >= 2 ? (size * 0.50f) : (size * 0.56f));
                    int keyTenthPx = (int)Math.Round(emPx * 10.0f);
                    using (System.Drawing.Brush txtB = new System.Drawing.SolidBrush(numColor))
                    {
                        System.Drawing.RectangleF rc = new System.Drawing.RectangleF(0, -size * 0.055f, size, size);
                        lock (GdiSync)
                        {
                            if (cachedFormat == null)
                            {
                                cachedFormat = new System.Drawing.StringFormat(System.Drawing.StringFormat.GenericTypographic);
                                cachedFormat.Alignment = System.Drawing.StringAlignment.Center;
                                cachedFormat.LineAlignment = System.Drawing.StringAlignment.Center;
                            }
                            System.Drawing.Font cachedFont;
                            if (!FontCacheByTenthPx.TryGetValue(keyTenthPx, out cachedFont))
                            {
                                if (FontCacheByTenthPx.Count > 12)
                                {
                                    foreach (System.Drawing.Font oldFont in FontCacheByTenthPx.Values)
                                    {
                                        try { oldFont.Dispose(); } catch { }
                                    }
                                    FontCacheByTenthPx.Clear();
                                }
                                cachedFont = new System.Drawing.Font("Segoe UI", keyTenthPx / 10.0f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
                                FontCacheByTenthPx[keyTenthPx] = cachedFont;
                            }
                            g.DrawString(txt, cachedFont, txtB, rc, cachedFormat);
                        }
                    }
                }
            }
            return bmp;
        }

        public static System.Drawing.Icon Make(int size, int usedPercent, MemoryPressure pressure, bool lightTaskbar, bool isWorking)
        {
            using (System.Drawing.Bitmap bmp = MakeBitmap(size, usedPercent, pressure, lightTaskbar, isWorking))
            {
                IntPtr hIcon = bmp.GetHicon();
                return System.Drawing.Icon.FromHandle(hIcon);
            }
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
        {
            float d = r * 2.0f;
            System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + w - d, y, d, d, 270, 90);
            path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            path.AddArc(x, y + h - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
