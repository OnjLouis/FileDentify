using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace FileDentify
{
    internal static partial class FileInspector
    {
        private static string MusicProjectFormatTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".ablbundle") return "Ableton Move/Live bundle";
            if (ext == ".abl") return "Ableton Move/Live song JSON";
            if (ext == ".ablpreset") return "Ableton preset JSON";
            if (ext == ".als") return "Ableton Live Set";
            if (ext == ".adg") return "Ableton device rack";
            if (ext == ".adv") return "Ableton device preset";
            if (ext == ".helm") return "Helm synthesizer preset";
            if (ext == ".wt" && StartsWith(header, Encoding.ASCII.GetBytes("vawt"))) return "Surge wavetable";
            var audioResourceType = AudioSampleResourceTypeName(path, header);
            if (audioResourceType != null) return audioResourceType;
            if (ext == ".nam") return "Neural Amp Modeler model";
            if (ext == ".mtdrum") return "Microtonic drum preset";
            if (ext == ".chords") return "Chord preset";
            var spitfireType = SpitfireAudioTypeName(path, header);
            if (spitfireType != null) return spitfireType;
            if (ext == ".syx" || (header.Length > 0 && header[0] == 0xF0)) return "MIDI System Exclusive data";
            if (ext == ".svd" || StartsWith(header, Encoding.ASCII.GetBytes("\0nSVD1"))) return "Roland sound data";
            if (ext == ".svq" || StartsWith(header, Encoding.ASCII.GetBytes("RSVQ"))) return "Roland sequencer song";
            if (ext == ".smp" && StartsWith(header, Encoding.ASCII.GetBytes("RFWV"))) return "Roland FA sample data";
            if (ext == ".jgl" && StartsWith(header, Encoding.ASCII.GetBytes("JunoGLibrarianFile"))) return "Roland Juno-G Librarian data";
            if (IsRolandFantomLibrarian(path, header)) return "Roland Fantom Librarian data";
            if (IsYamahaSoftSynthTable(path, header)) return "Yamaha S-YXG software-synthesizer table";
            if (IsOpenAlSpatialAudio(path, header)) return OpenAlSpatialAudioTypeName(path);
            var productionResource = ProductionAudioResourceTypeName(path, header);
            if (productionResource != null) return productionResource;
            if (LhaMethod(header) != null || ext == ".lha" || ext == ".lzh") return "LHA/LZH archive";
            if (ext == ".mogg") return "MOGG multitrack Ogg audio";
            if (ext == ".sfark") return "sfArk compressed SoundFont archive";
            if ((ext == ".rpp" || ext == ".rpp-bak") && IsReaperProject(header)) return ext == ".rpp-bak" ? "REAPER project backup" : "REAPER project";
            if (ext == ".reaperthemezip") return "REAPER theme package";
            if (ext == ".wrk") return "Cakewalk WRK project";
            if (ext == ".cwp") return "Cakewalk/Sonar CWP project";
            if (ext == ".sfz") return "SFZ sampler instrument";
            if (ext == ".exs") return "Apple Logic EXS sampler instrument";
            if (ext == ".ecw" && StartsWith(header, Encoding.ASCII.GetBytes("ECLW"))) return "Creative/E-mu ECW waveset";
            if (ext == ".bank" && header.Length >= 12 && StartsWith(header, Encoding.ASCII.GetBytes("RIFF")) && Encoding.ASCII.GetString(header, 8, 4) == "FEV ") return "FMOD Designer FEV bank";
            if (ext == ".wem") return "Audiokinetic Wwise encoded media";
            return TrackerModuleType(ext, header);
        }

        private static void AddMusicProjectFormatInfo(List<ReportSection> sections, string path, byte[] header, byte[] stringSample, long fileLength)
        {
            AddAbletonInfo(sections, path, header);
            AddSynthPresetInfo(sections, path, header);
            AddSynthHardwareInfo(sections, path, header);
            AddLhaInfo(sections, path, header);
            AddTrackerModuleInfo(sections, path, header);
            AddMoggInfo(sections, path, header, fileLength);
            AddSfArkInfo(sections, path, header);
            AddReaperProjectInfo(sections, path, stringSample, fileLength);
            AddReaperThemeInfo(sections, path, header, fileLength);
            AddCakewalkInfo(sections, path, stringSample);
            AddSamplerInstrumentInfo(sections, path, stringSample);
            AddCreativeEcwInfo(sections, path, header);
            AddFmodBankInfo(sections, path, header);
            AddWwiseMediaInfo(sections, path, header);
            AddSpitfireAudioInfo(sections, path, header, stringSample, fileLength);
            AddAudioSampleResourceInfo(sections, path, header, stringSample, fileLength);
            AddYamahaSoftSynthInfo(sections, path, header, stringSample, fileLength);
            AddOpenAlSpatialAudioInfo(sections, path, header);
            AddProductionAudioResourceInfo(sections, path, header, stringSample, fileLength);
        }

        private static string SpitfireAudioTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".spitfire": return "Spitfire Audio sample container";
                case ".zmulti": return "Spitfire Audio multi/patch data";
                case ".zpreset": return "Spitfire Audio preset data";
                case ".zconfig": return "Spitfire Audio configuration data";
                case ".lm":
                    if (IsSpitfireAudioPath(path)) return "Spitfire Audio licence or library metadata";
                    break;
                case ".db":
                    if (IsSpitfireAudioPath(path)) return "Spitfire Audio SQLite catalogue";
                    break;
                case ".nksf":
                    if (IsSpitfireAudioPath(path)) return "Spitfire Audio NKS preset";
                    break;
            }
            if (StartsWith(header, Encoding.ASCII.GetBytes("Spitfire")))
                return "Spitfire Audio sample container or metadata";
            return null;
        }

        private static void AddSpitfireAudioInfo(List<ReportSection> sections, string path, byte[] header, byte[] stringSample, long fileLength)
        {
            var typeName = SpitfireAudioTypeName(path, header);
            if (typeName == null && !IsSpitfireAudioPath(path))
                return;

            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (typeName == null && ext != ".json" && ext != ".meta")
                return;

            var section = AddSection(sections, "Spitfire Audio");
            Add(section, "Format hint", typeName ?? "Spitfire Audio support or metadata file");

            var library = SpitfireLibraryFromPath(path);
            if (!string.IsNullOrWhiteSpace(library))
                Add(section, "Library", library);

            var role = SpitfireRoleFromPath(path, ext);
            if (!string.IsNullOrWhiteSpace(role))
                Add(section, "Role", role);

            var versionFolder = SpitfireVersionFolder(path);
            if (!string.IsNullOrWhiteSpace(versionFolder))
                Add(section, "Version folder", versionFolder);

            var cleanName = CleanSpitfireName(Path.GetFileNameWithoutExtension(path));
            if (!string.IsNullOrWhiteSpace(cleanName))
                Add(section, "File name as title", cleanName);

            if (StartsWith(header, Encoding.ASCII.GetBytes("Spitfire")))
                Add(section, "Header marker", "Spitfire");

            if (ext == ".db" && StartsWith(header, Encoding.ASCII.GetBytes("SQLite format 3\0")))
                Add(section, "Database", "SQLite catalogue/database used by Spitfire Audio libraries.");

            if (ext == ".nksf")
                Add(section, "NKS note", "Native Kontrol Standard preset associated with a Spitfire library.");

            var visibleNames = SpitfireVisibleNames(stringSample);
            if (visibleNames.Count > 0)
                Add(section, "Visible names", string.Join(Environment.NewLine, visibleNames.ToArray()));

            Add(section, "Notes", "Spitfire formats are mostly proprietary or compressed. FileDentify reports bounded headers, visible strings, folder role, inferred library name, and catalogue hints; it does not decode the sample payload.");
        }

        private static bool IsSpitfireAudioPath(string path)
        {
            return path.IndexOf("Spitfire Audio", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("SpitfireAudio", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string SpitfireLibraryFromPath(string path)
        {
            var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            for (var i = 0; i < parts.Length; i++)
            {
                if (!parts[i].Equals("Spitfire Audio", StringComparison.OrdinalIgnoreCase) &&
                    !parts[i].Equals("SpitfireAudio", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (i + 1 < parts.Length)
                {
                    var first = parts[i + 1];
                    if (first.Equals("Spitfire Audio - LABS", StringComparison.OrdinalIgnoreCase) && i + 2 < parts.Length)
                        return first + " / " + parts[i + 2];
                    return first;
                }
            }
            return string.Empty;
        }

        private static string SpitfireRoleFromPath(string path, string ext)
        {
            var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var start = 0;
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i].Equals("Spitfire Audio", StringComparison.OrdinalIgnoreCase) ||
                    parts[i].Equals("SpitfireAudio", StringComparison.OrdinalIgnoreCase))
                {
                    start = Math.Min(parts.Length, i + 2);
                    if (i + 1 < parts.Length && parts[i + 1].Equals("Spitfire Audio - LABS", StringComparison.OrdinalIgnoreCase))
                        start = Math.Min(parts.Length, i + 3);
                    break;
                }
            }

            for (var i = start; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part.Equals("Samples", StringComparison.OrdinalIgnoreCase))
                    return ext == ".db" ? "Sample catalogue database" : "Sample payload or sample-side metadata";
                if (part.Equals("Presets", StringComparison.OrdinalIgnoreCase))
                    return "Preset";
                if (part.Equals("Patches", StringComparison.OrdinalIgnoreCase))
                    return "Patch";
                if (part.Equals("NKS", StringComparison.OrdinalIgnoreCase))
                    return "NKS browser preset";
                if (part.Equals("PAResources", StringComparison.OrdinalIgnoreCase))
                    return "Player/resource metadata";
                if (part.Equals("dist_database", StringComparison.OrdinalIgnoreCase))
                    return "Distribution database";
            }

            switch (ext)
            {
                case ".spitfire": return "Spitfire sample container";
                case ".zmulti": return "Multi or patch data";
                case ".zpreset": return "Preset";
                case ".zconfig": return "Configuration";
                case ".lm": return "Licence or library metadata";
                case ".db": return "Catalogue database";
                default: return string.Empty;
            }
        }

        private static string SpitfireVersionFolder(string path)
        {
            foreach (var part in path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                if (Regex.IsMatch(part, "^v\\d+(\\.\\d+)+$", RegexOptions.IgnoreCase))
                    return part;
            return string.Empty;
        }

        private static string CleanSpitfireName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;
            var cleaned = Regex.Replace(name, "[_\\-]+", " ").Trim();
            cleaned = Regex.Replace(cleaned, "\\s+", " ");
            return cleaned;
        }

        private static List<string> SpitfireVisibleNames(byte[] sample)
        {
            var names = new List<string>();
            if (sample == null || sample.Length == 0)
                return names;
            foreach (var item in FindAsciiStrings(sample, 5, 80))
            {
                var value = item.Value.Trim();
                if (value.Length == 0)
                    continue;
                if (value.IndexOf(".wav", StringComparison.OrdinalIgnoreCase) < 0 &&
                    value.IndexOf(".flac", StringComparison.OrdinalIgnoreCase) < 0 &&
                    value.IndexOf(".spitfire", StringComparison.OrdinalIgnoreCase) < 0 &&
                    value.IndexOf("Spitfire", StringComparison.OrdinalIgnoreCase) < 0 &&
                    value.IndexOf("LABS", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (!names.Contains(value))
                    names.Add(value);
                if (names.Count >= 12)
                    break;
            }
            return names;
        }

        private static void AddCreativeEcwInfo(List<ReportSection> sections, string path, byte[] header)
        {
            if (!string.Equals(Path.GetExtension(path), ".ecw", StringComparison.OrdinalIgnoreCase) ||
                !StartsWith(header, Encoding.ASCII.GetBytes("ECLW")))
                return;

            var section = AddSection(sections, "Creative ECW waveset");
            Add(section, "Format", "Creative/E-mu ECW wavetable bank");
            Add(section, "Header marker", "ECLW");
            var copyright = ReadNullTerminatedLatin1(header, 0x10, 80);
            var name = ReadNullTerminatedLatin1(header, 0x60, 80);
            var fileName = ReadNullTerminatedLatin1(header, 0xB0, 64);
            if (!string.IsNullOrWhiteSpace(name))
                Add(section, "Visible name", name);
            if (!string.IsNullOrWhiteSpace(fileName))
                Add(section, "Internal file name", fileName);
            if (!string.IsNullOrWhiteSpace(copyright))
                Add(section, "Copyright", copyright);
            Add(section, "Common use", "Creative/E-mu software wavetable sound sets used by older Sound Blaster and soft-synth components.");
        }

        private static string ReadNullTerminatedLatin1(byte[] data, int offset, int maxLength)
        {
            if (data == null || offset < 0 || offset >= data.Length)
                return string.Empty;
            var count = 0;
            while (offset + count < data.Length && count < maxLength && data[offset + count] != 0)
                count++;
            return count == 0 ? string.Empty : Encoding.GetEncoding(28591).GetString(data, offset, count).Trim();
        }

        private static void AddAbletonInfo(List<ReportSection> sections, string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".ablbundle" && ext != ".ablpreset" && ext != ".als" && ext != ".adg" && ext != ".adv")
            {
                if (ext == ".abl")
                {
                    var songSection = AddSection(sections, "Ableton");
                    Add(songSection, "Format hint", AbletonFormatHint(ext));
                    if (LooksLikeJson(header))
                        AddAbletonSongJsonInfo(songSection, header);
                    return;
                }
                return;
            }

            var section = AddSection(sections, "Ableton");
            Add(section, "Format hint", AbletonFormatHint(ext));

            if (StartsWith(header, Encoding.ASCII.GetBytes("PK\x03\x04")))
            {
                Add(section, "Container", "ZIP-compatible bundle");
                AddZipEntrySummary(section, path, new[] { ".als", ".json", ".wav", ".aif", ".aiff", ".ogg", ".flac", ".adg", ".adv", ".ablpreset" });
                return;
            }

            if (LooksLikeJson(header))
                AddAbletonJsonInfo(section, header);
            else if (StartsWith(header, new byte[] { 0x1F, 0x8B }))
                Add(section, "Container", "gzip-compressed Ableton document or preset");
        }

        private static string AbletonFormatHint(string ext)
        {
            switch (ext)
            {
                case ".ablbundle": return "Ableton bundle, often used by Ableton Move and Live exports";
                case ".abl": return "Ableton Move/Live song JSON";
                case ".ablpreset": return "Ableton preset JSON";
                case ".als": return "Ableton Live Set";
                case ".adg": return "Ableton device rack";
                case ".adv": return "Ableton device preset";
                default: return "Ableton-related file";
            }
        }

        private static void AddAbletonJsonInfo(ReportSection section, byte[] header)
        {
            var text = Encoding.UTF8.GetString(header.Take(Math.Min(header.Length, 512 * 1024)).ToArray()).Trim('\uFEFF', '\0', ' ', '\r', '\n', '\t');
            Add(section, "Container", "JSON");
            try
            {
                var serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = 1024 * 1024;
                var root = serializer.DeserializeObject(text) as Dictionary<string, object>;
                if (root == null)
                    return;
                AddJsonString(section, root, "$schema", "Schema");
                AddJsonString(section, root, "kind", "Kind");
                AddJsonString(section, root, "name", "Name");
                AddJsonString(section, root, "creator", "Creator");
                AddJsonString(section, root, "vendor", "Vendor");
                Add(section, "Top-level keys", string.Join(", ", root.Keys.Take(20).ToArray()));
            }
            catch
            {
                Add(section, "JSON note", "Looks like JSON, but the sampled header could not be parsed as a complete object.");
            }
        }

        private static void AddAbletonSongJsonInfo(ReportSection section, byte[] header)
        {
            var text = Encoding.UTF8.GetString(header.Take(Math.Min(header.Length, 1024 * 1024)).ToArray()).Trim('\uFEFF', '\0', ' ', '\r', '\n', '\t');
            Add(section, "Container", "JSON");
            try
            {
                var serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = 2 * 1024 * 1024;
                var root = serializer.DeserializeObject(text) as Dictionary<string, object>;
                if (root == null)
                    return;
                AddJsonString(section, root, "$schema", "Schema");
                AddJsonString(section, root, "stepEditorResolution", "Step editor resolution");
                AddJsonString(section, root, "scale", "Scale");
                AddJsonString(section, root, "melodicLayout", "Melodic layout");
                AddJsonNumber(section, root, "tempo", "Tempo");
                var tracks = GetList(root, "tracks");
                if (tracks != null)
                {
                    Add(section, "Track count", tracks.Count.ToString(CultureInfo.InvariantCulture));
                    var trackKinds = tracks
                        .OfType<Dictionary<string, object>>()
                        .Select(t => GetJsonString(t, "kind"))
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
                        .Select(g => g.Key + " " + g.Count().ToString(CultureInfo.InvariantCulture))
                        .ToArray();
                    if (trackKinds.Length > 0)
                        Add(section, "Track kinds", string.Join("\r\n", trackKinds));
                }
                Add(section, "Top-level keys", string.Join(", ", root.Keys.Take(20).ToArray()));
            }
            catch
            {
                Add(section, "JSON note", "Looks like Ableton song JSON, but the sampled header could not be parsed as a complete object.");
            }
        }

        private static void AddSynthPresetInfo(List<ReportSection> sections, string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".helm")
                AddHelmPresetInfo(sections, header);
            else if (ext == ".wt" && StartsWith(header, Encoding.ASCII.GetBytes("vawt")))
                AddSurgeWavetableInfo(sections, header);
            else if (ext == ".nam")
                AddNamModelInfo(sections, header);
            else if (ext == ".mtdrum")
                AddMicrotonicDrumInfo(sections, header);
            else if (ext == ".chords")
                AddChordPresetInfo(sections, header);
        }

        private static void AddHelmPresetInfo(List<ReportSection> sections, byte[] header)
        {
            var section = AddSection(sections, "Synth preset");
            Add(section, "Format hint", "Helm synthesizer preset");
            var root = ParseJsonObject(header, 1024 * 1024);
            if (root == null)
            {
                Add(section, "JSON note", "Looks like a Helm preset, but the sampled file could not be parsed as JSON.");
                return;
            }
            AddJsonString(section, root, "patch_name", "Patch name");
            AddJsonString(section, root, "folder_name", "Folder/category");
            AddJsonString(section, root, "author", "Author");
            AddJsonString(section, root, "synth_version", "Synth version");
            var settings = GetDictionary(root, "settings");
            if (settings != null)
            {
                Add(section, "Setting count", settings.Count.ToString(CultureInfo.InvariantCulture));
                AddInterestingJsonSettings(section, settings, "filter_on", "cutoff", "resonance", "delay_on", "distortion_on", "arp_on", "beats_per_minute");
            }
        }

        private static void AddSurgeWavetableInfo(List<ReportSection> sections, byte[] header)
        {
            var section = AddSection(sections, "Surge wavetable");
            Add(section, "Format hint", "Surge wavetable file");
            Add(section, "Header marker", "vawt");
            if (header.Length >= 16)
                Add(section, "Header bytes", HexPreview(header, 16));
            Add(section, "Notes", "Surge wavetable support is header-level. FileDentify identifies the wavetable family without rendering oscillator frames.");
        }

        private static void AddNamModelInfo(List<ReportSection> sections, byte[] header)
        {
            var section = AddSection(sections, "Neural Amp Modeler");
            Add(section, "Format hint", "Neural Amp Modeler model");
            var root = ParseJsonObject(header, 2 * 1024 * 1024);
            if (root == null)
            {
                Add(section, "JSON note", "Looks like a NAM model, but the sampled file could not be parsed as JSON.");
                return;
            }
            AddJsonString(section, root, "version", "Model format version");
            AddJsonString(section, root, "architecture", "Architecture");
            var config = GetDictionary(root, "config");
            if (config != null)
            {
                AddJsonNumber(section, config, "sample_rate", "Sample rate");
                var layers = GetList(config, "layers");
                if (layers != null)
                    Add(section, "Layer count", layers.Count.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void AddMicrotonicDrumInfo(List<ReportSection> sections, byte[] header)
        {
            var section = AddSection(sections, "Drum preset");
            Add(section, "Format hint", "Microtonic-style drum preset");
            var fields = ParseSimpleKeyValueText(header);
            AddSimpleField(section, fields, "OscWave", "Oscillator wave");
            AddSimpleField(section, fields, "OscFreq", "Oscillator frequency");
            AddSimpleField(section, fields, "OscDcy", "Oscillator decay");
            AddSimpleField(section, fields, "NFilMod", "Noise filter mode");
            AddSimpleField(section, fields, "Level", "Level");
            Add(section, "Parameter count", fields.Count.ToString(CultureInfo.InvariantCulture));
        }

        private static void AddChordPresetInfo(List<ReportSection> sections, byte[] header)
        {
            var section = AddSection(sections, "Chord preset");
            Add(section, "Format hint", "Text chord preset");
            var fields = ParseSimpleKeyValueText(header);
            AddSimpleField(section, fields, "Name", "Name");
            var chords = fields.Keys.Count(k => Regex.IsMatch(k, @"^\d+$", RegexOptions.CultureInvariant));
            Add(section, "Chord count", chords.ToString(CultureInfo.InvariantCulture));
            var preview = fields
                .Where(p => Regex.IsMatch(p.Key, @"^\d+$", RegexOptions.CultureInvariant))
                .OrderBy(p => int.Parse(p.Key, CultureInfo.InvariantCulture))
                .Take(12)
                .Select(p => p.Key + ": " + p.Value)
                .ToArray();
            if (preview.Length > 0)
                Add(section, "First chords", string.Join("\r\n", preview));
        }

        private static void AddSynthHardwareInfo(List<ReportSection> sections, string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".syx" || (header.Length > 0 && header[0] == 0xF0))
                AddSysExInfo(sections, header);
            else if (ext == ".svd" || StartsWith(header, Encoding.ASCII.GetBytes("\0nSVD1")))
                AddRolandSvdInfo(sections, header);
            else if (ext == ".svq" || StartsWith(header, Encoding.ASCII.GetBytes("RSVQ")))
                AddRolandSvqInfo(sections, header);
            else if (ext == ".smp" && StartsWith(header, Encoding.ASCII.GetBytes("RFWV")))
                AddRolandSampleInfo(sections, header);
            else if (ext == ".jgl" && StartsWith(header, Encoding.ASCII.GetBytes("JunoGLibrarianFile")))
                AddRolandJunoGLibrarianInfo(sections, header);
            else if (IsRolandFantomLibrarian(path, header))
                AddRolandFantomLibrarianInfo(sections, path, header);
        }

        private static void AddSysExInfo(List<ReportSection> sections, byte[] header)
        {
            if (header.Length == 0 || header[0] != 0xF0)
                return;

            var section = AddSection(sections, "MIDI System Exclusive");
            Add(section, "Format hint", "MIDI System Exclusive dump");
            var messages = CountSysExMessages(header);
            Add(section, "Visible SysEx messages", messages.ToString(CultureInfo.InvariantCulture));
            if (header.Length > 1)
                Add(section, "Manufacturer", MidiManufacturerName(header[1]) + " (0x" + header[1].ToString("X2", CultureInfo.InvariantCulture) + ")");
            if (header.Length > 6 && header[1] == 0x41)
            {
                Add(section, "Roland device id", "0x" + header[2].ToString("X2", CultureInfo.InvariantCulture));
                Add(section, "Roland model id", string.Join(" ", header.Skip(3).Take(3).Select(b => b.ToString("X2", CultureInfo.InvariantCulture)).ToArray()));
                Add(section, "Roland command", header[6] == 0x12 ? "DT1 data set" : "0x" + header[6].ToString("X2", CultureInfo.InvariantCulture));
            }
            var names = FindReadableTextLines(header, 4, 40).Take(20).ToArray();
            if (names.Length > 0)
                Add(section, "Visible text", string.Join("\r\n", names));
        }

        private static int CountSysExMessages(byte[] data)
        {
            var count = 0;
            for (var i = 0; i < data.Length; i++)
                if (data[i] == 0xF0)
                    count++;
            return count;
        }

        private static string MidiManufacturerName(byte id)
        {
            switch (id)
            {
                case 0x41: return "Roland";
                case 0x42: return "Korg";
                case 0x43: return "Yamaha";
                case 0x44: return "Casio";
                case 0x47: return "Akai";
                default: return "Manufacturer";
            }
        }

        private static void AddRolandSvdInfo(List<ReportSection> sections, byte[] header)
        {
            var markerOffset = StartsWith(header, Encoding.ASCII.GetBytes("\0nSVD1")) ? 2 : IndexOfAscii(header, "SVD1");
            if (markerOffset < 0)
                return;

            var section = AddSection(sections, "Roland sound data");
            Add(section, "Format hint", "Roland SVD sound/backup data");
            Add(section, "Marker", "SVD1 at 0x" + markerOffset.ToString("X", CultureInfo.InvariantCulture));
            var chunks = new List<string>();
            for (var offset = 0x10; offset + 16 <= header.Length && chunks.Count < 20; offset += 16)
            {
                var id = ReadFixedAscii(header, offset, 4);
                if (!Regex.IsMatch(id, "^[A-Za-z0-9]{3,4}$", RegexOptions.CultureInvariant))
                    break;
                var family = ReadFixedAscii(header, offset + 4, 4);
                var chunkOffset = ReadUInt32BigEndian(header, offset + 8);
                var chunkSize = ReadUInt32BigEndian(header, offset + 12);
                chunks.Add(id + " " + family + " at 0x" + chunkOffset.ToString("X", CultureInfo.InvariantCulture) + ", " + FormatBytes(chunkSize));
            }
            if (chunks.Count > 0)
                Add(section, "Visible chunk table", string.Join("\r\n", chunks.ToArray()));
        }

        private static void AddRolandSvqInfo(List<ReportSection> sections, byte[] header)
        {
            if (!StartsWith(header, Encoding.ASCII.GetBytes("RSVQ")))
                return;

            var section = AddSection(sections, "Roland sequencer song");
            Add(section, "Format hint", "Roland SVQ sequencer song");
            Add(section, "Marker", "RSVQ");
            if (header.Length >= 0x50)
                Add(section, "Visible song name", ReadFixedAscii(header, 0x30, 32));
            if (header.Length >= 0x1C)
            {
                Add(section, "Raw version/flags", "0x" + ReadUInt32BigEndian(header, 4).ToString("X8", CultureInfo.InvariantCulture));
                Add(section, "Data offset-like field", "0x" + ReadUInt32BigEndian(header, 0x14).ToString("X", CultureInfo.InvariantCulture));
                Add(section, "Data size-like field", FormatBytes(ReadUInt32BigEndian(header, 0x18)));
            }
        }

        private static void AddRolandSampleInfo(List<ReportSection> sections, byte[] header)
        {
            if (!StartsWith(header, Encoding.ASCII.GetBytes("RFWV")))
                return;

            var section = AddSection(sections, "Roland sample data");
            Add(section, "Format hint", "Roland FA sample waveform data");
            Add(section, "Marker", "RFWV");
            if (header.Length >= 20)
            {
                Add(section, "Payload size-like field", FormatBytes(ReadUInt32BigEndian(header, 4)));
                Add(section, "Sample rate", ReadUInt32BigEndian(header, 8).ToString(CultureInfo.InvariantCulture) + " Hz");
                Add(section, "Channel count", ReadUInt32BigEndian(header, 12).ToString(CultureInfo.InvariantCulture));
                Add(section, "Bits/format-like field", ReadUInt32BigEndian(header, 16).ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void AddRolandJunoGLibrarianInfo(List<ReportSection> sections, byte[] header)
        {
            if (!StartsWith(header, Encoding.ASCII.GetBytes("JunoGLibrarianFile")))
                return;

            var section = AddSection(sections, "Roland sound data");
            Add(section, "Format hint", "Roland Juno-G Librarian file");
            Add(section, "Marker", "JunoGLibrarianFile");
            var names = FindReadableTextLines(header, 4, 40)
                .Where(line => !line.Equals("JunoGLibrarianFile0000", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(24)
                .ToArray();
            if (names.Length > 0)
                Add(section, "Visible patch or library names", string.Join("\r\n", names));
            Add(section, "Notes", "Juno-G Librarian files are Roland patch/library dumps. FileDentify reports visible names and markers only; it does not send SysEx or write data to hardware.");
        }

        private static bool IsRolandFantomLibrarian(string path, byte[] header)
        {
            var ext = Path.GetExtension(path);
            if (StartsWith(header, Encoding.ASCII.GetBytes("FantomXLibrarianFile")) ||
                StartsWith(header, Encoding.ASCII.GetBytes("FantomSLibrarianFile")))
                return true;
            return string.Equals(ext, ".fxl", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".fsl", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddRolandFantomLibrarianInfo(List<ReportSection> sections, string path, byte[] header)
        {
            if (!IsRolandFantomLibrarian(path, header))
                return;

            var marker = StartsWith(header, Encoding.ASCII.GetBytes("FantomSLibrarianFile"))
                ? "FantomSLibrarianFile"
                : StartsWith(header, Encoding.ASCII.GetBytes("FantomXLibrarianFile"))
                    ? "FantomXLibrarianFile"
                    : string.Empty;

            var ext = Path.GetExtension(path);
            var section = AddSection(sections, "Roland Fantom Librarian");
            Add(section, "Format hint", string.Equals(ext, ".fsl", StringComparison.OrdinalIgnoreCase) || marker.StartsWith("FantomS", StringComparison.OrdinalIgnoreCase)
                ? "Roland Fantom-S Librarian file"
                : "Roland Fantom-X Librarian file");
            if (!string.IsNullOrWhiteSpace(marker))
                Add(section, "Marker", marker);
            Add(section, "Likely target", string.Equals(ext, ".fsl", StringComparison.OrdinalIgnoreCase) || marker.StartsWith("FantomS", StringComparison.OrdinalIgnoreCase)
                ? "Fantom-S / Fantom-S88 patch library"
                : "Fantom-X / Fantom-XR patch library");
            Add(section, "File role", "Patch, performance, rhythm, arpeggio, or librarian-bank data for Roland Fantom hardware.");

            var names = FindReadableTextLines(header, 4, 40)
                .Select(line => line.Trim())
                .Where(LooksLikeRolandPatchName)
                .Select(CleanRolandPatchName)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(24)
                .ToArray();
            if (names.Length > 0)
                Add(section, "Visible patch or library names", string.Join("\r\n", names));
            Add(section, "Notes", "Roland Fantom Librarian files are proprietary hardware patch/library dumps. FileDentify reports visible names and markers only; it does not decode synth parameters, send SysEx, or write data to hardware.");
        }

        private static bool IsYamahaSoftSynthTable(string path, byte[] header)
        {
            if (StartsWith(header, Encoding.ASCII.GetBytes("UTG VPRM")))
                return true;

            var name = Path.GetFileName(path) ?? string.Empty;
            var ext = Path.GetExtension(path);
            if (!string.Equals(ext, ".tbl", StringComparison.OrdinalIgnoreCase))
                return false;

            var lower = path.ToLowerInvariant();
            return lower.IndexOf("yamaha", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (lower.IndexOf("s-yxg", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 lower.IndexOf("syxg", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.StartsWith("sxg", StringComparison.OrdinalIgnoreCase));
        }

        private static void AddYamahaSoftSynthInfo(List<ReportSection> sections, string path, byte[] header, byte[] stringSample, long fileLength)
        {
            if (!IsYamahaSoftSynthTable(path, header))
                return;

            var section = AddSection(sections, "Yamaha softsynth");
            Add(section, "Format hint", "Yamaha S-YXG software-synthesizer table");
            Add(section, "File role", YamahaSoftSynthRole(path, header));
            Add(section, "File size", FormatBytes(fileLength));
            if (StartsWith(header, Encoding.ASCII.GetBytes("UTG VPRM")))
                Add(section, "Header marker", ReadAsciiUntil(header, 0, 24));
            else if (IndexOfAscii(header, "XSCA") >= 0)
                Add(section, "Header marker", "XSCA near file start");

            var strings = FindAsciiStrings(stringSample, 4, 80)
                .Select(item => item.Value.Trim())
                .Where(value => value.Length > 0)
                .Where(value => value.IndexOf("Yamaha", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("S-YXG", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("XG", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("MIDI", StringComparison.OrdinalIgnoreCase) >= 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(16)
                .ToArray();
            if (strings.Length > 0)
                Add(section, "Visible strings", string.Join(Environment.NewLine, strings));
            Add(section, "Notes", "Yamaha S-YXG table files are support data for older Yamaha software MIDI synthesizers. FileDentify reports file role, visible markers, and safe string clues only; it does not decode wavetable payloads or load the synthesizer.");
        }

        private static string YamahaSoftSynthRole(string path, byte[] header)
        {
            var name = Path.GetFileName(path) ?? string.Empty;
            if (StartsWith(header, Encoding.ASCII.GetBytes("UTG VPRM")) || name.IndexOf("bnw", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Voice, program, or bank mapping table";
            if (name.IndexOf("dat", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Large wavetable/support data table";
            return "S-YXG software-synthesizer support table";
        }

        private static bool LooksLikeRolandPatchName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            if (value.IndexOf("LibrarianFile", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            if (value.IndexOf('\\') >= 0 || value.IndexOf('%') >= 0)
                return false;
            if (value.Count(ch => ch == '@') > value.Length / 2)
                return false;
            var useful = value.Count(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) || "-_+'/&().".IndexOf(ch) >= 0);
            var lettersOrDigits = value.Count(char.IsLetterOrDigit);
            return lettersOrDigits >= 4 && value.Any(char.IsLetterOrDigit) && useful >= Math.Max(3, value.Length - 2);
        }

        private static string CleanRolandPatchName(string value)
        {
            var text = (value ?? string.Empty).Trim();
            if (text.Length > 4 && text[0] == 'O' && char.IsLetterOrDigit(text[1]))
                text = text.Substring(1).Trim();
            return text;
        }

        private static bool IsOpenAlSpatialAudio(string path, byte[] header)
        {
            var ext = Path.GetExtension(path);
            return string.Equals(ext, ".mhr", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".ambdec", StringComparison.OrdinalIgnoreCase) ||
                StartsWith(header, Encoding.ASCII.GetBytes("MinPHR"));
        }

        private static string OpenAlSpatialAudioTypeName(string path)
        {
            var ext = Path.GetExtension(path);
            if (string.Equals(ext, ".mhr", StringComparison.OrdinalIgnoreCase))
                return "OpenAL Soft HRTF data";
            if (string.Equals(ext, ".ambdec", StringComparison.OrdinalIgnoreCase))
                return "Ambisonic decoder configuration";
            return "OpenAL spatial audio data";
        }

        private static void AddOpenAlSpatialAudioInfo(List<ReportSection> sections, string path, byte[] header)
        {
            if (!IsOpenAlSpatialAudio(path, header))
                return;

            var ext = Path.GetExtension(path);
            var section = AddSection(sections, "OpenAL spatial audio");
            Add(section, "Format hint", OpenAlSpatialAudioTypeName(path));
            if (StartsWith(header, Encoding.ASCII.GetBytes("MinPHR")))
            {
                Add(section, "Header marker", ReadAsciiUntil(header, 0, 16));
                Add(section, "Common use", "Head-related transfer function data used by OpenAL Soft and games/tools that provide headphone spatialisation.");
                var sampleRate = OpenAlSampleRateFromName(path);
                if (!string.IsNullOrWhiteSpace(sampleRate))
                    Add(section, "Filename sample rate", sampleRate);
            }
            else if (string.Equals(ext, ".ambdec", StringComparison.OrdinalIgnoreCase))
            {
                var text = DecodeTextSample(header);
                Add(section, "Common use", "Ambisonic speaker decoder preset used by OpenAL Soft.");
                AddAmbdecField(section, text, "/description", "Description");
                AddAmbdecField(section, text, "/version", "Version");
                AddAmbdecField(section, text, "/dec/chan_mask", "Channel mask");
                Add(section, "Text format", "Line-oriented AmbDec configuration");
            }
            Add(section, "Notes", "Spatial audio support is identification-level. FileDentify reports header and configuration clues; it does not render HRTF filters or validate decoder matrices.");
        }

        private static string OpenAlSampleRateFromName(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
            var match = Regex.Match(name, @"(?<!\d)(?<rate>44(?:100)?|48(?:000)?)(?!\d)", RegexOptions.CultureInvariant);
            if (!match.Success)
                return string.Empty;
            var value = match.Groups["rate"].Value;
            if (value == "44")
                value = "44100";
            if (value == "48")
                value = "48000";
            return value + " Hz";
        }

        private static void AddAmbdecField(ReportSection section, string text, string key, string label)
        {
            var match = Regex.Match(text ?? string.Empty, @"(?im)^\s*" + Regex.Escape(key) + @"\s+(?<value>.+?)\s*$");
            if (match.Success)
                Add(section, label, match.Groups["value"].Value.Trim());
        }

        private static string ProductionAudioResourceTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (StartsWith(header, Encoding.ASCII.GetBytes("FORM")) && header.Length >= 12 && Encoding.ASCII.GetString(header, 8, 4) == "PTCH")
                return "Reason NN-XT sampler patch";
            if (StartsWith(header, Encoding.ASCII.GetBytes("RIFF")) && header.Length >= 12)
            {
                var form = Encoding.ASCII.GetString(header, 8, 4);
                if (form == "APRG") return "Akai sampler program";
                if (form == "AMUL") return "Akai sampler multi";
                if (form == "SEKD") return "MAGIX/SEK'D sample-library metadata";
            }
            if (StartsWith(header, Encoding.ASCII.GetBytes("CAT ")) && header.Length >= 12 && Encoding.ASCII.GetString(header, 8, 4) == "PRBM")
                return "Propellerhead ReBirth mod";
            if (StartsWith(header, Encoding.ASCII.GetBytes("gchT")))
                return "GarageBand chord/tuning table";
            if (StartsWith(header, Encoding.ASCII.GetBytes("GNOA")))
                return "GN Audio container with embedded MIDI";
            switch (ext)
            {
                case ".sxt": return "Reason NN-XT sampler patch";
                case ".lso": return "Emagic Logic song/project";
                case ".chtr": return "GarageBand chord/tuning table";
                case ".nac": return "Native Instruments sample-add data";
                case ".nov": return "Native Instruments sample-add instrument data";
                case ".h0": return "MAGIX/SEK'D waveform overview";
                case ".hdp": return "MAGIX/SEK'D sample-library metadata";
                case ".ovm": return "MAGIX object/volume metadata";
                case ".akp": return "Akai sampler program";
                case ".akm": return "Akai sampler multi";
                case ".rbm": return "Propellerhead ReBirth mod";
                case ".mdd": return "GN Audio container with embedded MIDI";
                default: return null;
            }
        }

        private static void AddProductionAudioResourceInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            var type = ProductionAudioResourceTypeName(path, header);
            if (type == null)
                return;

            var ext = Path.GetExtension(path).ToLowerInvariant();
            var section = AddSection(sections, "Production audio resource");
            Add(section, "Format hint", type);
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "Role", ProductionAudioResourceRole(path));

            if (StartsWith(header, Encoding.ASCII.GetBytes("FORM")) && header.Length >= 12)
            {
                Add(section, "Container", "IFF/FORM");
                Add(section, "FORM type", Encoding.ASCII.GetString(header, 8, 4));
            }
            else if (StartsWith(header, Encoding.ASCII.GetBytes("RIFF")) && header.Length >= 12)
            {
                Add(section, "Container", "RIFF");
                Add(section, "RIFF form", Encoding.ASCII.GetString(header, 8, 4));
            }
            else if (StartsWith(header, Encoding.ASCII.GetBytes("CAT ")) && header.Length >= 12)
            {
                Add(section, "Container", "IFF/CAT");
                Add(section, "CAT type", Encoding.ASCII.GetString(header, 8, 4));
            }
            else if (StartsWith(header, Encoding.ASCII.GetBytes("gchT")))
            {
                Add(section, "Header marker", "gchT");
                var utf16 = FindUtf16Strings(sample, 4, 12, true).Select(s => s.Value).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToArray();
                if (utf16.Length > 0)
                    Add(section, "Visible Unicode strings", string.Join(Environment.NewLine, utf16));
            }
            else if (StartsWith(header, Encoding.ASCII.GetBytes("GNOA")))
            {
                Add(section, "Header marker", "GNOA");
                if (IndexOfAscii(sample, "MThd") >= 0)
                    Add(section, "Embedded MIDI", "MThd marker found in sampled bytes.");
            }

            if (ext == ".sxt" || ext == ".akp" || ext == ".akm" || ext == ".rbm" || ext == ".mdd")
            {
                var sizeField = HeaderSizeField(header);
                if (!string.IsNullOrWhiteSpace(sizeField))
                    Add(section, "Header size field", sizeField);
            }

            var strings = FindAsciiStrings(sample, 4, 80)
                .Select(s => s.Value.Trim())
                .Where(value => value.Length > 0)
                .Where(value => value.Any(char.IsLetterOrDigit))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToArray();
            if (strings.Length > 0)
                Add(section, "Visible strings", string.Join(Environment.NewLine, strings));

            Add(section, "Notes", "These are production-audio support files from DAWs, samplers, sample CDs, or bundled music tools. FileDentify reports container markers, file roles, visible sample/project strings, and embedded MIDI clues only; it does not load instruments or reconstruct projects.");
        }

        private static string ProductionAudioResourceRole(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".sxt": return "Reason NN-XT sampler patch, usually referencing external WAV samples.";
                case ".lso": return "Classic Logic/Emagic song or loop project file.";
                case ".chtr": return "GarageBand Smart Guitar chord/tuning table.";
                case ".nac": return "Native Instruments sample-add analysis/support data.";
                case ".nov": return "Native Instruments sample-add instrument or voice-support data.";
                case ".h0": return "MAGIX/SEK'D waveform overview or analysis sidecar.";
                case ".hdp": return "MAGIX/SEK'D RIFF metadata sidecar.";
                case ".ovm": return "MAGIX object or volume/envelope metadata sidecar.";
                case ".akp": return "Akai sampler program/preset data.";
                case ".akm": return "Akai sampler multi data.";
                case ".rbm": return "Propellerhead ReBirth mod/package.";
                case ".mdd": return "GN Audio container, often accompanying WAV assets and capable of carrying MIDI data.";
                default: return "Production-audio support data.";
            }
        }

        private static string HeaderSizeField(byte[] header)
        {
            if (header.Length < 8)
                return string.Empty;
            if (StartsWith(header, Encoding.ASCII.GetBytes("FORM")) || StartsWith(header, Encoding.ASCII.GetBytes("CAT ")))
                return FormatBytes(ReadUInt32BigEndian(header, 4));
            if (StartsWith(header, Encoding.ASCII.GetBytes("RIFF")))
                return FormatBytes(ReadUInt32LittleEndian(header, 4));
            return string.Empty;
        }

        private static void AddJsonString(ReportSection section, Dictionary<string, object> root, string key, string label)
        {
            object value;
            if (root.TryGetValue(key, out value) && value != null)
            {
                var text = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(text))
                    Add(section, label, text);
            }
        }

        private static void AddJsonNumber(ReportSection section, Dictionary<string, object> root, string key, string label)
        {
            object value;
            if (root.TryGetValue(key, out value) && value != null)
            {
                var text = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(text))
                    Add(section, label, text);
            }
        }

        private static string GetJsonString(Dictionary<string, object> root, string key)
        {
            object value;
            if (root == null || !root.TryGetValue(key, out value) || value == null)
                return string.Empty;
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static List<object> GetList(Dictionary<string, object> root, string key)
        {
            object value;
            if (root == null || !root.TryGetValue(key, out value))
                return null;
            return value as List<object>;
        }

        private static Dictionary<string, object> GetDictionary(Dictionary<string, object> root, string key)
        {
            object value;
            if (root == null || !root.TryGetValue(key, out value))
                return null;
            return value as Dictionary<string, object>;
        }

        private static Dictionary<string, object> ParseJsonObject(byte[] header, int maxBytes)
        {
            if (!LooksLikeJson(header))
                return null;
            var text = Encoding.UTF8.GetString(header.Take(Math.Min(header.Length, maxBytes)).ToArray()).Trim('\uFEFF', '\0', ' ', '\r', '\n', '\t');
            try
            {
                var serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = Math.Max(maxBytes, 1024 * 1024);
                return serializer.DeserializeObject(text) as Dictionary<string, object>;
            }
            catch
            {
                return null;
            }
        }

        private static void AddInterestingJsonSettings(ReportSection section, Dictionary<string, object> settings, params string[] keys)
        {
            var values = new List<string>();
            foreach (var key in keys)
            {
                object value;
                if (settings.TryGetValue(key, out value) && value != null)
                    values.Add(key + ": " + Convert.ToString(value, CultureInfo.InvariantCulture));
            }
            if (values.Count > 0)
                Add(section, "Selected settings", string.Join("\r\n", values.ToArray()));
        }

        private static Dictionary<string, string> ParseSimpleKeyValueText(byte[] header)
        {
            var text = DecodeTextSample(header);
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var index = line.IndexOf(':');
                if (index <= 0)
                    continue;
                var key = line.Substring(0, index).Trim();
                var value = line.Substring(index + 1).Trim();
                if (key.Length > 0 && !fields.ContainsKey(key))
                    fields[key] = value;
            }
            return fields;
        }

        private static void AddSimpleField(ReportSection section, Dictionary<string, string> fields, string key, string label)
        {
            string value;
            if (fields.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value))
                Add(section, label, value);
        }

        private static void AddZipEntrySummary(ReportSection section, string path, string[] interestingExtensions)
        {
            try
            {
                using (var archive = ZipFile.OpenRead(path))
                {
                    Add(section, "Entries", archive.Entries.Count.ToString(CultureInfo.InvariantCulture));
                    var totalUncompressed = archive.Entries.Sum(e => e.Length);
                    Add(section, "Uncompressed total", FormatBytes(totalUncompressed) + " (" + totalUncompressed.ToString(CultureInfo.InvariantCulture) + " bytes)");
                    var extensionCounts = archive.Entries
                        .Where(e => !string.IsNullOrEmpty(e.Name))
                        .GroupBy(e => Path.GetExtension(e.Name).ToLowerInvariant())
                        .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                        .OrderByDescending(g => g.Count())
                        .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                        .Take(12)
                        .Select(g => g.Key + " " + g.Count().ToString(CultureInfo.InvariantCulture))
                        .ToArray();
                    if (extensionCounts.Length > 0)
                        Add(section, "Entry extensions", string.Join("\r\n", extensionCounts));

                    var interesting = archive.Entries
                        .Where(e => interestingExtensions.Contains(Path.GetExtension(e.Name), StringComparer.OrdinalIgnoreCase))
                        .Take(20)
                        .Select(e => e.FullName + " (" + FormatBytes(e.Length) + ")")
                        .ToArray();
                    if (interesting.Length > 0)
                        Add(section, "Interesting entries", string.Join("\r\n", interesting));
                }
            }
            catch (Exception ex)
            {
                Add(section, "ZIP read note", ex.Message);
            }
        }

        private static void AddLhaInfo(List<ReportSection> sections, string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var method = LhaMethod(header);
            if (method == null && ext != ".lha" && ext != ".lzh")
                return;

            var section = AddSection(sections, "LHA archive");
            Add(section, "Format hint", "LHA/LZH archive, common on Amiga, Japanese DOS/Windows, and old module collections");
            if (method != null)
                Add(section, "Compression method", method);
            if (header.Length > 21)
            {
                var nameLength = header[21];
                if (nameLength > 0 && 22 + nameLength <= header.Length)
                    Add(section, "First entry name", CleanMetadataText(Encoding.GetEncoding(28591).GetString(header, 22, nameLength)));
            }
            Add(section, "Notes", "Header-level LHA parsing only; FileDentify does not extract archive contents.");
        }

        private static string LhaMethod(byte[] header)
        {
            if (header.Length >= 7 && header[2] == (byte)'-' && header[6] == (byte)'-')
            {
                var method = Encoding.ASCII.GetString(header, 2, 5);
                if (Regex.IsMatch(method, "^-l[hzo][0-9d]-$", RegexOptions.IgnoreCase))
                    return method;
            }
            if (header.Length >= 8 && header[3] == (byte)'-' && header[7] == (byte)'-')
            {
                var method = Encoding.ASCII.GetString(header, 3, 5);
                if (Regex.IsMatch(method, "^-l[hzo][0-9d]-$", RegexOptions.IgnoreCase))
                    return method;
            }
            return null;
        }

        private static void AddTrackerModuleInfo(List<ReportSection> sections, string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var type = TrackerModuleType(ext, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Tracker module");
            Add(section, "Format hint", type);
            if (header.Length >= 20)
                Add(section, "Title", CleanMetadataText(Encoding.GetEncoding(28591).GetString(header, 0, 20).TrimEnd('\0', ' ')));

            if (IsProtrackerMod(header))
            {
                Add(section, "Signature", Encoding.ASCII.GetString(header, 1080, 4));
                Add(section, "Sample slots", "31");
                if (header.Length > 951)
                {
                    Add(section, "Song length", header[950].ToString(CultureInfo.InvariantCulture) + " patterns");
                    Add(section, "Restart byte", header[951].ToString(CultureInfo.InvariantCulture));
                }
            }
            else if (StartsWith(header, Encoding.ASCII.GetBytes("Extended Module: ")))
            {
                Add(section, "Signature", "Extended Module");
                if (header.Length > 37)
                    Add(section, "Tracker name", ReadNullTerminatedText(header, 38, 20));
            }
            else if (header.Length >= 48 && Encoding.ASCII.GetString(header, 44, 4) == "SCRM")
            {
                Add(section, "Signature", "SCRM");
                Add(section, "Initial speed", header.Length > 49 ? header[49].ToString(CultureInfo.InvariantCulture) : string.Empty);
                Add(section, "Initial tempo", header.Length > 50 ? header[50].ToString(CultureInfo.InvariantCulture) : string.Empty);
            }
            else if (StartsWith(header, Encoding.ASCII.GetBytes("IMPM")))
            {
                Add(section, "Signature", "IMPM");
                if (header.Length >= 0x30)
                    Add(section, "Order count", ReadUInt16LittleEndian(header, 0x20).ToString(CultureInfo.InvariantCulture));
            }
        }

        private static string TrackerModuleType(string ext, byte[] header)
        {
            if (IsProtrackerMod(header)) return "ProTracker/Amiga MOD module";
            if (StartsWith(header, Encoding.ASCII.GetBytes("Extended Module: "))) return "FastTracker XM module";
            if (header.Length >= 48 && Encoding.ASCII.GetString(header, 44, 4) == "SCRM") return "Scream Tracker 3 module";
            if (StartsWith(header, Encoding.ASCII.GetBytes("IMPM"))) return ext == ".mptm" ? "OpenMPT module" : "Impulse Tracker module";
            switch (ext)
            {
                case ".mod": return "Tracker module, likely ProTracker/Amiga MOD";
                case ".xm": return "FastTracker XM module";
                case ".s3m": return "Scream Tracker 3 module";
                case ".it": return "Impulse Tracker module";
                case ".mptm": return "OpenMPT module";
                default: return null;
            }
        }

        private static bool IsProtrackerMod(byte[] header)
        {
            if (header.Length < 1084)
                return false;
            var sig = Encoding.ASCII.GetString(header, 1080, 4);
            return sig == "M.K." || sig == "M!K!" || sig == "M&K!" || sig == "N.T." || sig == "FLT4" || sig == "FLT8" ||
                Regex.IsMatch(sig, "^[0-9][0-9]CH$") || Regex.IsMatch(sig, "^[0-9]CHN$");
        }

        private static void AddMoggInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
        {
            if (!string.Equals(Path.GetExtension(path), ".mogg", StringComparison.OrdinalIgnoreCase))
                return;

            var section = AddSection(sections, "MOGG multitrack audio");
            Add(section, "Format hint", "Multi-channel Ogg container used by some rhythm-game and multitrack workflows");
            var oggOffset = IndexOfAscii(header, "OggS", 0);
            if (oggOffset >= 0)
            {
                Add(section, "Ogg payload offset", "0x" + oggOffset.ToString("X", CultureInfo.InvariantCulture));
                Add(section, "Prefix size", FormatBytes(oggOffset));
            }
            else
                Add(section, "Ogg payload", "Not found in first sample");
            if (header.Length >= 4)
                Add(section, "First table value", ReadUInt32LittleEndian(header, 0).ToString(CultureInfo.InvariantCulture));
            Add(section, "File size", FormatBytes(fileLength) + " (" + fileLength.ToString(CultureInfo.InvariantCulture) + " bytes)");
        }

        private static void AddSfArkInfo(List<ReportSection> sections, string path, byte[] header)
        {
            if (!string.Equals(Path.GetExtension(path), ".sfArk", StringComparison.OrdinalIgnoreCase))
                return;

            var section = AddSection(sections, "sfArk SoundFont archive");
            Add(section, "Format hint", "sfArk compressed SoundFont archive");
            var strings = FindAsciiStrings(header, 4, 80).Select(s => s.Value).ToList();
            var version = strings.FirstOrDefault(s => s.IndexOf("sfArk", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrWhiteSpace(version))
                Add(section, "Visible version string", version);
            var target = strings.FirstOrDefault(s => s.IndexOf(".sf2", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrWhiteSpace(target))
                Add(section, "Target SoundFont", target);
            Add(section, "Notes", "sfArk is a legacy compressed SoundFont format. FileDentify identifies the container and visible target name but does not decompress it.");
        }

        private static void AddCakewalkInfo(List<ReportSection> sections, string path, byte[] sample)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var strings = FindReadableTextLines(sample, 3, 600);
            var hasCakewalk = strings.Any(s =>
                s.IndexOf("CAKEWALK", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Cakewalk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("SONAR", StringComparison.OrdinalIgnoreCase) >= 0);
            if (ext != ".wrk" && ext != ".cwp" && !hasCakewalk)
                return;

            var section = AddSection(sections, "Cakewalk project");
            Add(section, "Format hint", ext == ".wrk" ? "Classic Cakewalk WRK project" : ext == ".cwp" ? "Cakewalk/Sonar CWP project" : "Cakewalk/Sonar-readable markers found");
            Add(section, "Detection basis", ext == ".wrk" || ext == ".cwp" ? "Known Cakewalk extension plus sampled readable strings." : "Cakewalk/Sonar readable strings found in sampled data.");
            AddCategory(section, "Cakewalk/Sonar markers", strings, "CAKEWALK", "Cakewalk", "SONAR");
            AddCategory(section, "Driver or audio system strings", strings, "ASIO", "DirectX", "MME", "MIDI", "VST");
            var versions = strings
                .Where(s => Regex.IsMatch(s, @"\b\d{1,3}\.\d{1,3}(\.\d{1,5})?\b", RegexOptions.CultureInvariant))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .ToArray();
            if (versions.Length > 0)
                Add(section, "Visible version strings", string.Join("\r\n", versions));
            var references = strings
                .Where(s => s.IndexOf(".wav", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.IndexOf(".aif", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.IndexOf(".flac", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.IndexOf(".sf2", StringComparison.OrdinalIgnoreCase) >= 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(30)
                .ToArray();
            if (references.Length > 0)
                Add(section, "Visible sample or SoundFont references", string.Join("\r\n", references));
            Add(section, "Notes", "Cakewalk project formats are proprietary. FileDentify reports extension-level identity and readable project clues, not a full arrangement parse.");
        }

        private static bool IsReaperProject(byte[] sample)
        {
            if (sample == null || sample.Length == 0 || !LooksLikeText(sample))
                return false;
            var text = DecodeTextSample(sample.Take(Math.Min(sample.Length, 4096)).ToArray()).TrimStart('\uFEFF', ' ', '\r', '\n', '\t');
            return text.StartsWith("<REAPER_PROJECT", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddReaperProjectInfo(List<ReportSection> sections, string path, byte[] sample, long fileLength)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".rpp" && ext != ".rpp-bak")
                return;
            if (!IsReaperProject(sample))
                return;

            var text = DecodeTextSample(sample);
            var lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            var section = AddSection(sections, "REAPER project");
            Add(section, "Format hint", ext == ".rpp-bak" ? "REAPER project backup" : "REAPER project");
            Add(section, "Sampled size", FormatBytes(sample.Length) + (sample.Length < fileLength ? " of " + FormatBytes(fileLength) : string.Empty));

            var header = lines.FirstOrDefault(line => line.TrimStart().StartsWith("<REAPER_PROJECT", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(header))
            {
                Add(section, "Header", header.Trim());
                var version = Regex.Match(header, "\"(?<version>[^\"]+)\"");
                if (version.Success)
                    Add(section, "Project version", version.Groups["version"].Value);
                var timestamp = Regex.Match(header, "\\s(?<stamp>\\d{9,11})\\s*$");
                if (timestamp.Success)
                {
                    Add(section, "Project timestamp", timestamp.Groups["stamp"].Value);
                    long unix;
                    if (long.TryParse(timestamp.Groups["stamp"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out unix))
                    {
                        try
                        {
                            var date = new DateTime(1970, 1, 1).AddSeconds(unix);
                            if (date.Year >= 1990 && date.Year <= 2100)
                                Add(section, "Project timestamp UTC", date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                        }
                        catch (ArgumentOutOfRangeException) { }
                    }
                }
            }

            AddReaperTempoInfo(section, lines);
            Add(section, "Tracks in sample", CountReaperBlocks(lines, "<TRACK").ToString(CultureInfo.InvariantCulture));
            Add(section, "Items in sample", CountReaperBlocks(lines, "<ITEM").ToString(CultureInfo.InvariantCulture));
            Add(section, "Takes in sample", CountReaperBlocks(lines, "<TAKE").ToString(CultureInfo.InvariantCulture));

            var render = FirstReaperLine(lines, "RENDER_FILE");
            if (!string.IsNullOrWhiteSpace(render))
                Add(section, "Render target", ExtractFirstQuoted(render));

            var pluginLines = lines
                .Select(line => line.Trim())
                .Where(line => line.StartsWith("<VST", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("<AU", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("<JS", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("<CLAP", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (pluginLines.Length > 0)
            {
                Add(section, "Plug-ins found in sample", pluginLines.Length.ToString(CultureInfo.InvariantCulture));
                var preview = pluginLines.Select(ExtractFirstQuoted).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).Take(24).ToArray();
                if (preview.Length > 0)
                    Add(section, "Plug-in preview", string.Join(Environment.NewLine, preview));
            }

            var media = lines
                .Select(line => line.Trim())
                .Where(line => line.StartsWith("FILE ", StringComparison.OrdinalIgnoreCase))
                .Select(ExtractFirstQuoted)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(40)
                .ToArray();
            if (media.Length > 0)
            {
                Add(section, "Media references found in sample", media.Length.ToString(CultureInfo.InvariantCulture));
                Add(section, "Media reference preview", string.Join(Environment.NewLine, media));
            }

            Add(section, "Notes", "REAPER RPP files are text project files. FileDentify reports bounded project structure, plug-in markers, and visible media references; it does not load or validate the arrangement.");
        }

        private static void AddReaperThemeInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
        {
            if (!Path.GetExtension(path).Equals(".reaperthemezip", StringComparison.OrdinalIgnoreCase))
                return;

            var section = AddSection(sections, "REAPER project");
            Add(section, "Format hint", "REAPER theme package");
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "Container", IsZipHeader(header) ? "ZIP-compatible theme package" : "Theme package by extension");

            if (IsZipHeader(header))
            {
                try
                {
                    using (var archive = ZipFile.OpenRead(path))
                    {
                        Add(section, "Archive entries", archive.Entries.Count.ToString(CultureInfo.InvariantCulture));
                        var entries = archive.Entries
                            .Select(entry => entry.FullName)
                            .Where(name => !string.IsNullOrWhiteSpace(name))
                            .Take(20)
                            .ToArray();
                        if (entries.Length > 0)
                            Add(section, "First entries", string.Join(Environment.NewLine, entries));
                    }
                }
                catch (Exception ex)
                {
                    Add(section, "Archive read note", ex.Message);
                }
            }

            Add(section, "Notes", "REAPER theme ZIP files package UI themes, images, and configuration resources for REAPER. FileDentify reports package structure without installing or applying the theme.");
        }

        private static void AddReaperTempoInfo(ReportSection section, IEnumerable<string> lines)
        {
            var tempo = FirstReaperLine(lines, "TEMPO");
            if (!string.IsNullOrWhiteSpace(tempo))
            {
                var parts = tempo.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                    Add(section, "Tempo", parts[1] + " BPM, " + parts[2] + "/" + parts[3]);
                else
                    Add(section, "Tempo", tempo);
            }

            var sampleRate = FirstReaperLine(lines, "SAMPLERATE");
            if (!string.IsNullOrWhiteSpace(sampleRate))
            {
                var parts = sampleRate.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && parts[1] != "0")
                    Add(section, "Sample rate", parts[1] + " Hz");
            }
        }

        private static string FirstReaperLine(IEnumerable<string> lines, string key)
        {
            return lines.Select(line => line.Trim()).FirstOrDefault(line => line.StartsWith(key + " ", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        }

        private static int CountReaperBlocks(IEnumerable<string> lines, string marker)
        {
            return lines.Count(line => line.TrimStart().StartsWith(marker, StringComparison.OrdinalIgnoreCase));
        }

        private static string ExtractFirstQuoted(string line)
        {
            var match = Regex.Match(line ?? string.Empty, "\"(?<value>[^\"]*)\"");
            return match.Success ? CleanMetadataText(match.Groups["value"].Value) : CleanMetadataText(line ?? string.Empty);
        }

        private static void AddSamplerInstrumentInfo(List<ReportSection> sections, string path, byte[] sample)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".sfz" && ext != ".exs")
                return;

            var text = DecodeTextSample(sample);
            var section = AddSection(sections, "Sampler instrument");
            Add(section, "Format hint", ext == ".sfz" ? "SFZ text sampler instrument" : "Apple Logic EXS sampler instrument");
            if (ext == ".sfz")
            {
                Add(section, "Region count", Regex.Matches(text, "<region>", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
                Add(section, "Group count", Regex.Matches(text, "<group>", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
                Add(section, "Control count", Regex.Matches(text, "<control>", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
                var samples = Regex.Matches(text, @"sample\s*=\s*([^\r\n]+)", RegexOptions.IgnoreCase)
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value.Trim())
                    .Where(v => v.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(20)
                    .ToArray();
                if (samples.Length > 0)
                    Add(section, "Referenced samples", string.Join("\r\n", samples));
            }
            else
            {
                Add(section, "Readable marker count", FindAsciiStrings(sample, 4, 80).Count.ToString(CultureInfo.InvariantCulture));
                var hints = FindAsciiStrings(sample, 4, 60)
                    .Select(s => s.Value)
                    .Where(v => v.IndexOf(".wav", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        v.IndexOf(".aif", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        v.IndexOf("EXS", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(20)
                    .ToArray();
                if (hints.Length > 0)
                    Add(section, "Visible sample or EXS strings", string.Join("\r\n", hints));
            }
        }

        private static string AudioSampleResourceTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".arta": return "Arturia sample payload";
                case ".astr": return "Arturia bitmap/UI resource";
                case ".eiiwav": return "Arturia Emulator II V sample audio";
                case ".roliaudio": return "ROLI Equator sample audio";
                case ".ignitex": return "Initial Audio Sektor sample data";
                case ".grir": return "Native Instruments Guitar Rig impulse response";
                case ".sdir": return "Apple Space Designer impulse response";
                case ".caf": return StartsWith(header, Encoding.ASCII.GetBytes("caff")) ? "Core Audio Format audio" : "Core Audio Format audio";
                case ".scl": return "Scala tuning scale";
                case ".wt": return StartsWith(header, Encoding.ASCII.GetBytes("vawt")) ? "Surge wavetable" : "Wavetable data";
                default: return null;
            }
        }

        private static void AddAudioSampleResourceInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            var type = AudioSampleResourceTypeName(path, header);
            if (type == null)
                return;

            var ext = Path.GetExtension(path).ToLowerInvariant();
            var section = AddSection(sections, "Audio sample resource");
            Add(section, "Format hint", type);
            Add(section, "Product or vendor", AudioResourceVendor(path, ext));
            Add(section, "Library folder", AudioResourceLibrary(path));
            Add(section, "Role", AudioResourceRole(path, ext));
            Add(section, "File size", FormatBytes(fileLength));

            if (ext == ".caf")
                AddCoreAudioFormatInfo(section, header);
            else if (ext == ".scl")
                AddScalaScaleInfo(section, sample);
            else if (ext == ".wt")
                AddWavetableResourceInfo(section, header, sample);
            else
                AddAudioResourceVisibleStrings(section, sample);

            Add(section, "Notes", "These audio-library resource files are reported from extension, folder context, headers, and visible metadata. FileDentify does not decode proprietary sample payloads or load plug-ins.");
        }

        private static void AddCoreAudioFormatInfo(ReportSection section, byte[] header)
        {
            if (!StartsWith(header, Encoding.ASCII.GetBytes("caff")))
            {
                Add(section, "Header marker", "Not present in sampled header; reported from .caf extension.");
                return;
            }

            Add(section, "Header marker", "caff");
            if (header.Length >= 8)
            {
                Add(section, "CAF version", ReadUInt16BigEndian(header, 4).ToString(CultureInfo.InvariantCulture));
                Add(section, "CAF flags", "0x" + ReadUInt16BigEndian(header, 6).ToString("X4", CultureInfo.InvariantCulture));
            }

            var chunks = new List<string>();
            var offset = 8;
            while (offset + 12 <= header.Length && chunks.Count < 12)
            {
                var id = Encoding.ASCII.GetString(header, offset, 4);
                if (!Regex.IsMatch(id, "^[A-Za-z0-9_ ]{4}$"))
                    break;
                var size = ReadUInt64BigEndian(header, offset + 4);
                chunks.Add(id.Trim() + " (" + (size == ulong.MaxValue ? "variable size" : FormatUnsignedBytes(size)) + ")");
                if (size == ulong.MaxValue || size > int.MaxValue)
                    break;
                var next = offset + 12 + (int)size;
                if (next <= offset)
                    break;
                offset = next;
            }
            if (chunks.Count > 0)
                Add(section, "CAF chunks", string.Join(Environment.NewLine, chunks.ToArray()));
        }

        private static void AddScalaScaleInfo(ReportSection section, byte[] sample)
        {
            var text = DecodeTextSample(sample);
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(line => CleanMetadataText(line.Trim()))
                .Where(line => line.Length > 0 && !line.StartsWith("!", StringComparison.Ordinal))
                .Take(12)
                .ToArray();
            if (lines.Length > 0)
                Add(section, "Scale text", string.Join(Environment.NewLine, lines));

            var firstNumber = lines.FirstOrDefault(line => Regex.IsMatch(line, "^\\d+\\s*$"));
            if (!string.IsNullOrWhiteSpace(firstNumber))
                Add(section, "Declared note count", firstNumber);
        }

        private static void AddWavetableResourceInfo(ReportSection section, byte[] header, byte[] sample)
        {
            if (StartsWith(header, Encoding.ASCII.GetBytes("vawt")))
                Add(section, "Header marker", "vawt");
            var names = FindReadableTextLines(sample, 4, 80)
                .Where(line => line.IndexOf(".wav", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("wavetable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("wave", StringComparison.OrdinalIgnoreCase) >= 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(16)
                .ToArray();
            if (names.Length > 0)
                Add(section, "Visible wavetable strings", string.Join(Environment.NewLine, names));
        }

        private static void AddAudioResourceVisibleStrings(ReportSection section, byte[] sample)
        {
            var visible = FindReadableTextLines(sample, 4, 100)
                .Where(IsUsefulAudioResourceString)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(18)
                .ToArray();
            if (visible.Length > 0)
                Add(section, "Visible strings", string.Join(Environment.NewLine, visible));
        }

        private static bool IsUsefulAudioResourceString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            return value.IndexOf(".wav", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf(".aif", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf(".flac", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("sample", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("impulse", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("cabinet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("wavetable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Arturia", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("ROLI", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string AudioResourceVendor(string path, string ext)
        {
            foreach (var vendor in new[] { "Arturia", "ROLI", "Initial Audio", "Native Instruments", "Apple", "Sonic Charge", "KV331 Audio" })
                if (path.IndexOf(vendor, StringComparison.OrdinalIgnoreCase) >= 0)
                    return vendor;
            switch (ext)
            {
                case ".caf":
                case ".sdir": return "Apple";
                case ".scl": return "Scala tuning format";
                default: return ParentName(path);
            }
        }

        private static string AudioResourceLibrary(string path)
        {
            var vendorProduct = AudioResourceProductAfterVendor(path);
            if (!string.IsNullOrWhiteSpace(vendorProduct))
                return vendorProduct;

            foreach (var segment in new[] { "Samples", "resources", "Resources", "Expansions", "Content", "Audio", "Impulse Responses" })
            {
                var value = SegmentAfter(path, segment);
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
            return ParentName(path);
        }

        private static string AudioResourceProductAfterVendor(string path)
        {
            var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (var vendor in new[] { "Arturia", "ROLI", "Initial Audio", "Native Instruments", "Sonic Charge", "KV331 Audio" })
            {
                for (var i = 0; i < parts.Length; i++)
                {
                    if (!parts[i].Equals(vendor, StringComparison.OrdinalIgnoreCase))
                        continue;
                    for (var j = i + 1; j < parts.Length; j++)
                    {
                        if (IsGenericAudioResourceSegment(parts[j]))
                            continue;
                        return parts[j];
                    }
                }
            }
            return string.Empty;
        }

        private static bool IsGenericAudioResourceSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;
            foreach (var generic in new[] { "Samples", "resources", "Resources", "Factory", "Content", "Presets", "Third Party", "Native Instruments", "Multisamples", "instruments", "internal_presets", "wt", "scl" })
                if (value.Equals(generic, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static string AudioResourceRole(string path, string ext)
        {
            if (path.IndexOf("Impulse Responses", StringComparison.OrdinalIgnoreCase) >= 0 || ext == ".sdir" || ext == ".grir")
                return "Impulse response or cabinet response";
            if (path.IndexOf("Wavetables", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("\\wt\\", StringComparison.OrdinalIgnoreCase) >= 0 || ext == ".wt")
                return "Wavetable";
            if (path.IndexOf("bitmap", StringComparison.OrdinalIgnoreCase) >= 0 || ext == ".astr")
                return "UI bitmap/resource";
            if (path.IndexOf("SFZ", StringComparison.OrdinalIgnoreCase) >= 0 || ext == ".arta" || ext == ".eiiwav" || ext == ".roliaudio" || ext == ".ignitex")
                return "Sampler audio payload";
            if (ext == ".scl")
                return "Microtuning scale";
            if (ext == ".caf")
                return "Core Audio sound or voice payload";
            return "Audio-library support data";
        }

        private static void AddFmodBankInfo(List<ReportSection> sections, string path, byte[] header)
        {
            if (!string.Equals(Path.GetExtension(path), ".bank", StringComparison.OrdinalIgnoreCase))
                return;

            var riffFev = header.Length >= 12 && StartsWith(header, Encoding.ASCII.GetBytes("RIFF")) && Encoding.ASCII.GetString(header, 8, 4) == "FEV ";
            if (!riffFev && IndexOfAscii(header, "FMOD", 0) < 0 && IndexOfAscii(header, "FSB", 0) < 0)
                return;

            var section = AddSection(sections, "FMOD bank");
            Add(section, "Format hint", riffFev ? "FMOD Designer FEV bank in RIFF container" : "FMOD-related bank");
            if (riffFev)
                Add(section, "RIFF form", "FEV ");
            var markers = new[] { "FMOD", "FSB", "PROJ", "BNKI", "IBUS", "EVNT", "SND " }
                .Where(marker => IndexOfAscii(header, marker, 0) >= 0)
                .ToArray();
            if (markers.Length > 0)
                Add(section, "Visible markers", string.Join(", ", markers));
        }

        private static void AddWwiseMediaInfo(List<ReportSection> sections, string path, byte[] header)
        {
            if (!string.Equals(Path.GetExtension(path), ".wem", StringComparison.OrdinalIgnoreCase))
                return;

            var section = AddSection(sections, "Wwise media");
            Add(section, "Format hint", "Audiokinetic Wwise encoded media");
            if (header.Length >= 12 && StartsWith(header, Encoding.ASCII.GetBytes("RIFF")))
            {
                Add(section, "Container", "RIFF/WAVE");
                if (header.Length >= 24)
                {
                    var format = ReadUInt16LittleEndian(header, 20);
                    Add(section, "WAVE format code", "0x" + format.ToString("X4", CultureInfo.InvariantCulture) + " (" + WaveFormatName(format) + ")");
                    Add(section, "Channels", ReadUInt16LittleEndian(header, 22).ToString(CultureInfo.InvariantCulture));
                    Add(section, "Sample rate", ReadUInt32LittleEndian(header, 24).ToString(CultureInfo.InvariantCulture) + " Hz");
                }
            }
            Add(section, "Notes", "WEM audio is often Vorbis or platform-specific codec data wrapped for Wwise. FileDentify reports container evidence but does not decode audio.");
        }

        private static bool LooksLikeJson(byte[] header)
        {
            var text = Encoding.UTF8.GetString(header.Take(Math.Min(header.Length, 512)).ToArray()).TrimStart('\uFEFF', ' ', '\r', '\n', '\t');
            return text.StartsWith("{", StringComparison.Ordinal) || text.StartsWith("[", StringComparison.Ordinal);
        }

        private static string DecodeTextSample(byte[] sample)
        {
            if (sample == null || sample.Length == 0)
                return string.Empty;
            if (sample.Length >= 2 && sample[0] == 0xFF && sample[1] == 0xFE)
                return Encoding.Unicode.GetString(sample);
            if (sample.Length >= 2 && sample[0] == 0xFE && sample[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(sample);
            return Encoding.UTF8.GetString(sample);
        }

        private static string ReadNullTerminatedText(byte[] data, int offset, int maxLength)
        {
            if (offset < 0 || offset >= data.Length)
                return string.Empty;
            var count = 0;
            while (offset + count < data.Length && count < maxLength && data[offset + count] != 0)
                count++;
            return CleanMetadataText(Encoding.GetEncoding(28591).GetString(data, offset, count));
        }

        private static int IndexOfAscii(byte[] data, string value, int start)
        {
            if (data == null || string.IsNullOrEmpty(value))
                return -1;
            var needle = Encoding.ASCII.GetBytes(value);
            for (var i = Math.Max(0, start); i + needle.Length <= data.Length; i++)
            {
                var ok = true;
                for (var j = 0; j < needle.Length; j++)
                {
                    if (data[i + j] != needle[j])
                    {
                        ok = false;
                        break;
                    }
                }
                if (ok)
                    return i;
            }
            return -1;
        }
    }
}
