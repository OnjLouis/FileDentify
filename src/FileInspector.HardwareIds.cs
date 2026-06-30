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
        private static string HardwareIdDatabaseTypeName(string path, byte[] header)
        {
            if (!LooksLikeHardwareIdDatabase(path, header))
                return null;

            var name = Path.GetFileName(path) ?? string.Empty;
            if (name.Equals("usb.ids", StringComparison.OrdinalIgnoreCase))
                return "USB ID database";
            if (name.Equals("pci.ids", StringComparison.OrdinalIgnoreCase))
                return "PCI ID database";
            return "Hardware ID database";
        }

        private static void AddHardwareIdDatabaseInfo(List<ReportSection> sections, string path, byte[] header)
        {
            var type = HardwareIdDatabaseTypeName(path, header);
            if (type == null)
                return;

            var text = DecodeHardwareIdText(header);
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var section = AddSection(sections, "Hardware ID database");
            Add(section, "Database", type);
            Add(section, "File name", Path.GetFileName(path));
            AddCommentField(section, lines, "Version");
            AddCommentField(section, lines, "Date");

            var vendorLines = lines.Where(line => Regex.IsMatch(line, @"^[0-9A-Fa-f]{4}\s+\S")).ToArray();
            var childLines = lines.Where(line => Regex.IsMatch(line, @"^\t[0-9A-Fa-f]{4}\s+\S")).ToArray();
            var interfaceLines = lines.Where(line => Regex.IsMatch(line, @"^\t\t[0-9A-Fa-f]{2}\s+\S")).ToArray();
            var classLines = lines.Where(line => Regex.IsMatch(line, @"^C\s+[0-9A-Fa-f]{2}\s+\S")).ToArray();

            Add(section, "Vendor entries in sample", vendorLines.Length.ToString(CultureInfo.InvariantCulture));
            Add(section, "Device entries in sample", childLines.Length.ToString(CultureInfo.InvariantCulture));
            if (interfaceLines.Length > 0)
                Add(section, "Interface entries in sample", interfaceLines.Length.ToString(CultureInfo.InvariantCulture));
            if (classLines.Length > 0)
                Add(section, "Class entries in sample", classLines.Length.ToString(CultureInfo.InvariantCulture));

            AddExamples(section, "Example vendors", vendorLines, 12);
            AddExamples(section, "Example devices", childLines.Select(line => line.TrimStart('\t')).ToArray(), 12);
            AddExamples(section, "Example classes", classLines, 12);
            Add(section, "Notes", "Hardware ID databases map numeric USB/PCI identifiers to vendor, device, class, subclass, and protocol names. FileDentify reports the database structure and sampled entries; it does not treat the database as a device scan.");
        }

        private static bool LooksLikeHardwareIdDatabase(string path, byte[] header)
        {
            var name = Path.GetFileName(path) ?? string.Empty;
            if (!name.Equals("usb.ids", StringComparison.OrdinalIgnoreCase) && !name.Equals("pci.ids", StringComparison.OrdinalIgnoreCase))
                return false;

            var text = DecodeHardwareIdText(header);
            return text.IndexOf("List of USB ID", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("List of PCI ID", StringComparison.OrdinalIgnoreCase) >= 0 ||
                Regex.IsMatch(text, @"(?m)^[0-9A-Fa-f]{4}\s+\S");
        }

        private static void AddCommentField(ReportSection section, string[] lines, string field)
        {
            var prefix = "# " + field + ":";
            var line = lines.FirstOrDefault(value => value.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (line == null)
                return;
            Add(section, field, CleanMetadataText(line.Trim().Substring(prefix.Length).Trim()));
        }

        private static void AddExamples(ReportSection section, string title, IEnumerable<string> lines, int max)
        {
            var values = lines
                .Select(line => CleanMetadataText(line.Trim()))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Take(max)
                .ToArray();
            if (values.Length > 0)
                Add(section, title, string.Join("\r\n", values));
        }

        private static string DecodeHardwareIdText(byte[] header)
        {
            if (header == null || header.Length == 0)
                return string.Empty;
            return Encoding.UTF8.GetString(header.Take(Math.Min(header.Length, 1024 * 1024)).ToArray());
        }
    }
}
