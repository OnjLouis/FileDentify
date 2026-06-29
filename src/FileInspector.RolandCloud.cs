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
        private static string RolandCloudTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (StartsWith(header, Encoding.ASCII.GetBytes("VEXP")) || ext == ".exz")
                return "Roland Cloud expansion package";
            if (StartsWith(header, Encoding.ASCII.GetBytes("KoaBankFile")))
                return "Roland Cloud preset bank";
            if (Path.GetFileName(path).Equals("InstalledBankNames.dat", StringComparison.OrdinalIgnoreCase) && IsRolandCloudPath(path))
                return "Roland Cloud installed bank list";
            return null;
        }

        private static void AddRolandCloudInfo(List<ReportSection> sections, string path, byte[] header, byte[] stringSample, long fileLength)
        {
            var type = RolandCloudTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Roland Cloud");
            Add(section, "Format hint", type);
            Add(section, "Product folder", RolandCloudProductFromPath(path));
            Add(section, "File size", FormatBytes(fileLength));

            if (StartsWith(header, Encoding.ASCII.GetBytes("VEXP")))
                AddRolandCloudExpansionInfo(section, header);
            else if (StartsWith(header, Encoding.ASCII.GetBytes("KoaBankFile")))
                AddRolandCloudPresetBankInfo(section, header, stringSample);
            else if (Path.GetFileName(path).Equals("InstalledBankNames.dat", StringComparison.OrdinalIgnoreCase))
                AddRolandCloudInstalledBanks(section, stringSample);

            Add(section, "Notes", "Roland Cloud files are proprietary synthesizer, preset, and expansion data. FileDentify reports visible header fields and bank names only; it does not decode synth parameters or audio payloads.");
        }

        private static void AddRolandCloudExpansionInfo(ReportSection section, byte[] header)
        {
            Add(section, "Header marker", "VEXP");
            var code = ReadFixedAscii(header, 0x12, 14).Trim();
            var name = ReadFixedAscii(header, 0x20, 28).Trim();
            if (!string.IsNullOrWhiteSpace(code))
                Add(section, "Expansion code", code);
            if (!string.IsNullOrWhiteSpace(name))
                Add(section, "Expansion name", name);
            if (header.Length >= 0x10)
                Add(section, "Header size field", ReadUInt32LittleEndian(header, 4).ToString(CultureInfo.InvariantCulture));
        }

        private static void AddRolandCloudPresetBankInfo(ReportSection section, byte[] header, byte[] sample)
        {
            Add(section, "Header marker", "KoaBankFile");
            var version = ReadFixedAscii(header, 11, 5).Trim();
            if (!string.IsNullOrWhiteSpace(version))
                Add(section, "Bank file version", version);
            var bankName = ReadFixedAscii(header, 16, 24).Trim();
            if (!string.IsNullOrWhiteSpace(bankName))
                Add(section, "Visible bank name", bankName);

            var names = FindReadableTextLines(sample, 4, 80)
                .Where(s => s.IndexOf("Preset", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.IndexOf("PG-", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.IndexOf("Bank", StringComparison.OrdinalIgnoreCase) >= 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToArray();
            if (names.Length > 0)
                Add(section, "Visible bank strings", string.Join(Environment.NewLine, names));
        }

        private static void AddRolandCloudInstalledBanks(ReportSection section, byte[] sample)
        {
            var names = FindReadableTextLines(sample, 2, 80)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(30)
                .ToArray();
            if (names.Length > 0)
                Add(section, "Installed banks", string.Join(Environment.NewLine, names));
        }

        private static bool IsRolandCloudPath(string path)
        {
            return path.IndexOf("Roland Cloud", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string RolandCloudProductFromPath(string path)
        {
            var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            for (var i = 0; i < parts.Length; i++)
                if (parts[i].Equals("Roland Cloud", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length)
                    return parts[i + 1];
            var parent = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);
            return parent ?? string.Empty;
        }
    }
}
