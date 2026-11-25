using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Legram
{
    public static class Log
    {
        private static readonly object Sync = new object();

        public static string DirectoryPath
        {
            get
            {
                string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(baseDir, "Legram");
            }
        }

        public static string LogFilePath
        {
            get { return Path.Combine(DirectoryPath, "legram.log"); }
        }

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Error(string context, Exception ex)
        {
            Write("ERR ", context + ": " + (ex == null ? "null" : ex.GetType().Name + " - " + ex.Message));
        }

        private static void Write(string level, string message)
        {
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(DirectoryPath);
                    FileInfo fi = new FileInfo(LogFilePath);
                    if (fi.Exists && fi.Length > 262144)
                    {
                        string bak = LogFilePath + ".old";
                        if (File.Exists(bak)) File.Delete(bak);
                        File.Move(LogFilePath, bak);
                    }
                    string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
                        + " [" + level + "] " + message + Environment.NewLine;
                    File.AppendAllText(LogFilePath, line, Encoding.UTF8);
                }
            }
            catch
            {
            }
        }
    }

    public sealed class AppSettings
    {
        private readonly object immuneLock = new object();
        public double WindowX = double.NaN;
        public double WindowY = double.NaN;
        public bool ShowWindow = true;
        public bool FoldedLens = false;
        public bool Pinned = true;
        public bool AutoGuardEnabled = true;
        public bool EcoQosEnabled = true;
        public bool GameModeAutoPause = true;
        public bool VirtualDesktopSleepEnabled = true;
        public readonly HashSet<string> ImmuneGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static string FilePath
        {
            get { return Path.Combine(Log.DirectoryPath, "ui-state.json"); }
        }

        public bool IsGroupImmune(string groupKeyOrBaseName)
        {
            if (string.IsNullOrEmpty(groupKeyOrBaseName)) return false;
            lock (immuneLock)
            {
                return ImmuneGroups.Contains(groupKeyOrBaseName.Trim());
            }
        }

        public bool SetGroupImmune(string groupKey, bool immune)
        {
            if (string.IsNullOrEmpty(groupKey)) return false;
            string clean = groupKey.Trim().ToLowerInvariant();
            if (clean == "protected-dev" || clean == "orphan-ai") return false;
            bool changed;
            lock (immuneLock)
            {
                if (immune) changed = ImmuneGroups.Add(clean);
                else changed = ImmuneGroups.Remove(clean);
            }
            if (changed) Save();
            return changed;
        }

        public bool ToggleGroupImmune(string groupKey)
        {
            if (string.IsNullOrEmpty(groupKey)) return false;
            bool nowImmune = !IsGroupImmune(groupKey);
            SetGroupImmune(groupKey, nowImmune);
            return nowImmune;
        }

        public HashSet<string> SnapshotImmuneGroups()
        {
            lock (immuneLock)
            {
                return new HashSet<string>(ImmuneGroups, StringComparer.OrdinalIgnoreCase);
            }
        }

        public static AppSettings Load()
        {
            AppSettings s = new AppSettings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                string json = File.ReadAllText(FilePath, Encoding.UTF8);
                s.WindowX = ReadDouble(json, "windowX", double.NaN);
                s.WindowY = ReadDouble(json, "windowY", double.NaN);
                s.ShowWindow = ReadBool(json, "showWindow", true);
                s.FoldedLens = ReadBool(json, "foldedLens", false);
                s.Pinned = ReadBool(json, "pinned", true);
                s.AutoGuardEnabled = ReadBool(json, "autoGuardEnabled", true);
                s.EcoQosEnabled = ReadBool(json, "ecoQosEnabled", true);
                s.GameModeAutoPause = ReadBool(json, "gameModeAutoPause", true);
                s.VirtualDesktopSleepEnabled = ReadBool(json, "virtualDesktopSleepEnabled", true);

                Match arrMatch = Regex.Match(json, "\"immuneGroups\"\\s*:\\s*\\[([^\\]]*)\\]", RegexOptions.IgnoreCase);
                if (arrMatch.Success)
                {
                    MatchCollection items = Regex.Matches(arrMatch.Groups[1].Value, "\"([^\"]+)\"");
                    lock (s.immuneLock)
                    {
                        for (int i = 0; i < items.Count; i++)
                        {
                            string k = items[i].Groups[1].Value.Trim().ToLowerInvariant();
                            if (!string.IsNullOrEmpty(k)) s.ImmuneGroups.Add(k);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Settings.Load", ex);
            }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Log.DirectoryPath);
                List<string> immList;
                lock (immuneLock)
                {
                    immList = new List<string>(ImmuneGroups);
                }
                immList.Sort(StringComparer.OrdinalIgnoreCase);
                List<string> quoted = new List<string>();
                for (int i = 0; i < immList.Count; i++)
                {
                    string safe = immList[i].Replace("\\", "").Replace("\"", "").Trim();
                    if (!string.IsNullOrEmpty(safe)) quoted.Add("\"" + safe + "\"");
                }

                StringBuilder sb = new StringBuilder();
                sb.Append("{\n");
                sb.AppendFormat(CultureInfo.InvariantCulture, "  \"windowX\": {0},\n", double.IsNaN(WindowX) ? "null" : WindowX.ToString("F1", CultureInfo.InvariantCulture));
                sb.AppendFormat(CultureInfo.InvariantCulture, "  \"windowY\": {0},\n", double.IsNaN(WindowY) ? "null" : WindowY.ToString("F1", CultureInfo.InvariantCulture));
                sb.AppendFormat("  \"showWindow\": {0},\n", ShowWindow ? "true" : "false");
                sb.AppendFormat("  \"foldedLens\": {0},\n", FoldedLens ? "true" : "false");
                sb.AppendFormat("  \"pinned\": {0},\n", Pinned ? "true" : "false");
                sb.AppendFormat("  \"autoGuardEnabled\": {0},\n", AutoGuardEnabled ? "true" : "false");
                sb.AppendFormat("  \"ecoQosEnabled\": {0},\n", EcoQosEnabled ? "true" : "false");
                sb.AppendFormat("  \"gameModeAutoPause\": {0},\n", GameModeAutoPause ? "true" : "false");
                sb.AppendFormat("  \"virtualDesktopSleepEnabled\": {0},\n", VirtualDesktopSleepEnabled ? "true" : "false");
                sb.AppendFormat("  \"immuneGroups\": [{0}]\n", string.Join(", ", quoted.ToArray()));
                sb.Append("}\n");

                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8);
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
                else File.Move(tmp, FilePath);
            }
            catch (Exception ex)
            {
                Log.Error("Settings.Save", ex);
            }
        }

        private static bool ReadBool(string json, string key, bool defVal)
        {
            Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
            if (!m.Success) return defVal;
            return string.Equals(m.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static int ReadInt(string json, string key, int defVal)
        {
            Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(-?[0-9]+)", RegexOptions.IgnoreCase);
            if (!m.Success) return defVal;
            int v;
            if (int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) return v;
            return defVal;
        }

        private static double ReadDouble(string json, string key, double defVal)
        {
            Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(-?[0-9]+(?:\\.[0-9]+)?)", RegexOptions.IgnoreCase);
            if (!m.Success) return defVal;
            double v;
            if (double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
            return defVal;
        }
    }
}
