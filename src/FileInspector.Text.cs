using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace FileDentify
{
    internal static partial class FileInspector
    {        private static void AddTextInfo(List<ReportSection> sections, byte[] data)
        {
            if (!LooksLikeText(data))
                return;
            var section = AddSection(sections, "Text hints");
            var encoding = "No BOM detected";
            if (StartsWith(data, new byte[] { 0xef, 0xbb, 0xbf })) encoding = "UTF-8 with BOM";
            else if (StartsWith(data, new byte[] { 0xff, 0xfe, 0x00, 0x00 })) encoding = "UTF-32 little-endian BOM";
            else if (StartsWith(data, new byte[] { 0x00, 0x00, 0xfe, 0xff })) encoding = "UTF-32 big-endian BOM";
            else if (StartsWith(data, new byte[] { 0xff, 0xfe })) encoding = "UTF-16 little-endian BOM";
            else if (StartsWith(data, new byte[] { 0xfe, 0xff })) encoding = "UTF-16 big-endian BOM";
            Add(section, "Encoding marker", encoding);
            var crlf = CountSequence(data, new byte[] { 0x0d, 0x0a });
            var lf = data.Count(b => b == 0x0a) - crlf;
            var cr = data.Count(b => b == 0x0d) - crlf;
            Add(section, "Line endings in sample", "CRLF: " + crlf + ", LF: " + lf + ", CR: " + cr);
        }

        private static int CountSequence(byte[] data, byte[] sequence)
        {
            var count = 0;
            for (var i = 0; i <= data.Length - sequence.Length; i++)
            {
                var match = true;
                for (var j = 0; j < sequence.Length; j++)
                    if (data[i + j] != sequence[j])
                    {
                        match = false;
                        break;
                    }
                if (match)
                    count++;
            }
            return count;
        }

        private static List<FoundString> FindAsciiStrings(byte[] data, int minLength, int maxResults)
        {
            var results = new List<FoundString>();
            var i = 0;
            while (i < data.Length && results.Count < maxResults)
            {
                if (data[i] >= 32 && data[i] < 127)
                {
                    var start = i;
                    while (i < data.Length && data[i] >= 32 && data[i] < 127)
                        i++;
                    if (i - start >= minLength)
                    {
                        var value = Encoding.ASCII.GetString(data, start, Math.Min(i - start, 160));
                        results.Add(new FoundString { Offset = start, Value = value });
                    }
                }
                else
                {
                    i++;
                }
            }
            return results;
        }

        private static List<FoundString> FindHighBitAsciiStrings(byte[] data, int minLength, int maxResults)
        {
            var results = new List<FoundString>();
            var i = 0;
            while (i < data.Length && results.Count < maxResults)
            {
                if (!IsHighBitPrintableAscii(data[i]))
                {
                    i++;
                    continue;
                }

                var start = i;
                var chars = new List<byte>();
                chars.Add((byte)(data[i] & 0x7f));
                i++;
                while (i < data.Length && data[i] >= 32 && data[i] < 127)
                {
                    chars.Add(data[i]);
                    i++;
                    if (chars.Count >= 160)
                        break;
                }

                if (chars.Count >= minLength)
                {
                    var value = Encoding.ASCII.GetString(chars.ToArray());
                    results.Add(new FoundString { Offset = start, Value = value });
                }
            }
            return results;
        }

        private static void AddReadableTextInfo(List<ReportSection> sections, byte[] data)
        {
            const int maxLines = 200;
            var lines = FindReadableTextLines(data, 4, maxLines);
            var section = AddSection(sections, "Readable text");
            if (lines.Count == 0)
            {
                Add(section, "No readable text found", "No ASCII or UTF-16 text runs of at least four characters were found in the sampled data.");
                return;
            }

            foreach (var line in lines)
                Add(section, line, line);
            Add(section, "Scan note", "Plain text view of strings found in the first " + FormatBytes(data.Length) + ". Use F4 Advanced file viewer for deeper text, hex, binary, or octal review; offsets are kept in the separate Printable strings section.");
            if (lines.Count >= maxLines)
                Add(section, "Limit", "Showing the first " + maxLines.ToString(CultureInfo.InvariantCulture) + " readable text runs.");
        }

        private static List<string> FindReadableTextLines(byte[] data, int minLength, int maxResults)
        {
            var lines = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var candidates = new List<FoundString>();
            candidates.AddRange(FindAsciiStrings(data, minLength, maxResults * 2));
            candidates.AddRange(FindHighBitAsciiStrings(data, minLength, maxResults * 2));
            candidates.AddRange(FindUtf16Strings(data, minLength, maxResults, true));
            candidates.AddRange(FindUtf16Strings(data, minLength, maxResults, false));
            AddReadableLines(lines, seen, candidates.OrderBy(item => item.Offset).ThenByDescending(item => item.Value.Length).Select(s => s.Value), maxResults);
            return lines;
        }

        private static void AddReadableLines(List<string> lines, HashSet<string> seen, IEnumerable<string> values, int maxResults)
        {
            foreach (var value in values)
            {
                var cleaned = CleanReadableText(value);
                if (!IsUsefulReadableLine(cleaned) || seen.Contains(cleaned))
                    continue;

                if (IsReadableFragmentOfExistingLine(lines, cleaned))
                    continue;

                RemoveExistingReadableFragments(lines, seen, cleaned);

                seen.Add(cleaned);
                lines.Add(cleaned);
                if (lines.Count >= maxResults)
                    break;
            }
        }

        private static bool IsReadableFragmentOfExistingLine(List<string> lines, string candidate)
        {
            var normalizedCandidate = NormalizeReadableLineForFragmentMatch(candidate);
            if (normalizedCandidate.Length < 6)
                return false;

            foreach (var existing in lines)
            {
                var normalizedExisting = NormalizeReadableLineForFragmentMatch(existing);
                if (string.Equals(normalizedCandidate, normalizedExisting, StringComparison.Ordinal))
                    return true;
                if (IsReadableFragment(normalizedCandidate, normalizedExisting))
                    return true;
            }

            return false;
        }

        private static void RemoveExistingReadableFragments(List<string> lines, HashSet<string> seen, string candidate)
        {
            var normalizedCandidate = NormalizeReadableLineForFragmentMatch(candidate);
            if (normalizedCandidate.Length < 6)
                return;

            for (var i = lines.Count - 1; i >= 0; i--)
            {
                var normalizedExisting = NormalizeReadableLineForFragmentMatch(lines[i]);
                if (string.Equals(normalizedExisting, normalizedCandidate, StringComparison.Ordinal))
                {
                    seen.Remove(lines[i]);
                    lines.RemoveAt(i);
                    continue;
                }
                if (!IsReadableFragment(normalizedExisting, normalizedCandidate))
                    continue;
                seen.Remove(lines[i]);
                lines.RemoveAt(i);
            }
        }

        private static bool IsReadableFragment(string possibleFragment, string possibleFullLine)
        {
            if (string.IsNullOrEmpty(possibleFragment) || string.IsNullOrEmpty(possibleFullLine))
                return false;
            if (possibleFragment.Length >= possibleFullLine.Length)
                return false;
            if (possibleFragment.Length < 6)
                return false;
            if (possibleFullLine.Length - possibleFragment.Length < 1)
                return false;
            if (possibleFullLine.IndexOf(possibleFragment, StringComparison.Ordinal) < 0)
                return false;

            var ratio = (double)possibleFragment.Length / possibleFullLine.Length;
            return ratio >= 0.55;
        }

        private static string NormalizeReadableLineForFragmentMatch(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var sb = new StringBuilder(value.Length);
            foreach (var ch in value.Trim())
            {
                if (ch > 127)
                    continue;
                if (char.IsLetterOrDigit(ch))
                    sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }

        private static string CleanReadableText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            var sb = new StringBuilder();
            var lastWasSpace = false;
            foreach (var ch in value.Trim())
            {
                if (char.IsControl(ch))
                    continue;
                if (char.IsWhiteSpace(ch))
                {
                    if (!lastWasSpace)
                        sb.Append(' ');
                    lastWasSpace = true;
                }
                else
                {
                    sb.Append(ch);
                    lastWasSpace = false;
                }
                if (sb.Length >= 240)
                    break;
            }
            return sb.ToString().Trim();
        }

        private static bool IsUsefulReadableLine(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length < 6)
                return false;

            if (value.IndexOf("ABCDEFGHIJKLMNOPQRSTUVWXYZ", StringComparison.Ordinal) >= 0)
                return false;

            var lettersOrDigits = value.Count(char.IsLetterOrDigit);
            if (lettersOrDigits < 3)
                return false;

            var symbols = value.Count(ch => !char.IsLetterOrDigit(ch) && !char.IsWhiteSpace(ch));
            if (symbols > lettersOrDigits)
                return false;

            var extendedChars = value.Count(ch => ch > 127);
            if (extendedChars > 0 && extendedChars > value.Length / 5)
                return false;
            if (extendedChars >= 2 && symbols >= 4)
                return false;
            if (!value.Any(char.IsWhiteSpace) && (extendedChars >= 2 || (extendedChars > 0 && value.Length > 10)))
                return false;

            if (!value.Any(char.IsWhiteSpace) && symbols >= 4)
                return false;

            if (!value.Any(char.IsWhiteSpace) && symbols >= 2 && value.IndexOfAny(new[] { ':', '\\', '/', '.' }) < 0)
                return false;

            if (!value.Any(char.IsWhiteSpace) && value.Length >= 10 && symbols >= 2 && NaturalWordCount(value) <= 1)
                return false;

            if (LooksLikeLeadingTailFragment(value))
                return false;

            if (HasLongAscendingAsciiLetterRun(value, 5))
                return false;

            if (value.Length > 30)
            {
                var dominant = value.Where(char.IsLetterOrDigit).GroupBy(ch => ch).Select(g => g.Count()).DefaultIfEmpty(0).Max();
                if (dominant > lettersOrDigits / 3)
                    return false;
            }

            var naturalWords = NaturalWordCount(value);
            if (naturalWords == 0)
                return false;

            if (value.Any(char.IsWhiteSpace) && value.Length >= 8 && naturalWords >= 2)
                return true;

            if (value.Length >= 8 && naturalWords >= 1 && value.Any(ch => ch == ':' || ch == '\\' || ch == '/' || ch == '.' || ch == '-' || ch == '_'))
                return true;

            var vowels = value.Count(ch => "aeiouAEIOU".IndexOf(ch) >= 0);
            if (value.Length >= 12 && vowels >= 2 && symbols <= lettersOrDigits / 4)
                return true;

            return false;
        }

        private static bool LooksLikeLeadingTailFragment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var match = Regex.Match(value.Trim(), @"^(?<first>[a-z]{2,5})\s+(?<second>[A-Z][A-Za-z0-9]{2,})(?:\s|$)", RegexOptions.CultureInvariant);
            if (!match.Success)
                return false;

            if (value.IndexOfAny(new[] { ',', ':', ';', '.', '\\', '/', '-' }) >= 0)
                return false;

            switch (match.Groups["first"].Value)
            {
                case "ical":
                case "tion":
                case "ment":
                case "able":
                case "ible":
                case "ance":
                case "ence":
                case "ing":
                case "ed":
                    return true;
                default:
                    return false;
            }
        }

        private static bool HasLongAscendingAsciiLetterRun(string value, int minRunLength)
        {
            var runLength = 1;
            var previous = '\0';
            foreach (var raw in value)
            {
                var ch = char.ToLowerInvariant(raw);
                if (ch < 'a' || ch > 'z')
                {
                    runLength = 1;
                    previous = '\0';
                    continue;
                }

                if (previous != '\0' && ch == previous + 1)
                    runLength++;
                else
                    runLength = 1;

                if (runLength >= minRunLength)
                    return true;
                previous = ch;
            }

            return false;
        }

        private static int NaturalWordCount(string value)
        {
            var words = 0;
            var lettersInRun = 0;
            foreach (var ch in value)
            {
                if (char.IsLetter(ch))
                {
                    lettersInRun++;
                    continue;
                }
                if (lettersInRun >= 2)
                    words++;
                lettersInRun = 0;
            }
            if (lettersInRun >= 2)
                words++;
            return words;
        }

        private static List<FoundString> FindUtf16Strings(byte[] data, int minLength, int maxResults, bool littleEndian)
        {
            var results = new List<FoundString>();
            for (var alignment = 0; alignment < 2 && results.Count < maxResults; alignment++)
            {
                var i = alignment;
                while (i + 1 < data.Length && results.Count < maxResults)
                {
                    var ch = ReadUtf16Char(data, i, littleEndian);
                    if (IsReadableUtf16TextChar(ch))
                    {
                        var start = i;
                        var chars = new List<char>();
                        while (i + 1 < data.Length)
                        {
                            ch = ReadUtf16Char(data, i, littleEndian);
                            if (!IsReadableUtf16TextChar(ch))
                                break;
                            chars.Add(ch);
                            i += 2;
                            if (chars.Count >= 160)
                                break;
                        }
                        if (chars.Count >= minLength)
                            results.Add(new FoundString { Offset = start, Value = new string(chars.ToArray()) });
                    }
                    else
                    {
                        i += 2;
                    }
                }
            }
            return results;
        }

        private static char ReadUtf16Char(byte[] data, int offset, bool littleEndian)
        {
            return littleEndian
                ? (char)(data[offset] | (data[offset + 1] << 8))
                : (char)((data[offset] << 8) | data[offset + 1]);
        }

        private static bool IsReadableTextChar(char ch)
        {
            if (ch == '\t' || ch == '\r' || ch == '\n')
                return true;
            return ch >= 32 && ch != 0x7f && !char.IsSurrogate(ch);
        }

        private static bool IsHighBitPrintableAscii(byte value)
        {
            var masked = value & 0x7f;
            return value >= 0xa0 && masked >= 32 && masked < 127 && char.IsLetterOrDigit((char)masked);
        }

        private static bool IsReadableUtf16TextChar(char ch)
        {
            if (ch == '\t' || ch == '\r' || ch == '\n')
                return true;
            return ch >= 32 && ch < 256 && ch != 0x7f;
        }

        private static bool StartsWith(byte[] data, byte[] signature)
        {
            if (data.Length < signature.Length)
                return false;
            for (var i = 0; i < signature.Length; i++)
                if (data[i] != signature[i])
                    return false;
            return true;
        }

    }
}

