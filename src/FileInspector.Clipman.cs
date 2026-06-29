using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace FileDentify
{
    internal static partial class FileInspector
    {
        private static readonly byte[] ClipmanCompressedMagic = Encoding.ASCII.GetBytes("CLIPDB1");
        private static readonly byte[] ClipmanEncryptedMagic = Encoding.ASCII.GetBytes("CLIPDB2");

        private static string ClipmanTypeName(string path, byte[] header)
        {
            if (StartsWith(header, ClipmanEncryptedMagic))
                return "Clipman encrypted history database";
            if (StartsWith(header, ClipmanCompressedMagic))
                return "Clipman compressed history database";
            if (string.Equals(Path.GetExtension(path), ".clipdb", StringComparison.OrdinalIgnoreCase))
            {
                if (StartsWith(header, new byte[] { 0x1F, 0x8B }))
                    return "Clipman legacy compressed history database";
                return "Clipman history database";
            }

            if (IsClipmanJsonFile(path, header))
                return ClipmanJsonTypeName(path, header);

            return null;
        }

        private static byte[] RedactSensitiveClipmanSample(string path, byte[] sample)
        {
            if (sample == null || sample.Length == 0 || !IsClipmanJsonFile(path, sample))
                return sample;

            var text = Encoding.UTF8.GetString(sample);
            if (text.IndexOf("\"ProtectedDatabasePassword\"", StringComparison.OrdinalIgnoreCase) < 0)
                return sample;

            text = Regex.Replace(
                text,
                "(\"ProtectedDatabasePassword\"\\s*:\\s*\")([^\"]*)(\")",
                delegate(Match match)
                {
                    var value = match.Groups[2].Value;
                    return match.Groups[1].Value + (string.IsNullOrEmpty(value) ? "" : "[redacted]") + match.Groups[3].Value;
                },
                RegexOptions.IgnoreCase);
            return Encoding.UTF8.GetBytes(text);
        }

        private static void AddClipmanInfo(List<ReportSection> sections, string path, byte[] sample, long length)
        {
            var type = ClipmanTypeName(path, sample);
            if (type == null)
                return;

            var section = AddSection(sections, "Clipman");
            Add(section, "Format hint", type);
            Add(section, "Detection basis", ClipmanDetectionBasis(path, sample));

            if (string.Equals(Path.GetExtension(path), ".clipdb", StringComparison.OrdinalIgnoreCase) ||
                StartsWith(sample, ClipmanCompressedMagic) ||
                StartsWith(sample, ClipmanEncryptedMagic))
            {
                AddClipmanDatabaseInfo(section, path, sample, length);
                return;
            }

            AddClipmanJsonInfo(section, path, sample);
        }

        private static string ClipmanDetectionBasis(string path, byte[] header)
        {
            var parts = new List<string>();
            var name = Path.GetFileName(path);
            var ext = Path.GetExtension(path);
            if (!string.IsNullOrWhiteSpace(ext))
                parts.Add("extension " + ext);
            if (!string.IsNullOrWhiteSpace(name) && name.IndexOf("clipman", StringComparison.OrdinalIgnoreCase) >= 0)
                parts.Add("Clipman filename");
            if (name.EndsWith("-settings.json", StringComparison.OrdinalIgnoreCase))
                parts.Add("machine settings filename");
            if (name.EndsWith("-file-history.clipdb", StringComparison.OrdinalIgnoreCase))
                parts.Add("machine file-history filename");
            if (StartsWith(header, ClipmanCompressedMagic))
                parts.Add("CLIPDB1 magic");
            if (StartsWith(header, ClipmanEncryptedMagic))
                parts.Add("CLIPDB2 magic");
            if (IsClipmanJsonFile(path, header))
                parts.Add("Clipman JSON keys");
            return parts.Count == 0 ? "Clipman extension or filename convention" : string.Join(", ", parts.ToArray());
        }

        private static void AddClipmanDatabaseInfo(ReportSection section, string path, byte[] sample, long length)
        {
            var name = Path.GetFileName(path);
            if (string.Equals(name, "clipman-history.clipdb", StringComparison.OrdinalIgnoreCase))
                Add(section, "Database role", "Shared text clipboard history");
            else if (name.EndsWith("-file-history.clipdb", StringComparison.OrdinalIgnoreCase))
                Add(section, "Database role", "Machine-specific file clipboard history");
            else
                Add(section, "Database role", "Clipman import/export or history database");

            if (StartsWith(sample, ClipmanEncryptedMagic))
            {
                Add(section, "Container", "CLIPDB2 encrypted gzip-compressed JSON");
                if (sample.Length >= 7 + 1 + 16 + 16 + 32)
                {
                    Add(section, "Format version", sample[7].ToString(CultureInfo.InvariantCulture));
                    Add(section, "Salt", ToHex(sample, 8, 16));
                    Add(section, "IV", ToHex(sample, 24, 16));
                    Add(section, "Ciphertext size", FormatBytes(Math.Max(0, length - 7 - 1 - 16 - 16 - 32)));
                    Add(section, "HMAC-SHA256", "Present at end of file");
                }
                Add(section, "Encryption", "AES-256-CBC with PBKDF2-HMAC-SHA1 key derivation and HMAC-SHA256 authentication.");
                Add(section, "Payload", "Encrypted. FileDentify does not prompt for or store Clipman history passwords.");
                return;
            }

            if (StartsWith(sample, ClipmanCompressedMagic) || StartsWith(sample, new byte[] { 0x1F, 0x8B }))
            {
                Add(section, "Container", StartsWith(sample, ClipmanCompressedMagic)
                    ? "CLIPDB1 gzip-compressed JSON"
                    : "Legacy gzip-compressed JSON without CLIPDB1 marker");
                var json = TryReadClipmanCompressedJson(path);
                if (json == null)
                {
                    Add(section, "Payload", "Could not decompress JSON payload.");
                    return;
                }
                AddClipmanDatabaseJsonInfo(section, json);
                return;
            }

            Add(section, "Container", "Unknown .clipdb form");
            Add(section, "Payload", "No CLIPDB1, CLIPDB2, or gzip marker was found at the beginning of the file.");
        }

        private static void AddClipmanDatabaseJsonInfo(ReportSection section, string json)
        {
            var root = DeserializeJsonObject(json);
            if (root == null)
            {
                Add(section, "JSON payload", "Decompressed, but the root object could not be parsed.");
                return;
            }

            AddJsonValue(section, root, "Version", "Database version");
            AddUnixMs(section, root, "UpdatedUnixMs", "Updated");

            var entries = GetArray(root, "Entries");
            if (entries != null)
            {
                Add(section, "Payload type", "Text clipboard history");
                Add(section, "Text entries", entries.Count.ToString(CultureInfo.InvariantCulture));
                AddClipmanEntryStats(section, entries);
                return;
            }

            var events = GetArray(root, "Events");
            if (events != null)
            {
                Add(section, "Payload type", "File clipboard history");
                Add(section, "File events", events.Count.ToString(CultureInfo.InvariantCulture));
                AddClipmanFileEventStats(section, events);
                return;
            }

            Add(section, "Payload type", "Clipman JSON database object");
            Add(section, "Top-level keys", string.Join("\r\n", root.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray()));
        }

        private static void AddClipmanEntryStats(ReportSection section, ArrayList entries)
        {
            var pinned = 0;
            var named = 0;
            var grouped = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var machines = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var preview = new List<string>();

            foreach (Dictionary<string, object> entry in entries.OfType<Dictionary<string, object>>())
            {
                if (GetBool(entry, "Pinned")) pinned++;
                if (!string.IsNullOrWhiteSpace(GetString(entry, "Name"))) named++;
                CountValue(grouped, GetString(entry, "Group"));
                CountValue(machines, GetString(entry, "SourceMachine"));
                if (preview.Count < 8)
                {
                    var label = GetString(entry, "Name");
                    var text = GetString(entry, "Text");
                    if (string.IsNullOrWhiteSpace(label))
                        label = TrimForReport(text, 80);
                    if (!string.IsNullOrWhiteSpace(label))
                        preview.Add(label);
                }
            }

            Add(section, "Pinned entries", pinned.ToString(CultureInfo.InvariantCulture));
            Add(section, "Named entries", named.ToString(CultureInfo.InvariantCulture));
            AddTopCounts(section, "Groups", grouped);
            AddTopCounts(section, "Source machines", machines);
            if (preview.Count > 0)
                Add(section, "Entry preview", string.Join("\r\n", preview.ToArray()));
        }

        private static void AddClipmanFileEventStats(ReportSection section, ArrayList events)
        {
            var pinned = 0;
            var totalFiles = 0;
            var operations = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var machines = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var sources = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var preview = new List<string>();

            foreach (Dictionary<string, object> item in events.OfType<Dictionary<string, object>>())
            {
                if (GetBool(item, "Pinned")) pinned++;
                totalFiles += GetInt(item, "FileCount");
                CountValue(operations, GetString(item, "Operation"));
                CountValue(machines, GetString(item, "SourceMachine"));
                CountValue(sources, GetString(item, "Source"));
                if (preview.Count < 8)
                {
                    var files = GetArray(item, "Files");
                    if (files != null && files.Count > 0)
                        preview.Add(Convert.ToString(files[0], CultureInfo.InvariantCulture));
                }
            }

            Add(section, "Pinned events", pinned.ToString(CultureInfo.InvariantCulture));
            Add(section, "Referenced files", totalFiles.ToString(CultureInfo.InvariantCulture));
            AddTopCounts(section, "Operations", operations);
            AddTopCounts(section, "Source machines", machines);
            AddTopCounts(section, "Source applications", sources);
            if (preview.Count > 0)
                Add(section, "File preview", string.Join("\r\n", preview.ToArray()));
        }

        private static void AddClipmanJsonInfo(ReportSection section, string path, byte[] sample)
        {
            var root = DeserializeJsonObject(Encoding.UTF8.GetString(sample));
            if (root == null)
            {
                Add(section, "JSON", "Could not parse sampled JSON.");
                return;
            }

            var name = Path.GetFileName(path);
            if (name.EndsWith("-settings.json", StringComparison.OrdinalIgnoreCase))
            {
                Add(section, "JSON role", "Machine-specific Clipman settings");
                Add(section, "Machine", name.Substring(0, name.Length - "-settings.json".Length));
                AddClipmanSettingsInfo(section, root);
                return;
            }

            if (name.Equals("settings-location.json", StringComparison.OrdinalIgnoreCase))
            {
                Add(section, "JSON role", "Clipman settings/data-folder pointer");
                AddJsonValue(section, root, "DataFolder", "Data folder");
                return;
            }

            if (name.Equals("clipman-shared-state.json", StringComparison.OrdinalIgnoreCase))
            {
                Add(section, "JSON role", "Clipman shared update/instance state");
                AddJsonValue(section, root, "Version", "Clipman version");
                AddJsonValue(section, root, "UpdatedByMachine", "Updated by machine");
                AddUnixMs(section, root, "UpdatedAtUtcMs", "Updated");
                AddUnixMs(section, root, "BuildStampUtcMs", "Build stamp");
                AddJsonValue(section, root, "ExeSha256", "Executable SHA-256");
                AddJsonValue(section, root, "CloseRequestedByMachine", "Close requested by machine");
                return;
            }

            Add(section, "JSON role", "Clipman JSON data");
            Add(section, "Top-level keys", string.Join("\r\n", root.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray()));
        }

        private static void AddClipmanSettingsInfo(ReportSection section, Dictionary<string, object> root)
        {
            foreach (var key in new[] { "DatabasePath", "UseDefaultDatabasePath", "Active", "RemoveDuplicates", "SoundsEnabled", "SaveListPosition", "ShowHistoryHotkey", "ToggleActiveHotkey", "SortMode", "SortDescending", "FileHistorySortMode", "FileHistorySortDescending", "SendToEnabled", "ShowHistoryAfterSendTo", "GroupFilter", "DuplicateMode", "AutoGroupByApp", "AutoRemoveUrlTracking", "RunAtStartup", "UpdateCheckFrequency", "InstallUpdatesSilently", "DatabaseEncryptionEnabled", "RememberDatabasePassword", "MaxHistoryEntries", "MaxHistoryDays", "DiagnosticsFileHistoryLimit" })
                AddJsonValue(section, root, key, SplitCamelCase(key));

            var ignored = GetArray(root, "IgnoredProcesses");
            if (ignored != null)
                Add(section, "Ignored processes", ignored.Count == 0 ? "0" : ignored.Count.ToString(CultureInfo.InvariantCulture) + "\r\n" + string.Join("\r\n", ignored.Cast<object>().Select(v => Convert.ToString(v, CultureInfo.InvariantCulture)).Take(20).ToArray()));

            var quick = GetArray(root, "QuickCopyHotkeys");
            if (quick != null)
                Add(section, "Quick Copy hotkeys", quick.Count.ToString(CultureInfo.InvariantCulture) + ClipmanQuickCopyPreview(quick));

            if (root.ContainsKey("ProtectedDatabasePassword"))
                Add(section, "Protected database password", string.IsNullOrWhiteSpace(GetString(root, "ProtectedDatabasePassword")) ? "Not stored" : "Present, redacted");
        }

        private static string TryReadClipmanCompressedJson(string path)
        {
            const int MaxDecompressedChars = 32 * 1024 * 1024;
            try
            {
                using (var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    var magic = new byte[ClipmanCompressedMagic.Length];
                    if (file.Read(magic, 0, magic.Length) == magic.Length && BytesEqual(magic, ClipmanCompressedMagic))
                    {
                    }
                    else
                    {
                        file.Position = 0;
                    }

                    using (var gzip = new GZipStream(file, CompressionMode.Decompress))
                    using (var reader = new StreamReader(gzip, Encoding.UTF8))
                        return ReadTextWithLimit(reader, MaxDecompressedChars);
                }
            }
            catch
            {
                return null;
            }
        }

        private static string ReadTextWithLimit(TextReader reader, int maxChars)
        {
            var buffer = new char[8192];
            var builder = new StringBuilder();
            while (builder.Length < maxChars)
            {
                var wanted = Math.Min(buffer.Length, maxChars - builder.Length);
                var read = reader.Read(buffer, 0, wanted);
                if (read <= 0)
                    break;
                builder.Append(buffer, 0, read);
            }
            return builder.ToString();
        }

        private static bool IsClipmanJsonFile(string path, byte[] header)
        {
            if (!string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
                return false;

            var name = Path.GetFileName(path);
            if (name.EndsWith("-settings.json", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("settings-location.json", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("clipman-shared-state.json", StringComparison.OrdinalIgnoreCase))
                return LooksLikeClipmanJson(header);

            return false;
        }

        private static bool LooksLikeClipmanJson(byte[] header)
        {
            var text = Encoding.UTF8.GetString(header);
            return text.IndexOf("\"DatabasePath\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("\"ShowHistoryHotkey\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("\"DataFolder\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("\"UpdatedByMachine\"", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ClipmanJsonTypeName(string path, byte[] header)
        {
            var name = Path.GetFileName(path);
            if (name.EndsWith("-settings.json", StringComparison.OrdinalIgnoreCase))
                return "Clipman machine settings JSON";
            if (name.Equals("settings-location.json", StringComparison.OrdinalIgnoreCase))
                return "Clipman settings location pointer";
            if (name.Equals("clipman-shared-state.json", StringComparison.OrdinalIgnoreCase))
                return "Clipman shared state JSON";
            return "Clipman JSON data";
        }

        private static Dictionary<string, object> DeserializeJsonObject(string json)
        {
            try
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                json = (json ?? string.Empty).Trim('\uFEFF', '\0', ' ', '\t', '\r', '\n');
                return serializer.DeserializeObject(json) as Dictionary<string, object>;
            }
            catch
            {
                return null;
            }
        }

        private static void AddJsonValue(ReportSection section, Dictionary<string, object> root, string key, string label)
        {
            object value;
            if (!root.TryGetValue(key, out value) || value == null)
                return;
            Add(section, label, Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        private static void AddUnixMs(ReportSection section, Dictionary<string, object> root, string key, string label)
        {
            object value;
            long ms;
            if (root.TryGetValue(key, out value) && long.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out ms) && ms > 0)
                Add(section, label, UnixMsToLocalString(ms));
        }

        private static ArrayList GetArray(Dictionary<string, object> root, string key)
        {
            object value;
            if (!root.TryGetValue(key, out value) || value == null)
                return null;
            var arrayList = value as ArrayList;
            if (arrayList != null)
                return arrayList;
            var objectArray = value as object[];
            if (objectArray == null)
                return null;
            arrayList = new ArrayList();
            arrayList.AddRange(objectArray);
            return arrayList;
        }

        private static string GetString(Dictionary<string, object> root, string key)
        {
            object value;
            return root.TryGetValue(key, out value) && value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : string.Empty;
        }

        private static int GetInt(Dictionary<string, object> root, string key)
        {
            object value;
            int result;
            return root.TryGetValue(key, out value) && int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ? result : 0;
        }

        private static bool GetBool(Dictionary<string, object> root, string key)
        {
            object value;
            bool result;
            return root.TryGetValue(key, out value) && bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out result) && result;
        }

        private static void CountValue(Dictionary<string, int> counts, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            if (!counts.ContainsKey(value))
                counts[value] = 0;
            counts[value]++;
        }

        private static void AddTopCounts(ReportSection section, string label, Dictionary<string, int> counts)
        {
            if (counts.Count == 0)
                return;
            Add(section, label, string.Join("\r\n", counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).Take(12).Select(kv => kv.Key + ": " + kv.Value.ToString(CultureInfo.InvariantCulture)).ToArray()));
        }

        private static string ClipmanQuickCopyPreview(ArrayList quick)
        {
            var rows = quick.OfType<Dictionary<string, object>>()
                .Select(item => GetString(item, "Hotkey"))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Take(12)
                .ToArray();
            return rows.Length == 0 ? string.Empty : "\r\n" + string.Join("\r\n", rows);
        }

        private static string UnixMsToLocalString(long ms)
        {
            try
            {
                var utc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(ms);
                return utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            }
            catch
            {
                return ms.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static string TrimForReport(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            value = value.Replace("\r", " ").Replace("\n", " ").Trim();
            return value.Length <= max ? value : value.Substring(0, max) + "...";
        }

        private static string SplitCamelCase(string value)
        {
            return Regex.Replace(value ?? string.Empty, "([a-z])([A-Z])", "$1 $2");
        }

        private static string ToHex(byte[] data, int offset, int length)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < length && offset + i < data.Length; i++)
                sb.Append(data[offset + i].ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;
            for (var i = 0; i < left.Length; i++)
                if (left[i] != right[i])
                    return false;
            return true;
        }
    }
}
