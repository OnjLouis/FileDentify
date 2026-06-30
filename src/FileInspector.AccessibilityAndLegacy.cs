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
        private static string AccessibilityDataTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if ((ext == ".ctb" || ext == ".utb" || ext == ".uti" || ext == ".cti" || ext == ".dis") && LooksLikeLiblouisTable(path, header))
                return "liblouis braille translation table";
            return null;
        }

        private static void AddAccessibilityDataInfo(List<ReportSection> sections, string path, byte[] header)
        {
            if (AccessibilityDataTypeName(path, header) == null)
                return;

            var text = DecodeTextSample(header, 512 * 1024);
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToArray();
            var section = AddSection(sections, "Accessibility data");
            Add(section, "Format hint", "liblouis braille translation table");
            Add(section, "File name", Path.GetFileName(path));
            Add(section, "Extension", Path.GetExtension(path));
            var title = lines.FirstOrDefault(line => line.StartsWith("# liblouis:", StringComparison.OrdinalIgnoreCase));
            if (title != null)
                Add(section, "Table title", CleanMetadataText(title.Substring("# liblouis:".Length).Trim()));
            var includes = lines.Where(line => line.StartsWith("include ", StringComparison.OrdinalIgnoreCase)).Take(12).ToArray();
            if (includes.Length > 0)
                Add(section, "Included tables", string.Join(Environment.NewLine, includes));
            Add(section, "Opcode lines in sample", lines.Count(line => !line.StartsWith("#", StringComparison.Ordinal)).ToString(CultureInfo.InvariantCulture));
            Add(section, "Notes", "liblouis tables define braille translation, display, contraction, and include rules. FileDentify reports table identity and sampled structure only; it does not compile or validate the table.");
        }

        private static string LegacyAppResourceTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (StartsWith(header, Encoding.ASCII.GetBytes("RocK")))
                return "Rockbox plug-in";
            if (ext == ".milk" && DecodeTextSample(header, 8192).IndexOf("[preset", StringComparison.OrdinalIgnoreCase) >= 0)
                return "MilkDrop visualisation preset";
            if (ext == ".maki" && StartsWith(header, Encoding.ASCII.GetBytes("FG")))
                return "Winamp Modern skin MAKI script";
            if (ext == ".avb" && DecodeTextSample(header, 4096).IndexOf("Microsoft Corporation", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Microsoft Chat Comic Art avatar";
            if (ext == ".hal" && DecodeTextSample(header, 4096).IndexOf("\\013", StringComparison.OrdinalIgnoreCase) >= 0)
                return "HAL speech mapping data";
            return null;
        }

        private static void AddLegacyAppResourceInfo(List<ReportSection> sections, string path, byte[] header, byte[] stringSample, long fileLength)
        {
            var type = LegacyAppResourceTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Legacy app/plugin resource");
            Add(section, "Format hint", type);
            Add(section, "File name", Path.GetFileName(path));
            Add(section, "File size", FormatBytes(fileLength));
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (type == "Rockbox plug-in")
            {
                Add(section, "Header marker", "RocK");
                Add(section, "Role", "Rockbox firmware plug-in or application module");
            }
            else if (type == "MilkDrop visualisation preset")
            {
                var text = DecodeTextSample(stringSample, 256 * 1024);
                Add(section, "Preset section", FirstNonEmptyLine(text));
                AddNamedAssignment(section, text, "fRating");
                AddNamedAssignment(section, text, "fGammaAdj");
                AddNamedAssignment(section, text, "fDecay");
                Add(section, "Equation lines in sample", text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Count(line => line.IndexOf("per_", StringComparison.OrdinalIgnoreCase) >= 0).ToString(CultureInfo.InvariantCulture));
            }
            else if (type == "Winamp Modern skin MAKI script")
            {
                Add(section, "Header marker", "FG");
                var strings = FindAsciiStrings(stringSample, 4, 80).Select(item => item.Value.Trim()).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(16).ToArray();
                if (strings.Length > 0)
                    Add(section, "Visible script strings", string.Join(Environment.NewLine, strings));
            }
            else if (type == "Microsoft Chat Comic Art avatar")
            {
                var strings = FindAsciiStrings(stringSample, 4, 80).Select(item => item.Value.Trim()).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToArray();
                if (strings.Length > 0)
                    Add(section, "Visible avatar strings", string.Join(Environment.NewLine, strings));
            }
            else if (type == "HAL speech mapping data")
            {
                var text = DecodeTextSample(stringSample, 64 * 1024);
                var entries = text.Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(item => item.Trim()).Where(item => item.Length > 0).Take(20).ToArray();
                if (entries.Length > 0)
                    Add(section, "Sample mappings", string.Join(Environment.NewLine, entries));
            }

            Add(section, "Notes", "This is legacy application or plug-in support data. FileDentify reports visible structure and role clues only; it does not execute plug-ins, scripts, avatars, or speech mappings.");
        }

        private static bool LooksLikeLiblouisTable(string path, byte[] header)
        {
            if (path.IndexOf("\\louis\\tables\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            var text = DecodeTextSample(header, 8192);
            return text.IndexOf("liblouis", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("include ", StringComparison.OrdinalIgnoreCase) >= 0 && text.IndexOf("braille", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string DecodeTextSample(byte[] data, int maxBytes)
        {
            if (data == null || data.Length == 0)
                return string.Empty;
            return Encoding.UTF8.GetString(data.Take(Math.Min(data.Length, maxBytes)).ToArray());
        }

        private static string FirstNonEmptyLine(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            return text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0) ?? string.Empty;
        }

        private static void AddNamedAssignment(ReportSection section, string text, string name)
        {
            var prefix = name + "=";
            var line = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(item => item.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (line != null)
                Add(section, name, CleanMetadataText(line.Substring(prefix.Length).Trim()));
        }
    }
}
