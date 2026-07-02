using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FileDentify
{
    internal static partial class FileInspector
    {
        private static string SpeechVoiceTypeName(string path, byte[] header)
        {
            if (LooksLikeIvonaSpeechData(path, header))
                return "IVONA speech voice package";
            if (LooksLikeLoquendoSpeechData(path, header))
                return "Loquendo TTS package";
            if (LooksLikeAttNaturalVoice(path, header))
                return "AT&T Natural Voices package";
            if (LooksLikeDolphinSpeechData(path, header))
                return DolphinSpeechTypeName(path, header);
            if (LooksLikeEspeakSpeechData(path, header))
                return EspeakSpeechTypeName(path);
            if (LooksLikeSuperTonicSpeechData(path, header))
                return "SuperTonic neural TTS data";
            if (LooksLikeOrpheusSpeechData(path, header))
                return OrpheusSpeechTypeName(path);
            if (LooksLikeEloquenceOrIbmTtsData(path, header))
                return EloquenceOrIbmTtsTypeName(path, header);
            if (LooksLikeRhVoiceData(path, header))
                return RhVoiceTypeName(path);
            if (LooksLikeFlexVoiceData(path, header))
                return FlexVoiceTypeName(path);
            if (LooksLikeOtherNvdaSpeechEngineData(path, header))
                return OtherNvdaSpeechEngineTypeName(path, header);
            if (LooksLikeScanSoftRealSpeakMobile(path, header))
                return "ScanSoft RealSpeak Mobile speech data";
            if (LooksLikeModelTalkerData(path, header))
                return "ModelTalker speech data";
            if (LooksLikeMicrosoftSpeechVoice(path, header))
                return MicrosoftSpeechVoiceTypeName(path, header);
            if (LooksLikeAcapelaVoicePath(path))
            {
                var acapelaExt = Path.GetExtension(path).ToLowerInvariant();
                if (acapelaExt == ".qvcu")
                    return "Acapela voice data";
                if (acapelaExt == ".nuul216")
                    return "Acapela voice support data";
                if (acapelaExt == ".clb")
                    return "Acapela/Infovox speech library module";
            }
            if (!LooksLikePiperVoicePath(path))
                return null;
            var fileName = Path.GetFileName(path);
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".onnx")
                return "Piper neural speech voice model";
            if (ext == ".json" || fileName.Equals("MODEL_CARD", StringComparison.OrdinalIgnoreCase))
                return "Piper neural speech voice metadata";
            return null;
        }

        private static void AddSpeechVoiceInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
        {
            var type = SpeechVoiceTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Speech voice");
            Add(section, "Format hint", type);
            Add(section, "Voice folder", Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty));
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "Role", SpeechVoiceRole(path, header));

            if (LooksLikeAttNaturalVoice(path, header))
            {
                AddAttNaturalVoiceInfo(section, path, header, fileLength);
                return;
            }

            if (LooksLikeIvonaSpeechData(path, header))
            {
                AddIvonaSpeechInfo(section, path, header);
                return;
            }

            if (LooksLikeLoquendoSpeechData(path, header))
            {
                AddLoquendoSpeechInfo(section, path, header);
                return;
            }

            if (LooksLikeScanSoftRealSpeakMobile(path, header))
            {
                AddScanSoftRealSpeakMobileInfo(section, header);
                return;
            }

            if (LooksLikeModelTalkerData(path, header))
            {
                AddModelTalkerInfo(section, path, header);
                return;
            }

            if (LooksLikeSuperTonicSpeechData(path, header) ||
                LooksLikeOrpheusSpeechData(path, header) ||
                LooksLikeEloquenceOrIbmTtsData(path, header) ||
                LooksLikeRhVoiceData(path, header) ||
                LooksLikeFlexVoiceData(path, header) ||
                LooksLikeEspeakSpeechData(path, header) ||
                LooksLikeOtherNvdaSpeechEngineData(path, header))
            {
                AddNvdaSpeechEngineInfo(section, path, header);
                return;
            }

            if (LooksLikeDolphinSpeechData(path, header))
            {
                AddDolphinSpeechInfo(section, path, header, fileLength);
                return;
            }

            if (LooksLikeMicrosoftSpeechVoice(path, header))
            {
                AddMicrosoftSpeechVoiceInfo(section, path, header);
                return;
            }

            if (LooksLikeAcapelaVoicePath(path))
            {
                Add(section, "Voice family", SegmentAfter(path, "Engines"));
                if (Path.GetExtension(path).Equals(".clb", StringComparison.OrdinalIgnoreCase) && StartsWith(header, Encoding.ASCII.GetBytes("MZ")))
                    Add(section, "Container", "Windows PE library/module");
                Add(section, "Notes", "Acapela voice packages are commercial text-to-speech voice assets used by screen readers, assistive software, and embedded speech products. FileDentify reports the voice folder, role, and size so large speech-engine assets are easier to recognise without decoding them.");
                return;
            }

            if (LooksLikeText(header))
            {
                var text = Encoding.UTF8.GetString(header.Take(Math.Min(header.Length, 1024 * 1024)).ToArray());
                AddJsonValue(section, text, "sample_rate", "Sample rate");
                AddJsonValue(section, text, "quality", "Quality");
                AddJsonValue(section, text, "voice", "eSpeak voice");
                AddJsonValue(section, text, "num_symbols", "Symbol count");
                AddJsonValue(section, text, "num_speakers", "Speaker count");
                AddJsonValue(section, text, "speaker_id", "Speaker ID");
                var language = Regex.Match(text, "\"language\"\\s*:\\s*\\{(?<body>.*?)\\}", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (language.Success)
                {
                    AddJsonValue(section, language.Groups["body"].Value, "code", "Language code");
                    AddJsonValue(section, language.Groups["body"].Value, "name_english", "Language");
                    AddJsonValue(section, language.Groups["body"].Value, "country_english", "Country");
                }
            }

            Add(section, "Notes", "Piper voices usually pair one or more ONNX model files with JSON configuration and optional MODEL_CARD text. FileDentify reports voice metadata and model role without loading the neural network.");
        }

        private static string SpeechVoiceRole(string path, byte[] header)
        {
            if (LooksLikeMicrosoftSpeechVoice(path, header))
                return MicrosoftSpeechVoiceRole(path);
            if (LooksLikeAcapelaVoicePath(path))
                return AcapelaVoiceRole(path);
            if (LooksLikeIvonaSpeechData(path, header))
                return IvonaSpeechRole(path, header);
            if (LooksLikeLoquendoSpeechData(path, header))
                return LoquendoSpeechRole(path, header);
            if (LooksLikeDolphinSpeechData(path, header))
                return DolphinSpeechRole(path);
            if (LooksLikeEspeakSpeechData(path, header))
                return EspeakSpeechRole(path);
            if (LooksLikeSuperTonicSpeechData(path, header))
                return SuperTonicSpeechRole(path);
            if (LooksLikeOrpheusSpeechData(path, header))
                return OrpheusSpeechRole(path);
            if (LooksLikeEloquenceOrIbmTtsData(path, header))
                return EloquenceOrIbmTtsRole(path, header);
            if (LooksLikeRhVoiceData(path, header))
                return RhVoiceRole(path);
            if (LooksLikeFlexVoiceData(path, header))
                return FlexVoiceRole(path);
            if (LooksLikeOtherNvdaSpeechEngineData(path, header))
                return OtherNvdaSpeechEngineRole(path, header);
            if (LooksLikeScanSoftRealSpeakMobile(path, header))
                return "mobile TTS engine or voice header";
            if (LooksLikeModelTalkerData(path, header))
                return "voice codebook or model support file";
            return PiperVoiceRole(path);
        }

        private static bool LooksLikeModelTalkerData(string path, byte[] header)
        {
            if (!Path.GetExtension(path).Equals(".cbk", StringComparison.OrdinalIgnoreCase))
                return false;
            var text = DecodeTextSample(header, 8192);
            return path.IndexOf("\\modeltalker\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("Default name", StringComparison.OrdinalIgnoreCase) >= 0 && text.IndexOf("Page 0", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddModelTalkerInfo(ReportSection section, string path, byte[] header)
        {
            Add(section, "Voice system", "ModelTalker");
            Add(section, "Voice folder", ParentName(path));
            var strings = FindReadableTextLines(header, 3, 80)
                .Where(line => line.Any(char.IsLetterOrDigit))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .ToArray();
            if (strings.Length > 0)
                Add(section, "Visible labels", string.Join(Environment.NewLine, strings));
            Add(section, "Notes", "ModelTalker files are text-to-speech voice assets. FileDentify reports file role and visible labels only; it does not synthesize speech or decode proprietary voice data.");
        }

        private static bool LooksLikeScanSoftRealSpeakMobile(string path, byte[] header)
        {
            if (!Path.GetExtension(path).Equals(".hdr", StringComparison.OrdinalIgnoreCase))
                return false;

            var text = AsciiPreview(header, Math.Min(header.Length, 1024));
            return text.IndexOf("<SCANSOFT>", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("RealSpeak Mobile", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (path.IndexOf("\\rsm\\", StringComparison.OrdinalIgnoreCase) >= 0 && text.IndexOf("<COMPTYPE>", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void AddScanSoftRealSpeakMobileInfo(ReportSection section, byte[] header)
        {
            Add(section, "Vendor/family", "ScanSoft RealSpeak Mobile");
            AddXmlishTag(section, header, "COMPTYPE", "Component type");
            AddXmlishTag(section, header, "NAME", "Name");
            AddXmlishTag(section, header, "BROKERSTRING", "Broker string");
            AddXmlishTag(section, header, "ENGINE", "Engine");
            AddXmlishTag(section, header, "VERSION", "Version");
            Add(section, "Notes", "ScanSoft RealSpeak Mobile was a compact embedded/mobile text-to-speech engine, later associated with Nuance speech technology. FileDentify reports small text headers and safe metadata only; it does not load the TTS engine or decode proprietary voice payloads.");
        }

        private static void AddXmlishTag(ReportSection section, byte[] header, string tag, string label)
        {
            var text = Encoding.UTF8.GetString(header.Take(Math.Min(header.Length, 64 * 1024)).ToArray());
            var match = Regex.Match(text, "<" + Regex.Escape(tag) + ">(?<value>.*?)</" + Regex.Escape(tag) + ">", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success)
            {
                var value = match.Groups["value"].Value.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                    Add(section, label, value);
            }
        }

        private static bool LooksLikeAttNaturalVoice(string path, byte[] header)
        {
            var lower = path.ToLowerInvariant();
            var name = Path.GetFileName(path) ?? string.Empty;
            if (lower.Contains("\\at&t\\") || lower.Contains("\\att natural") || lower.Contains("\\at&t natural"))
                return HasSpeechPackageExtension(path) || IsZipHeader(header) || StartsWith(header, Encoding.ASCII.GetBytes("MZ")) || string.Equals(Path.GetExtension(path), ".iso", StringComparison.OrdinalIgnoreCase);
            return name.IndexOf("AT&T Labs' Natural Voices", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasSpeechPackageExtension(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".zip":
                case ".rar":
                case ".7z":
                case ".exe":
                case ".msi":
                case ".iso":
                case ".cab":
                    return true;
                default:
                    return false;
            }
        }

        private static bool LooksLikeIvonaSpeechData(string path, byte[] header)
        {
            var lower = path.ToLowerInvariant();
            var name = Path.GetFileName(path) ?? string.Empty;
            if (lower.Contains("\\ivona\\"))
                return HasSpeechPackageExtension(path) || name.StartsWith("ivona", StringComparison.OrdinalIgnoreCase);
            return name.StartsWith("ivona", StringComparison.OrdinalIgnoreCase) &&
                (HasSpeechPackageExtension(path) || StartsWith(header, Encoding.ASCII.GetBytes("MZ")));
        }

        private static string IvonaSpeechRole(string path, byte[] header)
        {
            var name = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
            if (name.IndexOf("installer", StringComparison.OrdinalIgnoreCase) >= 0)
                return "voice installer package";
            if (StartsWith(header, Encoding.ASCII.GetBytes("MZ")))
                return "voice self-extracting installer";
            return "speech voice package";
        }

        private static void AddIvonaSpeechInfo(ReportSection section, string path, byte[] header)
        {
            Add(section, "Vendor/family", "IVONA speech voices");
            Add(section, "Package voice hint", IvonaVoiceNameFromPath(path));
            Add(section, "Container role", IvonaSpeechRole(path, header));
            AddSpeechPackageIndexSummary(section, path, header);
            Add(section, "Notes", "IVONA was a commercial text-to-speech voice family, later acquired by Amazon. Its voice installers can be very large self-extracting packages. FileDentify reports filename, container, and safe package-index clues only; it does not run installers or extract voice payloads.");
        }

        private static string IvonaVoiceNameFromPath(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
            name = Regex.Replace(name, @"(?i)^ivona2?_installer_pak_", string.Empty);
            name = name.Replace("_", " ").Trim();
            return string.IsNullOrWhiteSpace(name) ? "(voice not visible in filename)" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.ToLowerInvariant());
        }

        private static bool LooksLikeLoquendoSpeechData(string path, byte[] header)
        {
            var lower = path.ToLowerInvariant();
            var name = Path.GetFileName(path) ?? string.Empty;
            if (lower.Contains("\\loquendo"))
                return HasSpeechPackageExtension(path) || name.IndexOf("Loquendo_TTS", StringComparison.OrdinalIgnoreCase) >= 0;
            return name.IndexOf("Loquendo_TTS", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (HasSpeechPackageExtension(path) || StartsWith(header, Encoding.ASCII.GetBytes("MZ")));
        }

        private static string LoquendoSpeechRole(string path, byte[] header)
        {
            var name = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
            if (name.IndexOf("Engine", StringComparison.OrdinalIgnoreCase) >= 0)
                return "TTS engine distribution";
            if (name.IndexOf("SDK", StringComparison.OrdinalIgnoreCase) >= 0)
                return "TTS SDK distribution";
            if (name.IndexOf("Remote_API", StringComparison.OrdinalIgnoreCase) >= 0)
                return "remote API distribution";
            if (name.IndexOf("High_Quality", StringComparison.OrdinalIgnoreCase) >= 0)
                return "high-quality voice distribution";
            if (name.IndexOf("Distribution", StringComparison.OrdinalIgnoreCase) >= 0)
                return "language distribution";
            return "TTS package";
        }

        private static void AddLoquendoSpeechInfo(ReportSection section, string path, byte[] header)
        {
            Add(section, "Vendor/family", "Loquendo TTS");
            Add(section, "Package voice hint", LoquendoNameFromPath(path));
            Add(section, "Container role", LoquendoSpeechRole(path, header));
            Add(section, "Distribution folder", LoquendoDistributionFolder(path));
            AddSpeechPackageIndexSummary(section, path, header);
            Add(section, "Notes", "Loquendo TTS was a commercial speech engine used in accessibility, telephony, and embedded systems. FileDentify reports distribution filenames, archive indexes, and safe header metadata only; it does not install voices or load speech engines.");
        }

        private static string LoquendoNameFromPath(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
            name = Regex.Replace(name, @"(?i)^Loquendo_TTS_7-Win32_", string.Empty);
            name = Regex.Replace(name, @"(?i)_Distribution_.*$", string.Empty);
            name = name.Replace("_", " ").Trim();
            return string.IsNullOrWhiteSpace(name) ? "(package name not visible)" : CleanMetadataText(name);
        }

        private static string LoquendoDistributionFolder(string path)
        {
            var match = Regex.Match(path, @"\\(?<folder>[0-9]\.[^\\]+)\\", RegexOptions.IgnoreCase);
            if (match.Success)
                return CleanMetadataText(match.Groups["folder"].Value);
            return ValueOrNotReported(Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty));
        }

        private static void AddSpeechPackageIndexSummary(ReportSection section, string path, byte[] header)
        {
            if (IsZipHeader(header))
            {
                try
                {
                    using (var archive = ZipFile.OpenRead(path))
                    {
                        Add(section, "Archive entries", archive.Entries.Count.ToString(CultureInfo.InvariantCulture));
                        var largest = archive.Entries
                            .Where(e => !string.IsNullOrEmpty(e.Name))
                            .OrderByDescending(e => e.Length)
                            .Take(10)
                            .Select(e => e.FullName + " (" + FormatUnsignedBytes((ulong)e.Length) + ")")
                            .ToArray();
                        if (largest.Length > 0)
                            Add(section, "Largest package payloads", string.Join(Environment.NewLine, largest));
                    }
                }
                catch (Exception ex)
                {
                    Add(section, "Archive read note", ex.Message);
                }
            }
            else if (StartsWith(header, Encoding.ASCII.GetBytes("MZ")))
            {
                Add(section, "Executable note", "Windows executable or self-extracting speech package.");
            }
            else if (string.Equals(Path.GetExtension(path), ".rar", StringComparison.OrdinalIgnoreCase))
            {
                Add(section, "Archive note", "RAR speech package. FileDentify identifies the family but does not unpack RAR payloads.");
            }
        }

        private static void AddAttNaturalVoiceInfo(ReportSection section, string path, byte[] header, long fileLength)
        {
            Add(section, "Vendor/family", "AT&T Natural Voices");
            Add(section, "Package voice hint", AttVoiceNameFromPath(path));
            Add(section, "Container role", AttPackageRole(path, header));

            if (IsZipHeader(header))
            {
                try
                {
                    using (var archive = ZipFile.OpenRead(path))
                    {
                        Add(section, "Archive entries", archive.Entries.Count.ToString(CultureInfo.InvariantCulture));
                        var installer = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".msi", StringComparison.OrdinalIgnoreCase));
                        if (installer != null)
                            Add(section, "Installer package", installer.FullName + " (" + FormatUnsignedBytes((ulong)installer.Length) + ")");
                        var largest = archive.Entries
                            .Where(e => !string.IsNullOrEmpty(e.Name))
                            .OrderByDescending(e => e.Length)
                            .Take(10)
                            .Select(e => e.FullName + " (" + FormatUnsignedBytes((ulong)e.Length) + ")")
                            .ToArray();
                        if (largest.Length > 0)
                            Add(section, "Largest voice/install payloads", string.Join(Environment.NewLine, largest));
                    }
                }
                catch (Exception ex)
                {
                    Add(section, "Archive read note", ex.Message);
                }
            }
            else if (string.Equals(Path.GetExtension(path), ".iso", StringComparison.OrdinalIgnoreCase))
            {
                Add(section, "Disc image note", "AT&T Natural Voices installer disc image. The ISO volume section may also report optical-disc metadata when available.");
            }
            else if (StartsWith(header, Encoding.ASCII.GetBytes("MZ")))
            {
                Add(section, "Executable note", "Likely self-extracting AT&T Natural Voices installer.");
            }

            Add(section, "Notes", "AT&T Natural Voices was a commercial TTS voice family from AT&T Labs. Packages often contain very large voice-data cabinets. FileDentify reports package structure and sizes only; it does not run installers or extract voice payloads.");
        }

        private static string AttVoiceNameFromPath(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
            if (name.StartsWith("AT&T", StringComparison.OrdinalIgnoreCase))
                return "(installer set)";
            return CleanMetadataText(name.Replace("_", " "));
        }

        private static string AttPackageRole(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (IsZipHeader(header))
                return "voice installer archive";
            if (ext == ".rar" || ext == ".7z")
                return "voice installer archive";
            if (ext == ".iso")
                return "voice installer disc image";
            if (ext == ".cab")
                return "voice data cabinet";
            if (ext == ".msi")
                return "voice installer package";
            if (StartsWith(header, Encoding.ASCII.GetBytes("MZ")))
                return "voice self-extracting installer";
            return "voice package";
        }

        private static bool LooksLikeSuperTonicSpeechData(string path, byte[] header)
        {
            var lower = path.ToLowerInvariant();
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (lower.Contains("\\supertonic\\") && (ext == ".onnx" || ext == ".json" || ext == ".yaml" || ext == ".yml"))
                return true;
            if (ext == ".onnx" && lower.Contains("\\synthdrivers\\") && (SpeechHeaderAscii(header, 256).IndexOf("tts.", StringComparison.OrdinalIgnoreCase) >= 0 || lower.Contains("\\models\\onnx\\")))
                return true;
            return false;
        }

        private static bool LooksLikeEspeakSpeechData(string path, byte[] header)
        {
            var lower = path.ToLowerInvariant();
            if (!lower.Contains("\\espeak-ng-data\\") && !lower.Contains("\\espeak-data\\"))
                return false;
            var name = Path.GetFileName(path) ?? string.Empty;
            if (name.EndsWith("_dict", StringComparison.OrdinalIgnoreCase))
                return true;
            switch (name.ToLowerInvariant())
            {
                case "phondata":
                case "phondata-manifest":
                case "phonindex":
                case "phontab":
                case "intonations":
                case "klatt":
                    return true;
                default:
                    return false;
            }
        }

        private static string EspeakSpeechTypeName(string path)
        {
            var name = Path.GetFileName(path) ?? string.Empty;
            if (name.EndsWith("_dict", StringComparison.OrdinalIgnoreCase))
                return "eSpeak NG pronunciation dictionary";
            if (name.StartsWith("phon", StringComparison.OrdinalIgnoreCase))
                return "eSpeak NG phoneme data";
            return "eSpeak NG speech data";
        }

        private static string EspeakSpeechRole(string path)
        {
            var name = Path.GetFileName(path) ?? string.Empty;
            if (name.EndsWith("_dict", StringComparison.OrdinalIgnoreCase))
                return "compiled language dictionary " + name.Substring(0, Math.Max(0, name.Length - 5));
            if (name.Equals("phondata", StringComparison.OrdinalIgnoreCase))
                return "compiled phoneme data";
            if (name.Equals("phondata-manifest", StringComparison.OrdinalIgnoreCase))
                return "phoneme data manifest";
            if (name.Equals("phonindex", StringComparison.OrdinalIgnoreCase))
                return "phoneme index";
            if (name.Equals("phontab", StringComparison.OrdinalIgnoreCase))
                return "phoneme table";
            return "speech synthesis data";
        }

        private static string SuperTonicSpeechRole(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
            if (name.IndexOf("vocoder", StringComparison.OrdinalIgnoreCase) >= 0)
                return "neural vocoder model";
            if (name.IndexOf("encoder", StringComparison.OrdinalIgnoreCase) >= 0)
                return "neural text/acoustic encoder model";
            if (name.IndexOf("estimator", StringComparison.OrdinalIgnoreCase) >= 0)
                return "neural feature estimator model";
            if (name.IndexOf("duration", StringComparison.OrdinalIgnoreCase) >= 0)
                return "neural duration model";
            if (Path.GetExtension(path).Equals(".onnx", StringComparison.OrdinalIgnoreCase))
                return "ONNX neural TTS model";
            return "neural TTS metadata";
        }

        private static bool LooksLikeOrpheusSpeechData(string path, byte[] header)
        {
            var lower = path.ToLowerInvariant();
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (!lower.Contains("\\orpheus\\"))
                return false;
            if (ext == ".tts" || ext == ".dat" || ext == ".ini" || ext == ".bin")
                return true;
            var name = Path.GetFileName(path) ?? string.Empty;
            return name.IndexOf("orpheus", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (StartsWith(header, Encoding.ASCII.GetBytes("MZ")) || ext == ".dll" || ext == ".exe");
        }

        private static string OrpheusSpeechTypeName(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".tts")
                return "Dolphin Orpheus TTS language data";
            if (ext == ".ini")
                return "Dolphin Orpheus TTS configuration";
            if (ext == ".dll" || ext == ".exe")
                return "Dolphin Orpheus TTS engine component";
            return "Dolphin Orpheus TTS data";
        }

        private static string OrpheusSpeechRole(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".tts")
                return "language voice data";
            if (ext == ".ini")
                return "engine/language configuration";
            if (ext == ".dll")
                return "speech engine library";
            if (ext == ".exe")
                return "speech engine host";
            return "speech component data";
        }

        private static bool LooksLikeEloquenceOrIbmTtsData(string path, byte[] header)
        {
            var lower = path.ToLowerInvariant();
            if (!(lower.Contains("\\eloquence\\") || lower.Contains("\\ibmtts\\")))
                return false;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".syn" || ext == ".dll" || ext == ".exe")
                return true;
            return false;
        }

        private static string EloquenceOrIbmTtsTypeName(string path, byte[] header)
        {
            var family = path.IndexOf("\\IBMTTS\\", StringComparison.OrdinalIgnoreCase) >= 0 ? "IBM TTS" : "Eloquence";
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".syn")
                return family + " synthesizer language module";
            if (ext == ".dll")
                return family + " synthesizer library";
            if (ext == ".exe")
                return family + " synthesizer host";
            return family + " synthesizer data";
        }

        private static string EloquenceOrIbmTtsRole(string path, byte[] header)
        {
            var name = Path.GetFileName(path) ?? string.Empty;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".syn")
                return "language module " + Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
            if (name.EndsWith("rom.dll", StringComparison.OrdinalIgnoreCase))
                return "language ROM support library";
            if (ext == ".dll")
                return "speech engine library";
            if (ext == ".exe")
                return "speech engine host";
            return "synthesizer component";
        }

        private static bool LooksLikeRhVoiceData(string path, byte[] header)
        {
            var lower = path.ToLowerInvariant();
            if (!(lower.Contains("\\rhvoice-voice-") || lower.Contains("\\rhvoice\\")))
                return false;
            var name = Path.GetFileName(path) ?? string.Empty;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (name.Equals("voice.data", StringComparison.OrdinalIgnoreCase) || ext == ".fst")
                return true;
            if (ext == ".pdf" && lower.Contains("\\data\\") && !StartsWith(header, Encoding.ASCII.GetBytes("%PDF")))
                return true;
            if (ext == ".dll" && name.IndexOf("rhvoice", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return false;
        }

        private static string RhVoiceTypeName(string path)
        {
            var name = Path.GetFileName(path) ?? string.Empty;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (name.Equals("voice.data", StringComparison.OrdinalIgnoreCase))
                return "RHVoice HTS voice data";
            if (ext == ".fst")
                return "RHVoice pronunciation lexicon";
            if (ext == ".pdf")
                return "RHVoice acoustic model data";
            if (ext == ".dll")
                return "RHVoice synthesizer library";
            return "RHVoice speech data";
        }

        private static string RhVoiceRole(string path)
        {
            var name = Path.GetFileName(path) ?? string.Empty;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (name.Equals("voice.data", StringComparison.OrdinalIgnoreCase))
                return "HTS voice definition";
            if (ext == ".fst")
                return "finite-state pronunciation dictionary";
            if (ext == ".pdf")
                return "binary acoustic-model parameter data, not a PDF document";
            if (ext == ".dll")
                return "speech engine library";
            return "speech model data";
        }

        private static bool LooksLikeFlexVoiceData(string path, byte[] header)
        {
            var lower = path.ToLowerInvariant();
            if (!lower.Contains("\\flexvoice\\"))
                return false;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".dat" || ext == ".bin" || ext == ".dll")
                return true;
            var ascii = SpeechHeaderAscii(header, 128);
            return ascii.IndexOf("FlexVoice", StringComparison.OrdinalIgnoreCase) >= 0 ||
                ascii.IndexOf("MM.Dictionary", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FlexVoiceTypeName(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".bin")
                return "Mindmaker FlexVoice voice definition";
            if (ext == ".dat")
                return "Mindmaker FlexVoice dictionary/data";
            if (ext == ".dll")
                return "Mindmaker FlexVoice engine library";
            return "Mindmaker FlexVoice speech data";
        }

        private static string FlexVoiceRole(string path)
        {
            var name = Path.GetFileName(path) ?? string.Empty;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".bin")
                return "voice definition";
            if (name.IndexOf("HL", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("L2", StringComparison.OrdinalIgnoreCase) >= 0)
                return "dictionary or language data";
            if (ext == ".dll")
                return "speech engine library";
            return "speech data";
        }

        private static bool LooksLikeOtherNvdaSpeechEngineData(string path, byte[] header)
        {
            var lower = path.ToLowerInvariant();
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (lower.Contains("\\stspeech\\") && (ext == ".dll" || Path.GetFileName(path).Equals("cmudict_data.py", StringComparison.OrdinalIgnoreCase) || ext == ".pyc"))
                return true;
            if (lower.Contains("\\bestspeech\\") && (ext == ".dll" || ext == ".exe"))
                return true;
            if (lower.Contains("\\tgspeechbox\\") && (ext == ".tsv" || ext == ".dll" || ext == ".exe"))
                return true;
            return false;
        }

        private static string OtherNvdaSpeechEngineTypeName(string path, byte[] header)
        {
            var lower = path.ToLowerInvariant();
            if (lower.Contains("\\stspeech\\"))
                return "STSpeech synthesizer data";
            if (lower.Contains("\\bestspeech\\"))
                return "BestSpeech synthesizer component";
            if (lower.Contains("\\tgspeechbox\\"))
                return "tgSpeechBox speech data";
            return "NVDA speech engine data";
        }

        private static string OtherNvdaSpeechEngineRole(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var name = Path.GetFileName(path) ?? string.Empty;
            if (ext == ".dll")
                return "speech engine library";
            if (ext == ".exe")
                return "speech engine host";
            if (name.IndexOf("cmudict", StringComparison.OrdinalIgnoreCase) >= 0)
                return "pronunciation dictionary";
            if (ext == ".tsv")
                return "speech dictionary/table";
            return "speech engine support data";
        }

        private static void AddNvdaSpeechEngineInfo(ReportSection section, string path, byte[] header)
        {
            Add(section, "Vendor/family", NvdaSpeechFamily(path));
            Add(section, "Component", NvdaSpeechComponent(path));

            var manifest = FindNearestAddonManifest(path);
            if (manifest != null)
            {
                var manifestText = SafeReadTextFile(manifest, 64 * 1024);
                AddIniTopLevelValue(section, manifestText, "name", "Add-on name");
                AddIniTopLevelValue(section, manifestText, "summary", "Add-on summary");
                AddIniTopLevelValue(section, manifestText, "version", "Add-on version");
                AddNvdaSpeechManifestVersion(section, manifestText);
            }

            if (LooksLikeSuperTonicSpeechData(path, header))
                AddSuperTonicDetails(section, path, header);
            else if (LooksLikeOrpheusSpeechData(path, header))
                AddOrpheusDetails(section, path, header);
            else if (LooksLikeRhVoiceData(path, header))
                AddRhVoiceDetails(section, path, header);
            else if (LooksLikeFlexVoiceData(path, header))
                AddFlexVoiceDetails(section, path, header);
            else if (LooksLikeEspeakSpeechData(path, header))
                AddEspeakDetails(section, path, header);
            else if (LooksLikeText(header))
                AddSpeechTextClues(section, header);

            Add(section, "Notes", "NVDA speech-engine files can be open-source voice data, neural models, dictionaries, or wrappers around commercial engines such as Eloquence, IBM TTS, RHVoice, eSpeak, Orpheus, and SuperTonic. FileDentify reports known add-on paths, package manifests, filenames, and safe text/header metadata; it does not load synthesizers, run engine components, or decode proprietary voice payloads.");
        }

        private static string NvdaSpeechFamily(string path)
        {
            if (path.IndexOf("\\supertonic\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "SuperTonic neural TTS";
            if (path.IndexOf("\\orpheus\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Dolphin Orpheus TTS";
            if (path.IndexOf("\\IBMTTS\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "IBM TTS";
            if (path.IndexOf("\\Eloquence\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Eloquence";
            if (path.IndexOf("\\RHVoice", StringComparison.OrdinalIgnoreCase) >= 0)
                return "RHVoice";
            if (path.IndexOf("\\Flexvoice\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Mindmaker FlexVoice";
            if (path.IndexOf("\\espeak-ng-data\\", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("\\espeak-data\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "eSpeak NG";
            if (path.IndexOf("\\stspeech\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "STSpeech";
            if (path.IndexOf("\\BestSpeech\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "BestSpeech";
            if (path.IndexOf("\\tgSpeechBox\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "tgSpeechBox";
            return "NVDA speech engine";
        }

        private static string NvdaSpeechComponent(string path)
        {
            var lower = path.ToLowerInvariant();
            if (lower.Contains("\\synthdrivers\\"))
                return SegmentAfter(path, "synthDrivers");
            if (lower.Contains("\\data\\"))
                return SegmentAfter(path, "data");
            if (lower.Contains("\\langdata\\"))
                return SegmentAfter(path, "langdata");
            return Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);
        }

        private static void AddSuperTonicDetails(ReportSection section, string path, byte[] header)
        {
            if (Path.GetExtension(path).Equals(".onnx", StringComparison.OrdinalIgnoreCase))
            {
                var visible = FindReadableTextLines(header, 3, 80)
                    .Where(line => line.IndexOf("tts.", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        line.IndexOf("pytorch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        line.IndexOf("normalizer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        line.IndexOf("vocoder", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Take(12)
                    .ToArray();
                if (visible.Length > 0)
                    Add(section, "Model strings", string.Join(Environment.NewLine, visible));
            }
        }

        private static void AddOrpheusDetails(ReportSection section, string path, byte[] header)
        {
            var languageFolder = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);
            if (Regex.IsMatch(languageFolder ?? string.Empty, @"^\d{5}$"))
                Add(section, "Language folder", languageFolder);

            var unicode = DecodeUtf16LeFromHeader(header);
            if (!string.IsNullOrWhiteSpace(unicode))
            {
                var lines = unicode.Split(new[] { '\r', '\n', '\0' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(CleanMetadataText)
                    .Where(v => v.Length > 1)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(12)
                    .ToArray();
                if (lines.Length > 0)
                    Add(section, "Visible language strings", string.Join(Environment.NewLine, lines));
            }
        }

        private static void AddRhVoiceDetails(ReportSection section, string path, byte[] header)
        {
            var voiceFolder = Regex.Match(path, @"RHVoice-voice-(?<name>[^\\]+)", RegexOptions.IgnoreCase);
            if (voiceFolder.Success)
                Add(section, "Voice package", CleanMetadataText(voiceFolder.Groups["name"].Value.Replace("-", " ")));
            var rateFolder = Regex.Match(path, @"\\data\\(?<rate>\d{4,6})\\", RegexOptions.IgnoreCase);
            if (rateFolder.Success)
                Add(section, "Data sample rate folder", rateFolder.Groups["rate"].Value + " Hz");
            if (LooksLikeText(header))
            {
                var text = DecodeWindowsText(header);
                AddColonValue(section, text, "HTS_VOICE_VERSION", "HTS voice version");
                AddColonValue(section, text, "SAMPLING_FREQUENCY", "Sampling frequency");
                AddColonValue(section, text, "FRAME_PERIOD", "Frame period");
                AddColonValue(section, text, "NUM_STATES", "State count");
                AddColonValue(section, text, "NUM_STREAMS", "Stream count");
            }
        }

        private static void AddFlexVoiceDetails(ReportSection section, string path, byte[] header)
        {
            var language = SegmentAfter(path, "synthDrivers");
            if (!string.IsNullOrWhiteSpace(language))
                Add(section, "Language/component path", language);
            var visible = FindReadableTextLines(header, 4, 80)
                .Where(line => line.IndexOf("FlexVoice", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("Dictionary", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("SpeechRecord", StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(12)
                .ToArray();
            if (visible.Length > 0)
                Add(section, "Visible format strings", string.Join(Environment.NewLine, visible));
        }

        private static void AddEspeakDetails(ReportSection section, string path, byte[] header)
        {
            var name = Path.GetFileName(path) ?? string.Empty;
            if (name.EndsWith("_dict", StringComparison.OrdinalIgnoreCase))
                Add(section, "Language code", name.Substring(0, Math.Max(0, name.Length - 5)));
            var parent = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);
            Add(section, "Data folder", parent);
            var visible = FindReadableTextLines(header, 3, 80)
                .Where(line => line.IndexOf("base", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("phon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("voice", StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(12)
                .ToArray();
            if (visible.Length > 0)
                Add(section, "Visible data strings", string.Join(Environment.NewLine, visible));
        }

        private static void AddSpeechTextClues(ReportSection section, byte[] header)
        {
            var lines = FindReadableTextLines(header, 4, 100)
                .Where(line => line.IndexOf("speech", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("voice", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("synth", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("dictionary", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("phon", StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(16)
                .ToArray();
            if (lines.Length > 0)
                Add(section, "Visible speech strings", string.Join(Environment.NewLine, lines));
        }

        private static string FindNearestAddonManifest(string path)
        {
            var dir = Path.GetDirectoryName(path);
            while (!string.IsNullOrWhiteSpace(dir))
            {
                var manifest = Path.Combine(dir, "manifest.ini");
                if (File.Exists(manifest))
                    return manifest;
                var parent = Directory.GetParent(dir);
                if (parent == null)
                    return null;
                if (dir.EndsWith("\\addons", StringComparison.OrdinalIgnoreCase))
                    return null;
                dir = parent.FullName;
            }
            return null;
        }

        private static string SafeReadTextFile(string path, int maxBytes)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                if (bytes.Length > maxBytes)
                    bytes = bytes.Take(maxBytes).ToArray();
                return DecodeWindowsText(bytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void AddIniTopLevelValue(ReportSection section, string text, string key, string label)
        {
            var match = Regex.Match(text ?? string.Empty, @"(?im)^\s*" + Regex.Escape(key) + @"\s*=\s*(?<value>[^\r\n]+)");
            if (match.Success)
                Add(section, label, CleanMetadataText(match.Groups["value"].Value));
        }

        private static void AddNvdaSpeechManifestVersion(ReportSection section, string text)
        {
            var match = Regex.Match(text ?? string.Empty, @"(?im)^\s*lastTestedNVDAVersion\s*=\s*(?<value>[^\r\n]+)");
            if (match.Success)
                Add(section, "Last tested NVDA", FormatNvdaManifestVersion(CleanMetadataText(match.Groups["value"].Value.Trim().Trim('"'))));
        }

        private static void AddColonValue(ReportSection section, string text, string key, string label)
        {
            var match = Regex.Match(text ?? string.Empty, @"(?im)^\s*" + Regex.Escape(key) + @"\s*:\s*(?<value>[^\r\n]+)");
            if (match.Success)
                Add(section, label, CleanMetadataText(match.Groups["value"].Value));
        }

        private static string DecodeUtf16LeFromHeader(byte[] header)
        {
            if (header == null || header.Length < 4)
                return string.Empty;
            try
            {
                return Encoding.Unicode.GetString(header);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string SpeechHeaderAscii(byte[] header, int maxBytes)
        {
            if (header == null || header.Length == 0)
                return string.Empty;
            var count = Math.Min(header.Length, maxBytes);
            var sb = new StringBuilder(count);
            for (var i = 0; i < count; i++)
                sb.Append(header[i] >= 32 && header[i] < 127 ? (char)header[i] : '.');
            return sb.ToString();
        }

        private static bool LooksLikeDolphinSpeechData(string path, byte[] header)
        {
            if (path.IndexOf("\\Dolphin\\", StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            var name = Path.GetFileName(path) ?? string.Empty;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (name.Equals("voices.ini", StringComparison.OrdinalIgnoreCase))
                return true;
            if (path.IndexOf("\\Sam\\VocExpr\\speech\\", StringComparison.OrdinalIgnoreCase) >= 0 && (ext == ".dat" || ext == ".ini" || ext == ".bin"))
                return true;
            if (path.IndexOf("\\orpheus\\", StringComparison.OrdinalIgnoreCase) >= 0 && (ext == ".tts" || ext == ".dat" || ext == ".ini" || ext == ".bin" || ext == ".phm"))
                return true;
            if (path.IndexOf("\\Optimised_Maps\\", StringComparison.OrdinalIgnoreCase) >= 0 && ext == ".dtl")
                return true;
            if (path.IndexOf("\\SnovaSuite\\defaults\\", StringComparison.OrdinalIgnoreCase) >= 0 && Regex.IsMatch(ext, @"^\.hk[ab][0-9]?$", RegexOptions.IgnoreCase))
                return true;
            return false;
        }

        private static string DolphinSpeechTypeName(string path, byte[] header)
        {
            var name = Path.GetFileName(path) ?? string.Empty;
            if (name.Equals("voices.ini", StringComparison.OrdinalIgnoreCase))
                return "Dolphin voice selection table";
            if (path.IndexOf("\\VocExpr\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Nuance Vocalizer Expressive speech data";
            if (path.IndexOf("\\orpheus\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Dolphin Orpheus speech data";
            if (path.IndexOf("\\Optimised_Maps\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Dolphin application map data";
            if (path.IndexOf("\\SnovaSuite\\defaults\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Dolphin default configuration data";
            return "Dolphin speech data";
        }

        private static void AddDolphinSpeechInfo(ReportSection section, string path, byte[] header, long fileLength)
        {
            Add(section, "Vendor/family", "Dolphin screen-reader speech");
            Add(section, "Component", DolphinComponentFromPath(path));

            if (LooksLikeText(header))
            {
                var text = DecodeWindowsText(header);
                if ((Path.GetFileName(path) ?? string.Empty).Equals("voices.ini", StringComparison.OrdinalIgnoreCase))
                {
                    var identifiers = Regex.Matches(text, @"(?im)identifier\s*=\s*(?<value>[^\r\n]+)")
                        .Cast<Match>()
                        .Select(m => CleanMetadataText(m.Groups["value"].Value))
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(24)
                        .ToArray();
                    if (identifiers.Length > 0)
                        Add(section, "Synthesizer identifiers", string.Join(Environment.NewLine, identifiers));
                    var languageSections = Regex.Matches(text, @"(?m)^\[[0-9]{5}\]").Count;
                    Add(section, "Language sections", languageSections.ToString(CultureInfo.InvariantCulture));
                    var voiceLines = Regex.Matches(text, @"(?m)^\d{2}\s*=\s*(?<value>[^\r\n]+)")
                        .Cast<Match>()
                        .Select(m => CleanMetadataText(m.Groups["value"].Value))
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(24)
                        .ToArray();
                    if (voiceLines.Length > 0)
                        Add(section, "First voice preferences", string.Join(Environment.NewLine, voiceLines));
                }
                else
                {
                    var lines = FindReadableTextLines(header, 4, 80)
                        .Where(line => line.IndexOf("voice", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("synth", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("language", StringComparison.OrdinalIgnoreCase) >= 0)
                        .Take(12)
                        .ToArray();
                    if (lines.Length > 0)
                        Add(section, "Visible speech strings", string.Join(Environment.NewLine, lines));
                }
            }

            Add(section, "Notes", "Dolphin speech files come from Dolphin screen-reader and assistive-technology components. Depending on the folder, they may be Dolphin Orpheus data, Nuance Vocalizer Expressive voice resources, application maps, or voice-selection tables. FileDentify reports known component paths and safe text metadata; it does not load synthesizer engines or install screen-reader components.");
        }

        private static string DolphinComponentFromPath(string path)
        {
            if (path.IndexOf("\\VocExpr\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Nuance Vocalizer Expressive";
            if (path.IndexOf("\\orpheus\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Orpheus";
            if (path.IndexOf("\\Optimised_Maps\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "application maps";
            if (path.IndexOf("\\SnovaSuite\\defaults\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "default keyboard/language data";
            if ((Path.GetFileName(path) ?? string.Empty).Equals("voices.ini", StringComparison.OrdinalIgnoreCase))
                return "voice selection";
            return "Dolphin speech";
        }

        private static string DolphinSpeechRole(string path)
        {
            var name = Path.GetFileName(path) ?? string.Empty;
            if (name.Equals("voices.ini", StringComparison.OrdinalIgnoreCase))
                return "language-to-synthesizer voice preference table";
            if (name.StartsWith("synth_", StringComparison.OrdinalIgnoreCase))
                return "synthesizer voice data";
            if (name.StartsWith("clc_", StringComparison.OrdinalIgnoreCase))
                return "language component data";
            if (name.StartsWith("uselect_", StringComparison.OrdinalIgnoreCase))
                return "unit selection data";
            return "speech component data";
        }

        private static bool LooksLikeMicrosoftSpeechVoice(string path, byte[] header)
        {
            var name = Path.GetFileName(path) ?? string.Empty;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if ((ext == ".msix" || ext == ".appx") && IsZipHeader(header) && name.StartsWith("MicrosoftWindows.Voice.", StringComparison.OrdinalIgnoreCase))
                return true;
            if (name.StartsWith("MicrosoftWindows.Voice.", StringComparison.OrdinalIgnoreCase) && IsZipHeader(header))
                return true;
            if (name.StartsWith("MSTTSLoc", StringComparison.OrdinalIgnoreCase) && (ext == ".dat" || ext == ".ini"))
                return true;
            if (name.Equals("Tokens.xml", StringComparison.OrdinalIgnoreCase) || name.Equals("phones.txt", StringComparison.OrdinalIgnoreCase) || name.Equals("punc.txt", StringComparison.OrdinalIgnoreCase))
                return IsLikelyMicrosoftTtsFolder(path);
            if ((ext == ".bin" || ext == ".dat") && IsLikelyMicrosoftTtsFolder(path))
                return name.IndexOf("decoder", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("encoder", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("vocoder", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.StartsWith("EnUS.", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("EnGB.", StringComparison.OrdinalIgnoreCase);
            return false;
        }

        private static bool IsLikelyMicrosoftTtsFolder(string path)
        {
            var lower = path.ToLowerInvariant();
            return lower.Contains("\\tts\\") ||
                lower.Contains("\\natural voice") ||
                lower.Contains("\\microsoftwindows.voice.") ||
                lower.Contains("\\microsoft speech") ||
                lower.Contains("\\mstts");
        }

        private static string MicrosoftSpeechVoiceTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var name = Path.GetFileName(path) ?? string.Empty;
            if ((ext == ".msix" || ext == ".appx") && IsZipHeader(header))
                return "Microsoft natural voice package";
            if (name.StartsWith("MSTTSLoc", StringComparison.OrdinalIgnoreCase))
                return "Microsoft speech lexicon/localisation data";
            if (ext == ".bin")
                return "Microsoft neural speech model data";
            if (ext == ".dat")
                return "Microsoft speech voice data";
            if (ext == ".xml" || ext == ".txt" || ext == ".ini")
                return "Microsoft speech voice metadata";
            return "Microsoft speech voice data";
        }

        private static string MicrosoftSpeechVoiceRole(string path)
        {
            var name = Path.GetFileName(path) ?? string.Empty;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".msix" || ext == ".appx")
                return "installable Windows natural voice package";
            if (name.IndexOf("decoder", StringComparison.OrdinalIgnoreCase) >= 0)
                return "neural decoder model";
            if (name.IndexOf("encoder", StringComparison.OrdinalIgnoreCase) >= 0)
                return "neural encoder model";
            if (name.IndexOf("vocoder", StringComparison.OrdinalIgnoreCase) >= 0)
                return "streaming vocoder model";
            if (name.StartsWith("MSTTSLoc", StringComparison.OrdinalIgnoreCase))
                return ext == ".ini" ? "voice locale configuration" : "voice locale data";
            if (name.Equals("Tokens.xml", StringComparison.OrdinalIgnoreCase))
                return "voice token metadata";
            if (name.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
                return "domain lexicon/data file";
            return "voice metadata";
        }

        private static void AddMicrosoftSpeechVoiceInfo(ReportSection section, string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if ((ext == ".msix" || ext == ".appx") && IsZipHeader(header))
            {
                Add(section, "Package kind", "Windows AppX/MSIX speech voice package");
                try
                {
                    using (var archive = ZipFile.OpenRead(path))
                    {
                        Add(section, "Package entries", archive.Entries.Count.ToString(CultureInfo.InvariantCulture));
                        var manifest = archive.GetEntry("AppxManifest.xml");
                        if (manifest != null)
                        {
                            var text = ReadZipEntryText(manifest, 256 * 1024);
                            Add(section, "Package identity name", ValueOrNotReported(FirstXmlAttribute(text, "Identity", "Name")));
                            Add(section, "Package version", ValueOrNotReported(FirstXmlAttribute(text, "Identity", "Version")));
                        }

                        var iniEntries = archive.Entries.Where(e => e.FullName.EndsWith(".INI", StringComparison.OrdinalIgnoreCase)).Take(12).Select(e => e.FullName).ToArray();
                        var dataEntries = archive.Entries.Where(e => e.FullName.EndsWith(".dat", StringComparison.OrdinalIgnoreCase) || e.FullName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)).ToArray();
                        if (iniEntries.Length > 0)
                            Add(section, "Voice configuration files", string.Join(Environment.NewLine, iniEntries));
                        if (dataEntries.Length > 0)
                        {
                            Add(section, "Voice data/model files", dataEntries.Length.ToString(CultureInfo.InvariantCulture));
                            var largest = dataEntries
                                .OrderByDescending(e => e.Length)
                                .Take(8)
                                .Select(e => e.FullName + " (" + FormatUnsignedBytes((ulong)e.Length) + ")")
                                .ToArray();
                            Add(section, "Largest voice payloads", string.Join(Environment.NewLine, largest));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Add(section, "Package read note", ex.Message);
                }
            }
            else if (LooksLikeText(header))
            {
                var text = DecodeWindowsText(header);
                AddIniValue(section, text, "Domain", "Number", "Domain count");
                AddIniValue(section, text, "TN", "TNScope", "Text normalization scope");
                AddIniValue(section, text, "TN", "TNBoundaryType", "Text normalization boundary");
                var domainFiles = Regex.Matches(text ?? string.Empty, @"(?im)^FileName\d+\s*=\s*(?<name>[^\r\n]+)")
                    .Cast<Match>()
                    .Select(m => m.Groups["name"].Value.Trim())
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Take(20)
                    .ToArray();
                if (domainFiles.Length > 0)
                    Add(section, "Referenced domain files", string.Join(Environment.NewLine, domainFiles));
            }

            Add(section, "Notes", "Microsoft speech voice files can be Windows AppX/MSIX voice packages, text-normalization data, lexicons, or neural model payloads. FileDentify reports package indexes, names, and safe text metadata only; it does not install voices or load speech models.");
        }

        private static void AddIniValue(ReportSection section, string text, string sectionName, string key, string label)
        {
            var pattern = @"(?ims)^\s*\[" + Regex.Escape(sectionName) + @"\]\s*(?<body>.*?)(^\s*\[|\z)";
            var match = Regex.Match(text ?? string.Empty, pattern);
            if (!match.Success)
                return;
            var value = Regex.Match(match.Groups["body"].Value, @"(?im)^\s*" + Regex.Escape(key) + @"\s*=\s*(?<value>[^\r\n]+)");
            if (value.Success)
                Add(section, label, CleanMetadataText(value.Groups["value"].Value));
        }

        private static bool LooksLikeAcapelaVoicePath(string path)
        {
            var ext = Path.GetExtension(path);
            return path.IndexOf("\\Acapela\\", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (ext.Equals(".qvcu", StringComparison.OrdinalIgnoreCase) ||
                 ext.Equals(".nuul216", StringComparison.OrdinalIgnoreCase) ||
                 ext.Equals(".clb", StringComparison.OrdinalIgnoreCase));
        }

        private static string AcapelaVoiceRole(string path)
        {
            var ext = Path.GetExtension(path);
            if (ext.Equals(".qvcu", StringComparison.OrdinalIgnoreCase))
                return Path.GetFileName(path).IndexOf("extras", StringComparison.OrdinalIgnoreCase) >= 0 ? "extra voice data" : "main voice data";
            if (ext.Equals(".nuul216", StringComparison.OrdinalIgnoreCase))
                return "compiled voice support/index data";
            if (ext.Equals(".clb", StringComparison.OrdinalIgnoreCase))
                return "speech library module";
            return "voice data";
        }

        private static bool LooksLikePiperVoicePath(string path)
        {
            return path.IndexOf("\\sonata\\voices\\piper\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (path.IndexOf("\\piper\\", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    (Path.GetExtension(path).Equals(".onnx", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(path).Equals("MODEL_CARD", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(path).Equals("piper-voices.json", StringComparison.OrdinalIgnoreCase)));
        }

        private static string PiperVoiceRole(string path)
        {
            var name = Path.GetFileName(path);
            if (name.Equals("encoder.onnx", StringComparison.OrdinalIgnoreCase))
                return "real-time voice encoder model";
            if (name.Equals("decoder.onnx", StringComparison.OrdinalIgnoreCase))
                return "real-time voice decoder model";
            if (name.Equals("MODEL_CARD", StringComparison.OrdinalIgnoreCase))
                return "voice model card";
            if (name.Equals("piper-voices.json", StringComparison.OrdinalIgnoreCase))
                return "Piper voice catalogue";
            if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
                return "voice configuration";
            return "voice model";
        }
    }
}
