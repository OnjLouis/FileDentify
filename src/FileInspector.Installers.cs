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
        private static string InstallerDataTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (StartsWith(header, Encoding.ASCII.GetBytes("SZDD")))
                return "Windows setup compressed file";
            if (StartsWith(header, Encoding.ASCII.GetBytes("aLuZ")))
                return "InstallShield compiled setup script";
            if (StartsWith(header, Encoding.ASCII.GetBytes("ISc(")))
                return "InstallShield setup header/data file";
            if (ext == ".inx")
                return "InstallShield setup script";
            if (ext == ".hdr")
                return "InstallShield setup header/data file";
            return null;
        }

        private static void AddInstallerDataInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
        {
            var type = InstallerDataTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Installer data");
            Add(section, "Format hint", type);
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "File role", InstallerRole(path, header));

            if (StartsWith(header, Encoding.ASCII.GetBytes("SZDD")))
            {
                Add(section, "Magic", "SZDD");
                if (header.Length >= 14)
                {
                    Add(section, "Compression mode byte", "0x" + header[8].ToString("X2", CultureInfo.InvariantCulture));
                    Add(section, "Original final character", header[9] >= 32 && header[9] < 127 ? ((char)header[9]).ToString() : "0x" + header[9].ToString("X2", CultureInfo.InvariantCulture));
                    Add(section, "Packed payload starts near", "0x0E");
                }
                Add(section, "Common use", "Old Windows setup/compress.exe single-file compression, often seen as .ex_, .dl_, .sy_, or similar underscore extensions.");
            }
            else if (StartsWith(header, Encoding.ASCII.GetBytes("aLuZ")))
            {
                Add(section, "Magic", "aLuZ");
                var notice = ReadAsciiZ(header, 6, 160);
                if (!string.IsNullOrWhiteSpace(notice))
                    Add(section, "Visible notice", notice);
            }
            else if (StartsWith(header, Encoding.ASCII.GetBytes("ISc(")))
            {
                Add(section, "Magic", "ISc(");
                if (header.Length >= 20)
                {
                    Add(section, "Header word 1", "0x" + ReadUInt32LittleEndian(header, 4).ToString("X8", CultureInfo.InvariantCulture));
                    Add(section, "Header word 2", "0x" + ReadUInt32LittleEndian(header, 16).ToString("X8", CultureInfo.InvariantCulture));
                }
            }

            var strings = FindReadableTextLines(header, 4, 80)
                .Where(IsUsefulInstallerString)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .ToArray();
            if (strings.Length > 0)
                Add(section, "Visible setup strings", string.Join(Environment.NewLine, strings));

            Add(section, "Notes", "Installer support reports header and setup-structure clues only. FileDentify does not execute installers or expand payloads.");
        }

        private static string InstallerRole(string path, byte[] header)
        {
            var name = Path.GetFileName(path) ?? string.Empty;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (StartsWith(header, Encoding.ASCII.GetBytes("SZDD")))
                return "compressed setup payload";
            if (name.Equals("setup.inx", StringComparison.OrdinalIgnoreCase) || ext == ".inx")
                return "compiled InstallShield setup script";
            if (ext == ".hdr")
                return "InstallShield cabinet/header metadata";
            return "installer support file";
        }

        private static bool IsUsefulInstallerString(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 160)
                return false;
            return value.IndexOf("InstallShield", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Copyright", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("setup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.EndsWith(".cab", StringComparison.OrdinalIgnoreCase) ||
                value.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
        }
    }
}
