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
        public string UpdateCheckFrequency = "Startup";
        public bool InstallUpdatesQuietly;
        public DateTime? LastAutomaticUpdateCheckUtc;

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
                else if (key.Equals("UpdateCheckFrequency", StringComparison.OrdinalIgnoreCase))
                    settings.UpdateCheckFrequency = UpdateService.NormalizeUpdateCheckFrequency(value);
                else if (key.Equals("InstallUpdatesQuietly", StringComparison.OrdinalIgnoreCase))
                    settings.InstallUpdatesQuietly = ParseBool(value, settings.InstallUpdatesQuietly);
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
                "UpdateCheckFrequency=" + UpdateService.NormalizeUpdateCheckFrequency(UpdateCheckFrequency),
                "InstallUpdatesQuietly=" + InstallUpdatesQuietly,
                "LastAutomaticUpdateCheckUtc=" + (LastAutomaticUpdateCheckUtc.HasValue ? LastAutomaticUpdateCheckUtc.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) : string.Empty)
            };
            File.WriteAllLines(SettingsPath, lines.ToArray(), Encoding.ASCII);
        }

        private static bool ParseBool(string value, bool fallback)
        {
            bool parsed;
            return bool.TryParse(value, out parsed) ? parsed : fallback;
        }
    }

}

