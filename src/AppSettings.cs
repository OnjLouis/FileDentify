using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace FileDentify
{    internal sealed class AppSettings
    {
        public bool SendToEnabled;
        public bool DesktopShortcutEnabled;
        public bool FileAssociationEnabled;
        public bool AutoSaveLastReport = true;
        public bool HtmlDetailsView;
        public string UpdateCheckFrequency = "Startup";
        public bool InstallUpdatesQuietly;
        public DateTime? LastAutomaticUpdateCheckUtc;
        public List<string> SectionOrder = new List<string>();
        public List<string> RecentReports = new List<string>();

        public static string SettingsPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FileDentify.ini"); }
        }

        public static AppSettings Load()
        {
            var settings = new AppSettings();
            if (!File.Exists(SettingsPath))
                return settings;
            foreach (var raw in File.ReadAllLines(SettingsPath))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("[", StringComparison.Ordinal) || line.StartsWith("#", StringComparison.Ordinal))
                    continue;
                var split = line.IndexOf('=');
                if (split <= 0)
                    continue;
                var key = line.Substring(0, split).Trim();
                var value = line.Substring(split + 1).Trim();
                if (key.Equals("SendToEnabled", StringComparison.OrdinalIgnoreCase))
                    settings.SendToEnabled = ParseBool(value, settings.SendToEnabled);
                else if (key.Equals("DesktopShortcutEnabled", StringComparison.OrdinalIgnoreCase))
                    settings.DesktopShortcutEnabled = ParseBool(value, settings.DesktopShortcutEnabled);
                else if (key.Equals("FileAssociationEnabled", StringComparison.OrdinalIgnoreCase))
                    settings.FileAssociationEnabled = ParseBool(value, settings.FileAssociationEnabled);
                else if (key.Equals("AutoSaveLastReport", StringComparison.OrdinalIgnoreCase))
                    settings.AutoSaveLastReport = ParseBool(value, settings.AutoSaveLastReport);
                else if (key.Equals("HtmlDetailsView", StringComparison.OrdinalIgnoreCase))
                    settings.HtmlDetailsView = ParseBool(value, settings.HtmlDetailsView);
                else if (key.Equals("UpdateCheckFrequency", StringComparison.OrdinalIgnoreCase))
                    settings.UpdateCheckFrequency = UpdateService.NormalizeUpdateCheckFrequency(value);
                else if (key.Equals("InstallUpdatesQuietly", StringComparison.OrdinalIgnoreCase))
                    settings.InstallUpdatesQuietly = ParseBool(value, settings.InstallUpdatesQuietly);
                else if (key.Equals("SectionOrder", StringComparison.OrdinalIgnoreCase))
                    settings.SectionOrder = ParseList(value);
                else if (key.Equals("RecentReports", StringComparison.OrdinalIgnoreCase))
                    settings.RecentReports = ParseList(value);
                else if (key.Equals("LastAutomaticUpdateCheckUtc", StringComparison.OrdinalIgnoreCase))
                {
                    DateTime parsed;
                    if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed))
                        settings.LastAutomaticUpdateCheckUtc = parsed;
                }
            }
            return settings;
        }

        public void Save()
        {
            var lines = new List<string>
            {
                "[Settings]",
                "SendToEnabled=" + SendToEnabled,
                "DesktopShortcutEnabled=" + DesktopShortcutEnabled,
                "FileAssociationEnabled=" + FileAssociationEnabled,
                "AutoSaveLastReport=" + AutoSaveLastReport,
                "HtmlDetailsView=" + HtmlDetailsView,
                "UpdateCheckFrequency=" + UpdateService.NormalizeUpdateCheckFrequency(UpdateCheckFrequency),
                "InstallUpdatesQuietly=" + InstallUpdatesQuietly,
                "SectionOrder=" + FormatList(SectionOrder),
                "RecentReports=" + FormatList(RecentReports),
                "LastAutomaticUpdateCheckUtc=" + (LastAutomaticUpdateCheckUtc.HasValue ? LastAutomaticUpdateCheckUtc.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) : string.Empty)
            };
            File.WriteAllLines(SettingsPath, lines.ToArray(), Encoding.ASCII);
        }

        private static bool ParseBool(string value, bool fallback)
        {
            bool parsed;
            return bool.TryParse(value, out parsed) ? parsed : fallback;
        }

        private static List<string> ParseList(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return new List<string>();
            return value.Split('|')
                .Select(DecodeListValue)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string FormatList(IEnumerable<string> values)
        {
            if (values == null)
                return string.Empty;
            return string.Join("|", values
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(EncodeListValue)
                .ToArray());
        }

        private static string EncodeListValue(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string DecodeListValue(string value)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value ?? string.Empty));
            }
            catch
            {
                return string.Empty;
            }
        }
    }

}

