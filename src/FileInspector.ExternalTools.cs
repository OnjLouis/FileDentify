using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace FileDentify
{
    internal static partial class FileInspector
    {        private static void AddZipHints(ReportSection section, string path)
        {
            try
            {
                using (var archive = ZipFile.OpenRead(path))
                {
                    var hasAbletonBundleInfo = archive.GetEntry("BundleInfo.json") != null;
                    var hasAbletonSong = archive.GetEntry("Song.abl") != null;
                    Add(section, "ZIP entries", archive.Entries.Count.ToString(CultureInfo.InvariantCulture));
                    var dirs = archive.Entries.Count(e => e.FullName.EndsWith("/", StringComparison.Ordinal));
                    var compressed = archive.Entries.Sum(e => e.CompressedLength);
                    var uncompressed = archive.Entries.Sum(e => e.Length);
                    Add(section, "Directory entries", dirs.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Compressed payload total", FormatBytes(compressed));
                    Add(section, "Uncompressed payload total", FormatBytes(uncompressed));
                    if (compressed > 0)
                        Add(section, "Overall expansion ratio", ((double)uncompressed / compressed).ToString("0.00", CultureInfo.InvariantCulture) + "x");
                    var names = archive.Entries.Take(30).Select(e => e.FullName + (e.FullName.EndsWith("/", StringComparison.Ordinal) ? "" : " (" + FormatBytes(e.Length) + ")")).ToArray();
                    Add(section, "First entries", names.Length == 0 ? "(empty archive)" : string.Join("\r\n", names));
                    if (archive.GetEntry("[Content_Types].xml") != null)
                        Add(section, "Office Open XML hint", "The archive contains [Content_Types].xml, common in docx/xlsx/pptx packages.");
                    if (archive.GetEntry("META-INF/MANIFEST.MF") != null)
                        Add(section, "Java/JAR hint", "The archive contains META-INF/MANIFEST.MF.");
                    if (archive.Entries.Any(e => e.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)))
                        Add(section, "Open Packaging Convention hint", "The archive contains .rels relationship files.");
                    if (string.Equals(Path.GetExtension(path), ".ablbundle", StringComparison.OrdinalIgnoreCase) || (hasAbletonBundleInfo && hasAbletonSong))
                        Add(section, "Ableton bundle hint", "The archive looks like an Ableton Move/Live bundle, with project metadata and embedded samples.");
                }
            }
            catch (Exception ex)
            {
                Add(section, "ZIP read error", ex.Message);
            }
        }

        private static void AddExternalToolInfo(List<ReportSection> sections, string path)
        {
            if (!ShouldRunFfprobe(path))
                return;
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var ffprobe = Path.Combine(appDir, "ffprobe.exe");
            if (!File.Exists(ffprobe))
                return;

            var output = RunTool(ffprobe, "-hide_banner -v error -show_format -show_streams \"" + path + "\"", 8000);
            if (string.IsNullOrWhiteSpace(output))
                return;
            if (output.IndexOf("Invalid data found", StringComparison.OrdinalIgnoreCase) >= 0)
                return;
            AddFfprobeSummary(sections, output);
            var media = AddSection(sections, "ffprobe");
            Add(media, "Output", output.Trim());
        }

        private static void AddFfprobeSummary(List<ReportSection> sections, string output)
        {
            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var format = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var tags = new List<string>();
            var streams = new List<Dictionary<string, string>>();
            Dictionary<string, string> current = null;
            var inFormat = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line == "[STREAM]")
                {
                    current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    streams.Add(current);
                    inFormat = false;
                    continue;
                }
                if (line == "[/STREAM]")
                {
                    current = null;
                    continue;
                }
                if (line == "[FORMAT]")
                {
                    inFormat = true;
                    current = null;
                    continue;
                }
                if (line == "[/FORMAT]")
                {
                    inFormat = false;
                    continue;
                }

                var equals = line.IndexOf('=');
                if (equals <= 0)
                    continue;
                var key = line.Substring(0, equals);
                var value = line.Substring(equals + 1);
                if (string.IsNullOrWhiteSpace(value) || value == "N/A")
                    continue;
                if (key.StartsWith("TAG:", StringComparison.OrdinalIgnoreCase))
                {
                    var tagName = key.Substring(4);
                    if (!string.IsNullOrWhiteSpace(tagName))
                        tags.Add(tagName + "=" + value);
                    continue;
                }
                if (current != null)
                    current[key] = value;
                else if (inFormat)
                    format[key] = value;
            }

            if (format.Count == 0 && streams.Count == 0 && tags.Count == 0)
                return;

            var section = AddSection(sections, "Media details");
            AddDictionaryValue(section, format, "format_long_name", "Format");
            AddDictionaryDuration(section, format, "duration", "Duration");
            AddDictionaryBitRate(section, format, "bit_rate", "Bit rate");
            AddDictionaryValue(section, format, "nb_streams", "Streams");

            var streamLines = new List<string>();
            for (var i = 0; i < streams.Count && i < 8; i++)
            {
                var stream = streams[i];
                string codecType;
                string codecName;
                stream.TryGetValue("codec_type", out codecType);
                stream.TryGetValue("codec_name", out codecName);
                var parts = new List<string>();
                parts.Add("Stream " + i.ToString(CultureInfo.InvariantCulture));
                if (!string.IsNullOrWhiteSpace(codecType))
                    parts.Add(codecType);
                if (!string.IsNullOrWhiteSpace(codecName))
                    parts.Add(codecName);
                AddStreamPart(parts, stream, "sample_rate", "Hz");
                AddStreamPart(parts, stream, "channels", "channels");
                AddStreamPart(parts, stream, "width", "wide");
                AddStreamPart(parts, stream, "height", "high");
                string streamDuration;
                if (stream.TryGetValue("duration", out streamDuration))
                {
                    double seconds;
                    if (double.TryParse(streamDuration, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
                        parts.Add(FormatDuration(seconds));
                }
                streamLines.Add(string.Join(", ", parts.ToArray()));
            }
            if (streamLines.Count > 0)
                Add(section, "Stream summary", string.Join("\r\n", streamLines.ToArray()));
            if (tags.Count > 0)
                Add(section, "Tags", string.Join("\r\n", tags.Take(24).ToArray()));
        }

        private static void AddDictionaryValue(ReportSection section, Dictionary<string, string> values, string key, string label)
        {
            string value;
            if (values.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value))
                Add(section, label, value);
        }

        private static void AddDictionaryDuration(ReportSection section, Dictionary<string, string> values, string key, string label)
        {
            string value;
            double seconds;
            if (values.TryGetValue(key, out value) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
                Add(section, label, FormatDuration(seconds));
        }

        private static void AddDictionaryBitRate(ReportSection section, Dictionary<string, string> values, string key, string label)
        {
            string value;
            long bitsPerSecond;
            if (values.TryGetValue(key, out value) && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out bitsPerSecond))
                Add(section, label, FormatBitRate(bitsPerSecond));
        }

        private static void AddStreamPart(List<string> parts, Dictionary<string, string> stream, string key, string suffix)
        {
            string value;
            if (stream.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value))
                parts.Add(value + " " + suffix);
        }

        private static string FormatBitRate(long bitsPerSecond)
        {
            if (bitsPerSecond >= 1000000)
                return (bitsPerSecond / 1000000.0).ToString("0.###", CultureInfo.InvariantCulture) + " Mbps";
            if (bitsPerSecond >= 1000)
                return (bitsPerSecond / 1000.0).ToString("0.###", CultureInfo.InvariantCulture) + " kbps";
            return bitsPerSecond.ToString(CultureInfo.InvariantCulture) + " bps";
        }

        private static void AddCompanionToolInfo(List<ReportSection> sections, string path)
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();

            if (ext == "flac")
            {
                var metaflac = Path.Combine(appDir, "metaflac.exe");
                if (File.Exists(metaflac))
                {
                    var output = RunTool(metaflac, "--list --block-number=0 \"" + path + "\"", 8000);
                    if (!string.IsNullOrWhiteSpace(output))
                        Add(AddSection(sections, "metaflac"), "STREAMINFO", TrimToolOutput(output, 12000));
                }
            }

            if (ext == "opus" || ext == "ogg" || ext == "oga")
            {
                var opusinfo = Path.Combine(appDir, "opusinfo.exe");
                if (File.Exists(opusinfo))
                {
                    var output = RunTool(opusinfo, "\"" + path + "\"", 8000);
                    if (!string.IsNullOrWhiteSpace(output) && output.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) < 0)
                        Add(AddSection(sections, "opusinfo"), "Output", TrimToolOutput(output, 12000));
                }
            }

            var vgmstream = Path.Combine(appDir, "vgmstream-cli.exe");
            if (File.Exists(vgmstream) && IsLikelyGameAudioExtension(ext))
            {
                var output = RunTool(vgmstream, "-m \"" + path + "\"", 8000);
                if (!string.IsNullOrWhiteSpace(output) && output.IndexOf("failed", StringComparison.OrdinalIgnoreCase) < 0 && output.IndexOf("unsupported", StringComparison.OrdinalIgnoreCase) < 0)
                    Add(AddSection(sections, "vgmstream"), "Metadata", TrimToolOutput(output, 12000));
            }
        }

        private static bool IsLikelyGameAudioExtension(string ext)
        {
            var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "adx", "brstm", "dsp", "fsb", "hca", "msf", "nus3audio", "rsd", "str", "vgmstream", "wem", "xma", "xvag"
            };
            return exts.Contains(ext);
        }

        private static string TrimToolOutput(string output, int maxChars)
        {
            output = (output ?? string.Empty).Trim();
            if (output.Length <= maxChars)
                return output;
            return output.Substring(0, maxChars) + Environment.NewLine + "[Output truncated]";
        }

        private static bool ShouldRunFfprobe(string path)
        {
            var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            var mediaExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "aac", "aif", "aiff", "alac", "ape", "avi", "flac", "flv", "m4a", "m4v", "mkv", "mov", "mp3", "mp4", "mpeg", "mpg", "oga", "ogg", "opus", "ts", "wav", "webm", "wma", "wmv"
            };
            return mediaExtensions.Contains(ext);
        }

        private static string RunTool(string exe, string args, int timeoutMs)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (var process = Process.Start(psi))
                {
                    if (!process.WaitForExit(timeoutMs))
                    {
                        try { process.Kill(); }
                        catch { }
                        return string.Empty;
                    }
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    if (!string.IsNullOrWhiteSpace(output))
                        return TrimToolOutput(output, 20000);
                    return TrimToolOutput(error, 4000);
                }
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}

