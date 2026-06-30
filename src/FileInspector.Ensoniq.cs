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
        private static string EnsoniqTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var headerText = DecodeEnsoniqHeaderText(header);
            if (ext == ".efe" || headerText.IndexOf("Eps File:", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Ensoniq EPS instrument file";
            if (ext == ".eda" || headerText.IndexOf("ASR-10 Disk", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Ensoniq ASR-10 disk image";
            if (ext == ".edt" || headerText.IndexOf("TS-10", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Ensoniq TS disk image";
            return null;
        }

        private static void AddEnsoniqInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            var type = EnsoniqTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Ensoniq sampler");
            Add(section, "Format hint", type);
            Add(section, "Extension", Path.GetExtension(path));
            Add(section, "File size", FormatBytes(fileLength) + " (" + fileLength.ToString(CultureInfo.InvariantCulture) + " bytes)");

            var headerText = DecodeEnsoniqHeaderText(header);
            var title = FirstReadableEnsoniqHeaderLine(headerText);
            if (!string.IsNullOrWhiteSpace(title))
                Add(section, "Header title", title);

            var role = EnsoniqRoleFromHeader(type, headerText);
            if (!string.IsNullOrWhiteSpace(role))
                Add(section, "Role", role);

            var catalog = FindEnsoniqCatalogStrings(sample)
                .Take(30)
                .ToArray();
            if (catalog.Length > 0)
                Add(section, "Visible catalog strings", string.Join(Environment.NewLine, catalog));

            Add(section, "Notes", "Ensoniq EPS/ASR/TS files are proprietary sampler and workstation disk/instrument data. FileDentify reports visible header and catalog strings only; it does not mount, convert, or decode sample payloads.");
        }

        private static string DecodeEnsoniqHeaderText(byte[] data)
        {
            if (data == null || data.Length == 0)
                return string.Empty;
            return Encoding.ASCII.GetString(data.Take(Math.Min(data.Length, 4096)).ToArray()).Replace('\0', ' ');
        }

        private static string FirstReadableEnsoniqHeaderLine(string text)
        {
            foreach (var line in (text ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var cleaned = CleanMetadataText(line);
                if (cleaned.Length >= 4 && cleaned.Any(char.IsLetterOrDigit))
                    return cleaned;
            }
            return string.Empty;
        }

        private static string EnsoniqRoleFromHeader(string type, string headerText)
        {
            if (headerText.IndexOf("Instrument", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Instrument";
            if (type.IndexOf("disk", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Disk image";
            return string.Empty;
        }

        private static IEnumerable<string> FindEnsoniqCatalogStrings(byte[] sample)
        {
            return FindReadableTextLines(sample, 4, 80)
                .Select(CleanMetadataText)
                .Where(value => value.Length >= 4 && value.Length <= 60)
                .Where(value => value.Any(char.IsLetterOrDigit))
                .Where(value => value.Count(ch => ch == '?' || ch == '\uFFFD') <= Math.Max(1, value.Length / 5))
                .Where(value => !value.Equals("Eps File:", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }
    }
}
