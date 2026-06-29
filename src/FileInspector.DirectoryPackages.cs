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
        public static bool IsReportableDirectoryPackage(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".logicx":
                case ".logic":
                case ".band":
                case ".garageband":
                case ".sparsebundle":
                case ".app":
                case ".framework":
                case ".bundle":
                case ".plugin":
                case ".appex":
                case ".kext":
                case ".prefpane":
                case ".component":
                case ".vst":
                case ".vst3":
                case ".clap":
                case ".aaxplugin":
                case ".lunacomponent":
                    return Directory.Exists(path);
                default:
                    return IsAppleMobileBackupDirectory(path) || IsNintendoSwitchContentDirectory(path);
            }
        }

        private static FileReport InspectDirectoryPackage(string path)
        {
            var dir = new DirectoryInfo(path);
            var report = new FileReport();
            report.DisplayName = dir.Name;
            report.OriginalPath = dir.FullName;

            var sections = report.Sections;
            var summary = AddSection(sections, "Summary");
            Add(summary, "Likely type", DirectoryPackageTypeName(dir.FullName));
            Add(summary, "Path", dir.FullName);
            Add(summary, "Extension", string.IsNullOrEmpty(dir.Extension) ? "(none)" : dir.Extension);
            Add(summary, "Modified", dir.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            Add(summary, "Created", dir.CreationTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

            AddDirectoryPackageFilesystemInfo(sections, dir);
            AddLogicProjectPackageInfo(sections, dir.FullName);
            AddSparseBundleInfo(sections, dir.FullName);
            AddAppleBundleDirectoryInfo(sections, dir.FullName);
            AddMacAudioPluginDirectoryInfo(sections, dir.FullName);
            AddUniversalAudioLunaDirectoryInfo(sections, dir.FullName);
            AddAppleMobileBackupDirectoryInfo(sections, dir.FullName);
            AddNintendoSwitchContentDirectoryInfo(sections, dir.FullName);

            report.FullText = BuildReportText(report);
            return report;
        }

        private static string DirectoryPackageTypeName(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".logicx": return "Logic Pro project package";
                case ".logic": return "Logic Pro project package";
                case ".band": return "GarageBand project package";
                case ".garageband": return "GarageBand project package";
                case ".sparsebundle": return "Apple sparse bundle disk image";
                case ".app": return "macOS application bundle";
                case ".framework": return "macOS framework bundle";
                case ".bundle": return "macOS loadable bundle";
                case ".plugin": return "macOS plug-in bundle";
                case ".appex": return "Apple app extension bundle";
                case ".kext": return "macOS kernel extension bundle";
                case ".prefpane": return "macOS preference pane bundle";
                case ".component": return "Apple Audio Unit plug-in bundle";
                case ".vst": return "Mac VST plug-in bundle";
                case ".vst3": return "VST3 plug-in bundle";
                case ".clap": return "CLAP plug-in bundle";
                case ".aaxplugin": return "Avid AAX plug-in bundle";
                case ".lunacomponent": return "Universal Audio LUNA component bundle";
                default:
                    if (IsAppleMobileBackupDirectory(path))
                        return "Apple iPhone or iPad backup folder";
                    if (IsNintendoSwitchContentDirectory(path))
                        return "Nintendo Switch content folder";
                    return "Directory package";
            }
        }

        private static bool IsNintendoSwitchContentDirectory(string path)
        {
            if (!Directory.Exists(path))
                return false;
            if (!string.Equals(Path.GetFileName(path), "Nintendo", StringComparison.OrdinalIgnoreCase))
                return false;
            return Directory.Exists(Path.Combine(path, "Contents", "registered")) ||
                Directory.Exists(Path.Combine(path, "save")) ||
                Directory.Exists(Path.Combine(path, "Album"));
        }

        private static void AddNintendoSwitchContentDirectoryInfo(List<ReportSection> sections, string path)
        {
            if (!IsNintendoSwitchContentDirectory(path))
                return;

            var section = AddSection(sections, "Nintendo Switch content");
            Add(section, "Format hint", "Nintendo Switch console storage/content folder");
            Add(section, "Folder", Path.GetFileName(path));

            var registered = Path.Combine(path, "Contents", "registered");
            if (Directory.Exists(registered))
            {
                var ncaPackages = SafeDirectoryDirectoriesRecursive(registered, 10000)
                    .Where(dir => dir.EndsWith(".nca.CONCAT", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                Add(section, "Registered NCA packages", ncaPackages.Length.ToString(CultureInfo.InvariantCulture) + (ncaPackages.Length == 10000 ? " or more" : string.Empty));
                var segments = ncaPackages.SelectMany(SafeDirectoryFiles).ToArray();
                Add(section, "Registered NCA segments", segments.Length.ToString(CultureInfo.InvariantCulture));

                var largest = ncaPackages
                    .Select(dir => new { Path = dir, Size = SafeDirectorySize(dir, 128) })
                    .OrderByDescending(item => item.Size)
                    .Take(8)
                    .Select(item => RelativePath(path, item.Path) + " (" + FormatBytes(item.Size) + ")")
                    .ToArray();
                if (largest.Length > 0)
                    Add(section, "Largest sampled NCA packages", string.Join(Environment.NewLine, largest));
            }

            var saveDir = Path.Combine(path, "save");
            if (Directory.Exists(saveDir))
            {
                var saves = SafeDirectoryFiles(saveDir).ToArray();
                Add(section, "Save files", saves.Length.ToString(CultureInfo.InvariantCulture));
                var largestSaves = saves.OrderByDescending(SafeLength)
                    .Take(8)
                    .Select(file => Path.GetFileName(file) + " (" + FormatBytes(SafeLength(file)) + ")")
                    .ToArray();
                if (largestSaves.Length > 0)
                    Add(section, "Largest save files", string.Join(Environment.NewLine, largestSaves));
            }

            var albumDir = Path.Combine(path, "Album");
            if (Directory.Exists(albumDir))
            {
                var albumFiles = SafeDirectoryFilesRecursive(albumDir, 10000).ToArray();
                Add(section, "Album media files", albumFiles.Length.ToString(CultureInfo.InvariantCulture) + (albumFiles.Length == 10000 ? " or more" : string.Empty));
                var mediaCounts = albumFiles
                    .Select(file => Path.GetExtension(file))
                    .Where(ext => !string.IsNullOrWhiteSpace(ext))
                    .GroupBy(ext => ext, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(group => group.Count())
                    .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.Key + " " + group.Count().ToString(CultureInfo.InvariantCulture))
                    .ToArray();
                if (mediaCounts.Length > 0)
                    Add(section, "Album media extensions", string.Join(Environment.NewLine, mediaCounts));
            }

            Add(section, "Notes", "Nintendo Switch content folders can contain encrypted NCA content segments, save data, and album media. FileDentify reports folder structure and sizes only; it does not decrypt, validate, or extract game content.");
        }

        private static bool IsAppleMobileBackupDirectory(string path)
        {
            if (!Directory.Exists(path))
                return false;
            return File.Exists(Path.Combine(path, "Manifest.db")) &&
                File.Exists(Path.Combine(path, "Manifest.plist")) &&
                File.Exists(Path.Combine(path, "Info.plist")) &&
                File.Exists(Path.Combine(path, "Status.plist"));
        }

        private static void AddAppleMobileBackupDirectoryInfo(List<ReportSection> sections, string path)
        {
            if (!IsAppleMobileBackupDirectory(path))
                return;

            var section = AddSection(sections, "Apple mobile backup");
            Add(section, "Format hint", "Apple iPhone/iPad backup folder");
            Add(section, "Backup identifier", Path.GetFileName(path));

            foreach (var name in new[] { "Manifest.db", "Manifest.plist", "Info.plist", "Status.plist" })
            {
                var file = Path.Combine(path, name);
                if (File.Exists(file))
                    Add(section, name, "Present (" + FormatBytes(new FileInfo(file).Length) + ")");
            }

            var shardDirs = SafeDirectoryDirectories(path)
                .Select(Path.GetFileName)
                .Where(name => Regex.IsMatch(name ?? string.Empty, "^[0-9a-f]{2}$", RegexOptions.IgnoreCase))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            Add(section, "Hashed shard folders", shardDirs.Length.ToString(CultureInfo.InvariantCulture));

            var sampledFiles = SafeDirectoryFilesRecursive(path, 50000).ToArray();
            var hashedPayloads = sampledFiles.Count(IsAppleMobileBackupStoredFile);
            Add(section, "Sampled stored files", sampledFiles.Length.ToString(CultureInfo.InvariantCulture) + (sampledFiles.Length == 50000 ? " or more" : string.Empty));
            Add(section, "Sampled extensionless hashed files", hashedPayloads.ToString(CultureInfo.InvariantCulture));

            var largest = sampledFiles
                .Where(IsAppleMobileBackupStoredFile)
                .OrderByDescending(SafeLength)
                .Take(8)
                .Select(file => RelativePath(path, file) + " (" + FormatBytes(SafeLength(file)) + ")")
                .ToArray();
            if (largest.Length > 0)
                Add(section, "Largest sampled stored files", string.Join(Environment.NewLine, largest));

            var visibleExtensions = sampledFiles
                .Select(file => Path.GetExtension(file))
                .Where(ext => !string.IsNullOrWhiteSpace(ext))
                .GroupBy(ext => ext, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .Select(group => group.Key + " " + group.Count().ToString(CultureInfo.InvariantCulture))
                .ToArray();
            if (visibleExtensions.Length > 0)
                Add(section, "Visible extension counts", string.Join(Environment.NewLine, visibleExtensions));

            Add(section, "Lookup note", "Stored files are named by backup file ID. Manifest.db maps those IDs back to app domains and original paths; FileDentify identifies the backup structure without extracting personal content.");
            Add(section, "Privacy note", "Mobile backups can contain messages, photos, app data, keychain-related material, and other personal data. Share reports from this folder carefully.");
        }

        private static void AddDirectoryPackageFilesystemInfo(List<ReportSection> sections, DirectoryInfo dir)
        {
            var section = AddSection(sections, "Filesystem");
            Add(section, "Directory", dir.Parent == null ? string.Empty : dir.Parent.FullName);
            Add(section, "Base name", Path.GetFileNameWithoutExtension(dir.Name));
            Add(section, "Attributes", dir.Attributes.ToString());
            Add(section, "Modified UTC", dir.LastWriteTimeUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            Add(section, "Created UTC", dir.CreationTimeUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            Add(section, "Accessed UTC", dir.LastAccessTimeUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

            var stats = BoundedDirectoryStats(dir.FullName, 10000);
            Add(section, "Sampled file count", stats.FileCount.ToString(CultureInfo.InvariantCulture) + (stats.Truncated ? " or more" : string.Empty));
            Add(section, "Sampled folder count", stats.DirectoryCount.ToString(CultureInfo.InvariantCulture) + (stats.Truncated ? " or more" : string.Empty));
            Add(section, "Sampled size", FormatBytes(stats.TotalBytes) + (stats.Truncated ? " or more" : string.Empty));
        }

        private static void AddLogicProjectPackageInfo(List<ReportSection> sections, string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".logicx" && ext != ".logic" && ext != ".band" && ext != ".garageband")
                return;

            var section = AddSection(sections, ext == ".logicx" || ext == ".logic" ? "Logic Pro project" : "GarageBand project");
            Add(section, "Format hint", DirectoryPackageTypeName(path));
            Add(section, "Project name", Path.GetFileNameWithoutExtension(path));

            var projectInfo = Path.Combine(path, "Resources", "ProjectInformation.plist");
            if (File.Exists(projectInfo))
                Add(section, "ProjectInformation.plist", "Present (" + FormatBytes(new FileInfo(projectInfo).Length) + ")");

            var metadata = Path.Combine(path, "Alternatives", "000", "MetaData.plist");
            if (File.Exists(metadata))
            {
                Add(section, "MetaData.plist", "Present (" + FormatBytes(new FileInfo(metadata).Length) + ")");
                AddLogicMetadataStrings(section, metadata);
            }

            var projectData = Path.Combine(path, "Alternatives", "000", "ProjectData");
            if (File.Exists(projectData))
                Add(section, "ProjectData", FormatBytes(new FileInfo(projectData).Length));

            var audioDir = Path.Combine(path, "Media", "Audio Files");
            if (Directory.Exists(audioDir))
            {
                var audioFiles = SafeDirectoryFiles(audioDir).Take(20).ToArray();
                Add(section, "Audio files folder", audioFiles.Length.ToString(CultureInfo.InvariantCulture) + (audioFiles.Length == 20 ? " or more files" : " files"));
                if (audioFiles.Length > 0)
                    Add(section, "First audio files", string.Join(Environment.NewLine, audioFiles.Select(Path.GetFileName).ToArray()));
            }

            Add(section, "Notes", "Logic and GarageBand projects are macOS package folders. FileDentify reports package-level metadata and bounded internal structure; it does not parse the full arrangement.");
        }

        private static void AddSparseBundleInfo(List<ReportSection> sections, string path)
        {
            if (!string.Equals(Path.GetExtension(path), ".sparsebundle", StringComparison.OrdinalIgnoreCase))
                return;

            var section = AddSection(sections, "Apple sparse bundle");
            Add(section, "Format hint", "Apple sparse bundle disk image package");
            var info = Path.Combine(path, "Info.plist");
            if (File.Exists(info))
            {
                Add(section, "Info.plist", "Present (" + FormatBytes(new FileInfo(info).Length) + ")");
                var text = TryReadTextPrefix(info, 65536);
                AddPlistValue(section, text, "diskimage-bundle-type", "Bundle type");
                AddPlistValue(section, text, "uuid", "UUID");
                AddPlistInteger(section, text, "band-size", "Band size");
                AddPlistInteger(section, text, "size", "Declared virtual size");
            }

            var bands = Path.Combine(path, "bands");
            if (Directory.Exists(bands))
            {
                var bandFiles = SafeDirectoryFiles(bands).Take(10000).ToArray();
                Add(section, "Sampled band files", bandFiles.Length.ToString(CultureInfo.InvariantCulture) + (bandFiles.Length == 10000 ? " or more" : string.Empty));
            }

            foreach (var name in new[] { "com.apple.TimeMachine.MachineID.plist", "com.apple.TimeMachine.Results.plist", "com.apple.TimeMachine.SnapshotHistory.plist" })
            {
                var file = Path.Combine(path, name);
                if (File.Exists(file))
                    Add(section, name, "Present (" + FormatBytes(new FileInfo(file).Length) + ")");
            }
            Add(section, "Notes", "Sparse bundles are directory-backed disk images. FileDentify reports package metadata and band counts only; it does not mount, repair, or traverse the backup filesystem.");
        }

        private static void AddAppleBundleDirectoryInfo(List<ReportSection> sections, string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".app" && ext != ".framework" && ext != ".bundle" && ext != ".plugin" && ext != ".appex" && ext != ".kext" && ext != ".prefpane")
                return;
            var plist = Path.Combine(path, "Contents", "Info.plist");
            if (!File.Exists(plist))
                return;
            var section = AddSection(sections, "Apple bundle");
            Add(section, "Bundle kind", AppleBundleKind(path));
            Add(section, "Bundle name", Path.GetFileName(path));
            Add(section, "Info.plist", "Present (" + FormatBytes(new FileInfo(plist).Length) + ")");
            var text = TryReadTextPrefix(plist, 65536);
            AddPlistValue(section, text, "CFBundleDisplayName", "Display name");
            AddPlistValue(section, text, "CFBundleName", "Bundle display name");
            AddPlistValue(section, text, "CFBundleIdentifier", "Bundle identifier");
            AddPlistValue(section, text, "CFBundleExecutable", "Executable");
            AddPlistValue(section, text, "CFBundleShortVersionString", "Short version");
            AddPlistValue(section, text, "CFBundleVersion", "Bundle version");
            AddPlistValue(section, text, "LSMinimumSystemVersion", "Minimum macOS");
        }

        private static void AddMacAudioPluginDirectoryInfo(List<ReportSection> sections, string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".component" && ext != ".vst" && ext != ".vst3" && ext != ".clap" && ext != ".aaxplugin")
                return;
            AddMacAudioPluginInfo(sections, Path.Combine(path, "Contents", "Info.plist"), File.Exists(Path.Combine(path, "Contents", "Info.plist")) ? ReadPrefix(Path.Combine(path, "Contents", "Info.plist"), HeaderReadSize) : new byte[0]);
        }

        private static void AddUniversalAudioLunaDirectoryInfo(List<ReportSection> sections, string path)
        {
            if (!string.Equals(Path.GetExtension(path), ".lunacomponent", StringComparison.OrdinalIgnoreCase))
                return;

            var section = AddSection(sections, "Universal Audio LUNA");
            Add(section, "Format hint", "Universal Audio LUNA component bundle");
            Add(section, "Component name", Path.GetFileNameWithoutExtension(path));

            foreach (var sub in new[] { "data", "data\\dat", "data\\datx" })
            {
                var dir = Path.Combine(path, sub);
                if (!Directory.Exists(dir))
                    continue;
                var stats = BoundedDirectoryStats(dir, 2000);
                Add(section, sub + " files", stats.FileCount.ToString(CultureInfo.InvariantCulture) + (stats.Truncated ? " or more" : string.Empty));
                Add(section, sub + " sampled size", FormatBytes(stats.TotalBytes) + (stats.Truncated ? " or more" : string.Empty));
            }

            var largest = SafeDirectoryFilesRecursive(path, 5000)
                .OrderByDescending(SafeLength)
                .Take(8)
                .Select(file => RelativePath(path, file) + " (" + FormatBytes(SafeLength(file)) + ")")
                .ToArray();
            if (largest.Length > 0)
                Add(section, "Largest sampled internal files", string.Join(Environment.NewLine, largest));

            Add(section, "Notes", "LUNA component bundles are proprietary Universal Audio instrument/effect packages. FileDentify reports bounded package structure and internal data-file sizes without decoding sample or model payloads.");
        }

        private static void AddLogicMetadataStrings(ReportSection section, string metadata)
        {
            var sample = ReadPrefix(metadata, 512 * 1024);
            var text = Encoding.GetEncoding(28591).GetString(sample);
            AddLogicKeyList(section, "Visible musical fields", text,
                "BeatsPerMinute", "SongKey", "SampleRate", "NumberOfTracks", "SongSignatureNumerator", "SongSignatureDenominator", "FrameRateIndex", "isTimeCodeBased");
            AddLogicKeyList(section, "Visible media fields", text,
                "AudioFiles", "SamplerInstrumentsFiles", "QuicksamplerFiles", "PlaybackFiles", "ImpulsResponsesFiles", "UltrabeatFiles", "UnusedAudioFiles");
            AddLogicKeyList(section, "Visible project fields", text,
                "Version", "Logic Pro", "Project", "VariantNames", "ActiveVariant", "HasProjectFolder", "LastSavedFrom");
        }

        private static void AddLogicKeyList(ReportSection section, string title, string text, params string[] keys)
        {
            var found = keys
                .Where(key => text.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(FriendlyLogicKeyName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (found.Length > 0)
                Add(section, title, string.Join(Environment.NewLine, found));
        }

        private static string FriendlyLogicKeyName(string key)
        {
            switch (key)
            {
                case "BeatsPerMinute": return "Beats per minute";
                case "SongKey": return "Song key";
                case "SampleRate": return "Sample rate";
                case "NumberOfTracks": return "Number of tracks";
                case "SongSignatureNumerator": return "Song signature numerator";
                case "SongSignatureDenominator": return "Song signature denominator";
                case "FrameRateIndex": return "Frame rate index";
                case "isTimeCodeBased": return "Time-code based flag";
                case "AudioFiles": return "Audio files";
                case "SamplerInstrumentsFiles": return "Sampler instrument files";
                case "QuicksamplerFiles": return "Quick Sampler files";
                case "PlaybackFiles": return "Playback files";
                case "ImpulsResponsesFiles": return "Impulse response files";
                case "UltrabeatFiles": return "Ultrabeat files";
                case "UnusedAudioFiles": return "Unused audio files";
                case "VariantNames": return "Variant names";
                case "ActiveVariant": return "Active variant";
                case "HasProjectFolder": return "Has project folder";
                case "LastSavedFrom": return "Last saved from";
                default: return key;
            }
        }

        private static void AddPlistInteger(ReportSection section, string plistText, string key, string label)
        {
            var match = Regex.Match(plistText ?? string.Empty, "<key>\\s*" + Regex.Escape(key) + "\\s*</key>\\s*<integer>(.*?)</integer>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!match.Success)
                return;
            long value;
            if (long.TryParse(match.Groups[1].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                Add(section, label, FormatBytes(value) + " (" + value.ToString(CultureInfo.InvariantCulture) + " bytes)");
        }

        private static string TryReadTextPrefix(string path, int maxBytes)
        {
            try
            {
                var data = ReadPrefix(path, maxBytes);
                return LooksLikeText(data) ? Encoding.UTF8.GetString(data) : Encoding.GetEncoding(28591).GetString(data);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static IEnumerable<string> SafeDirectoryFiles(string path)
        {
            try { return Directory.GetFiles(path); }
            catch { return new string[0]; }
        }

        private static IEnumerable<string> SafeDirectoryDirectories(string path)
        {
            try { return Directory.GetDirectories(path); }
            catch { return new string[0]; }
        }

        private static IEnumerable<string> SafeDirectoryDirectoriesRecursive(string root, int maxDirectories)
        {
            var pending = new Stack<string>();
            pending.Push(root);
            var count = 0;
            while (pending.Count > 0 && count < maxDirectories)
            {
                var directory = pending.Pop();
                string[] dirs;
                try { dirs = Directory.GetDirectories(directory); }
                catch { dirs = new string[0]; }
                foreach (var dir in dirs)
                {
                    yield return dir;
                    count++;
                    if (count >= maxDirectories)
                        yield break;
                    pending.Push(dir);
                }
            }
        }

        private static long SafeDirectorySize(string root, int maxFiles)
        {
            long total = 0;
            foreach (var file in SafeDirectoryFilesRecursive(root, maxFiles))
                total += SafeLength(file);
            return total;
        }

        private static IEnumerable<string> SafeDirectoryFilesRecursive(string root, int maxFiles)
        {
            var pending = new Stack<string>();
            pending.Push(root);
            var count = 0;
            while (pending.Count > 0 && count < maxFiles)
            {
                var directory = pending.Pop();
                string[] files;
                try { files = Directory.GetFiles(directory); }
                catch { files = new string[0]; }
                foreach (var file in files)
                {
                    yield return file;
                    count++;
                    if (count >= maxFiles)
                        yield break;
                }

                string[] dirs;
                try { dirs = Directory.GetDirectories(directory); }
                catch { dirs = new string[0]; }
                foreach (var dir in dirs)
                    pending.Push(dir);
            }
        }

        private static long SafeLength(string path)
        {
            try { return new FileInfo(path).Length; }
            catch { return 0; }
        }

        private static string RelativePath(string root, string path)
        {
            if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return path;
        }

        private static DirectoryStats BoundedDirectoryStats(string root, int maxItems)
        {
            var stats = new DirectoryStats();
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0 && stats.FileCount + stats.DirectoryCount < maxItems)
            {
                var directory = pending.Pop();
                string[] files;
                try { files = Directory.GetFiles(directory); }
                catch { files = new string[0]; }
                foreach (var file in files)
                {
                    stats.FileCount++;
                    try { stats.TotalBytes += new FileInfo(file).Length; } catch { }
                    if (stats.FileCount + stats.DirectoryCount >= maxItems)
                    {
                        stats.Truncated = true;
                        return stats;
                    }
                }

                string[] dirs;
                try { dirs = Directory.GetDirectories(directory); }
                catch { dirs = new string[0]; }
                foreach (var dir in dirs)
                {
                    stats.DirectoryCount++;
                    pending.Push(dir);
                    if (stats.FileCount + stats.DirectoryCount >= maxItems)
                    {
                        stats.Truncated = true;
                        return stats;
                    }
                }
            }
            return stats;
        }

        private sealed class DirectoryStats
        {
            public int FileCount;
            public int DirectoryCount;
            public long TotalBytes;
            public bool Truncated;
        }
    }
}
