using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace FileDentify
{
    internal static partial class FileInspector
    {
        private static string SymbianPackageTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path);
            if (IsSymbianPackageHeader(header) || ext.Equals(".sis", StringComparison.OrdinalIgnoreCase) || ext.Equals(".sisx", StringComparison.OrdinalIgnoreCase))
                return ext.Equals(".sisx", StringComparison.OrdinalIgnoreCase) ? "Symbian signed installation package" : "Symbian installation package";
            return null;
        }

        private static string SymbianAppResourceTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".app": return "Symbian OS application binary";
                case ".aif": return "Symbian application information file";
                case ".rsc": return "Symbian compiled resource file";
                case ".mbm": return "Symbian multi-bitmap image resource";
                case ".mif": return "Symbian icon/resource file";
                case ".mdl": return "Symbian recognizer or plug-in module";
                default:
                    if (PathLooksSymbian(path) && (ext == ".dat" || ext == ".bin") && HasSymbianUidFields(header))
                        return "Symbian app support data";
                    return null;
            }
        }

        private static string JavaMidletTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".jad")
                return "Java ME MIDlet descriptor";
            if (ext == ".jar" && PathLooksMidlet(path))
                return "Java ME MIDlet archive";
            return null;
        }

        private static void AddSymbianPackageInfo(List<ReportSection> sections, string path, byte[] header)
        {
            var ext = Path.GetExtension(path);
            if (!IsSymbianPackageHeader(header) && !ext.Equals(".sis", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".sisx", StringComparison.OrdinalIgnoreCase))
                return;

            var section = AddSection(sections, "Symbian package");
            Add(section, "Format hint", SymbianPackageTypeName(path, header) ?? "Symbian installation package");
            if (header.Length >= 16)
            {
                Add(section, "UID1", "0x" + ReadUInt32LittleEndian(header, 0).ToString("X8", CultureInfo.InvariantCulture) + SymbianUidMeaning(ReadUInt32LittleEndian(header, 0)));
                Add(section, "UID2", "0x" + ReadUInt32LittleEndian(header, 4).ToString("X8", CultureInfo.InvariantCulture));
                Add(section, "UID3/package UID", "0x" + ReadUInt32LittleEndian(header, 8).ToString("X8", CultureInfo.InvariantCulture));
                Add(section, "UID checksum", "0x" + ReadUInt32LittleEndian(header, 12).ToString("X8", CultureInfo.InvariantCulture));
            }
            if (header.Length >= 0x40)
            {
                Add(section, "Header fields", string.Join("\r\n", new[]
                {
                    "0x10: " + ReadUInt32LittleEndian(header, 0x10).ToString(CultureInfo.InvariantCulture),
                    "0x14: " + FormatBytes(ReadUInt32LittleEndian(header, 0x14)),
                    "0x18: " + ReadUInt32LittleEndian(header, 0x18).ToString(CultureInfo.InvariantCulture),
                    "0x20: " + FormatBytes(ReadUInt32LittleEndian(header, 0x20)),
                    "0x24: " + ReadUInt32LittleEndian(header, 0x24).ToString(CultureInfo.InvariantCulture)
                }));
            }
            var zlibOffset = IndexOfBytes(header, new byte[] { 0x78, 0x9C });
            if (zlibOffset >= 0)
                Add(section, "Compressed stream hint", "zlib stream marker at 0x" + zlibOffset.ToString("X", CultureInfo.InvariantCulture));
            var strings = FindReadableTextLines(header, 4, 40)
                .Where(s => s.IndexOf("Symbian", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.IndexOf("Nokia", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.IndexOf("S60", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.IndexOf("Mobile", StringComparison.OrdinalIgnoreCase) >= 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToArray();
            if (strings.Length > 0)
                Add(section, "Visible package strings", string.Join("\r\n", strings));
            Add(section, "Notes", "Symbian SIS/SISX package support is header-level. FileDentify reports package identity and visible markers without unpacking or installing content.");
        }

        private static void AddSymbianAppResourceInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            var type = SymbianAppResourceTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Symbian app/resource");
            Add(section, "Format hint", type);
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "Folder role", SymbianFolderRole(path));
            Add(section, "Item name", Path.GetFileName(path));

            if (HasSymbianUidFields(header))
            {
                Add(section, "UID1", "0x" + ReadUInt32LittleEndian(header, 0).ToString("X8", CultureInfo.InvariantCulture) + SymbianUidMeaning(ReadUInt32LittleEndian(header, 0)));
                Add(section, "UID2", "0x" + ReadUInt32LittleEndian(header, 4).ToString("X8", CultureInfo.InvariantCulture));
                Add(section, "UID3", "0x" + ReadUInt32LittleEndian(header, 8).ToString("X8", CultureInfo.InvariantCulture));
                Add(section, "UID checksum", "0x" + ReadUInt32LittleEndian(header, 12).ToString("X8", CultureInfo.InvariantCulture));
            }

            if (string.Equals(Path.GetExtension(path), ".mbm", StringComparison.OrdinalIgnoreCase) && header.Length >= 20)
            {
                Add(section, "Header marker", "Symbian multi-bitmap/resource-style file");
                Add(section, "First data offset-like field", "0x" + ReadUInt32LittleEndian(header, 16).ToString("X", CultureInfo.InvariantCulture));
            }

            var strings = FindReadableTextLines(sample, 4, 80)
                .Where(IsUsefulSymbianString)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(30)
                .ToArray();
            if (strings.Length > 0)
                Add(section, "Visible strings", string.Join(Environment.NewLine, strings));

            Add(section, "Notes", "Symbian installed-app files are reported from path, extension, UID-like header fields, and visible strings only. FileDentify does not execute, install, or disassemble them.");
        }

        private static void AddJavaMidletInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
        {
            var type = JavaMidletTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Java MIDlet");
            Add(section, "Format hint", type);
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "Folder role", PathLooksMidlet(path) ? "Installed Java ME MIDlet folder" : "Java ME application file");

            if (Path.GetExtension(path).Equals(".jad", StringComparison.OrdinalIgnoreCase))
                AddJadFields(section, header);
            else if (Path.GetExtension(path).Equals(".jar", StringComparison.OrdinalIgnoreCase))
                Add(section, "Archive note", "Java ME MIDlet JAR; see ZIP/package structure for manifest and entries.");

            Add(section, "Notes", "MIDlet support reports descriptor and archive metadata only. FileDentify does not run Java ME code.");
        }

        private static void AddJadFields(ReportSection section, byte[] header)
        {
            var text = Encoding.UTF8.GetString(header);
            var fields = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && line.IndexOf(':') > 0)
                .Select(line => new { Key = line.Substring(0, line.IndexOf(':')).Trim(), Value = line.Substring(line.IndexOf(':') + 1).Trim() })
                .ToList();
            foreach (var key in new[] { "MIDlet-Name", "MIDlet-Version", "MIDlet-Vendor", "MIDlet-Jar-URL", "MIDlet-Jar-Size", "MicroEdition-Profile", "MicroEdition-Configuration" })
            {
                var match = fields.FirstOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                if (match != null && !string.IsNullOrWhiteSpace(match.Value))
                    Add(section, FriendlyJadKey(match.Key), match.Value);
            }
            var midlets = fields
                .Where(item => item.Key.StartsWith("MIDlet-", StringComparison.OrdinalIgnoreCase) && item.Key.Length > 7 && char.IsDigit(item.Key[7]))
                .Take(20)
                .Select(item => item.Key + ": " + item.Value)
                .ToArray();
            if (midlets.Length > 0)
                Add(section, "MIDlet entries", string.Join(Environment.NewLine, midlets));
        }

        private static bool IsSymbianPackageHeader(byte[] header)
        {
            return header.Length >= 4 && ReadUInt32LittleEndian(header, 0) == 0x10201A7A;
        }

        private static string SymbianUidMeaning(uint uid)
        {
            return uid == 0x10201A7A ? " (SIS package UID)" : string.Empty;
        }

        private static bool HasSymbianUidFields(byte[] header)
        {
            if (header == null || header.Length < 16)
                return false;
            var uid1 = ReadUInt32LittleEndian(header, 0);
            var uid2 = ReadUInt32LittleEndian(header, 4);
            var uid3 = ReadUInt32LittleEndian(header, 8);
            return uid1 != 0 || uid2 != 0 || uid3 != 0;
        }

        private static bool PathLooksSymbian(string path)
        {
            var value = path ?? string.Empty;
            return value.IndexOf("\\System\\Apps\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("\\System\\Recogs\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("\\System\\Libs\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("\\System\\Data\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("\\Private\\", StringComparison.OrdinalIgnoreCase) >= 0 && value.IndexOf("\\System\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool PathLooksMidlet(string path)
        {
            var value = path ?? string.Empty;
            return value.IndexOf("\\MIDlets\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("\\MIDlet\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string SymbianFolderRole(string path)
        {
            var value = path ?? string.Empty;
            if (value.IndexOf("\\System\\Apps\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Installed Symbian application folder";
            if (value.IndexOf("\\System\\Recogs\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Recognizer or file-type plug-in";
            if (value.IndexOf("\\System\\Libs\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Shared library/support folder";
            if (value.IndexOf("\\System\\Data\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Application data/resource folder";
            return "Symbian OS file";
        }

        private static bool IsUsefulSymbianString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            return value.IndexOf("Nokia", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Symbian", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Series 60", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("S60", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("MIDlet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf(".app", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf(".rsc", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf(".mbm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("\\System\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FriendlyJadKey(string key)
        {
            switch (key)
            {
                case "MIDlet-Name": return "Name";
                case "MIDlet-Version": return "Version";
                case "MIDlet-Vendor": return "Vendor";
                case "MIDlet-Jar-URL": return "JAR file";
                case "MIDlet-Jar-Size": return "JAR size";
                case "MicroEdition-Profile": return "Java ME profile";
                case "MicroEdition-Configuration": return "Java ME configuration";
                default: return key;
            }
        }

        private static int IndexOfBytes(byte[] data, byte[] needle)
        {
            if (data == null || needle == null || needle.Length == 0)
                return -1;
            for (var i = 0; i + needle.Length <= data.Length; i++)
            {
                var ok = true;
                for (var j = 0; j < needle.Length; j++)
                {
                    if (data[i + j] != needle[j])
                    {
                        ok = false;
                        break;
                    }
                }
                if (ok)
                    return i;
            }
            return -1;
        }
    }
}
