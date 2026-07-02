using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace FileDentify
{
    internal enum AdvancedViewMode
    {
        ReadableText = 0,
        Hex = 1,
        Binary = 2,
        Octal = 3
    }

    internal static class AdvancedFileViewRenderer
    {
        public static string RenderFile(string path, AdvancedViewMode mode, long maxBytes)
        {
            var file = new FileInfo(path);
            var count = (int)Math.Min(Math.Min(file.Length, maxBytes), int.MaxValue);
            var data = new byte[count];
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var offset = 0;
                while (offset < count)
                {
                    var read = fs.Read(data, offset, count - offset);
                    if (read == 0)
                        break;
                    offset += read;
                }
                if (offset != count)
                    Array.Resize(ref data, offset);
            }

            var sb = new StringBuilder();
            sb.AppendLine("FileDentify advanced file view");
            sb.AppendLine("==============================");
            sb.AppendLine("Path:");
            sb.AppendLine(path);
            sb.AppendLine("Mode:");
            sb.AppendLine(ModeName(mode));
            sb.AppendLine("Bytes rendered:");
            sb.AppendLine(FormatBytes(data.LongLength) + " of " + FormatBytes(file.Length));
            sb.AppendLine();
            sb.AppendLine(RenderChunk(data, 0, mode));
            return sb.ToString().TrimEnd();
        }

        public static string RenderChunk(byte[] data, long start, AdvancedViewMode mode)
        {
            switch (mode)
            {
                case AdvancedViewMode.Hex: return RenderBase(data, start, 16, 16);
                case AdvancedViewMode.Binary: return RenderBase(data, start, 2, 6);
                case AdvancedViewMode.Octal: return RenderBase(data, start, 8, 10);
                default: return RenderReadable(data);
            }
        }

        public static string ModeName(AdvancedViewMode mode)
        {
            switch (mode)
            {
                case AdvancedViewMode.Hex: return "Hex";
                case AdvancedViewMode.Binary: return "Binary";
                case AdvancedViewMode.Octal: return "Octal";
                default: return "Readable text";
            }
        }

        public static bool TryParseMode(string value, out AdvancedViewMode mode)
        {
            mode = AdvancedViewMode.ReadableText;
            if (string.IsNullOrWhiteSpace(value))
                return true;
            switch (value.Trim().ToLowerInvariant())
            {
                case "text":
                case "readable":
                case "readable-text":
                case "strings":
                    mode = AdvancedViewMode.ReadableText;
                    return true;
                case "hex":
                    mode = AdvancedViewMode.Hex;
                    return true;
                case "binary":
                case "bin":
                    mode = AdvancedViewMode.Binary;
                    return true;
                case "octal":
                case "oct":
                    mode = AdvancedViewMode.Octal;
                    return true;
                default:
                    return false;
            }
        }

        private static string RenderReadable(byte[] data)
        {
            var structured = RenderStructuredText(data);
            if (!string.IsNullOrWhiteSpace(structured))
                return structured;

            var candidates = new List<ReadableCandidate>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            AddReadable(candidates, seen, ExtractAsciiStrings(data, 4), false, 10000);
            AddReadable(candidates, seen, ExtractHighBitAsciiStrings(data, 4), false, 10000);
            AddReadable(candidates, seen, ExtractUtf16Strings(data, 4, true), true, 10000);
            AddReadable(candidates, seen, ExtractUtf16Strings(data, 4, false), true, 10000);
            if (candidates.Count == 0)
                return "(No readable text found.)";

            return string.Join(Environment.NewLine, candidates
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Order)
                .Select(candidate => candidate.Value)
                .ToArray());
        }

        private static string RenderStructuredText(byte[] data)
        {
            var text = DecodeLikelyUtf8Text(data);
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var trimmed = text.Trim();
            if (trimmed.Length < 2)
                return string.Empty;

            if ((trimmed[0] == '{' && trimmed[trimmed.Length - 1] == '}') ||
                (trimmed[0] == '[' && trimmed[trimmed.Length - 1] == ']'))
                return PrettyPrintJsonLikeText(trimmed);

            return string.Empty;
        }

        private static string DecodeLikelyUtf8Text(byte[] data)
        {
            if (data == null || data.Length == 0)
                return string.Empty;

            var offset = data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
            var suspicious = 0;
            for (var i = offset; i < data.Length; i++)
            {
                var b = data[i];
                if (b == 0 || (b < 9) || (b > 13 && b < 32))
                    suspicious++;
            }
            if (suspicious > Math.Max(8, data.Length / 20))
                return string.Empty;

            try
            {
                var utf8 = new UTF8Encoding(false, true);
                return utf8.GetString(data, offset, data.Length - offset);
            }
            catch (DecoderFallbackException)
            {
                return string.Empty;
            }
        }

        private static string PrettyPrintJsonLikeText(string text)
        {
            var sb = new StringBuilder();
            var indent = 0;
            var inString = false;
            var escaped = false;

            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                if (inString)
                {
                    sb.Append(ch);
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (ch == '\\')
                    {
                        escaped = true;
                    }
                    else if (ch == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                switch (ch)
                {
                    case '"':
                        inString = true;
                        sb.Append(ch);
                        break;
                    case '{':
                    case '[':
                        sb.Append(ch);
                        AppendNewJsonLine(sb, ++indent);
                        break;
                    case '}':
                    case ']':
                        AppendNewJsonLine(sb, Math.Max(0, --indent));
                        sb.Append(ch);
                        break;
                    case ',':
                        sb.Append(ch);
                        AppendNewJsonLine(sb, indent);
                        break;
                    case ':':
                        sb.Append(": ");
                        break;
                    default:
                        if (!char.IsWhiteSpace(ch))
                            sb.Append(ch);
                        break;
                }
            }

            return sb.ToString().TrimEnd();
        }

        private static void AppendNewJsonLine(StringBuilder sb, int indent)
        {
            sb.AppendLine();
            sb.Append(new string(' ', Math.Max(0, indent) * 2));
        }

        private static void AddReadable(List<ReadableCandidate> candidates, HashSet<string> seen, IEnumerable<string> values, bool preferStructuredText, int max)
        {
            foreach (var value in values)
            {
                var clean = CleanReadableText(value);
                if (!IsUsefulReadableLine(clean) || seen.Contains(clean))
                    continue;

                if (IsReadableFragmentOfExistingLine(candidates, clean))
                    continue;

                RemoveExistingReadableFragments(candidates, seen, clean);

                seen.Add(clean);
                candidates.Add(new ReadableCandidate
                {
                    Value = clean,
                    Score = ScoreReadableLine(clean, preferStructuredText),
                    Order = candidates.Count
                });
                if (candidates.Count >= max)
                    return;
            }
        }

        private static bool IsReadableFragmentOfExistingLine(List<ReadableCandidate> candidates, string candidate)
        {
            var normalizedCandidate = NormalizeReadableLineForFragmentMatch(candidate);
            if (normalizedCandidate.Length < 6)
                return false;

            foreach (var existing in candidates)
            {
                var normalizedExisting = NormalizeReadableLineForFragmentMatch(existing.Value);
                if (string.Equals(normalizedCandidate, normalizedExisting, StringComparison.Ordinal))
                    return true;
                if (IsReadableFragment(normalizedCandidate, normalizedExisting))
                    return true;
            }

            return false;
        }

        private static void RemoveExistingReadableFragments(List<ReadableCandidate> candidates, HashSet<string> seen, string candidate)
        {
            var normalizedCandidate = NormalizeReadableLineForFragmentMatch(candidate);
            if (normalizedCandidate.Length < 6)
                return;

            for (var i = candidates.Count - 1; i >= 0; i--)
            {
                var normalizedExisting = NormalizeReadableLineForFragmentMatch(candidates[i].Value);
                if (string.Equals(normalizedExisting, normalizedCandidate, StringComparison.Ordinal) ||
                    IsReadableFragment(normalizedExisting, normalizedCandidate))
                {
                    seen.Remove(candidates[i].Value);
                    candidates.RemoveAt(i);
                }
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

        private static IEnumerable<string> ExtractAsciiStrings(byte[] data, int minLength)
        {
            var i = 0;
            while (i < data.Length)
            {
                if (data[i] >= 32 && data[i] < 127)
                {
                    var start = i;
                    while (i < data.Length && data[i] >= 32 && data[i] < 127)
                        i++;
                    if (i - start >= minLength)
                        yield return Encoding.ASCII.GetString(data, start, i - start);
                }
                else
                    i++;
            }
        }

        private static IEnumerable<string> ExtractHighBitAsciiStrings(byte[] data, int minLength)
        {
            var i = 0;
            while (i < data.Length)
            {
                if (!IsHighBitPrintableAscii(data[i]))
                {
                    i++;
                    continue;
                }

                var chars = new List<byte>();
                chars.Add((byte)(data[i] & 0x7f));
                i++;
                while (i < data.Length && data[i] >= 32 && data[i] < 127)
                {
                    chars.Add(data[i]);
                    i++;
                }

                if (chars.Count >= minLength)
                    yield return Encoding.ASCII.GetString(chars.ToArray());
            }
        }

        private static IEnumerable<string> ExtractUtf16Strings(byte[] data, int minLength, bool littleEndian)
        {
            for (var alignment = 0; alignment < 2; alignment++)
            {
                var i = alignment;
                while (i + 1 < data.Length)
                {
                    var chars = new List<char>();
                    var start = i;
                    while (i + 1 < data.Length)
                    {
                        var ch = littleEndian ? (char)(data[i] | (data[i + 1] << 8)) : (char)((data[i] << 8) | data[i + 1]);
                        if (ch < 32 || ch >= 256 || ch == 0x7f)
                            break;
                        chars.Add(ch);
                        i += 2;
                    }
                    if (chars.Count >= minLength)
                        yield return new string(chars.ToArray());
                    if (i == start)
                        i += 2;
                }
            }
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

            if (value.Length > 30)
            {
                var dominant = value.Where(char.IsLetterOrDigit).GroupBy(ch => ch).Select(g => g.Count()).DefaultIfEmpty(0).Max();
                if (dominant > lettersOrDigits / 3)
                    return false;
            }

            var naturalWords = NaturalWordCount(value);
            if (naturalWords == 0)
                return false;

            if (LooksLikeLeadingTailFragment(value))
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

            var match = System.Text.RegularExpressions.Regex.Match(value.Trim(), @"^(?<first>[a-z]{2,5})\s+(?<second>[A-Z][A-Za-z0-9]{2,})(?:\s|$)", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
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

        private static int ScoreReadableLine(string value, bool preferStructuredText)
        {
            var score = preferStructuredText ? 20 : 0;
            var lettersOrDigits = value.Count(char.IsLetterOrDigit);
            var symbols = value.Count(ch => !char.IsLetterOrDigit(ch) && !char.IsWhiteSpace(ch));
            var words = NaturalWordCount(value);

            score += Math.Min(value.Length, 80);
            score += Math.Min(words * 8, 40);

            if (value.IndexOf('.') >= 0)
                score += 15;
            if (value.IndexOf('_') >= 0 || value.IndexOf('-') >= 0)
                score += 12;
            if (value.IndexOf('\\') >= 0 || value.IndexOf('/') >= 0 || value.IndexOf(':') >= 0)
                score += 12;
            if (RegexLikeExtension(value))
                score += 35;
            if (value.IndexOf(".ncw", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf(".nkx", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf(".nki", StringComparison.OrdinalIgnoreCase) >= 0)
                score += 60;

            if (lettersOrDigits > 0)
                score -= Math.Min((symbols * 100) / lettersOrDigits, 60);
            if (value.Length < 10)
                score -= 25;

            return score;
        }

        private static bool RegexLikeExtension(string value)
        {
            var dot = value.LastIndexOf('.');
            if (dot < 0 || dot >= value.Length - 2 || value.Length - dot > 8)
                return false;
            for (var i = dot + 1; i < value.Length; i++)
            {
                if (!char.IsLetterOrDigit(value[i]))
                    return false;
            }
            return true;
        }

        private static bool IsHighBitPrintableAscii(byte value)
        {
            var masked = value & 0x7f;
            return value >= 0xa0 && masked >= 32 && masked < 127 && char.IsLetterOrDigit((char)masked);
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

        private static string RenderBase(byte[] data, long start, int numberBase, int bytesPerLine)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < data.Length; i += bytesPerLine)
            {
                sb.Append((start + i).ToString("X8", CultureInfo.InvariantCulture));
                sb.Append("  ");
                for (var j = 0; j < bytesPerLine && i + j < data.Length; j++)
                {
                    if (numberBase == 16)
                        sb.Append(data[i + j].ToString("X2", CultureInfo.InvariantCulture));
                    else if (numberBase == 8)
                        sb.Append(Convert.ToString(data[i + j], 8).PadLeft(3, '0'));
                    else
                        sb.Append(Convert.ToString(data[i + j], 2).PadLeft(8, '0'));
                    sb.Append(' ');
                }
                sb.Append(" ");
                for (var j = 0; j < bytesPerLine && i + j < data.Length; j++)
                {
                    var b = data[i + j];
                    sb.Append(b >= 32 && b < 127 ? (char)b : '.');
                }
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = { "bytes", "KiB", "MiB", "GiB", "TiB" };
            double value = bytes;
            var unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }
            return unit == 0 ? bytes.ToString(CultureInfo.InvariantCulture) + " bytes" : value.ToString("0.##", CultureInfo.InvariantCulture) + " " + units[unit];
        }

        private sealed class ReadableCandidate
        {
            public string Value;
            public int Score;
            public int Order;
        }
    }
}
