using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace FileDentify
{
    internal static partial class FileInspector
    {
        private static string LegacySoundBankTypeName(string path, byte[] header, long fileLength)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".jlw" || ext == ".vop")
                return "Legacy JLW/VOP sound bank or voice data";
            return null;
        }

        private static void AddLegacySoundBankInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            var type = LegacySoundBankTypeName(path, header, fileLength);
            if (type == null)
                return;

            var section = AddSection(sections, "Legacy sound bank");
            Add(section, "Format hint", type);
            Add(section, "Extension", Path.GetExtension(path));
            Add(section, "File size", FormatBytes(fileLength) + " (" + fileLength.ToString(CultureInfo.InvariantCulture) + " bytes)");
            Add(section, "Public extension clues", ".jlw and .vop are reported by public extension databases as files used by RE-BELL RE-CORDER, DIY SoundBank, and USB MP3 Clock Writer.");

            if (fileLength == 262160)
                Add(section, "Size clue", "262,160 bytes equals 256 KiB plus 16 bytes, consistent with a small legacy device sound/voice bank or raw data payload with a tiny header.");

            if (header.Length >= 4)
                Add(section, "First big-endian words", string.Join(Environment.NewLine, FirstBigEndianWords(header, 16).ToArray()));

            var strings = FindReadableTextLines(sample, 4, 40)
                .Where(value => value.Length <= 120)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(16)
                .ToArray();
            if (strings.Length > 0)
                Add(section, "Readable strings", string.Join(Environment.NewLine, strings));

            Add(section, "Notes", "The payload is proprietary or compressed. Libmagic may misread the first bytes as an Adobe Photoshop color swatch, but the full file is much larger than that header would explain. FileDentify treats the extension and size pattern as stronger evidence for legacy sound-bank/voice data.");
        }

        private static IEnumerable<string> FirstBigEndianWords(byte[] data, int maxWords)
        {
            for (var offset = 0; offset + 4 <= data.Length && offset / 4 < maxWords; offset += 4)
            {
                var value = ReadUInt32BigEndian(data, offset);
                yield return "0x" + offset.ToString("X4", CultureInfo.InvariantCulture) + ": " + value.ToString(CultureInfo.InvariantCulture) + " (0x" + value.ToString("X8", CultureInfo.InvariantCulture) + ")";
            }
        }
    }
}
