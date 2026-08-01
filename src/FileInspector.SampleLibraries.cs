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
            var pluginAlliance = PluginAllianceTypeName(path);
            if (pluginAlliance != null)
                return pluginAlliance;
            var vital = VitalTypeName(path);
            if (vital != null)
                return vital;
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
            AddPluginAllianceInfo(sections, path, fileLength);
            AddVitalInfo(sections, path, header, sample, fileLength);
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
                Add(section, "Payload", LooksLikeText(header) ? "Text-like header" : "Binary/proprietary package header");
            }
            else
            {
                var banks = FindReadableTextLines(sample, 2, 80).Distinct(StringComparer.OrdinalIgnoreCase).Take(30).ToArray();
                if (banks.Length > 0)
                    Add(section, "Installed banks", string.Join(Environment.NewLine, banks));
            }

            Add(section, "Notes", "XLN Audio packages are sound-library assets for products such as Addictive Drums, Addictive Keys, Addictive Trigger, and XO. FileDentify reports product, pack, size, and visible bank names where available; it does not unpack sample payloads.");
        }

        private static string SpectrasonicsTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".db")
            {
                if (StartsWith(header, Encoding.ASCII.GetBytes("<FileSystem>")))
                    return "Spectrasonics STEAM/SAGE sample container";
                if (IsSpectrasonicsPath(path))
                    return "Spectrasonics database or sample container";
                return null;
            }
            if (ext == ".mlt_omn") return "Spectrasonics Omnisphere multi";
            if (ext == ".mlt_key") return "Spectrasonics Keyscape multi";
            if (ext == ".mlt_trl") return "Spectrasonics Trilian multi";
            if (ext == ".mlt_rmx") return "Spectrasonics Stylus RMX multi";
            if (ext == ".fxp_rmx") return "Spectrasonics Stylus RMX effect preset";
            if (ext == ".fxr_rmx") return "Spectrasonics Stylus RMX effect rack";
            if (ext == ".kit_rmx") return "Spectrasonics Stylus RMX kit";
            if (ext == ".prt_rmx") return "Spectrasonics Stylus RMX part";
            if (ext == ".ctl_rmx") return "Spectrasonics Stylus RMX MIDI learn/controller map";
            if ((ext == ".k4s" || ext == ".mks") && LooksLikeKorgControllerScene(path, header))
                return "Spectrasonics Stylus RMX Korg controller scene";
            if (ext == ".db2") return "Spectrasonics STEAM/SAGE database";
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
            else if (LooksLikeKorgControllerScene(path, header))
            {
                Add(section, "Controller scene marker", ReadAsciiUntil(header, 0, Math.Min(header.Length, 32)).Trim());
                Add(section, "Role", "Korg controller scene or map bundled with Stylus RMX MIDI Learn support.");
                var strings = FindAsciiStrings(sample, 3, 80)
                    .Select(item => item.Value)
                    .Where(value => value.IndexOf("KORG", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("RMX", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(12)
                    .ToArray();
                if (strings.Length > 0)
                    Add(section, "Visible markers", string.Join(Environment.NewLine, strings));
            }

            Add(section, "Notes", "Spectrasonics STEAM/SAGE files belong to instruments such as Omnisphere, Keyscape, Trilian, and Stylus RMX. They can be presets, multis, indexes, or large sample containers. FileDentify reports visible index, product, and preset clues only.");
        }

        private static bool LooksLikeKorgControllerScene(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".k4s" && ext != ".mks")
                return false;
            return path.IndexOf("MIDI Learn", StringComparison.OrdinalIgnoreCase) >= 0 &&
                StartsWith(header, Encoding.ASCII.GetBytes("KORG"));
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
            if (StartsWith(header, Encoding.ASCII.GetBytes("p4pm")) || (inKorgPath && ext == ".mp4prog"))
                return "Korg Mono/Poly program preset";
            if (StartsWith(header, Encoding.ASCII.GetBytes("MSP1")) || (inKorgPath && ext == ".kmp"))
                return "Korg KMP multisample map";
            if (StartsWith(header, Encoding.ASCII.GetBytes("#KORG Script")) || (inKorgPath && ext == ".ksc"))
                return "Korg KSC sample script";
            if (StartsWith(header, Encoding.ASCII.GetBytes("KSCSNDRAW")) || (inKorgPath && Path.GetFileName(path).Equals("RAWSND", StringComparison.OrdinalIgnoreCase)))
                return "Korg Trinity raw PCM sound data";
            if (StartsWith(header, Encoding.ASCII.GetBytes("cmap")) && inKorgPath)
                return "Korg Collection controller/parameter map";
            if (StartsWith(header, Encoding.ASCII.GetBytes("CcnK")) && inKorgPath && (ext == ".fxp" || ext == ".fxb"))
                return ext == ".fxb" ? "Korg Collection VST bank" : "Korg Collection VST preset";
            if (inKorgPath)
            {
                if (ext == ".er1") return "Korg Electribe-R pattern/program";
                if (ext == ".program") return "Korg Collection synth program";
                if (ext == ".pcg") return "Korg PCG program/combi/global data";
                if (ext == ".bin" && path.IndexOf("TRITON", StringComparison.OrdinalIgnoreCase) >= 0) return "Korg Triton PCM ROM/sample image";
                if (Regex.IsMatch(ext, @"^\.\d{4}[hl]$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) &&
                    path.IndexOf("Binary4M", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Korg Trinity/TR-Rack split PCM ROM chunk";
                if (ext == ".json" && (Path.GetFileName(path).Equals("ProgramChange.json", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(path).Equals("PreviewList.json", StringComparison.OrdinalIgnoreCase) ||
                    path.IndexOf("\\Favorites\\", StringComparison.OrdinalIgnoreCase) >= 0))
                    return "Korg preset/index JSON";
                if (ext == ".xml" && LooksLikeText(header))
                    return "Korg Collection XML preset";
                if (ext == "" && path.IndexOf("\\InitData\\", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Korg Trinity init data";
            }
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
            else if (StartsWith(header, Encoding.ASCII.GetBytes("p4pm")))
            {
                Add(section, "Header marker", "p4pm");
                Add(section, "Preset name", ValueOrNotReported(ReadAsciiZ(header, 32, 48)));
                Add(section, "Plug-in/product marker", FirstUsefulKorgString(sample, "KORG", "KLM", "MonoPoly"));
            }
            else if (StartsWith(header, Encoding.ASCII.GetBytes("MSP1")))
            {
                Add(section, "Header marker", "MSP1");
                Add(section, "Multisample name", ValueOrNotReported(ReadAsciiZ(header, 8, 16)));
                AddKorgReferencedFiles(section, sample, ".KSF", "Referenced sample files");
            }
            else if (StartsWith(header, Encoding.ASCII.GetBytes("#KORG Script")) || Path.GetExtension(path).Equals(".ksc", StringComparison.OrdinalIgnoreCase))
            {
                Add(section, "Header marker", StartsWith(header, Encoding.ASCII.GetBytes("#KORG Script")) ? "#KORG Script" : "KSC script");
                AddKorgScriptSummary(section, sample);
            }
            else if (StartsWith(header, Encoding.ASCII.GetBytes("KSCSNDRAW")))
            {
                Add(section, "Header marker", "KSCSNDRAW");
                Add(section, "Payload", "Trinity KSC raw sample/audio payload");
            }
            else if (StartsWith(header, Encoding.ASCII.GetBytes("cmap")))
            {
                Add(section, "Header marker", "cmap");
                Add(section, "Map id", ValueOrNotReported(ReadAsciiZ(header, 16, 16)));
            }
            else if (StartsWith(header, Encoding.ASCII.GetBytes("CcnK")))
            {
                Add(section, "Header marker", "CcnK");
                Add(section, "VST chunk type", header.Length >= 12 ? Encoding.ASCII.GetString(header, 8, 4) : "not reported");
                Add(section, "Plug-in id", header.Length >= 20 ? Encoding.ASCII.GetString(header, 16, 4) : "not reported");
                Add(section, "Preset/bank name", ValueOrNotReported(ReadAsciiZ(header, 28, 48)));
                AddKorgVisibleNames(section, sample, "Visible preset names");
            }
            else if (Path.GetExtension(path).Equals(".pcg", StringComparison.OrdinalIgnoreCase))
            {
                if (StartsWith(header, Encoding.ASCII.GetBytes("KORG")))
                    Add(section, "Header marker", "KORG");
                AddKorgVisibleNames(section, sample, "Visible program/combi names");
            }
            else if (Path.GetExtension(path).Equals(".er1", StringComparison.OrdinalIgnoreCase))
            {
                Add(section, "Pattern/program name", CleanKorgPresetName(Path.GetFileNameWithoutExtension(path)));
                AddKorgVisibleNames(section, sample, "Visible names");
            }
            else if (Path.GetExtension(path).Equals(".program", StringComparison.OrdinalIgnoreCase))
            {
                Add(section, "Program name", ValueOrNotReported(ReadAsciiZ(header, 12, 48)));
            }
            else if (Path.GetExtension(path).Equals(".xml", StringComparison.OrdinalIgnoreCase) && LooksLikeText(header))
            {
                var text = Encoding.UTF8.GetString(sample.Take(Math.Min(sample.Length, 256 * 1024)).ToArray());
                Add(section, "Root element", ValueOrNotReported(FirstKorgXmlRoot(text)));
                Add(section, "Product", ValueOrNotReported(RegexAttribute(text, "product")));
                Add(section, "Vendor", ValueOrNotReported(RegexAttribute(text, "vendor")));
                Add(section, "Programmer", ValueOrNotReported(RegexAttribute(text, "programmer")));
            }
            else if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase) && LooksLikeText(header))
            {
                AddKorgJsonIndexInfo(section, sample);
            }
            else if (Regex.IsMatch(Path.GetExtension(path), @"^\.\d{4}[hl]$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                Add(section, "Chunk side", Path.GetExtension(path).EndsWith("H", StringComparison.OrdinalIgnoreCase) ? "High-byte chunk" : "Low-byte chunk");
                Add(section, "Chunk id", Path.GetExtension(path).TrimStart('.'));
            }
            else if (Path.GetExtension(path).Equals(".bin", StringComparison.OrdinalIgnoreCase) && path.IndexOf("TRITON", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Add(section, "Payload", "Triton PCM ROM/sample data");
                Add(section, "PCM resource", Path.GetFileNameWithoutExtension(path));
            }

            if (Path.GetExtension(path).Length == 0)
                Add(section, "Object id", Path.GetFileName(path));
            Add(section, "Notes", "Korg files can belong to KORG Collection instruments, Triton/Trinity PCM resources, wavestate/WaveMotion data, Electribe patterns, or legacy workstation sample scripts. FileDentify reports folder role, header markers, object names, preset/script clues, and visible identifiers; it does not decode proprietary synth parameters or sample payloads.");
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
            Add(section, "Payload", LooksLikeText(header) ? "Text-like header" : "Binary/proprietary tape-bank payload");
            Add(section, "Notes", "GForce M-Tron libraries emulate Mellotron/Chamberlin tape banks. These files are large proprietary tape-bank containers; FileDentify identifies the bank and context without unpacking or decoding the sample data.");
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

            Add(section, "Notes", "Toontrack files belong to drum and production libraries such as EZdrummer, Superior Drummer, EZX, SDX, and related MIDI/sound packs. FileDentify reports product, role, RIFF/container clues, and visible kit or microphone names where available.");
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

            Add(section, "Notes", "Decent Sampler presets are portable XML-based sample instruments. FileDentify reports sample references, note ranges, UI metadata, and counts without loading audio.");
        }

        private static bool IsXlnAudioPath(string path)
        {
            return path.IndexOf("XLN Audio", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("Addictive Drums", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("Addictive Keys", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("Addictive Trigger", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string PluginAllianceTypeName(string path)
        {
            if (Path.GetExtension(path).Equals(".pabundle", StringComparison.OrdinalIgnoreCase))
                return "Plugin Alliance installer bundle";
            return null;
        }

        private static void AddPluginAllianceInfo(List<ReportSection> sections, string path, long fileLength)
        {
            var type = PluginAllianceTypeName(path);
            if (type == null)
                return;

            var section = AddSection(sections, "Plugin Alliance");
            Add(section, "Format hint", type);
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "Product/folder", SegmentAfter(path, "Plugin Alliance"));
            Add(section, "Notes", "Plugin Alliance .pabundle files are large installer/content bundles used by Plugin Alliance software. FileDentify identifies the bundle and path context without extracting or installing it.");
        }

        private static string VitalTypeName(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".vitalbank")
                return "Vital synthesizer preset bank";
            if (ext == ".vital")
                return "Vital synthesizer preset";
            return null;
        }

        private static void AddVitalInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            var type = VitalTypeName(path);
            if (type == null)
                return;

            var section = AddSection(sections, "Vital synthesizer");
            Add(section, "Format hint", type);
            Add(section, "File size", FormatBytes(fileLength));
            var names = FindReadableTextLines(sample, 4, 80)
                .Where(value => value.IndexOf("preset", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("author", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("Vital", StringComparison.OrdinalIgnoreCase) >= 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToArray();
            if (names.Length > 0)
                Add(section, "Visible metadata strings", string.Join(Environment.NewLine, names));
            Add(section, "Notes", "Vital .vital and .vitalbank files store synth presets and preset banks. FileDentify reports role and visible metadata only; it does not load the synthesizer or expand the full bank.");
        }

        private static bool IsSpectrasonicsPath(string path)
        {
            return path.IndexOf("Spectrasonics", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\STEAM\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\SAGE\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("Omnisphere", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("Keyscape", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("Trilian", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("Stylus RMX", StringComparison.OrdinalIgnoreCase) >= 0;
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
            if (path.IndexOf("\\Presets\\", StringComparison.OrdinalIgnoreCase) >= 0) return "preset/program data";
            if (path.IndexOf("\\PCM\\", StringComparison.OrdinalIgnoreCase) >= 0) return "PCM sample or workstation resource";
            if (path.IndexOf("\\MIDI\\", StringComparison.OrdinalIgnoreCase) >= 0) return "MIDI preview/index data";
            if (path.IndexOf("\\Favorites\\", StringComparison.OrdinalIgnoreCase) >= 0) return "favorites/index data";
            if (path.IndexOf("\\Resource\\", StringComparison.OrdinalIgnoreCase) >= 0) return "product resource data";
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

        private static void AddKorgScriptSummary(ReportSection section, byte[] sample)
        {
            var lines = FindReadableTextLines(sample, 2, 120)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Take(80)
                .ToArray();
            var countLine = lines.FirstOrDefault(value => value.IndexOf("Multi Samples", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrWhiteSpace(countLine))
                Add(section, "Declared multisamples", countLine.TrimStart('#').Trim());
            var refs = lines
                .Where(value => value.EndsWith(".KMP", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(24)
                .ToArray();
            if (refs.Length > 0)
                Add(section, "Referenced multisamples", string.Join(Environment.NewLine, refs));
        }

        private static void AddKorgReferencedFiles(ReportSection section, byte[] sample, string extension, string title)
        {
            var refs = FindAsciiStrings(sample, 3, 100)
                .Select(item => item.Value.Trim())
                .Where(value => value.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(24)
                .ToArray();
            if (refs.Length > 0)
                Add(section, title, string.Join(Environment.NewLine, refs));
        }

        private static void AddKorgVisibleNames(ReportSection section, byte[] sample, string title)
        {
            var names = FindReadableTextLines(sample, 4, 120)
                .Select(CleanKorgPresetName)
                .Where(IsUsefulKorgVisibleName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(24)
                .ToArray();
            if (names.Length > 0)
                Add(section, title, string.Join(Environment.NewLine, names));
        }

        private static void AddKorgJsonIndexInfo(ReportSection section, byte[] sample)
        {
            var text = Encoding.UTF8.GetString(sample.Take(Math.Min(sample.Length, 512 * 1024)).ToArray());
            var pathMatches = Regex.Matches(text, "\"path\"\\s*:\\s*\"(?<path>[^\"]+)\"", RegexOptions.IgnoreCase);
            if (pathMatches.Count > 0)
            {
                Add(section, "Indexed paths in sample", pathMatches.Count.ToString(CultureInfo.InvariantCulture));
                var names = pathMatches.Cast<Match>()
                    .Select(match => Path.GetFileName(match.Groups["path"].Value.Replace("/", "\\")))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(CleanKorgPresetName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(20)
                    .ToArray();
                if (names.Length > 0)
                    Add(section, "First indexed entries", string.Join(Environment.NewLine, names));
            }
            var favoriteMatches = Regex.Matches(text, "\"name\"\\s*:\\s*\"(?<name>[^\"]+)\"", RegexOptions.IgnoreCase);
            if (favoriteMatches.Count > 0)
            {
                var names = favoriteMatches.Cast<Match>()
                    .Select(match => CleanKorgPresetName(match.Groups["name"].Value))
                    .Where(IsUsefulKorgVisibleName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(20)
                    .ToArray();
                if (names.Length > 0)
                    Add(section, "Visible names", string.Join(Environment.NewLine, names));
            }
        }

        private static string FirstUsefulKorgString(byte[] sample, params string[] contains)
        {
            var value = FindReadableTextLines(sample, 3, 80)
                .FirstOrDefault(line => contains.Any(part => line.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0));
            return ValueOrNotReported(value);
        }

        private static string FirstKorgXmlRoot(string text)
        {
            var match = Regex.Match(text ?? string.Empty, "<\\s*(?<name>[A-Za-z0-9_:-]+)\\b");
            return match.Success ? match.Groups["name"].Value : string.Empty;
        }

        private static string RegexAttribute(string text, string attribute)
        {
            var match = Regex.Match(text ?? string.Empty, "\\b" + Regex.Escape(attribute) + "\\s*=\\s*\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["value"].Value : string.Empty;
        }

        private static string CleanKorgPresetName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            var cleaned = value.Replace("%20", " ");
            try { cleaned = Uri.UnescapeDataString(cleaned); }
            catch { }
            return Regex.Replace(cleaned, "\\s+", " ").Trim();
        }

        private static bool IsUsefulKorgVisibleName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            value = value.Trim();
            if (value.Length < 4 || value.Length > 80)
                return false;
            if (value.IndexOf("KORG Script", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            if (Regex.IsMatch(value, @"[\\/?]"))
                return false;
            if (Regex.IsMatch(value, "^[0-9A-Fa-fx]+$"))
                return false;
            var letters = value.Count(char.IsLetter);
            if (letters < 3)
                return false;
            return Regex.IsMatch(value, "[AEIOUYaeiouy]");
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
