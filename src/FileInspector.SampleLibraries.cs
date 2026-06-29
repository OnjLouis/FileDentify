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
        private static string SampleLibraryTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".xpak" || IsXlnAudioPath(path))
                return XlnAudioTypeName(path, header);
            if (IsSpectrasonicsPath(path))
                return SpectrasonicsTypeName(path, header);
            var korg = KorgTypeName(path, header);
            if (korg != null)
                return korg;
            var gforce = GForceTypeName(path);
            if (gforce != null)
                return gforce;
            var toontrack = ToontrackTypeName(path, header);
            if (toontrack != null)
                return toontrack;
            var decentSampler = DecentSamplerTypeName(path, header);
            if (decentSampler != null)
                return decentSampler;
            var air = AirMusicTechnologyTypeName(path, header);
            if (air != null)
                return air;
            var maize = MaizeSamplerTypeName(path, header);
            if (maize != null)
                return maize;
            var universalAudio = UniversalAudioLunaTypeName(path, header);
            if (universalAudio != null)
                return universalAudio;
            var aas = AppliedAcousticsSystemsTypeName(path, header);
            if (aas != null)
                return aas;
            var audioModeling = AudioModelingTypeName(path, header);
            if (audioModeling != null)
                return audioModeling;
            var ujam = UjamTypeName(path, header);
            if (ujam != null)
                return ujam;
            var ujamStyleBlob = UjamStyleBlobTypeName(path, header);
            if (ujamStyleBlob != null)
                return ujamStyleBlob;
            var valhalla = ValhallaDspTypeName(path, header);
            if (valhalla != null)
                return valhalla;
            var modartt = ModarttTypeName(path, header);
            if (modartt != null)
                return modartt;
            return null;
        }

        private static void AddSampleLibraryInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            AddXlnAudioInfo(sections, path, header, sample, fileLength);
            AddSpectrasonicsInfo(sections, path, header, sample, fileLength);
            AddKorgInfo(sections, path, header, sample, fileLength);
            AddGForceInfo(sections, path, header, fileLength);
            AddToontrackInfo(sections, path, header, sample, fileLength);
            AddDecentSamplerInfo(sections, path, header, sample, fileLength);
            AddAirMusicTechnologyInfo(sections, path, header, sample, fileLength);
            AddMaizeSamplerInfo(sections, path, header, sample, fileLength);
            AddUniversalAudioLunaInfo(sections, path, header, sample, fileLength);
            AddAppliedAcousticsSystemsInfo(sections, path, header, sample, fileLength);
            AddAudioModelingInfo(sections, path, header, sample, fileLength);
            AddUjamInfo(sections, path, header, sample, fileLength);
            AddUjamStyleBlobInfo(sections, path, header, fileLength);
            AddValhallaDspInfo(sections, path, header, sample, fileLength);
            AddModarttInfo(sections, path, header, sample, fileLength);
        }

        private static string XlnAudioTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".xpak")
                return "XLN Audio sample pack";
            if (Path.GetFileName(path).Equals("InstalledBankNames.dat", StringComparison.OrdinalIgnoreCase) && IsXlnAudioPath(path))
                return "XLN Audio installed bank list";
            return null;
        }

        private static void AddXlnAudioInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            var type = XlnAudioTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "XLN Audio");
            Add(section, "Format hint", type);
            Add(section, "Product", XlnProductFromPath(path));
            Add(section, "Pack folder", ParentName(path));
            Add(section, "File size", FormatBytes(fileLength));

            if (Path.GetExtension(path).Equals(".xpak", StringComparison.OrdinalIgnoreCase))
            {
                Add(section, "Pack code", Path.GetFileNameWithoutExtension(path).Split('_').FirstOrDefault() ?? string.Empty);
                Add(section, "Pack name", CleanSampleLibraryName(Path.GetFileNameWithoutExtension(path)));
                Add(section, "Common use", "Large XLN Audio sound-data package used by Addictive Drums, Addictive Keys, Addictive Trigger, or XO.");
                Add(section, "Header note", LooksLikeText(header) ? "Text-like header" : "Binary/proprietary package header");
            }
            else
            {
                var banks = FindReadableTextLines(sample, 2, 80).Distinct(StringComparer.OrdinalIgnoreCase).Take(30).ToArray();
                if (banks.Length > 0)
                    Add(section, "Installed banks", string.Join(Environment.NewLine, banks));
            }

            Add(section, "Notes", "XLN Audio packages are proprietary. FileDentify reports product, pack, size, and visible bank names where available; it does not unpack sample payloads.");
        }

        private static string SpectrasonicsTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".db")
                return StartsWith(header, Encoding.ASCII.GetBytes("<FileSystem>"))
                    ? "Spectrasonics STEAM/SAGE sample container"
                    : "Spectrasonics database or sample container";
            if (ext == ".mlt_omn") return "Spectrasonics Omnisphere multi";
            if (ext == ".mlt_key") return "Spectrasonics Keyscape multi";
            if (ext == ".mlt_trl") return "Spectrasonics Trilian multi";
            if (ext == ".mlt_rmx") return "Spectrasonics Stylus RMX multi";
            if (ext == ".fxp_rmx") return "Spectrasonics Stylus RMX effect preset";
            if (ext == ".fxr_rmx") return "Spectrasonics Stylus RMX effect rack";
            if (ext == ".kit_rmx") return "Spectrasonics Stylus RMX kit";
            if (ext == ".prt_rmx") return "Spectrasonics Stylus RMX part";
            if (ext == ".index") return "Spectrasonics index file";
            return null;
        }

        private static void AddSpectrasonicsInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            var type = SpectrasonicsTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Spectrasonics");
            Add(section, "Format hint", type);
            Add(section, "Library family", SpectrasonicsFamilyFromPath(path));
            Add(section, "Product or folder", SpectrasonicsProductFromPath(path));
            Add(section, "File size", FormatBytes(fileLength));

            if (Path.GetExtension(path).Equals(".db", StringComparison.OrdinalIgnoreCase) &&
                StartsWith(header, Encoding.ASCII.GetBytes("<FileSystem>")))
            {
                Add(section, "Container", "Readable FileSystem index plus binary payload");
                AddSpectrasonicsFileSystemEntries(section, sample);
            }
            else if (LooksLikeText(header))
            {
                AddSpectrasonicsXmlInfo(section, header);
            }

            Add(section, "Notes", "Spectrasonics STEAM/SAGE files are proprietary sample-library and preset data. FileDentify reports visible index, product, and preset clues only.");
        }

        private static void AddSpectrasonicsFileSystemEntries(ReportSection section, byte[] sample)
        {
            var text = Encoding.GetEncoding(28591).GetString(sample);
            var matches = Regex.Matches(text, "<FILE\\s+name=\"(?<name>[^\"]+)\"\\s+offset=\"(?<offset>\\d+)\"\\s+size=\"(?<size>\\d+)\"", RegexOptions.IgnoreCase);
            Add(section, "Indexed file count in sample", matches.Count.ToString(CultureInfo.InvariantCulture));
            if (matches.Count == 0)
                return;

            var entries = matches.Cast<Match>()
                .Take(20)
                .Select(m => m.Groups["name"].Value + " (" + FormatParsedBytes(m.Groups["size"].Value) + ")")
                .ToArray();
            Add(section, "First indexed files", string.Join(Environment.NewLine, entries));
        }

        private static void AddSpectrasonicsXmlInfo(ReportSection section, byte[] header)
        {
            var text = Encoding.UTF8.GetString(header.Take(Math.Min(header.Length, 256 * 1024)).ToArray());
            var root = Regex.Match(text, "<\\s*(?<name>[A-Za-z0-9_:-]+)");
            if (root.Success)
                Add(section, "Root element", root.Groups["name"].Value);
            var effects = Regex.Matches(text, "Type=\"(?<type>[^\"]+)\"", RegexOptions.IgnoreCase)
                .Cast<Match>()
                .Select(m => m.Groups["type"].Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToArray();
            if (effects.Length > 0)
                Add(section, "Visible module types", string.Join(Environment.NewLine, effects));
        }

        private static string KorgTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var inKorgPath = IsKorgPath(path);
            if (StartsWith(header, Encoding.ASCII.GetBytes("WMMS")) || ext == ".wmss")
                return "Korg WaveMotion sample set";
            if (StartsWith(header, Encoding.ASCII.GetBytes("Korg")) || inKorgPath)
            {
                switch (ext)
                {
                    case ".adsr": return "Korg wavestate randomization ADSR data";
                    case ".voiceamp": return "Korg wavestate randomization voice amp data";
                    case ".pitch": return "Korg wavestate randomization pitch data";
                    case ".dynamicarpeggiator": return "Korg dynamic arpeggiator data";
                    case ".classicvectoreg": return "Korg classic vector envelope data";
                    case ".config": return "Korg configuration data";
                    case ".db": return StartsWith(header, Encoding.ASCII.GetBytes("SQLite format 3\0")) ? "Korg SQLite database" : "Korg database";
                    case "":
                        if (StartsWith(header, Encoding.ASCII.GetBytes("Korg")))
                            return "Korg sample-library object";
                        break;
                }
            }
            return null;
        }

        private static void AddKorgInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            var type = KorgTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Korg");
            Add(section, "Format hint", type);
            Add(section, "Product folder", SegmentAfter(path, "Korg"));
            Add(section, "Role", KorgRoleFromPath(path));
            Add(section, "File size", FormatBytes(fileLength));

            if (StartsWith(header, Encoding.ASCII.GetBytes("WMMS")))
            {
                Add(section, "Header marker", "WMMS");
                var name = ReadAsciiZ(header, 13, 48);
                if (!string.IsNullOrWhiteSpace(name))
                    Add(section, "Visible WaveMotion name", name);
                if (header.Length >= 0x34 && Encoding.ASCII.GetString(header, 0x30, 4) == "KMAP")
                    Add(section, "Keymap marker", "KMAP at 0x30");
            }
            else if (StartsWith(header, Encoding.ASCII.GetBytes("Korg")))
            {
                Add(section, "Header marker", "Korg");
                var markers = FindReadableTextLines(sample, 3, 80)
                    .Where(IsUsefulKorgMarker)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(20)
                    .ToArray();
                if (markers.Length > 0)
                    Add(section, "Visible object markers", string.Join(Environment.NewLine, markers));
            }
            else if (StartsWith(header, Encoding.ASCII.GetBytes("SQLite format 3\0")))
            {
                Add(section, "Header marker", "SQLite");
            }

            if (Path.GetExtension(path).Length == 0)
                Add(section, "Object id", Path.GetFileName(path));
            Add(section, "Notes", "Korg sample and synth-library files are proprietary. FileDentify reports folder role, header markers, object names, and visible identifiers; it does not decode the sample payload.");
        }

        private static string GForceTypeName(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".cpt2")
                return "GForce M-Tron tape bank";
            if (path.IndexOf("GForce", StringComparison.OrdinalIgnoreCase) >= 0 &&
                path.IndexOf("M-Tron", StringComparison.OrdinalIgnoreCase) >= 0)
                return "GForce M-Tron library file";
            return null;
        }

        private static void AddGForceInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
        {
            var type = GForceTypeName(path);
            if (type == null)
                return;

            var section = AddSection(sections, "GForce M-Tron");
            Add(section, "Format hint", type);
            Add(section, "Product folder", SegmentAfter(path, "GForce"));
            Add(section, "Library folder", SegmentBeforeFile(path, "M-Tron Pro Library"));
            Add(section, "Bank name", CleanSampleLibraryName(Path.GetFileNameWithoutExtension(path)));
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "Header note", LooksLikeText(header) ? "Text-like header" : "Binary/proprietary tape-bank payload");
            Add(section, "Notes", "GForce M-Tron tape banks are large proprietary library containers. FileDentify identifies the bank and context without unpacking or decoding the sample data.");
        }

        private static string ToontrackTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".obw")
                return "Toontrack sound library data";
            if (!IsToontrackPath(path))
                return null;
            var name = Path.GetFileName(path);
            if (name.Equals("soundstats", StringComparison.OrdinalIgnoreCase))
                return "Toontrack sound statistics/index";
            if (name.Equals("s3presetconf", StringComparison.OrdinalIgnoreCase))
                return "Toontrack Superior Drummer preset configuration";
            if (LooksLikeText(header))
                return "Toontrack text metadata or preset data";
            return "Toontrack library asset";
        }

        private static void AddToontrackInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            var type = ToontrackTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Toontrack");
            Add(section, "Format hint", type);
            Add(section, "Product folder", SegmentAfter(path, "Toontrack"));
            Add(section, "Role", ToontrackRoleFromPath(path));
            Add(section, "File size", FormatBytes(fileLength));

            if (StartsWith(header, Encoding.ASCII.GetBytes("RIFF")) && header.Length >= 12)
            {
                Add(section, "Container", "RIFF-like Toontrack sound bank");
                Add(section, "Form", Encoding.ASCII.GetString(header, 8, 4));
                var names = FindAsciiStrings(sample, 3, 120)
                    .Select(s => s.Value)
                    .Where(IsUsefulToontrackMarker)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(24)
                    .ToArray();
                if (names.Length > 0)
                    Add(section, "Visible channels or articulations", string.Join(Environment.NewLine, names));
            }
            else if (LooksLikeText(header))
            {
                var textLines = FindReadableTextLines(sample, 3, 120)
                    .Where(IsUsefulToontrackMarker)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(24)
                    .ToArray();
                if (textLines.Length > 0)
                    Add(section, "Visible preset or kit entries", string.Join(Environment.NewLine, textLines));
            }

            Add(section, "Notes", "Toontrack library files are proprietary drum-library data. FileDentify reports product, role, RIFF/container clues, and visible kit or microphone names where available.");
        }

        private static string DecentSamplerTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".dspreset")
                return "Decent Sampler preset";
            if (ext == ".dsbundle")
                return "Decent Sampler bundle";
            if (IsDecentSamplerPath(path) && LooksLikeText(header))
                return "Decent Sampler library metadata";
            return null;
        }

        private static void AddDecentSamplerInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            var type = DecentSamplerTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Decent Sampler");
            Add(section, "Format hint", type);
            Add(section, "Library folder", DecentSamplerLibraryFromPath(path));
            Add(section, "Preset name", Path.GetFileNameWithoutExtension(path));
            Add(section, "File size", FormatBytes(fileLength));

            if (LooksLikeText(header))
            {
                var text = Encoding.UTF8.GetString(sample.Take(Math.Min(sample.Length, 1024 * 1024)).ToArray());
                var sampleMatches = Regex.Matches(text, "<sample\\b[^>]*\\bpath=\"(?<path>[^\"]+)\"[^>]*", RegexOptions.IgnoreCase);
                Add(section, "Referenced samples", sampleMatches.Count.ToString(CultureInfo.InvariantCulture));
                var sampleLines = sampleMatches.Cast<Match>()
                    .Select(m => DecentSamplerSampleSummary(m))
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(24)
                    .ToArray();
                if (sampleLines.Length > 0)
                    Add(section, "Sample references", string.Join(Environment.NewLine, sampleLines));

                var groups = Regex.Matches(text, "<group\\b", RegexOptions.IgnoreCase).Count;
                if (groups > 0)
                    Add(section, "Groups", groups.ToString(CultureInfo.InvariantCulture));
                var controls = Regex.Matches(text, "<control\\b", RegexOptions.IgnoreCase).Count;
                if (controls > 0)
                    Add(section, "Controls", controls.ToString(CultureInfo.InvariantCulture));
                AddDecentSamplerXmlAttribute(section, text, "ui", "width", "UI width");
                AddDecentSamplerXmlAttribute(section, text, "ui", "height", "UI height");
                AddDecentSamplerXmlAttribute(section, text, "ui", "bgImage", "Background image");
            }

            Add(section, "Notes", "Decent Sampler presets are XML-based sampler instruments. FileDentify reports sample references, note ranges, UI metadata, and counts without loading audio.");
        }

        private static bool IsXlnAudioPath(string path)
        {
            return path.IndexOf("XLN Audio", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("Addictive Drums", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("Addictive Keys", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("Addictive Trigger", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsSpectrasonicsPath(string path)
        {
            return path.IndexOf("Spectrasonics", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\STEAM\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\SAGE\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsKorgPath(string path)
        {
            return path.IndexOf("\\Korg\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\KORG ", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsToontrackPath(string path)
        {
            return path.IndexOf("\\Toontrack\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\EZX_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\SL-", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsDecentSamplerPath(string path)
        {
            return path.IndexOf("\\DecentSampler\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\Decent Sampler", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf(".dsbundle", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string XlnProductFromPath(string path)
        {
            foreach (var product in new[] { "Addictive Drums 2", "Addictive Keys", "Addictive Trigger", "XO" })
                if (path.IndexOf(product, StringComparison.OrdinalIgnoreCase) >= 0)
                    return product;
            return "XLN Audio";
        }

        private static string SpectrasonicsFamilyFromPath(string path)
        {
            if (path.IndexOf("\\STEAM\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "STEAM";
            if (path.IndexOf("\\SAGE\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "SAGE";
            return "Spectrasonics";
        }

        private static string SpectrasonicsProductFromPath(string path)
        {
            foreach (var product in new[] { "Omnisphere", "Keyscape", "Trilian", "Stylus RMX" })
                if (path.IndexOf(product, StringComparison.OrdinalIgnoreCase) >= 0)
                    return product;
            return ParentName(path);
        }

        private static string ParentName(string path)
        {
            var parent = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);
            return parent ?? string.Empty;
        }

        private static string CleanSampleLibraryName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            return Regex.Replace(value, "[_\\-]+", " ").Trim();
        }

        private static string KorgRoleFromPath(string path)
        {
            if (path.IndexOf("\\WaveMotion\\", StringComparison.OrdinalIgnoreCase) >= 0) return "WaveMotion sample/keymap data";
            if (path.IndexOf("\\Randomization Data\\", StringComparison.OrdinalIgnoreCase) >= 0) return "wavestate randomization data";
            if (path.IndexOf("\\Collections\\Sample\\", StringComparison.OrdinalIgnoreCase) >= 0) return "sample collection object";
            if (path.IndexOf("\\Database\\", StringComparison.OrdinalIgnoreCase) >= 0) return "database/index";
            if (path.IndexOf("\\Effects\\IRs\\", StringComparison.OrdinalIgnoreCase) >= 0) return "effect impulse-response data";
            return "Korg library data";
        }

        private static string ToontrackRoleFromPath(string path)
        {
            var file = Path.GetFileName(path);
            if (path.IndexOf("\\Sounds\\", StringComparison.OrdinalIgnoreCase) >= 0 && Path.GetExtension(path).Equals(".obw", StringComparison.OrdinalIgnoreCase))
                return "sound payload bank";
            if (file.Equals("soundstats", StringComparison.OrdinalIgnoreCase)) return "sound statistics/index";
            if (file.Equals("s3presetconf", StringComparison.OrdinalIgnoreCase)) return "Superior Drummer preset configuration";
            if (path.IndexOf("\\Presets", StringComparison.OrdinalIgnoreCase) >= 0) return "preset data";
            if (path.IndexOf("\\Graphics", StringComparison.OrdinalIgnoreCase) >= 0) return "user-interface graphics";
            return "Toontrack library data";
        }

        private static string SegmentAfter(string path, string segment)
        {
            var parts = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length - 1; i++)
                if (parts[i].Equals(segment, StringComparison.OrdinalIgnoreCase))
                    return parts[i + 1];
            return string.Empty;
        }

        private static string SegmentBeforeFile(string path, string fallback)
        {
            var directory = Path.GetDirectoryName(path) ?? string.Empty;
            var name = Path.GetFileName(directory);
            return string.IsNullOrWhiteSpace(name) ? fallback : name;
        }

        private static string DecentSamplerLibraryFromPath(string path)
        {
            var parts = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i].Equals("DecentSampler", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length)
                    return parts[i + 1];
                if (parts[i].EndsWith(".dsbundle", StringComparison.OrdinalIgnoreCase))
                    return parts[i];
            }
            return ParentName(path);
        }

        private static string DecentSamplerSampleSummary(Match sample)
        {
            var path = sample.Groups["path"].Value;
            var attrs = new[] { "rootNote", "loNote", "hiNote", "loVel", "hiVel", "seqPosition" }
                .Select(name => AttributeValue(sample.Value, name))
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToArray();
            return attrs.Length == 0 ? path : path + " " + string.Join(" ", attrs);
        }

        private static string AttributeValue(string xml, string name)
        {
            var match = Regex.Match(xml, "\\b" + Regex.Escape(name) + "=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase);
            return match.Success ? name + "=" + match.Groups["value"].Value : string.Empty;
        }

        private static void AddDecentSamplerXmlAttribute(ReportSection section, string text, string element, string attribute, string label)
        {
            var match = Regex.Match(text, "<" + Regex.Escape(element) + "\\b[^>]*\\b" + Regex.Escape(attribute) + "=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase);
            if (match.Success)
                Add(section, label, match.Groups["value"].Value);
        }

        private static string ReadAsciiZ(byte[] data, int offset, int maxLength)
        {
            if (offset < 0 || offset >= data.Length)
                return string.Empty;
            var end = offset;
            var limit = Math.Min(data.Length, offset + maxLength);
            while (end < limit && data[end] >= 32 && data[end] < 127)
                end++;
            return end > offset ? Encoding.ASCII.GetString(data, offset, end - offset).Trim() : string.Empty;
        }

        private static bool IsUsefulKorgMarker(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 80)
                return false;
            return value.IndexOf("Korg", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Object", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("PCM", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("ADSR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Voice", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Pitch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Sample", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsUsefulToontrackMarker(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 120)
                return false;
            var trimmed = value.Trim().Trim('"');
            if (trimmed.Length < 3)
                return false;
            var lower = trimmed.ToLowerInvariant();
            if (lower == "_loop" || lower == "_pack0" || lower == "_intensity")
                return true;
            return lower.Contains("kick") ||
                lower.Contains("snare") ||
                lower.Contains("tom") ||
                lower.Contains("hat") ||
                lower.Contains("ride") ||
                lower.Contains("crash") ||
                lower.Contains("amb") ||
                lower.Contains("oh") ||
                lower.Contains("ezx") ||
                lower.Contains("avatar") ||
                lower.Contains("preset") ||
                lower.Contains("kit");
        }

        private static string FormatParsedBytes(string value)
        {
            long parsed;
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? FormatBytes(parsed)
                : value + " bytes";
        }
    }
}
