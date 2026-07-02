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
            if (LooksLikeSqliteDatabase(path, header)) return "SQLite database";
            if (LooksLikeMicrosoftJetDatabase(header)) return "Microsoft Access/Jet database";
            if (LooksLikeFirebirdDatabase(path, header)) return "Firebird database";
            if (LooksLikeFits(path, header)) return "FITS scientific data file";
            if (LooksLikeIccProfile(path, header)) return "ICC color profile";
            if (LooksLikeEmbeddedOpenType(path, header)) return "Embedded OpenType web font";
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
            AddSqliteDatabaseInfo(sections, path, header, sample, fileLength);
            AddJetDatabaseInfo(sections, path, header, sample, fileLength);
            AddFirebirdDatabaseInfo(sections, path, header, sample, fileLength);
            AddFitsInfo(sections, path, header, sample, fileLength);
            AddIccProfileInfo(sections, path, header, fileLength);
            AddEmbeddedOpenTypeInfo(sections, path, header, fileLength);
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

        private static bool LooksLikeSqliteDatabase(string path, byte[] header)
        {
            if (StartsWith(header, Encoding.ASCII.GetBytes("SQLite format 3\0")))
                return true;

            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".sqlite" || ext == ".sqlite3" || ext == ".db3";
        }

        private static bool IsSqliteDatabaseExtension(string extension)
        {
            var ext = (extension ?? string.Empty).ToLowerInvariant();
            return ext == ".sqlite" || ext == ".sqlite3" || ext == ".db" || ext == ".db3";
        }

        private static void AddSqliteDatabaseInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            if (!LooksLikeSqliteDatabase(path, header))
                return;

            var section = AddSection(sections, "Database");
            Add(section, "Format hint", "SQLite database");
            Add(section, "Header marker", StartsWith(header, Encoding.ASCII.GetBytes("SQLite format 3\0")) ? "SQLite format 3" : "Extension-level SQLite family hint");
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "Likely role", SqliteDatabaseRole(path));

            var visible = FindReadableTextLines(sample, 3, 120)
                .Where(IsUsefulSqliteVisibleString)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(24)
                .ToArray();
            if (visible.Length > 0)
                Add(section, "Visible schema or metadata strings", string.Join("\r\n", visible));

            Add(section, "Notes", "SQLite files commonly use .sqlite, .sqlite3, .db, or .db3 extensions. FileDentify reports the database container and visible schema strings only; it does not open tables, run queries, or extract records.");
        }

        private static string SqliteDatabaseRole(string path)
        {
            var lower = path.ToLowerInvariant();
            if (lower.Contains("favorites"))
                return "Favorites/bookmark catalogue or application preference database";
            if (lower.Contains("cache"))
                return "Application cache database";
            if (lower.Contains("history"))
                return "History database";
            if (lower.Contains("cookies"))
                return "Cookie database";
            if (lower.Contains("places.sqlite"))
                return "Firefox/Mozilla places history and bookmarks database";
            if (lower.Contains("manifest.db"))
                return "Apple mobile-backup manifest database";
            return "Application database or catalogue";
        }

        private static bool IsUsefulSqliteVisibleString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            return value.IndexOf("CREATE TABLE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("CREATE INDEX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("sqlite_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("table", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("index", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("schema", StringComparison.OrdinalIgnoreCase) >= 0;
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

        private static bool LooksLikeFirebirdDatabase(string path, byte[] header)
        {
            if (!Path.GetExtension(path).Equals(".fdb", StringComparison.OrdinalIgnoreCase))
                return false;
            return header.Length >= 32 && header[0] == 0x01 && header[4] == 0x1D;
        }

        private static void AddFirebirdDatabaseInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            if (!LooksLikeFirebirdDatabase(path, header))
                return;
            var section = AddSection(sections, "Database");
            Add(section, "Format hint", "Firebird database");
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "Likely role", Path.GetFileName(path).Equals("security3.fdb", StringComparison.OrdinalIgnoreCase) ? "Firebird security database" : "Firebird application database");
            var strings = FindReadableTextLines(sample, 4, 100)
                .Where(value => value.IndexOf("RDB$", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("Firebird", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("SQL", StringComparison.OrdinalIgnoreCase) >= 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToArray();
            if (strings.Length > 0)
                Add(section, "Visible database strings", string.Join("\r\n", strings));
            Add(section, "Notes", "Firebird .fdb files are relational databases used by Firebird and bundled applications such as LibreOffice Base support files. FileDentify reports the container role and safe visible strings only; it does not open tables, run queries, or extract records.");
        }

        private static bool LooksLikeFits(string path, byte[] header)
        {
            return Path.GetExtension(path).Equals(".fits", StringComparison.OrdinalIgnoreCase) &&
                StartsWith(header, Encoding.ASCII.GetBytes("SIMPLE  ="));
        }

        private static void AddFitsInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            if (!LooksLikeFits(path, header))
                return;
            var section = AddSection(sections, "Scientific data");
            Add(section, "Format hint", "FITS scientific/astronomy data file");
            Add(section, "File size", FormatBytes(fileLength));
            var text = Encoding.ASCII.GetString(sample.Take(Math.Min(sample.Length, 64 * 1024)).ToArray());
            var cards = FitsCards(text).ToArray();
            foreach (var key in new[] { "BITPIX", "NAXIS", "NAXIS1", "NAXIS2", "EXTEND", "BSCALE", "BZERO" })
                AddFitsCard(section, cards, key);
            Add(section, "Header cards in sample", cards.Count(card => Regex.IsMatch(card, @"^[A-Z0-9_-]{1,8}\s*=")).ToString(CultureInfo.InvariantCulture));
            Add(section, "Notes", "FITS files are scientific data containers common in astronomy and numerical libraries. FileDentify reports header cards only; it does not render images or parse data tables.");
        }

        private static IEnumerable<string> FitsCards(string text)
        {
            text = text ?? string.Empty;
            for (var offset = 0; offset + 80 <= text.Length; offset += 80)
                yield return text.Substring(offset, 80);
        }

        private static void AddFitsCard(ReportSection section, IEnumerable<string> cards, string key)
        {
            var match = (cards ?? Enumerable.Empty<string>())
                .Select(card => Regex.Match(card, "^" + Regex.Escape(key).PadRight(8) + @"=\s*(?<value>[^/]+)"))
                .FirstOrDefault(item => item.Success);
            if (match != null && match.Success)
                Add(section, key, match.Groups["value"].Value.Trim());
        }

        private static bool LooksLikeIccProfile(string path, byte[] header)
        {
            if (!Path.GetExtension(path).Equals(".icc", StringComparison.OrdinalIgnoreCase) && !Path.GetExtension(path).Equals(".icm", StringComparison.OrdinalIgnoreCase))
                return false;
            return header.Length >= 40 && Encoding.ASCII.GetString(header, 36, 4) == "acsp";
        }

        private static void AddIccProfileInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
        {
            if (!LooksLikeIccProfile(path, header))
                return;
            var section = AddSection(sections, "Color profile");
            Add(section, "Format hint", "ICC color profile");
            Add(section, "File size", FormatBytes(fileLength));
            if (header.Length >= 24)
            {
                Add(section, "Profile/device class", Encoding.ASCII.GetString(header, 12, 4).Trim());
                Add(section, "Color space", Encoding.ASCII.GetString(header, 16, 4).Trim());
                Add(section, "PCS", Encoding.ASCII.GetString(header, 20, 4).Trim());
            }
            Add(section, "Header marker", "acsp");
            Add(section, "Notes", "ICC/ICM profiles describe color-management transforms for displays, printers, scanners, PDFs, and image workflows. FileDentify reports header fields only; it does not apply or validate the profile.");
        }

        private static bool LooksLikeEmbeddedOpenType(string path, byte[] header)
        {
            if (!Path.GetExtension(path).Equals(".eot", StringComparison.OrdinalIgnoreCase))
                return false;
            return header.Length >= 36 && header[34] == (byte)'L' && header[35] == (byte)'P';
        }

        private static void AddEmbeddedOpenTypeInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
        {
            if (!LooksLikeEmbeddedOpenType(path, header))
                return;
            var section = AddSection(sections, "Font");
            Add(section, "Format", "Embedded OpenType web font");
            Add(section, "File size", FormatBytes(fileLength));
            if (header.Length >= 8)
            {
                Add(section, "EOT size field", FormatBytes(ReadUInt32LittleEndian(header, 0)));
                Add(section, "Embedded font size field", FormatBytes(ReadUInt32LittleEndian(header, 4)));
            }
            Add(section, "Header marker", "LP at EOT header font-data offset");
            Add(section, "Notes", "Embedded OpenType .eot files are legacy web fonts, mainly associated with older Internet Explorer workflows. FileDentify reports header fields only; it does not render or install the font.");
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
