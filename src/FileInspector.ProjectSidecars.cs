using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace FileDentify
{
    internal static partial class FileInspector
    {
        private static string ProjectSidecarTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (IsProToolsSession(path, header)) return "Pro Tools session";
            if (IsProToolsWaveCache(path, header)) return "Pro Tools waveform cache";
            if (IsProToolsOverview(path)) return "Pro Tools waveform overview sidecar";
            if (IsMoonShellAsset(path, header)) return MoonShellTypeName(path, header);
            if (IsIKaossilatorFile(path)) return "Korg iKaossilator project/index data";
            if (ext == ".logikcs") return "Logic Pro key commands";
            return null;
        }

        private static void AddProjectSidecarInfo(List<ReportSection> sections, string path, byte[] header, byte[] stringSample, long fileLength)
        {
            if (IsProToolsSession(path, header) || IsProToolsWaveCache(path, header) || IsProToolsOverview(path))
                AddProToolsInfo(sections, path, header, stringSample, fileLength);
            if (IsMoonShellAsset(path, header))
                AddMoonShellInfo(sections, path, header, stringSample, fileLength);
            if (IsIKaossilatorFile(path))
                AddIKaossilatorInfo(sections, path, stringSample, fileLength);
            if (Path.GetExtension(path).Equals(".logikcs", StringComparison.OrdinalIgnoreCase))
                AddLogicKeyCommandsInfo(sections, path, stringSample);
        }

        private static bool IsProToolsSession(string path, byte[] header)
        {
            if (!Path.GetExtension(path).Equals(".ptx", StringComparison.OrdinalIgnoreCase))
                return false;
            var text = AsciiPreview(header, Math.Min(header.Length, 2048));
            return text.IndexOf("Pro Tools", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("Session File", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsProToolsWaveCache(string path, byte[] header)
        {
            return Path.GetFileName(path).Equals("WaveCache.wfm", StringComparison.OrdinalIgnoreCase) ||
                StartsWith(header, Encoding.ASCII.GetBytes("DDZCHX"));
        }

        private static bool IsProToolsOverview(string path)
        {
            var ext = Path.GetExtension(path);
            return ext.Equals(".spn", StringComparison.OrdinalIgnoreCase) &&
                path.IndexOf("ProTools", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddProToolsInfo(List<ReportSection> sections, string path, byte[] header, byte[] stringSample, long fileLength)
        {
            var section = AddSection(sections, "Pro Tools");
            if (IsProToolsSession(path, header))
            {
                Add(section, "Format hint", "Pro Tools session file");
                Add(section, "Extension", ".ptx");
                var strings = FindAsciiStrings(header, 4, 80).Select(item => item.Value.Trim()).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToArray();
                if (strings.Length > 0)
                    Add(section, "Visible header strings", string.Join(Environment.NewLine, strings));
            }
            else if (IsProToolsWaveCache(path, header))
            {
                Add(section, "Format hint", "Pro Tools waveform cache");
                if (StartsWith(header, Encoding.ASCII.GetBytes("DDZCHX")))
                    Add(section, "Header marker", "DDZCHX");
                var strings = FindAsciiStrings(header, 6, 60).Select(item => item.Value.Trim()).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(10).ToArray();
                if (strings.Length > 0)
                    Add(section, "Visible cache strings", string.Join(Environment.NewLine, strings));
            }
            else
            {
                Add(section, "Format hint", "Pro Tools waveform overview or sidecar");
                Add(section, "Extension", Path.GetExtension(path));
            }

            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "Project folder", NearestNamedFolder(path, "ProTools"));
            Add(section, "Notes", "Pro Tools project files and waveform caches are proprietary. FileDentify reports visible session/cache clues only; it does not load the session or decode audio overviews.");
        }

        private static bool IsMoonShellAsset(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (StartsWith(header, Encoding.ASCII.GetBytes("MSP\0")))
                return true;
            if (path.IndexOf("moonshl", StringComparison.OrdinalIgnoreCase) < 0 &&
                path.IndexOf("MoonShell", StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            return ext == ".msp" || ext == ".mse" || ext == ".b15" || ext == ".u8m" || ext == ".glf" || ext == ".l2u";
        }

        private static string MoonShellTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (StartsWith(header, Encoding.ASCII.GetBytes("MSP\0")) || ext == ".msp") return "MoonShell plugin module";
            if (ext == ".u8m") return "MoonShell UI sound";
            if (ext == ".b15") return "MoonShell skin bitmap";
            if (ext == ".glf") return "MoonShell bitmap font";
            if (ext == ".l2u") return "MoonShell language/support data";
            if (ext == ".mse") return "MoonShell executable/plugin support file";
            return "MoonShell/R4 support file";
        }

        private static void AddMoonShellInfo(List<ReportSection> sections, string path, byte[] header, byte[] stringSample, long fileLength)
        {
            var section = AddSection(sections, "MoonShell/R4");
            var ext = Path.GetExtension(path).ToLowerInvariant();
            Add(section, "Format hint", MoonShellTypeName(path, header));
            Add(section, "Extension", string.IsNullOrEmpty(ext) ? "(none)" : ext);
            Add(section, "Role", MoonShellRole(path, header));
            Add(section, "File size", FormatBytes(fileLength));

            if (StartsWith(header, Encoding.ASCII.GetBytes("MSP\0")))
            {
                Add(section, "Header marker", "MSP");
                var strings = FindAsciiStrings(stringSample, 3, 80).Select(item => item.Value.Trim()).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToArray();
                if (strings.Length > 0)
                    Add(section, "Visible plugin strings", string.Join(Environment.NewLine, strings));
            }
            else if (ext == ".u8m")
            {
                Add(section, "Audio hint", "Small unsigned 8-bit style UI sound payload used by MoonShell themes.");
            }
            else if (ext == ".b15" && header.Length >= 4)
            {
                Add(section, "Likely dimensions", ReadProjectSidecarUInt16LittleEndian(header, 0).ToString(CultureInfo.InvariantCulture) + " x " + ReadProjectSidecarUInt16LittleEndian(header, 2).ToString(CultureInfo.InvariantCulture));
            }

            Add(section, "Notes", "MoonShell/R4 files are Nintendo DS flashcart media-player plugins, theme assets, fonts, and UI sounds. FileDentify reports header/path clues only; it does not run DS code or render custom assets.");
        }

        private static string MoonShellRole(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var file = Path.GetFileName(path);
            if (ext == ".msp") return "Media/player plugin";
            if (ext == ".mse") return "Executable/plugin support";
            if (ext == ".u8m") return "Theme/UI sound";
            if (ext == ".b15") return "Theme bitmap/control graphic";
            if (ext == ".glf") return "Bitmap font";
            if (ext == ".l2u") return "Language or loader support";
            return file;
        }

        private static bool IsIKaossilatorFile(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".loopparam" || ext == ".progindex" ||
                path.IndexOf(".ikaoss.d", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddIKaossilatorInfo(List<ReportSection> sections, string path, byte[] stringSample, long fileLength)
        {
            var section = AddSection(sections, "iKaossilator");
            var ext = Path.GetExtension(path).ToLowerInvariant();
            Add(section, "Format hint", ext == ".progindex" ? "Korg iKaossilator program index" : ext == ".loopparam" ? "Korg iKaossilator loop parameter data" : "Korg iKaossilator support data");
            Add(section, "File size", FormatBytes(fileLength));
            var refs = FindAsciiStrings(stringSample, 4, 80)
                .Select(item => item.Value.Trim())
                .Where(value => value.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || value.EndsWith(".aif", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .ToArray();
            if (refs.Length > 0)
                Add(section, "Referenced loops", string.Join(Environment.NewLine, refs));
            Add(section, "Notes", "iKaossilator files are mobile Korg project/index sidecars. FileDentify reports visible loop references and file roles only; it does not reconstruct the performance.");
        }

        private static void AddLogicKeyCommandsInfo(List<ReportSection> sections, string path, byte[] stringSample)
        {
            var section = AddSection(sections, "Logic Pro");
            Add(section, "Format hint", "Logic Pro key commands plist");
            Add(section, "Extension", ".logikcs");
            var text = Encoding.UTF8.GetString(stringSample.Take(Math.Min(stringSample.Length, 512 * 1024)).ToArray());
            var commandCount = RegexCount(text, "<key>");
            if (commandCount > 0)
                Add(section, "Visible plist key count", commandCount.ToString(CultureInfo.InvariantCulture));
            Add(section, "Notes", "Logic key-command files are property-list based shortcut/configuration exports. FileDentify reports structure only; it does not import or change Logic settings.");
        }

        private static string NearestNamedFolder(string path, string folderName)
        {
            var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            for (var i = parts.Length - 1; i >= 0; i--)
                if (parts[i].Equals(folderName, StringComparison.OrdinalIgnoreCase))
                    return string.Join(Path.DirectorySeparatorChar.ToString(), parts.Take(i + 1).ToArray());
            return string.Empty;
        }

        private static int ReadProjectSidecarUInt16LittleEndian(byte[] data, int offset)
        {
            if (data == null || offset < 0 || offset + 1 >= data.Length)
                return 0;
            return data[offset] | (data[offset + 1] << 8);
        }

        private static int RegexCount(string text, string pattern)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
            return System.Text.RegularExpressions.Regex.Matches(text, pattern).Count;
        }
    }
}
