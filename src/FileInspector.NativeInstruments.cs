using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace FileDentify
{
    internal static partial class FileInspector
    {
        private static readonly string[] NativeInstrumentExtensions =
        {
            ".nki", ".nkm", ".nkb", ".nkr", ".nkx", ".nkc", ".ncw", ".nicnt",
            ".nksf", ".nksfx", ".nksn",
            ".blob",
            ".ens", ".ism", ".mdl", ".rcc",
            ".kt3", ".nbkt",
            ".ksd", ".nfm8", ".nabs", ".nmsv", ".nrkt",
            ".mxprj", ".mxgrp", ".mxsnd"
        };

        private static void AddNativeInstrumentsInfo(List<ReportSection> sections, string path, byte[] sample)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var extensionHint = NativeInstrumentsTypeName(path);
            var readable = FindReadableTextLines(sample, 4, 2000);
            var evidence = FindNativeInstrumentsEvidence(readable).ToArray();

            if (extensionHint == null && evidence.Length == 0)
                return;

            var section = AddSection(sections, "Native Instruments");
            if (extensionHint != null)
                Add(section, "Format hint", extensionHint);
            var headerMarker = NativeHeaderMarker(sample);
            if (!string.IsNullOrWhiteSpace(headerMarker))
                Add(section, "Header marker", headerMarker);
            Add(section, "Detection basis", extensionHint != null
                ? "Known Native Instruments-related extension, plus sampled readable strings where available."
                : "Native Instruments-related readable strings found in the sampled data.");

            if (evidence.Length > 0)
                Add(section, "Visible product strings", string.Join("\r\n", evidence.Take(30).ToArray()));

            var hasNicntMetadata = AddNicntMetadata(section, path, ext, sample);

            var sampleRefs = FindNativeInstrumentsReferences(readable, NativeSampleReferencePattern())
                .Take(500)
                .ToArray();
            if (sampleRefs.Length > 0)
            {
                Add(section, "Sample references found", sampleRefs.Length.ToString(CultureInfo.InvariantCulture));
                Add(section, "Sample reference preview", string.Join("\r\n", sampleRefs.Take(60).ToArray()));
            }

            var instrumentRefs = FindNativeInstrumentsReferences(readable, NativeInstrumentReferencePattern())
                .Take(300)
                .ToArray();
            if (instrumentRefs.Length > 0)
            {
                Add(section, "Instrument or preset references found", instrumentRefs.Length.ToString(CultureInfo.InvariantCulture));
                Add(section, "Instrument or preset preview", string.Join("\r\n", instrumentRefs.Take(40).ToArray()));
            }

            if (!hasNicntMetadata)
            {
                var libraryNames = GuessNativeLibraryNames(path, readable).Take(20).ToArray();
                if (libraryNames.Length > 0)
                    Add(section, "Possible library or product names", string.Join("\r\n", libraryNames));
            }

            Add(section, "Notes", "Native Instruments formats are partly proprietary. FileDentify reports extension-level identity and readable metadata/string evidence; counts are based on sampled visible references, not a full container parse.");
        }

        private static string NativeInstrumentsTypeName(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".nki": return "Native Instruments Kontakt instrument";
                case ".nkm": return "Native Instruments Kontakt multi";
                case ".nkb": return "Native Instruments Kontakt bank";
                case ".nkr": return "Native Instruments Kontakt resource container";
                case ".nkx": return "Native Instruments Kontakt sample/library container";
                case ".nkc": return "Native Instruments Kontakt cache/index";
                case ".ncw": return "Native Instruments Kontakt compressed wave sample";
                case ".nicnt": return "Native Instruments Kontakt library metadata";
                case ".nksf": return "Native Instruments Native Kontrol Standard preset";
                case ".nksfx": return "Native Instruments Native Kontrol Standard effect preset";
                case ".nksn": return "Native Instruments Native Kontrol Standard snapshot";
                case ".blob": return "Native Instruments binary metadata/blob asset";
                case ".ens": return "Native Instruments Reaktor ensemble";
                case ".ism": return "Native Instruments Reaktor instrument or structure";
                case ".mdl": return "Native Instruments Reaktor module";
                case ".rcc": return "Native Instruments Reaktor core cell";
                case ".kt3": return "Native Instruments Battery kit";
                case ".nbkt": return "Native Instruments Battery kit";
                case ".ksd": return "Native Instruments Kore/FM8/Massive/Absynth sound preset";
                case ".nfm8": return "Native Instruments FM8 sound preset";
                case ".nabs": return "Native Instruments Absynth sound preset";
                case ".nmsv": return "Native Instruments Massive sound preset";
                case ".nrkt": return "Native Instruments Reaktor/Kontour preset";
                case ".mxprj": return "Native Instruments Maschine project";
                case ".mxgrp": return "Native Instruments Maschine group";
                case ".mxsnd": return "Native Instruments Maschine sound";
                default: return null;
            }
        }

        private static IEnumerable<string> FindNativeInstrumentsEvidence(IEnumerable<string> readable)
        {
            string[] needles =
            {
                "Native Instruments", "Kontakt", "Reaktor", "Battery", "FM8", "Massive",
                "Massive X", "Absynth", "Guitar Rig", "Maschine", "Komplete",
                "Kontakt Player", "Native Kontrol Standard", "NKS", "Kontour",
                "Monark", "Razor", "Rounds", "Prism"
            };

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in readable)
            {
                foreach (var needle in needles)
                {
                    if (line.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0 || !seen.Add(needle))
                        continue;
                    yield return needle;
                }
            }
        }

        private static Regex NativeSampleReferencePattern()
        {
            return new Regex(@"(?i)(^|[\\/ ])[^\\/ ]+\.(ncw|wav|aif|aiff|flac|ogg)$", RegexOptions.Compiled);
        }

        private static Regex NativeInstrumentReferencePattern()
        {
            return new Regex(@"(?i)(^|[\\/ ])[^\\/ ]+\.(nki|nkm|nkb|nkr|nksf|nksfx|nksn|ens|ism|kt3|nbkt|ksd|nfm8|nabs|nmsv|nrkt|mxprj|mxgrp|mxsnd)$", RegexOptions.Compiled);
        }

        private static IEnumerable<string> FindNativeInstrumentsReferences(IEnumerable<string> readable, Regex pattern)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in readable)
            {
                var cleaned = line.Trim();
                if (cleaned.Length == 0 || !pattern.IsMatch(cleaned) || !seen.Add(cleaned))
                    continue;
                yield return cleaned;
            }
        }

        private static IEnumerable<string> GuessNativeLibraryNames(string path, IEnumerable<string> readable)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var baseName = Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrWhiteSpace(baseName) && seen.Add(baseName))
                yield return baseName;

            foreach (var line in readable)
            {
                var cleaned = line.Trim();
                if (cleaned.Length < 4 || cleaned.Length > 80)
                    continue;
                if (cleaned.IndexOf('<') >= 0 || cleaned.IndexOf('>') >= 0)
                    continue;
                if (cleaned.IndexOf("library", StringComparison.OrdinalIgnoreCase) < 0 &&
                    cleaned.IndexOf("product", StringComparison.OrdinalIgnoreCase) < 0 &&
                    cleaned.IndexOf("instrument", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (seen.Add(cleaned))
                    yield return cleaned;
            }
        }

        private static bool AddNicntMetadata(ReportSection section, string path, string extension, byte[] sample)
        {
            if (!string.Equals(extension, ".nicnt", StringComparison.OrdinalIgnoreCase))
                return false;

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch
            {
                text = System.Text.Encoding.UTF8.GetString(sample);
            }

            var count = 0;
            if (AddXmlTagValue(section, text, "Name", "Library name")) count++;
            if (AddXmlTagValue(section, text, "RegKey", "Registration key")) count++;
            if (AddXmlTagValue(section, text, "SNPID", "SNPID")) count++;
            if (AddXmlTagValue(section, text, "Company", "Company")) count++;
            if (AddXmlTagValue(section, text, "AuthSystem", "Authorization system")) count++;
            if (AddXmlTagValue(section, text, "ProductSpecificName", "Product specific name")) count++;
            if (AddXmlTagValue(section, text, "Visibility", "Visibility")) count++;
            return count > 0;
        }

        private static bool AddXmlTagValue(ReportSection section, string text, string tag, string label)
        {
            var match = Regex.Match(text, "<" + Regex.Escape(tag) + @"[^>]*>(.*?)</" + Regex.Escape(tag) + ">", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!match.Success)
                return false;
            var value = Regex.Replace(match.Groups[1].Value, "<.*?>", string.Empty).Trim();
            if (value.Length == 0)
                return false;
            Add(section, label, value);
            return true;
        }

        private static string NativeHeaderMarker(byte[] data)
        {
            if (data == null || data.Length < 4)
                return string.Empty;

            var text = AsciiPreview(data, Math.Min(data.Length, 128));
            if (text.IndexOf("NI FC MTD", StringComparison.OrdinalIgnoreCase) >= 0)
                return "NI FC MTD";
            if (text.IndexOf("DSIN", StringComparison.OrdinalIgnoreCase) >= 0)
                return "DSIN";
            if (text.IndexOf("RTKR", StringComparison.OrdinalIgnoreCase) >= 0)
                return "RTKR";
            if (text.IndexOf("Native Instruments", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Native Instruments text marker";
            return string.Empty;
        }

        private static bool IsNativeInstrumentsExtension(string path)
        {
            var extension = Path.GetExtension(path);
            return NativeInstrumentExtensions.Any(ext => string.Equals(ext, extension, StringComparison.OrdinalIgnoreCase));
        }
    }
}
