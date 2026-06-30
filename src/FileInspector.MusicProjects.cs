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
            if (LhaMethod(header) != null || ext == ".lha" || ext == ".lzh") return "LHA/LZH archive";
            if (ext == ".mogg") return "MOGG multitrack Ogg audio";
            if (ext == ".sfark") return "sfArk compressed SoundFont archive";
            if ((ext == ".rpp" || ext == ".rpp-bak") && IsReaperProject(header)) return ext == ".rpp-bak" ? "REAPER project backup" : "REAPER project";
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
            AddCakewalkInfo(sections, path, stringSample);
            AddSamplerInstrumentInfo(sections, path, stringSample);
            AddCreativeEcwInfo(sections, path, header);
            AddFmodBankInfo(sections, path, header);
            AddWwiseMediaInfo(sections, path, header);
            AddSpitfireAudioInfo(sections, path, header, stringSample, fileLength);
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
            if (StartsWith(header, Encoding.ASCII.GetBytes("IMPM"))) return "Impulse Tracker module";
            switch (ext)
            {
                case ".mod": return "Tracker module, likely ProTracker/Amiga MOD";
                case ".xm": return "FastTracker XM module";
                case ".s3m": return "Scream Tracker 3 module";
                case ".it": return "Impulse Tracker module";
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
