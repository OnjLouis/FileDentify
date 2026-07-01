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
        private static string CommonDataTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (LooksLikeRtf(header)) return "Rich Text Format document";
            if (LooksLikeMicrosoftJetDatabase(header)) return "Microsoft Access/Jet database";
            if (ext == ".aff" && LooksLikeText(header)) return "Hunspell/MySpell affix dictionary";
            if (ext == ".dic" && LooksLikeText(header)) return DictionaryFormatHint(path, header);
            if (ext == ".dic" && PathLooksWindowsInputMethodDictionary(path)) return "Windows input-method dictionary";
            if (ext == ".tlb" && LooksLikeTypeLibrary(path, header)) return "COM type library";
            if (ext == ".pfb" || ext == ".pfm") return "PostScript Type 1 font support file";
            return null;
        }

        private static void AddCommonDataInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            AddRtfInfo(sections, path, header, sample, fileLength);
            AddDictionaryInfo(sections, path, header, sample, fileLength);
            AddJetDatabaseInfo(sections, path, header, sample, fileLength);
            AddTypeLibraryInfo(sections, path, header, sample, fileLength);
            AddPostScriptFontSupportInfo(sections, path, header, sample, fileLength);
        }

        private static bool LooksLikeRtf(byte[] header)
        {
            return StartsWith(header, Encoding.ASCII.GetBytes(@"{\rtf"));
        }

        private static void AddRtfInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            if (!LooksLikeRtf(header))
                return;
            var text = DecodeMostlyUtf8(sample);
            var section = AddSection(sections, "Rich Text Format");
            Add(section, "Format hint", "Rich Text Format document");
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "RTF version", Regex.Match(text, @"\\rtf(\d+)").Groups[1].Value);
            AddRegexValue(section, text, @"\\ansicpg(\d+)", "ANSI code page");
            AddRegexValue(section, text, @"\\deflang(\d+)", "Default language");
            Add(section, "Font table entries", Regex.Matches(text, @"\\f\d+\b").Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Color table entries", Regex.Matches(text, @"\\red\d+\\green\d+\\blue\d+", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
            var firstWords = RtfPlainTextPreview(text);
            if (!string.IsNullOrWhiteSpace(firstWords))
                Add(section, "Plain-text preview", firstWords);
            Add(section, "Notes", "FileDentify reports RTF header and safe visible text only. It does not render the document or execute embedded objects.");
        }

        private static void AddDictionaryInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".dic" && ext != ".aff")
                return;

            var textLike = LooksLikeText(header);
            var windowsInput = ext == ".dic" && PathLooksWindowsInputMethodDictionary(path);
            if (!textLike && !windowsInput)
                return;

            var section = AddSection(sections, "Dictionary / wordlist");
            Add(section, "Format hint", DictionaryFormatHint(path, header));
            Add(section, "File size", FormatBytes(fileLength));
            if (textLike)
            {
                var text = DecodeMostlyUtf8(sample);
                var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0)
                    .Take(24)
                    .ToArray();
                Add(section, "Non-empty lines in sample", CountNonEmptyLines(text).ToString(CultureInfo.InvariantCulture));
                Add(section, "Comment lines in sample", Regex.Matches(text, @"(?m)^\s*#").Count.ToString(CultureInfo.InvariantCulture));
                if (ext == ".aff")
                {
                    AddRegexValue(section, text, @"(?m)^\s*SET\s+(\S+)", "Declared encoding");
                    AddRegexValue(section, text, @"(?m)^\s*FLAG\s+(\S+)", "Flag format");
                    Add(section, "Affix rules in sample", Regex.Matches(text, @"(?m)^\s*(SFX|PFX)\s+", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
                }
                else if (lines.Length > 0 && Regex.IsMatch(lines[0], @"^\d+$"))
                {
                    Add(section, "Declared word count", lines[0]);
                }
                if (lines.Length > 0)
                    Add(section, "Preview lines", string.Join("\r\n", lines.Take(12).ToArray()));
            }
            else
            {
                Add(section, "Container", "Binary Windows input-method dictionary");
                Add(section, "Folder", Path.GetDirectoryName(path) ?? string.Empty);
            }
            Add(section, "Notes", "Dictionary files can be plain word lists, Hunspell/MySpell affix data, screen-reader speech dictionaries, or application-specific binary dictionaries. FileDentify reports safe structure and sample lines only.");
        }

        private static string DictionaryFormatHint(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".aff") return "Hunspell/MySpell affix dictionary";
            if (LooksLikeNvdaSpeechDictionary(path, header)) return "NVDA speech dictionary";
            if (path.IndexOf("\\NVDA\\", StringComparison.OrdinalIgnoreCase) >= 0) return "NVDA dictionary/support data";
            if (PathLooksWindowsInputMethodDictionary(path)) return "Windows input-method dictionary";
            return "Dictionary or wordlist";
        }

        private static bool LooksLikeNvdaSpeechDictionary(string path, byte[] header)
        {
            return path.IndexOf("\\speechDicts\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                Regex.IsMatch(DecodeMostlyUtf8(header), @"(?m)^\s*[^#;\r\n]+\t[^\r\n]*\t(?:0|1|true|false)\b", RegexOptions.IgnoreCase);
        }

        private static bool PathLooksWindowsInputMethodDictionary(string path)
        {
            return path.IndexOf("\\InputMethod\\Dictionaries\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool LooksLikeMicrosoftJetDatabase(byte[] header)
        {
            return header.Length >= 20 && Encoding.ASCII.GetString(header, 4, 15).Equals("Standard Jet DB", StringComparison.Ordinal);
        }

        private static void AddJetDatabaseInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            if (!LooksLikeMicrosoftJetDatabase(header))
                return;
            var section = AddSection(sections, "Database");
            Add(section, "Format hint", "Microsoft Access/Jet database");
            Add(section, "Header marker", "Standard Jet DB");
            Add(section, "File size", FormatBytes(fileLength));
            var version = CleanMetadataText(Encoding.ASCII.GetString(header, Math.Min(0x14C, header.Length), Math.Max(0, Math.Min(16, header.Length - 0x14C))).TrimEnd('\0'));
            if (!string.IsNullOrWhiteSpace(version))
                Add(section, "Jet version marker", version);
            var names = FindUtf16Strings(sample, 3, 80, true)
                .Select(item => item.Value)
                .Where(value => Regex.IsMatch(value, @"^[A-Za-z_][A-Za-z0-9_ ]{2,60}$"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(24)
                .ToArray();
            if (names.Length > 0)
                Add(section, "Visible object/table names", string.Join("\r\n", names));
            Add(section, "Notes", "FileDentify reports Jet database header and visible names only. It does not open tables, run queries, or extract records.");
        }

        private static bool LooksLikeTypeLibrary(string path, byte[] header)
        {
            if (!Path.GetExtension(path).Equals(".tlb", StringComparison.OrdinalIgnoreCase))
                return false;
            return IndexOfAscii(header, "TYPELIB") >= 0 || IndexOfAscii(header, "MSFT") >= 0 || StartsWith(header, Encoding.ASCII.GetBytes("MZ"));
        }

        private static void AddTypeLibraryInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            if (!LooksLikeTypeLibrary(path, header))
                return;
            var section = AddSection(sections, "COM type library");
            Add(section, "Format hint", "COM/OLE type library metadata");
            Add(section, "File size", FormatBytes(fileLength));
            if (StartsWith(header, Encoding.ASCII.GetBytes("MZ")))
                Add(section, "Container", "Portable Executable wrapper with type-library resource");
            var marker = IndexOfAscii(sample, "TYPELIB");
            if (marker >= 0)
                Add(section, "TYPELIB marker offset", "0x" + marker.ToString("X", CultureInfo.InvariantCulture));
            var msft = IndexOfAscii(sample, "MSFT");
            if (msft >= 0)
                Add(section, "MSFT marker offset", "0x" + msft.ToString("X", CultureInfo.InvariantCulture));
            Add(section, "Notes", "Type libraries describe COM classes, interfaces, methods, and automation metadata. FileDentify reports container markers only; it does not register or load the library.");
        }

        private static void AddPostScriptFontSupportInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".pfb" && ext != ".pfm")
                return;
            var section = AddSection(sections, "Font");
            Add(section, "Format", ext == ".pfb" ? "PostScript Type 1 printer font binary" : "PostScript Type 1 printer font metrics");
            Add(section, "File size", FormatBytes(fileLength));
            if (ext == ".pfb" && header.Length >= 6 && header[0] == 0x80)
            {
                Add(section, "PFB segment type", header[1].ToString(CultureInfo.InvariantCulture));
                Add(section, "First segment length", FormatBytes(ReadUInt32LittleEndian(header, 2)));
            }
            var strings = FindReadableTextLines(sample, 4, 40)
                .Where(line => line.IndexOf("Font", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("OTF", StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(12)
                .ToArray();
            if (strings.Length > 0)
                Add(section, "Visible font strings", string.Join("\r\n", strings));
            Add(section, "Notes", "Type 1 font support files are legacy PostScript font resources. FileDentify reports safe header and visible strings only.");
        }

        private static void AddRegexValue(ReportSection section, string text, string pattern, string title)
        {
            var match = Regex.Match(text ?? string.Empty, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
                Add(section, title, match.Groups[1].Value);
        }

        private static int CountNonEmptyLines(string text)
        {
            return (text ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Count(line => line.Trim().Length > 0);
        }

        private static string DecodeMostlyUtf8(byte[] data)
        {
            if (data == null || data.Length == 0)
                return string.Empty;
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
                return Encoding.UTF8.GetString(data, 3, data.Length - 3);
            return Encoding.UTF8.GetString(data);
        }

        private static string RtfPlainTextPreview(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            var cleaned = Regex.Replace(text, @"\\'[0-9a-fA-F]{2}", " ");
            cleaned = Regex.Replace(cleaned, @"\\[a-zA-Z]+\d* ?", " ");
            cleaned = cleaned.Replace("{", " ").Replace("}", " ").Replace("\\", " ");
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            return cleaned.Length > 600 ? cleaned.Substring(0, 600) + "..." : cleaned;
        }
    }
}
