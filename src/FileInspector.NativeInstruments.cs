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
            ".nki", ".nkm", ".nkb", ".nkp", ".nka", ".nkr", ".nkx", ".nkc", ".ncw", ".nicnt", ".nkl",
            ".nksf", ".nksfx", ".nksn", ".nksr",
            ".ens", ".ism", ".mdl", ".rcc", ".rkplr",
            ".kt3", ".nbkt",
            ".ksd", ".nfm8", ".nabs", ".nmsv", ".nrkt",
            ".mxprj", ".mxgrp", ".mxsnd", ".mxfx", ".mxinst", ".mprj", ".mgrp", ".msnd", ".ngrr", ".ndx"
        };

        private static void AddNativeInstrumentsInfo(List<ReportSection> sections, string path, byte[] sample)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".rpp" || ext == ".rpp-bak")
                return;
            var extensionHint = NativeInstrumentsTypeName(path);
            var readable = FindReadableTextLines(sample, 4, 2000);
            var evidence = FindNativeInstrumentsEvidence(readable).ToArray();
            var headerMarker = NativeHeaderMarker(sample);

            if (extensionHint == null && string.IsNullOrWhiteSpace(headerMarker) && BackupConfigTypeName(path, sample) != null)
                return;
            if (extensionHint == null && string.IsNullOrWhiteSpace(headerMarker) && !HasStrongNativeInstrumentsEvidence(path, evidence))
                return;

            var section = AddSection(sections, "Native Instruments");
            if (extensionHint != null)
            {
                Add(section, "Format hint", extensionHint);
                AddNativeInstrumentsProductRole(section, ext);
            }
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

            Add(section, "Notes", "Native Instruments files cover several product families: Kontakt instruments and snapshots, Reaktor ensembles, Battery kits, FM8/Absynth/Massive synth presets, Maschine projects, and NKS browser presets. FileDentify reports product/extension identity and readable metadata/string evidence; counts are based on sampled visible references, not a full container parse.");
        }

        private static string NativeInstrumentsTypeName(string path)
        {
            if (PathLooksSymbian(path))
                return null;
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".nki": return "Native Instruments Kontakt instrument";
                case ".nkm": return "Native Instruments Kontakt multi";
                case ".nkb": return "Native Instruments Kontakt bank";
                case ".nkp": return "Native Instruments Kontakt preset";
                case ".nka": return "Native Instruments Kontakt script array or sample-add data";
                case ".nkr": return "Native Instruments Kontakt resource container";
                case ".nkx": return "Native Instruments Kontakt sample/library container";
                case ".nkc": return "Native Instruments Kontakt cache/index";
                case ".ncw": return "Native Instruments Kontakt compressed wave sample";
                case ".nicnt": return "Native Instruments Kontakt library metadata";
                case ".nkl": return "Native Instruments Kontakt Leap kit";
                case ".nksf": return "Native Instruments Native Kontrol Standard preset";
                case ".nksfx": return "Native Instruments Native Kontrol Standard effect preset";
                case ".nksn": return "Native Instruments Kontakt snapshot";
                case ".nksr": return "Native Instruments Reaktor rack";
                case ".ens": return "Native Instruments Reaktor ensemble";
                case ".ism": return "Native Instruments Reaktor instrument or structure";
                case ".mdl": return "Native Instruments Reaktor module";
                case ".rcc": return "Native Instruments Reaktor core cell";
                case ".rkplr": return "Native Instruments Reaktor Player file";
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
                case ".mxfx": return "Native Instruments Maschine effect preset";
                case ".mxinst": return "Native Instruments Maschine instrument preset";
                case ".mprj": return "Native Instruments Maschine 1 project";
                case ".mgrp": return "Native Instruments Maschine 1 group";
                case ".msnd": return "Native Instruments Maschine 1 sound";
                case ".ngrr": return "Native Instruments Guitar Rig rack preset";
                case ".ndx": return "Native Instruments sample index data";
                default: return null;
            }
        }

        private static void AddNativeInstrumentsProductRole(ReportSection section, string extension)
        {
            var role = NativeInstrumentsExtensionRole(extension);
            if (role == null)
                return;
            Add(section, "Primary product or host", role.Item1);
            Add(section, "Extension role", role.Item2);
            if (!string.IsNullOrWhiteSpace(role.Item3))
                Add(section, "User context", role.Item3);
        }

        private static Tuple<string, string, string> NativeInstrumentsExtensionRole(string extension)
        {
            switch ((extension ?? string.Empty).ToLowerInvariant())
            {
                case ".nki": return Tuple.Create("Kontakt / Kontakt Player", "Kontakt instrument loaded into the Rack.", "Often found in Instruments folders inside Kontakt libraries.");
                case ".nkm": return Tuple.Create("Kontakt", "Kontakt Multi: a saved rack containing one or more instruments.", "Usually opened from Kontakt rather than Komplete Kontrol browser results.");
                case ".nkb": return Tuple.Create("Kontakt", "Kontakt Instrument Bank.", "A bank groups multiple Kontakt instruments for program/bank-style selection.");
                case ".nkp": return Tuple.Create("Kontakt", "Kontakt preset.", string.Empty);
                case ".nka": return Tuple.Create("Kontakt", "Kontakt script array or sample-add support data.", "Can appear as KSP array data or as internal data under Kontakt library sample-add folders.");
                case ".nkr": return Tuple.Create("Kontakt", "Kontakt resource file.", "Often used beside instruments for scripts, graphics, or other library resources.");
                case ".nkx": return Tuple.Create("Kontakt", "Kontakt monolith/sample-library container.", "Older Kontakt libraries may store packed samples or resources in NKX containers.");
                case ".nkc": return Tuple.Create("Kontakt", "Kontakt cache/index file.", "Usually a companion cache file rather than something a user opens directly.");
                case ".ncw": return Tuple.Create("Kontakt", "Kontakt lossless-compressed audio sample.", "Usually referenced by .nki instruments rather than opened directly.");
                case ".nicnt": return Tuple.Create("Kontakt / Native Access", "Kontakt library metadata and registration/visibility data.", "Helps Kontakt and Native Access recognise a licensed library.");
                case ".nksn": return Tuple.Create("Kontakt / Kontakt Player", "Kontakt Snapshot preset.", "User snapshots are commonly stored under Documents\\Native Instruments\\User Content\\<library>.");
                case ".nksf": return Tuple.Create("Komplete Kontrol / Maschine / NKS-aware hosts", "Native Kontrol Standard instrument preset.", "Can wrap NI or third-party preset metadata for browsing, tagging, previews, and controller integration.");
                case ".nksfx": return Tuple.Create("Komplete Kontrol / Maschine / NKS-aware hosts", "Native Kontrol Standard effect preset.", "Effect counterpart to .nksf for NKS browser/controller workflows.");
                case ".nksr": return Tuple.Create("Reaktor / Komplete Kontrol", "Reaktor Blocks Rack or NKS rack preset.", "Native Instruments forum posts describe .nksr as Reaktor rack data used for Blocks/controller mappings.");
                case ".ens": return Tuple.Create("Reaktor", "Reaktor ensemble.", "A complete Reaktor instrument/effect patch.");
                case ".ism": return Tuple.Create("Reaktor", "Reaktor instrument or structure.", string.Empty);
                case ".mdl": return Tuple.Create("Reaktor", "Reaktor module.", string.Empty);
                case ".rcc": return Tuple.Create("Reaktor", "Reaktor Core Cell.", string.Empty);
                case ".rkplr": return Tuple.Create("Reaktor / Reaktor Player", "Reaktor Player file.", "Loaded from Reaktor's Player tab for products such as Razor, Prism, The Mouth, and The Finger.");
                case ".kt3": return Tuple.Create("Battery", "Battery kit.", "Older Battery kit format.");
                case ".nbkt": return Tuple.Create("Battery", "Battery kit.", "Newer Native Instruments Battery kit format.");
                case ".ksd": return Tuple.Create("Kore / older NI synths", "Legacy NI sound preset.", "Commonly seen with older Massive, FM8, and Absynth-era libraries before newer product-specific formats.");
                case ".nfm8": return Tuple.Create("FM8", "FM8 sound preset.", "Modern FM8 sound format; older .ksd sounds may be imported/converted by FM8.");
                case ".nabs": return Tuple.Create("Absynth", "Absynth sound preset.", "Absynth 5/6-era sound preset format.");
                case ".nmsv": return Tuple.Create("Massive", "Massive sound preset.", "Native Instruments documents .nmsv under Massive user content. Massive X can import preset files, but FileDentify reports .nmsv as Massive unless stronger Massive X evidence is visible.");
                case ".nrkt": return Tuple.Create("Reaktor / Kontour", "Reaktor-family preset.", "Often associated with Reaktor instruments such as Kontour.");
                case ".mxprj": return Tuple.Create("Maschine", "Maschine project.", string.Empty);
                case ".mxgrp": return Tuple.Create("Maschine", "Maschine group.", string.Empty);
                case ".mxsnd": return Tuple.Create("Maschine", "Maschine sound.", string.Empty);
                case ".mxfx": return Tuple.Create("Maschine", "Maschine effect preset.", "Used by Maschine 2 factory-library effect presets.");
                case ".mxinst": return Tuple.Create("Maschine", "Maschine instrument preset.", "Used by Maschine 2 internal synth and drum-synth presets.");
                case ".mprj": return Tuple.Create("Maschine", "Maschine 1 project.", "Seen in older Maschine expansion libraries.");
                case ".mgrp": return Tuple.Create("Maschine", "Maschine 1 group.", "Seen in older Maschine expansion libraries.");
                case ".msnd": return Tuple.Create("Maschine", "Maschine 1 sound.", "Seen in older Maschine expansion libraries.");
                case ".ngrr": return Tuple.Create("Guitar Rig", "Guitar Rig rack preset.", "Seen in Traktor/Guitar Rig-style rack preset folders.");
                case ".ndx": return Tuple.Create("Kontakt", "Native Instruments sample index/support data.", "Seen beside Kontakt sample-add library payloads; normally not opened directly.");
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

        private static bool HasStrongNativeInstrumentsEvidence(string path, IEnumerable<string> evidence)
        {
            if (PathContainsNativeInstrumentsContext(path))
                return true;
            var list = evidence == null ? new List<string>() : evidence.ToList();
            if (list.Count >= 2)
                return true;
            return list.Any(item => !string.Equals(item, "Native Instruments", StringComparison.OrdinalIgnoreCase));
        }

        private static bool PathContainsNativeInstrumentsContext(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;
            return path.IndexOf("\\Native Instruments\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("/Native Instruments/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\Komplete\\NativeInstruments\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("/Komplete/NativeInstruments/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Regex NativeSampleReferencePattern()
        {
            return new Regex(@"(?i)(^|[\\/ ])[^\\/ ]+\.(ncw|wav|aif|aiff|flac|ogg)$", RegexOptions.Compiled);
        }

        private static Regex NativeInstrumentReferencePattern()
        {
            return new Regex(@"(?i)(^|[\\/ ])[^\\/ ]+\.(nki|nkm|nkb|nkp|nka|nkr|nksf|nksfx|nksn|nksr|nkl|ens|ism|rkplr|kt3|nbkt|ksd|nfm8|nabs|nmsv|nrkt|mxprj|mxgrp|mxsnd|mxfx|mxinst|mprj|mgrp|msnd|ngrr|ndx)$", RegexOptions.Compiled);
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
            const int MaxNicntTextBytes = 2 * 1024 * 1024;
            if (!string.Equals(extension, ".nicnt", StringComparison.OrdinalIgnoreCase))
                return false;

            string text;
            try
            {
                var info = new FileInfo(path);
                text = info.Length <= MaxNicntTextBytes
                    ? File.ReadAllText(path)
                    : System.Text.Encoding.UTF8.GetString(ReadPrefix(path, MaxNicntTextBytes));
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
