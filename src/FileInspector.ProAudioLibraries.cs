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
        private static string UniversalAudioLunaTypeName(string path, byte[] header)
        {
            if (!IsUniversalAudioLunaPath(path))
                return null;

            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".cir": return "Universal Audio LUNA convolution/impulse data";
                case ".cmr": return "Universal Audio LUNA model/resource data";
                case ".rev": return "Universal Audio LUNA reverb/response data";
                case ".dat": return "Universal Audio LUNA data payload";
                case ".bin": return "Universal Audio LUNA binary payload";
                case ".json": return "Universal Audio LUNA JSON metadata";
                default: return "Universal Audio LUNA component data";
            }
        }

        private static void AddUniversalAudioLunaInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            var type = UniversalAudioLunaTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Universal Audio LUNA");
            Add(section, "Format hint", type);
            Add(section, "Component", UniversalAudioComponentName(path));
            Add(section, "Role", UniversalAudioRoleFromPath(path));
            Add(section, "File size", FormatBytes(fileLength));
            if (LooksLikeText(header))
                AddVisibleKeyValuePairs(section, Encoding.UTF8.GetString(sample.Take(Math.Min(sample.Length, 256 * 1024)).ToArray()), "Visible metadata", 18);
            else
                Add(section, "Header note", "Binary/proprietary payload");
            Add(section, "Notes", "Universal Audio LUNA is Universal Audio's recording system and plug-in platform. These component files belong to LUNA instruments, effects, reverbs, or model data. FileDentify reports component context, role, size, and visible metadata only.");
        }

        private static string AirMusicTechnologyTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".big" && (IsAirMusicTechnologyPath(path) || StartsWith(header, Encoding.ASCII.GetBytes("WzooWzoo"))))
                return "AIR Music Technology content archive";
            if (ext == ".patch" && (IsAirMusicTechnologyPath(path) || LooksLikeAirPatch(header)))
                return "AIR Structure patch";
            return null;
        }

        private static void AddAirMusicTechnologyInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            var type = AirMusicTechnologyTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "AIR Music Technology");
            Add(section, "Format hint", type);
            Add(section, "Product folder", SegmentAfter(path, "AIR Music Technology"));
            Add(section, "Library folder", ParentName(path));
            Add(section, "File size", FormatBytes(fileLength));

            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".big")
            {
                if (StartsWith(header, Encoding.ASCII.GetBytes("WzooWzoo")))
                    Add(section, "Header marker", "WzooWzoo");
                var entries = FindReadableTextLines(sample, 5, 120)
                    .Where(IsLikelyArchivePath)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(24)
                    .ToArray();
                if (entries.Length > 0)
                    Add(section, "Visible archive entries", string.Join(Environment.NewLine, entries));
                var pkCount = CountAsciiMarker(sample, "PK");
                if (pkCount > 0)
                    Add(section, "ZIP markers in sample", pkCount.ToString(CultureInfo.InvariantCulture));
            }
            else if (LooksLikeText(header))
            {
                var text = Encoding.UTF8.GetString(sample.Take(Math.Min(sample.Length, 1024 * 1024)).ToArray());
                AddXmlRoot(section, text);
                AddXmlAttributes(section, text, "H3Part", "Name", "Visible parts", 20);
                AddXmlAttributes(section, text, "H3Part", "Type", "Visible part types", 20);
                AddXmlAttributes(section, text, "Sample", "File", "Referenced samples", 24);
            }

            Add(section, "Notes", "AIR Music Technology instruments such as Structure and Transfuser use patch files and large content archives for sampler/synth libraries. FileDentify reports patch XML and visible archive paths without unpacking proprietary payloads.");
        }

        private static string MaizeSamplerTypeName(string path, byte[] header)
        {
            if (Path.GetExtension(path).Equals(".mse", StringComparison.OrdinalIgnoreCase) || StartsWith(header, Encoding.ASCII.GetBytes("MSE ")))
                return "Maize Sampler exported instrument";
            return null;
        }

        private static void AddMaizeSamplerInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            var type = MaizeSamplerTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Maize Sampler");
            Add(section, "Format hint", type);
            Add(section, "Library folder", ParentName(path));
            Add(section, "Instrument file", Path.GetFileNameWithoutExtension(path));
            Add(section, "File size", FormatBytes(fileLength));
            if (StartsWith(header, Encoding.ASCII.GetBytes("MSE ")))
                Add(section, "Header marker", "MSE");

            var visible = FindReadableTextLines(sample, 3, 60)
                .Where(IsUsefulMaizeText)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(18)
                .ToArray();
            if (visible.Length > 0)
                Add(section, "Visible names", string.Join(Environment.NewLine, visible));

            Add(section, "Notes", "Maize Sampler lets small developers package sample instruments as plug-ins or players. .mse files are exported instrument payloads and are often proprietary or encrypted; FileDentify reports visible names and context only.");
        }

        private static string AppliedAcousticsSystemsTypeName(string path, byte[] header)
        {
            if (!IsAppliedAcousticsSystemsPath(path))
                return null;

            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".aasbank") return "Applied Acoustics Systems bank";
            if (ext == ".aas-gui") return "Applied Acoustics Systems GUI resource";
            if (ext == ".lbin") return StartsWith(header, new byte[] { 0x1B, (byte)'L', (byte)'u', (byte)'a' }) ? "Applied Acoustics Systems Lua resource bundle" : "Applied Acoustics Systems binary resource";
            if (ext.Contains("preset")) return "Applied Acoustics Systems preset";
            if (ext.Contains("bank")) return "Applied Acoustics Systems bank";
            if (ext.Contains("pack")) return "Applied Acoustics Systems pack";
            if (ext == ".meta" || ext == ".json" || ext == ".xml") return "Applied Acoustics Systems metadata";
            return null;
        }

        private static void AddAppliedAcousticsSystemsInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            var type = AppliedAcousticsSystemsTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Applied Acoustics Systems");
            Add(section, "Format hint", type);
            Add(section, "Product folder", SegmentAfter(path, "Applied Acoustics Systems"));
            Add(section, "Item name", Path.GetFileNameWithoutExtension(path));
            Add(section, "File size", FormatBytes(fileLength));

            var text = Encoding.GetEncoding(28591).GetString(sample.Take(Math.Min(sample.Length, 1024 * 1024)).ToArray());
            AddVisibleKeyValuePairs(section, text, "Visible preset metadata", 24,
                "name", "author", "category", "comment", "copyright", "creation_date", "cdate", "mdate", "engine", "version", "folder", "unqualifiedName");

            var nameCount = Regex.Matches(text, "\\bname\\s*=", RegexOptions.IgnoreCase).Count;
            if (nameCount > 0)
                Add(section, "Visible name fields", nameCount.ToString(CultureInfo.InvariantCulture));
            if (Path.GetExtension(path).Equals(".lbin", StringComparison.OrdinalIgnoreCase) && text.IndexOf("Lua", StringComparison.OrdinalIgnoreCase) >= 0)
                Add(section, "Script marker", "Lua bytecode/resource strings visible in sample");
            Add(section, "Notes", "Applied Acoustics Systems makes modeled instruments and sound-player libraries such as Lounge Lizard, Chromaphone, Strum, and Ultra Analog. FileDentify reports visible bank, preset, pack, and resource metadata without decoding protected sound data.");
        }

        private static string AudioModelingTypeName(string path, byte[] header)
        {
            if (!IsAudioModelingPath(path))
                return null;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".nksf": return "Audio Modeling / SWAM NKS preset";
                case ".nksfx": return "Audio Modeling / SWAM NKS effect preset";
                case ".ogg": return "Audio Modeling / SWAM preview audio";
                case ".json": return "Audio Modeling / SWAM JSON metadata";
                case ".meta": return "Audio Modeling / SWAM metadata";
                case ".webp": return "Audio Modeling / SWAM web image";
                default: return null;
            }
        }

        private static string UjamTypeName(string path, byte[] header)
        {
            if (!IsUjamPath(path))
                return null;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".blob": return "UJAM sound or model data blob";
                case ".patch": return "UJAM preset patch";
                case ".settings": return "UJAM settings file";
                case ".json": return "UJAM JSON metadata";
                case ".yaml": return "UJAM YAML metadata";
                case ".nksf": return "UJAM NKS preset";
                case ".nksfx": return "UJAM NKS effect preset";
                case ".meta": return "UJAM metadata";
                default: return null;
            }
        }

        private static string UjamStyleBlobTypeName(string path, byte[] header)
        {
            if (!IsUjamStyleBlobPath(path))
                return null;
            return "UJAM-style sound or model data blob";
        }

        private static string ValhallaDspTypeName(string path, byte[] header)
        {
            if (Path.GetExtension(path).Equals(".vpreset", StringComparison.OrdinalIgnoreCase) &&
                (IsValhallaDspPath(path) || LooksLikeText(header)))
                return "Valhalla DSP preset";
            return null;
        }

        private static string ModarttTypeName(string path, byte[] header)
        {
            if (!IsModarttPath(path))
                return null;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".ptq") return "Modartt Pianoteq add-on package";
            if (ext == ".fxp" || ext == ".mfxp" || ext == ".fxp,1") return "Modartt Pianoteq preset";
            if (ext == ".prefs") return "Modartt preferences";
            return null;
        }

        private static void AddAudioModelingInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            var type = AudioModelingTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Audio Modeling");
            Add(section, "Format hint", type);
            Add(section, "Product folder", SegmentAfter(path, "Audio Modeling"));
            Add(section, "Role", AudioModelingRoleFromPath(path));
            Add(section, "File size", FormatBytes(fileLength));
            if (LooksLikeText(header))
                AddVisibleKeyValuePairs(section, Encoding.UTF8.GetString(sample.Take(Math.Min(sample.Length, 512 * 1024)).ToArray()), "Visible metadata", 20);
            Add(section, "Notes", "Audio Modeling SWAM instruments are modeled solo-instrument plug-ins. Their support files can include NKS presets, preview audio, metadata, and artwork. FileDentify reports product and visible metadata while Native Instruments sections handle generic NKS details.");
        }

        private static void AddUjamInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            var type = UjamTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "UJAM");
            Add(section, "Format hint", type);
            Add(section, "Product folder", SegmentAfter(path, "UJAM"));
            Add(section, "Role", UjamRoleFromPath(path));
            Add(section, "File size", FormatBytes(fileLength));

            if (Path.GetExtension(path).Equals(".blob", StringComparison.OrdinalIgnoreCase))
            {
                var id = ReadAsciiZ(header, 0, 64);
                if (Regex.IsMatch(id ?? string.Empty, "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", RegexOptions.IgnoreCase))
                    Add(section, "Leading UUID", id);
                Add(section, "Header note", LooksLikeText(header) ? "Text-like header" : "Binary/proprietary payload");
            }
            else if (LooksLikeText(header))
            {
                var text = Encoding.UTF8.GetString(sample.Take(Math.Min(sample.Length, 512 * 1024)).ToArray());
                AddVisibleKeyValuePairs(section, text, "Visible metadata", 24,
                    "version", "buildNumber", "branch", "commitHash", "category", "author", "key", "valueName", "preset");
                var preset = Regex.Match(text, "\"key\"\\s*:\\s*\"preset\"[^\\{\\}\\[\\]]*\"valueName\"\\s*:\\s*\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase);
                if (preset.Success)
                    Add(section, "Preset name", CleanMetadataText(preset.Groups["value"].Value));
                AddJsonArrayCount(section, text, "dsp_settings", "DSP settings");
            }

            Add(section, "Notes", "UJAM products include virtual guitarists, beatmakers, drummers, bassists, and effect plug-ins. Their files can be presets, NKS metadata, settings, or large .blob content payloads. FileDentify reports product context, visible preset/build metadata, and blob identity without decoding proprietary payloads.");
        }

        private static void AddUjamStyleBlobInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
        {
            var type = UjamStyleBlobTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "UJAM-style blob");
            Add(section, "Format hint", type);
            Add(section, "Vendor folder", UjamStyleBlobVendor(path));
            Add(section, "Product folder", UjamStyleBlobProduct(path));
            Add(section, "Role", "content/model payload");
            Add(section, "File size", FormatBytes(fileLength));

            var id = ReadAsciiZ(header, 0, 64);
            if (Regex.IsMatch(id ?? string.Empty, "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", RegexOptions.IgnoreCase))
                Add(section, "Leading UUID", id);
            Add(section, "Header note", LooksLikeText(header) ? "Text-like header" : "Binary/proprietary payload");
            Add(section, "Notes", "Some vendors use UJAM-style .blob payload containers for sound or model content even when the product is not made by UJAM. FileDentify reports vendor and product path context, but does not decode the proprietary payload.");
        }

        private static void AddValhallaDspInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            var type = ValhallaDspTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Valhalla DSP");
            Add(section, "Format hint", type);
            Add(section, "Product folder", SegmentAfter(path, "Valhalla DSP, LLC"));
            Add(section, "Preset folder", ParentName(path));
            Add(section, "Preset file", Path.GetFileNameWithoutExtension(path));
            Add(section, "File size", FormatBytes(fileLength));

            if (LooksLikeText(header))
            {
                var text = Encoding.UTF8.GetString(sample.Take(Math.Min(sample.Length, 256 * 1024)).ToArray());
                AddXmlRoot(section, text);
                AddXmlAttribute(section, text, "pluginVersion", "Plugin version");
                AddXmlAttribute(section, text, "presetName", "Preset name");
                AddValhallaInterestingParameters(section, text);
            }

            Add(section, "Notes", "Valhalla DSP .vpreset files are small XML-style plug-in presets. FileDentify reports product, preset name, version, and selected visible parameters.");
        }

        private static void AddModarttInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            var type = ModarttTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Modartt Pianoteq");
            Add(section, "Format hint", type);
            Add(section, "Product folder", SegmentAfter(path, "Modartt"));
            Add(section, "Item name", Path.GetFileNameWithoutExtension(path));
            Add(section, "File size", FormatBytes(fileLength));

            if (StartsWith(header, Encoding.ASCII.GetBytes("CcnK")))
            {
                Add(section, "VST chunk marker", "CcnK");
                if (header.Length >= 20)
                    Add(section, "Plug-in id", Encoding.ASCII.GetString(header, 16, 4));
                var presetName = ReadAsciiZ(header, 28, 64);
                if (!string.IsNullOrWhiteSpace(presetName))
                    Add(section, "Preset name", presetName);
            }

            var visible = FindReadableTextLines(sample, 4, 80)
                .Where(IsUsefulModarttText)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(24)
                .ToArray();
            if (visible.Length > 0)
                Add(section, "Visible instrument or preset text", string.Join(Environment.NewLine, visible));

            Add(section, "Notes", "Modartt Pianoteq is a physically modeled piano/instrument platform. These files can be instrument add-ons, presets, or preferences. FileDentify reports visible instrument, version, preset, and package text only.");
        }

        private static bool IsUniversalAudioLunaPath(string path)
        {
            return path.IndexOf("Universal Audio", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf(".lunacomponent", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsAirMusicTechnologyPath(string path)
        {
            return path.IndexOf("AIR Music Technology", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\Structure\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\Transfuser\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsAppliedAcousticsSystemsPath(string path)
        {
            return path.IndexOf("Applied Acoustics Systems", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsAudioModelingPath(string path)
        {
            return path.IndexOf("Audio Modeling", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\SWAM", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsUjamPath(string path)
        {
            return path.IndexOf("\\UJAM\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("/UJAM/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsUjamStyleBlobPath(string path)
        {
            if (!Path.GetExtension(path).Equals(".blob", StringComparison.OrdinalIgnoreCase))
                return false;
            return path.IndexOf("\\Crow Hill\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("/Crow Hill/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\Rhodes\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("/Rhodes/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string UjamStyleBlobVendor(string path)
        {
            if (path.IndexOf("\\Crow Hill\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("/Crow Hill/", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Crow Hill";
            if (path.IndexOf("\\Rhodes\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("/Rhodes/", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Rhodes";
            return string.Empty;
        }

        private static string UjamStyleBlobProduct(string path)
        {
            var vendor = UjamStyleBlobVendor(path);
            if (!string.IsNullOrWhiteSpace(vendor))
                return SegmentAfter(path, vendor);
            return ParentName(path);
        }

        private static bool IsValhallaDspPath(string path)
        {
            return path.IndexOf("Valhalla DSP", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsModarttPath(string path)
        {
            return path.IndexOf("\\Modartt\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\Pianoteq", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string UniversalAudioComponentName(string path)
        {
            foreach (var part in path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries))
                if (part.EndsWith(".lunacomponent", StringComparison.OrdinalIgnoreCase))
                    return Path.GetFileNameWithoutExtension(part);
            return SegmentAfter(path, "Plug-Ins");
        }

        private static string UniversalAudioRoleFromPath(string path)
        {
            if (path.IndexOf("\\datx\\", StringComparison.OrdinalIgnoreCase) >= 0) return "large data payload";
            if (path.IndexOf("\\dat\\", StringComparison.OrdinalIgnoreCase) >= 0) return "component data";
            if (path.IndexOf("rotary_subplugin", StringComparison.OrdinalIgnoreCase) >= 0) return "rotary speaker subplugin data";
            if (path.IndexOf("upper_mics", StringComparison.OrdinalIgnoreCase) >= 0) return "upper microphone impulse data";
            if (path.IndexOf("lower_mics", StringComparison.OrdinalIgnoreCase) >= 0) return "lower microphone impulse data";
            return "LUNA component resource";
        }

        private static string AudioModelingRoleFromPath(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".nksf" || ext == ".nksfx") return "Native Kontrol Standard preset";
            if (ext == ".ogg") return "preview audio";
            if (ext == ".json" || ext == ".meta") return "metadata";
            if (ext == ".webp") return "artwork";
            return "SWAM resource";
        }

        private static string UjamRoleFromPath(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".blob") return "content/model payload";
            if (path.IndexOf("\\Presets\\", StringComparison.OrdinalIgnoreCase) >= 0) return "preset";
            if (path.IndexOf("\\assets\\", StringComparison.OrdinalIgnoreCase) >= 0) return "asset metadata";
            if (path.IndexOf("\\node_modules\\", StringComparison.OrdinalIgnoreCase) >= 0) return "embedded plug-in support module metadata";
            if (ext == ".nksf" || ext == ".nksfx") return "Native Kontrol Standard preset";
            return "UJAM resource";
        }

        private static bool LooksLikeAirPatch(byte[] header)
        {
            if (!LooksLikeText(header))
                return false;
            var text = Encoding.ASCII.GetString(header.Take(Math.Min(header.Length, 4096)).ToArray());
            return text.IndexOf("<H3Patch", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddXmlRoot(ReportSection section, string text)
        {
            var match = Regex.Match(text ?? string.Empty, "<\\s*(?<name>[A-Za-z0-9_:-]+)");
            if (match.Success)
                Add(section, "Root element", match.Groups["name"].Value);
        }

        private static void AddXmlAttributes(ReportSection section, string text, string element, string attribute, string title, int max)
        {
            var values = Regex.Matches(text ?? string.Empty, "<" + Regex.Escape(element) + "\\b[^>]*\\b" + Regex.Escape(attribute) + "=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase)
                .Cast<Match>()
                .Select(m => CleanMetadataText(m.Groups["value"].Value))
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(max)
                .ToArray();
            if (values.Length > 0)
                Add(section, title, string.Join(Environment.NewLine, values));
        }

        private static void AddXmlAttribute(ReportSection section, string text, string attribute, string title)
        {
            var match = Regex.Match(text ?? string.Empty, "\\b" + Regex.Escape(attribute) + "=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase);
            if (match.Success)
                Add(section, title, CleanMetadataText(match.Groups["value"].Value));
        }

        private static void AddValhallaInterestingParameters(ReportSection section, string text)
        {
            var names = new[] { "Mix", "DelaySync", "DelayNote", "Delay_Ms", "Warp", "Feedback", "Density", "Width", "ModRate", "ModDepth", "Mode" };
            var lines = new List<string>();
            foreach (var name in names)
            {
                var match = Regex.Match(text ?? string.Empty, "\\b" + Regex.Escape(name) + "=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase);
                if (match.Success)
                    lines.Add(name + ": " + CleanMetadataText(match.Groups["value"].Value));
            }
            if (lines.Count > 0)
                Add(section, "Visible parameters", string.Join(Environment.NewLine, lines.Take(16).ToArray()));
        }

        private static void AddJsonArrayCount(ReportSection section, string text, string property, string title)
        {
            var match = Regex.Match(text ?? string.Empty, "\"" + Regex.Escape(property) + "\"\\s*:\\s*\\[(?<value>.*?)\\]", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!match.Success)
                return;
            var count = Regex.Matches(match.Groups["value"].Value, "\\{").Count;
            if (count > 0)
                Add(section, title, count.ToString(CultureInfo.InvariantCulture));
        }

        private static void AddVisibleKeyValuePairs(ReportSection section, string text, string title, int max, params string[] preferredKeys)
        {
            var keys = preferredKeys == null || preferredKeys.Length == 0
                ? new[] { "name", "title", "author", "category", "version", "engine", "product", "identifier", "vendor" }
                : preferredKeys;
            var lines = new List<string>();
            foreach (var key in keys)
            {
                foreach (Match match in Regex.Matches(text ?? string.Empty, "\\b" + Regex.Escape(key) + "\\s*=\\s*\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase))
                {
                    var value = CleanMetadataText(match.Groups["value"].Value);
                    if (!string.IsNullOrWhiteSpace(value))
                        lines.Add(key + ": " + value);
                    if (lines.Count >= max)
                        break;
                }
                if (lines.Count >= max)
                    break;
            }

            if (lines.Count == 0)
            {
                lines.AddRange(FindReadableTextLines(Encoding.GetEncoding(28591).GetBytes(text ?? string.Empty), 4, max)
                    .Where(IsUsefulMetadataLine)
                    .Take(max));
            }

            if (lines.Count > 0)
                Add(section, title, string.Join(Environment.NewLine, lines.Distinct(StringComparer.OrdinalIgnoreCase).Take(max).ToArray()));
        }

        private static bool IsLikelyArchivePath(string value)
        {
            return value.IndexOf("/", StringComparison.Ordinal) >= 0 &&
                (value.IndexOf(".wav", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf(".aif", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf(".patch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf(".xml", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsUsefulMaizeText(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 120)
                return false;
            return value.IndexOf("SampleScience", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("ESL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Kit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Preset", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Drum", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Atmos", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Sound", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsUsefulMetadataLine(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 160)
                return false;
            return value.IndexOf("version", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("author", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("category", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("product", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsUsefulModarttText(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 180)
                return false;
            return value.IndexOf("Pianoteq", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Modartt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("factory", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Preset", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Modelled", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Steinway", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Rhodes", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Cimbalom", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int CountAsciiMarker(byte[] data, string marker)
        {
            var count = 0;
            var start = 0;
            while (start >= 0 && start < data.Length)
            {
                var found = IndexOfAscii(data, marker, start);
                if (found < 0)
                    break;
                count++;
                start = found + marker.Length;
            }
            return count;
        }
    }
}
