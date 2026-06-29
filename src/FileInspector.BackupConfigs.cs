using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FileDentify
{
    internal static partial class FileInspector
    {
        private static string BackupConfigTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var name = Path.GetFileName(path) ?? string.Empty;
            if (ext == ".ah4session")
                return "Audio Hijack session";
            if (ext == ".iaf" || StartsWith(header, Encoding.ASCII.GetBytes("fMAI")))
                return "Outlook Express account export";
            if (ext == ".dss")
                return "Synology DSM configuration backup";
            if (IsRouterBackupPath(path))
                return RouterBackupTypeName(path, header);
            if (name.IndexOf("homebridge-backup", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Homebridge backup archive";
            if (name.IndexOf("pi-hole", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Pi-hole backup archive";
            return null;
        }

        private static void AddBackupConfigInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            var type = BackupConfigTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Backup/config data");
            Add(section, "Format hint", type);
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "File name", Path.GetFileName(path));

            if (Path.GetExtension(path).Equals(".ah4session", StringComparison.OrdinalIgnoreCase))
                AddAudioHijackSessionInfo(section, header, sample);
            else if (Path.GetExtension(path).Equals(".iaf", StringComparison.OrdinalIgnoreCase) || StartsWith(header, Encoding.ASCII.GetBytes("fMAI")))
                AddOutlookExpressIafInfo(section, sample);
            else if (Path.GetExtension(path).Equals(".dss", StringComparison.OrdinalIgnoreCase))
                AddSynologyDssInfo(section, header);
            else if (IsRouterBackupPath(path))
                AddRouterBackupInfo(section, path, header, sample);

            Add(section, "Privacy note", "Backup and configuration files can contain server names, usernames, wireless keys, tokens, or passwords. FileDentify avoids password-looking values in this section, but review full reports carefully before sharing them.");
        }

        private static void AddAudioHijackSessionInfo(ReportSection section, byte[] header, byte[] sample)
        {
            Add(section, "Application", "Rogue Amoeba Audio Hijack");
            Add(section, "Storage", StartsWith(header, Encoding.ASCII.GetBytes("bplist00")) ? "Apple binary property list" : "Session file");
            var keys = FindReadableTextLines(sample, 4, 120)
                .SelectMany(ExtractAudioHijackTokens)
                .Where(line => line.IndexOf("session", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("block", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("source", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("recorder", StringComparison.OrdinalIgnoreCase) >= 0)
                .Where(line => !LooksSensitiveConfigLine(line))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(30)
                .ToArray();
            if (keys.Length > 0)
                Add(section, "Visible session keys", string.Join(Environment.NewLine, keys));
        }

        private static IEnumerable<string> ExtractAudioHijackTokens(string value)
        {
            value = CleanMetadataText(value ?? string.Empty).Trim();
            foreach (Match match in Regex.Matches(value, @"[A-Za-z][A-Za-z0-9_]*(?:session|Session|block|Block|source|Source|recorder|Recorder)[A-Za-z0-9_]*|(?:session|block|source|recorder)[A-Za-z0-9_]*"))
                yield return match.Value.Trim('_', '^', '[', ']', '\\', '/', '.', ':', ';', ',', '"', '\'');
        }

        private static void AddOutlookExpressIafInfo(ReportSection section, byte[] sample)
        {
            Add(section, "Header marker", "fMAI");
            Add(section, "Common use", "Outlook Express / Windows Mail account settings export");
            var strings = FindUtf16LittleEndianStrings(sample, 3, 80)
                .Where(value => !LooksSensitiveConfigLine(value))
                .Where(value => value.IndexOf('.') >= 0 ||
                    value.IndexOf("@", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("account", StringComparison.OrdinalIgnoreCase) >= 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToArray();
            if (strings.Length > 0)
                Add(section, "Visible non-password fields", string.Join(Environment.NewLine, strings));
            Add(section, "Credential warning", "IAF files can include mail server settings and account credentials. Do not share raw files or full string dumps casually.");
        }

        private static void AddSynologyDssInfo(ReportSection section, byte[] header)
        {
            Add(section, "Common use", "Synology DiskStation Manager configuration backup");
            Add(section, "Compression", StartsWith(header, new byte[] { 0xFD, (byte)'7', (byte)'z', (byte)'X', (byte)'Z', 0x00 }) ? "XZ compressed data" : "Unknown or proprietary");
            Add(section, "Credential warning", "NAS configuration backups can include users, network settings, certificates, or service secrets.");
        }

        private static void AddRouterBackupInfo(ReportSection section, string path, byte[] header, byte[] sample)
        {
            Add(section, "Device family", RouterFamilyFromPath(path));
            Add(section, "Storage", RouterStorageDescription(header));
            var keys = FindReadableTextLines(sample, 3, 120)
                .Where(line => line.IndexOf('=') > 0 || line.IndexOf("<", StringComparison.Ordinal) >= 0 || line.IndexOf("ssid", StringComparison.OrdinalIgnoreCase) >= 0)
                .Where(line => !LooksSensitiveConfigLine(line))
                .Select(TrimLongConfigLine)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(25)
                .ToArray();
            if (keys.Length > 0)
                Add(section, "Visible non-password keys", string.Join(Environment.NewLine, keys));
            Add(section, "Credential warning", "Router backups often contain ISP credentials, Wi-Fi keys, admin hashes, VPN keys, MAC addresses, and LAN topology.");
        }

        private static bool IsRouterBackupPath(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (path.IndexOf("\\routers\\", StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            return ext == ".cfg" || ext == ".conf" || ext == ".bin" || ext == ".dat" || ext == ".xml" || ext == ".gz";
        }

        private static string RouterBackupTypeName(string path, byte[] header)
        {
            var family = RouterFamilyFromPath(path);
            if (family.Length > 0)
                return family + " router/network-device backup";
            if (StartsWith(header, new byte[] { 0x1F, 0x8B }))
                return "compressed router/network-device backup";
            return "router/network-device configuration backup";
        }

        private static string RouterFamilyFromPath(string path)
        {
            var name = Path.GetFileName(path) ?? string.Empty;
            foreach (var family in new[] { "TP-Link", "Netgear", "Tomato", "Sky", "Homebridge", "Pi-hole", "HooToo", "OBI", "Asus" })
                if (name.IndexOf(family, StringComparison.OrdinalIgnoreCase) >= 0)
                    return family;
            return string.Empty;
        }

        private static string RouterStorageDescription(byte[] header)
        {
            if (StartsWith(header, new byte[] { 0x1F, 0x8B }))
                return "GZip-compressed configuration";
            if (StartsWith(header, Encoding.ASCII.GetBytes("<?xml")) || LooksLikeText(header))
                return "Text or XML configuration";
            return "Binary/proprietary backup";
        }

        private static IEnumerable<string> FindUtf16LittleEndianStrings(byte[] data, int minChars, int maxResults)
        {
            var results = new List<string>();
            var sb = new StringBuilder();
            for (var i = 0; i + 1 < data.Length; i += 2)
            {
                var ch = (char)(data[i] | data[i + 1] << 8);
                if (ch >= 32 && ch < 127)
                    sb.Append(ch);
                else
                {
                    AddUtf16Run(results, sb, minChars, maxResults);
                    if (results.Count >= maxResults)
                        break;
                }
            }
            AddUtf16Run(results, sb, minChars, maxResults);
            return results.Take(maxResults);
        }

        private static void AddUtf16Run(List<string> results, StringBuilder sb, int minChars, int maxResults)
        {
            if (sb.Length >= minChars && results.Count < maxResults)
                results.Add(sb.ToString());
            sb.Length = 0;
        }

        private static bool LooksSensitiveConfigLine(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;
            var lower = value.ToLowerInvariant();
            return lower.IndexOf("password", StringComparison.Ordinal) >= 0 ||
                lower.IndexOf("passwd", StringComparison.Ordinal) >= 0 ||
                lower.IndexOf("psk", StringComparison.Ordinal) >= 0 ||
                lower.IndexOf("secret", StringComparison.Ordinal) >= 0 ||
                lower.IndexOf("token", StringComparison.Ordinal) >= 0 ||
                lower.IndexOf("key=", StringComparison.Ordinal) >= 0 ||
                lower.IndexOf("wep", StringComparison.Ordinal) >= 0 ||
                lower.IndexOf("wpa", StringComparison.Ordinal) >= 0;
        }

        private static string TrimLongConfigLine(string value)
        {
            value = CleanMetadataText(value ?? string.Empty);
            return value.Length <= 140 ? value : value.Substring(0, 140) + "...";
        }
    }
}
