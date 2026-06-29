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
        private static string FirmwareTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (StartsWith(header, Encoding.ASCII.GetBytes("_PT_")))
                return "PC BIOS/UEFI firmware image";
            if (StartsWith(header, Encoding.ASCII.GetBytes("Roland SRX")))
                return "Roland SRX expansion ROM image";
            if (StartsWith(header, Encoding.ASCII.GetBytes("XMVh")))
                return "Roland FA screen saver/movie data";
            if (ext == ".bin" && PathLooksRolandExpansion(path))
                return "Roland FA/SRX expansion image";
            if (ext == ".1q8" || Regex.IsMatch(ext, @"^\.[0-9a-z]{3}$", RegexOptions.IgnoreCase))
            {
                var name = Path.GetFileName(path) ?? string.Empty;
                if (name.StartsWith("E7A32", StringComparison.OrdinalIgnoreCase) || StartsWith(header, Encoding.ASCII.GetBytes("_PT_")))
                    return "MSI motherboard BIOS image";
            }
            return null;
        }

        private static void AddFirmwareInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
        {
            var type = FirmwareTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Firmware / device image");
            Add(section, "Format hint", type);
            Add(section, "File size", FormatBytes(fileLength) + " (" + fileLength.ToString(CultureInfo.InvariantCulture) + " bytes)");

            if (StartsWith(header, Encoding.ASCII.GetBytes("_PT_")))
                AddPcFirmwareInfo(section, path, header);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("Roland SRX")) || PathLooksRolandExpansion(path))
                AddRolandExpansionInfo(section, path, header);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("XMVh")))
                AddRolandMovieInfo(section, header);

            Add(section, "Notes", "Firmware and device images are reported from headers, filenames, and visible strings only. FileDentify does not flash, unpack, or modify them.");
        }

        private static void AddPcFirmwareInfo(ReportSection section, string path, byte[] header)
        {
            Add(section, "Header marker", StartsWith(header, Encoding.ASCII.GetBytes("_PT_")) ? "_PT_" : "Not found in first sample");
            var strings = FindAsciiStrings(header, 4, 80).Select(s => s.Value).Distinct(StringComparer.OrdinalIgnoreCase).Take(30).ToArray();
            if (strings.Length > 0)
                Add(section, "Visible firmware strings", string.Join("\r\n", strings));
            var name = Path.GetFileName(path) ?? string.Empty;
            if (Regex.IsMatch(name, @"^E[0-9A-Z]{5,}\.", RegexOptions.IgnoreCase))
                Add(section, "Filename hint", "MSI-style motherboard BIOS filename");
        }

        private static void AddRolandExpansionInfo(ReportSection section, string path, byte[] header)
        {
            if (StartsWith(header, Encoding.ASCII.GetBytes("Roland SRX")))
                Add(section, "Header marker", "Roland SRX");
            var title = ReadFixedAscii(header, 16, 32);
            if (!string.IsNullOrWhiteSpace(title))
                Add(section, "Visible title", title);
            var parent = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(parent))
                Add(section, "Folder hint", parent);
            Add(section, "Common use", "Roland FA/Jupiter/Integra-style expansion image or SRX-derived sound expansion data.");
        }

        private static void AddRolandMovieInfo(ReportSection section, byte[] header)
        {
            Add(section, "Header marker", "XMVh");
            if (header.Length >= 16)
            {
                Add(section, "Width-like field", ReadUInt16BigEndian(header, 4).ToString(CultureInfo.InvariantCulture));
                Add(section, "Height-like field", ReadUInt16BigEndian(header, 6).ToString(CultureInfo.InvariantCulture));
                Add(section, "Frame/count-like field", ReadUInt32BigEndian(header, 8).ToString(CultureInfo.InvariantCulture));
            }
            if (IndexOfAscii(header, "XMVf") >= 0)
                Add(section, "Frame marker", "XMVf found in header sample");
        }

        private static bool PathLooksRolandExpansion(string path)
        {
            var value = path ?? string.Empty;
            return value.IndexOf("RolandFA", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("FA_EXP-", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("EXP", StringComparison.OrdinalIgnoreCase) >= 0 && value.IndexOf("Roland", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
