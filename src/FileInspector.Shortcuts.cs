using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FileDentify
{
    internal static partial class FileInspector
    {
        private static readonly byte[] ShellLinkClsid = new byte[]
        {
            0x01, 0x14, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00,
            0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46
        };

        private static bool IsWindowsShortcut(byte[] header)
        {
            if (header == null || header.Length < 0x4C)
                return false;
            if (ReadUInt32LittleEndian(header, 0) != 0x4C)
                return false;
            for (var i = 0; i < ShellLinkClsid.Length; i++)
            {
                if (header[4 + i] != ShellLinkClsid[i])
                    return false;
            }
            return true;
        }

        private static void AddWindowsShortcutInfo(List<ReportSection> sections, byte[] header)
        {
            if (!IsWindowsShortcut(header))
                return;

            var section = AddSection(sections, "Windows shortcut");
            Add(section, "Format", "Shell Link (.lnk)");
            Add(section, "Header size", ReadUInt32LittleEndian(header, 0).ToString(CultureInfo.InvariantCulture) + " bytes");
            Add(section, "Class identifier", "00021401-0000-0000-C000-000000000046");

            var flags = ReadUInt32LittleEndian(header, 0x14);
            var attributes = ReadUInt32LittleEndian(header, 0x18);
            Add(section, "Link flags", FormatBitFlags(flags, LinkFlagNames));
            Add(section, "Target attributes", FormatBitFlags(attributes, FileAttributeNames));
            Add(section, "Creation time", FormatShortcutFileTime(header, 0x1C));
            Add(section, "Access time", FormatShortcutFileTime(header, 0x24));
            Add(section, "Write time", FormatShortcutFileTime(header, 0x2C));
            Add(section, "Target file size", FormatBytes(ReadUInt32LittleEndian(header, 0x34)));
            Add(section, "Icon index", ((int)ReadUInt32LittleEndian(header, 0x38)).ToString(CultureInfo.InvariantCulture));
            Add(section, "Show command", ShortcutShowCommand(ReadUInt32LittleEndian(header, 0x3C)));
            Add(section, "Hotkey", ShortcutHotkey(ReadUInt16LittleEndian(header, 0x40)));
        }

        private static bool IsInternetShortcut(string path, byte[] header)
        {
            if (header == null || header.Length < 18)
                return false;
            var text = DecodeShortcutText(header);
            var trimmed = text.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
            if (trimmed.StartsWith("[InternetShortcut]", StringComparison.OrdinalIgnoreCase))
                return true;
            if (!string.Equals(System.IO.Path.GetExtension(path), ".url", StringComparison.OrdinalIgnoreCase))
                return false;
            return trimmed.IndexOf("URL=", StringComparison.OrdinalIgnoreCase) >= 0 ||
                trimmed.IndexOf("BASEURL=", StringComparison.OrdinalIgnoreCase) >= 0 ||
                trimmed.IndexOf("ORIGURL=", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddInternetShortcutInfo(List<ReportSection> sections, string path, byte[] header)
        {
            if (!IsInternetShortcut(path, header))
                return;

            var values = ParseIniLikeShortcut(header);
            var section = AddSection(sections, "Internet shortcut");
            Add(section, "Format", "Windows Internet shortcut or saved web favorite (.url)");
            AddShortcutValue(section, values, "URL", "URL");
            AddShortcutValue(section, values, "BASEURL", "Base URL");
            AddShortcutValue(section, values, "ORIGURL", "Original URL");
            AddShortcutValue(section, values, "WorkingDirectory", "Working directory");
            AddShortcutValue(section, values, "IconFile", "Icon file");
            AddShortcutValue(section, values, "IconIndex", "Icon index");
            AddShortcutValue(section, values, "HotKey", "Hotkey");
            AddShortcutValue(section, values, "IDList", "ID list");
            AddShortcutValue(section, values, "Prop3", "Prop3");
        }

        private static Dictionary<string, string> ParseIniLikeShortcut(byte[] header)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var text = DecodeShortcutText(header);
            foreach (var rawLine in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '[' || line[0] == ';')
                    continue;
                var equals = line.IndexOf('=');
                if (equals <= 0)
                    continue;
                var key = line.Substring(0, equals).Trim();
                var value = line.Substring(equals + 1).Trim();
                if (key.Length > 0 && value.Length > 0 && !values.ContainsKey(key))
                    values[key] = value;
            }
            return values;
        }

        private static string DecodeShortcutText(byte[] header)
        {
            if (header.Length >= 2 && header[0] == 0xFF && header[1] == 0xFE)
                return Encoding.Unicode.GetString(header);
            if (header.Length >= 3 && header[0] == 0xEF && header[1] == 0xBB && header[2] == 0xBF)
                return Encoding.UTF8.GetString(header);
            return Encoding.Default.GetString(header);
        }

        private static void AddShortcutValue(ReportSection section, Dictionary<string, string> values, string key, string label)
        {
            string value;
            if (values.TryGetValue(key, out value))
                Add(section, label, value);
        }

        private static readonly string[] LinkFlagNames = new string[]
        {
            "HasLinkTargetIDList",
            "HasLinkInfo",
            "HasName",
            "HasRelativePath",
            "HasWorkingDir",
            "HasArguments",
            "HasIconLocation",
            "IsUnicode",
            "ForceNoLinkInfo",
            "HasExpString",
            "RunInSeparateProcess",
            "Unused bit 11",
            "HasDarwinID",
            "RunAsUser",
            "HasExpIcon",
            "NoPidlAlias",
            "Unused bit 16",
            "RunWithShimLayer",
            "ForceNoLinkTrack",
            "EnableTargetMetadata",
            "DisableLinkPathTracking",
            "DisableKnownFolderTracking",
            "DisableKnownFolderAlias",
            "AllowLinkToLink",
            "UnaliasOnSave",
            "PreferEnvironmentPath",
            "KeepLocalIDListForUNCTarget"
        };

        private static readonly string[] FileAttributeNames = new string[]
        {
            "ReadOnly",
            "Hidden",
            "System",
            "Reserved bit 3",
            "Directory",
            "Archive",
            "Reserved bit 6",
            "Normal",
            "Temporary",
            "SparseFile",
            "ReparsePoint",
            "Compressed",
            "Offline",
            "NotContentIndexed",
            "Encrypted"
        };

        private static string FormatBitFlags(uint value, string[] names)
        {
            if (value == 0)
                return "None";

            var flags = new List<string>();
            for (var i = 0; i < names.Length; i++)
            {
                if ((value & (1u << i)) != 0)
                    flags.Add(names[i]);
            }

            var knownMask = names.Length >= 32 ? uint.MaxValue : (1u << names.Length) - 1;
            var unknown = value & ~knownMask;
            if (unknown != 0)
                flags.Add("Unknown bits 0x" + unknown.ToString("X", CultureInfo.InvariantCulture));

            return flags.Count == 0 ? "None" : string.Join(", ", flags.ToArray());
        }

        private static string FormatShortcutFileTime(byte[] header, int offset)
        {
            var value = ReadUInt64LittleEndian(header, offset);
            if (value == 0)
                return "Not set";

            try
            {
                var date = DateTime.FromFileTimeUtc((long)value).ToLocalTime();
                return date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            }
            catch
            {
                return "Invalid FILETIME 0x" + value.ToString("X", CultureInfo.InvariantCulture);
            }
        }

        private static string ShortcutShowCommand(uint value)
        {
            switch (value)
            {
                case 1: return "Normal";
                case 3: return "Maximized";
                case 7: return "Minimized";
                default: return "Unknown value " + value.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static string ShortcutHotkey(ushort value)
        {
            if (value == 0)
                return "None";

            var keyCode = value & 0xFF;
            var modifiers = (value >> 8) & 0xFF;
            var parts = new List<string>();
            if ((modifiers & 0x01) != 0) parts.Add("Shift");
            if ((modifiers & 0x02) != 0) parts.Add("Ctrl");
            if ((modifiers & 0x04) != 0) parts.Add("Alt");
            parts.Add(VirtualKeyName(keyCode));
            return string.Join("+", parts.ToArray());
        }

        private static string VirtualKeyName(int keyCode)
        {
            if (keyCode >= 'A' && keyCode <= 'Z')
                return ((char)keyCode).ToString();
            if (keyCode >= '0' && keyCode <= '9')
                return ((char)keyCode).ToString();
            if (keyCode >= 0x70 && keyCode <= 0x87)
                return "F" + (keyCode - 0x6F).ToString(CultureInfo.InvariantCulture);

            switch (keyCode)
            {
                case 0x08: return "Backspace";
                case 0x09: return "Tab";
                case 0x0D: return "Enter";
                case 0x1B: return "Escape";
                case 0x20: return "Space";
                case 0x21: return "PageUp";
                case 0x22: return "PageDown";
                case 0x23: return "End";
                case 0x24: return "Home";
                case 0x25: return "Left";
                case 0x26: return "Up";
                case 0x27: return "Right";
                case 0x28: return "Down";
                case 0x2D: return "Insert";
                case 0x2E: return "Delete";
                default: return "Virtual key 0x" + keyCode.ToString("X2", CultureInfo.InvariantCulture);
            }
        }
    }
}
