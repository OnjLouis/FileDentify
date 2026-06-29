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
        private static string SpeechVoiceTypeName(string path, byte[] header)
        {
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
            Add(section, "Role", PiperVoiceRole(path));

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
