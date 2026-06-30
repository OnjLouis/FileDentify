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
        private static string AiModelTypeName(string path, byte[] header)
        {
            if (StartsWith(header, Encoding.ASCII.GetBytes("GGUF")))
                return "GGUF machine-learning model";
            if (IsPyTorchCheckpoint(path, header))
                return "PyTorch model checkpoint";
            if (IsOllamaManifest(path, header))
                return "Ollama model manifest";
            if (IsOllamaBlobPath(path))
                return LooksLikeText(header) ? "Ollama metadata blob" : "Ollama model/blob layer";
            return null;
        }

        private static void AddAiModelInfo(List<ReportSection> sections, string path, byte[] header)
        {
            var type = AiModelTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "AI model / Ollama");
            Add(section, "Format hint", type);
            Add(section, "Role", OllamaRoleFromPath(path));
            Add(section, "File name", Path.GetFileName(path));

            if (StartsWith(header, Encoding.ASCII.GetBytes("GGUF")))
                AddGgufInfo(section, header);
            else if (IsPyTorchCheckpoint(path, header))
                AddPyTorchCheckpointInfo(section, path, header);
            else if (LooksLikeText(header))
                AddOllamaJsonInfo(section, Encoding.UTF8.GetString(header.Take(Math.Min(header.Length, 1024 * 1024)).ToArray()), path);

            Add(section, "Notes", "Ollama stores manifests and content-addressed blobs under .ollama. GGUF and PyTorch checkpoint files are large local model weights; FileDentify reports header and visible metadata without loading the model.");
        }

        private static bool IsPyTorchCheckpoint(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".ckpt" && ext != ".pt" && ext != ".pth" && ext != ".safetensors")
                return false;
            if (ext == ".safetensors")
                return header.Length >= 8;
            return IsZipHeader(header) || (header.Length > 0 && header[0] == 0x80);
        }

        private static void AddPyTorchCheckpointInfo(ReportSection section, string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            Add(section, "Model file", Path.GetFileName(path));
            if (ext == ".safetensors")
            {
                Add(section, "Container", "SafeTensors tensor file");
                if (header.Length >= 8)
                    Add(section, "Declared JSON header size", FormatUnsignedBytes(ReadUInt64LittleEndian(header, 0)));
                return;
            }

            if (IsZipHeader(header))
            {
                Add(section, "Container", "ZIP-based PyTorch checkpoint");
                try
                {
                    using (var archive = ZipFile.OpenRead(path))
                    {
                        Add(section, "Archive entries", archive.Entries.Count.ToString(CultureInfo.InvariantCulture));
                        var entries = archive.Entries
                            .Select(e => e.FullName)
                            .Where(name => !string.IsNullOrWhiteSpace(name))
                            .Take(20)
                            .ToArray();
                        if (entries.Length > 0)
                            Add(section, "First entries", string.Join(Environment.NewLine, entries));

                        var dataPickle = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith("/data.pkl", StringComparison.OrdinalIgnoreCase) || e.FullName.Equals("data.pkl", StringComparison.OrdinalIgnoreCase));
                        if (dataPickle != null)
                            Add(section, "Pickle metadata entry", dataPickle.FullName + " (" + FormatUnsignedBytes((ulong)dataPickle.Length) + ")");
                        if (archive.GetEntry("version") != null || archive.Entries.Any(e => e.FullName.EndsWith("/version", StringComparison.OrdinalIgnoreCase)))
                            Add(section, "Checkpoint version marker", "present");
                    }
                }
                catch (Exception ex)
                {
                    Add(section, "Archive read note", ex.Message);
                }
            }
            else if (header.Length > 0 && header[0] == 0x80)
            {
                Add(section, "Container", "Python pickle-based checkpoint");
                Add(section, "Pickle protocol", header.Length > 1 ? header[1].ToString(CultureInfo.InvariantCulture) : "unknown");
                var keys = FindReadableTextLines(header, 4, 80)
                    .Where(IsUsefulCheckpointString)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(20)
                    .ToArray();
                if (keys.Length > 0)
                    Add(section, "Visible tensor or module names", string.Join(Environment.NewLine, keys));
            }
        }

        private static bool IsUsefulCheckpointString(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 160)
                return false;
            return value.IndexOf("weight", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("bias", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("layer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("model", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("torch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("state_dict", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsOllamaManifest(string path, byte[] header)
        {
            if (path.IndexOf("\\.ollama\\models\\manifests\\", StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            if (!LooksLikeText(header))
                return false;
            var text = Encoding.ASCII.GetString(header.Take(Math.Min(header.Length, 512)).ToArray());
            return text.IndexOf("\"schemaVersion\"", StringComparison.OrdinalIgnoreCase) >= 0 &&
                text.IndexOf("application/vnd.docker.distribution.manifest", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsOllamaBlobPath(string path)
        {
            return path.IndexOf("\\.ollama\\models\\blobs\\sha256-", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string OllamaRoleFromPath(string path)
        {
            if (path.IndexOf("\\manifests\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "model manifest";
            if (path.IndexOf("\\blobs\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "content-addressed blob";
            if (path.IndexOf("\\logs\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Ollama log";
            return "Ollama data";
        }

        private static void AddGgufInfo(ReportSection section, byte[] header)
        {
            Add(section, "Magic", "GGUF");
            if (header.Length >= 24)
            {
                Add(section, "Version", ReadUInt32LittleEndian(header, 4).ToString(CultureInfo.InvariantCulture));
                Add(section, "Tensor count", ReadUInt64LittleEndian(header, 8).ToString(CultureInfo.InvariantCulture));
                Add(section, "Metadata key-value count", ReadUInt64LittleEndian(header, 16).ToString(CultureInfo.InvariantCulture));
            }

            var keys = FindReadableTextLines(header, 4, 120)
                .Where(IsUsefulGgufMetadataKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(30)
                .ToArray();
            if (keys.Length > 0)
                Add(section, "Visible metadata keys", string.Join(Environment.NewLine, keys));
        }

        private static void AddOllamaJsonInfo(ReportSection section, string text, string path)
        {
            AddJsonValue(section, text, "schemaVersion", "Schema version");
            AddJsonValue(section, text, "mediaType", "Media type");
            AddJsonValue(section, text, "model_format", "Model format");
            AddJsonValue(section, text, "model_family", "Model family");
            AddJsonValue(section, text, "model_type", "Model type");
            AddJsonValue(section, text, "file_type", "File type");
            AddJsonValue(section, text, "architecture", "Architecture");
            AddJsonValue(section, text, "os", "Operating system");

            var layers = Regex.Matches(text ?? string.Empty, "\"digest\"\\s*:\\s*\"sha256:(?<digest>[0-9a-f]{64})\"", RegexOptions.IgnoreCase);
            if (layers.Count > 0)
                Add(section, "Referenced SHA-256 layers", layers.Count.ToString(CultureInfo.InvariantCulture));

            var model = OllamaModelNameFromManifestPath(path);
            if (!string.IsNullOrWhiteSpace(model))
                Add(section, "Model tag", model);
        }

        private static void AddJsonValue(ReportSection section, string text, string key, string title)
        {
            var match = Regex.Match(text ?? string.Empty, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(\"(?<string>[^\"]*)\"|(?<number>-?\\d+))", RegexOptions.IgnoreCase);
            if (!match.Success)
                return;
            var value = match.Groups["string"].Success ? match.Groups["string"].Value : match.Groups["number"].Value;
            if (!string.IsNullOrWhiteSpace(value))
                Add(section, title, CleanMetadataText(value));
        }

        private static string OllamaModelNameFromManifestPath(string path)
        {
            var marker = "\\models\\manifests\\";
            var index = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return string.Empty;
            var tail = path.Substring(index + marker.Length);
            return tail.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }

        private static bool IsUsefulGgufMetadataKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 140)
                return false;
            return value.StartsWith("general.", StringComparison.OrdinalIgnoreCase) ||
                value.IndexOf(".attention.", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf(".context_length", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("tokenizer.", StringComparison.OrdinalIgnoreCase) >= 0;
        }

    }
}
