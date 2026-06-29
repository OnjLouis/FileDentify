using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace FileDentify
{
    internal static partial class FileInspector
    {
        private static string MacAudioPluginTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".component") return "Apple Audio Unit plug-in bundle";
            if (ext == ".vst") return "Mac VST plug-in bundle";
            if (ext == ".vst3") return "VST3 plug-in bundle";
            if (ext == ".clap") return "CLAP plug-in bundle";
            if (ext == ".aaxplugin") return "Avid AAX plug-in bundle";
            if (ext == ".aupreset") return "Apple Audio Unit preset";
            if (Path.GetFileName(path).Equals("Info.plist", StringComparison.OrdinalIgnoreCase) && IsInsideMacAudioPluginBundle(path))
                return "Mac audio plug-in bundle metadata";
            return null;
        }

        private static void AddMacAudioPluginInfo(List<ReportSection> sections, string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var isInfoPlist = Path.GetFileName(path).Equals("Info.plist", StringComparison.OrdinalIgnoreCase) && IsInsideMacAudioPluginBundle(path);
            if (ext != ".component" && ext != ".vst" && ext != ".vst3" && ext != ".clap" &&
                ext != ".aaxplugin" && ext != ".aupreset" && !isInfoPlist)
                return;

            var section = AddSection(sections, "Mac audio plug-in");
            var bundlePath = isInfoPlist ? FindMacAudioPluginBundlePath(path) : path;
            var bundleName = string.IsNullOrWhiteSpace(bundlePath) ? Path.GetFileName(path) : Path.GetFileName(bundlePath);
            var bundleExt = Path.GetExtension(bundleName).ToLowerInvariant();

            Add(section, "Format hint", MacAudioPluginBundleDescription(bundleExt, ext));
            if (!string.IsNullOrWhiteSpace(bundleName))
                Add(section, "Bundle name", bundleName);
            if (isInfoPlist)
                Add(section, "Metadata file", "Info.plist inside a Mac audio plug-in bundle");

            var plistText = isInfoPlist && LooksLikeText(header)
                ? Encoding.UTF8.GetString(header, 0, Math.Min(header.Length, 65536))
                : TryReadBundleInfoPlist(bundlePath);
            if (!string.IsNullOrWhiteSpace(plistText))
            {
                AddPlistValue(section, plistText, "CFBundleDisplayName", "Display name");
                AddPlistValue(section, plistText, "CFBundleName", "Bundle display name");
                AddPlistValue(section, plistText, "CFBundleIdentifier", "Bundle identifier");
                AddPlistValue(section, plistText, "CFBundleExecutable", "Executable");
                AddPlistValue(section, plistText, "CFBundleShortVersionString", "Short version");
                AddPlistValue(section, plistText, "CFBundleVersion", "Bundle version");
                AddPlistValue(section, plistText, "LSMinimumSystemVersion", "Minimum macOS");
                AddPlistValue(section, plistText, "DTPlatformName", "Built for platform");
                AddPlistValue(section, plistText, "DTSDKName", "SDK");
                AddPlistValue(section, plistText, "DTXcode", "Xcode");
            }
            else if (!isInfoPlist)
            {
                Add(section, "Bundle metadata", "Open the bundle's Contents\\Info.plist file for detailed name, identifier, version, and platform fields.");
            }
        }

        private static string MacAudioPluginBundleDescription(string bundleExt, string fileExt)
        {
            var ext = string.IsNullOrWhiteSpace(bundleExt) ? fileExt : bundleExt;
            switch (ext)
            {
                case ".component": return "Apple Audio Unit plug-in bundle";
                case ".vst": return "Mac VST 2 plug-in bundle";
                case ".vst3": return "VST3 plug-in bundle";
                case ".clap": return "CLAP plug-in bundle";
                case ".aaxplugin": return "Avid AAX plug-in bundle";
                case ".aupreset": return "Apple Audio Unit preset";
                default: return "Mac audio plug-in metadata";
            }
        }

        private static bool IsInsideMacAudioPluginBundle(string path)
        {
            return !string.IsNullOrWhiteSpace(FindMacAudioPluginBundlePath(path));
        }

        private static string FindMacAudioPluginBundlePath(string path)
        {
            var dir = new FileInfo(path).Directory;
            while (dir != null)
            {
                var ext = dir.Extension.ToLowerInvariant();
                if (ext == ".component" || ext == ".vst" || ext == ".vst3" || ext == ".clap" || ext == ".aaxplugin")
                    return dir.FullName;
                dir = dir.Parent;
            }
            return string.Empty;
        }

        private static string TryReadBundleInfoPlist(string bundlePath)
        {
            if (string.IsNullOrWhiteSpace(bundlePath))
                return string.Empty;
            var plist = Path.Combine(bundlePath, "Contents", "Info.plist");
            if (!File.Exists(plist))
                return string.Empty;
            try
            {
                var data = ReadPrefix(plist, 65536);
                return LooksLikeText(data) ? Encoding.UTF8.GetString(data) : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool AddPlistValue(ReportSection section, string plistText, string key, string label)
        {
            var match = Regex.Match(plistText, "<key>\\s*" + Regex.Escape(key) + "\\s*</key>\\s*<string>(.*?)</string>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!match.Success)
                return false;
            var value = Regex.Replace(match.Groups[1].Value, "<.*?>", string.Empty).Trim();
            if (value.Length == 0)
                return false;
            Add(section, label, value);
            return true;
        }
    }
}
