using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

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
            if (Path.GetFileName(path).Equals("moduleinfo.json", StringComparison.OrdinalIgnoreCase) && IsInsideMacAudioPluginBundle(path))
                return "VST3 module metadata";
            return null;
        }

        private static void AddMacAudioPluginInfo(List<ReportSection> sections, string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var isInfoPlist = Path.GetFileName(path).Equals("Info.plist", StringComparison.OrdinalIgnoreCase) && IsInsideMacAudioPluginBundle(path);
            var isModuleInfo = Path.GetFileName(path).Equals("moduleinfo.json", StringComparison.OrdinalIgnoreCase) && IsInsideMacAudioPluginBundle(path);
            if (ext != ".component" && ext != ".vst" && ext != ".vst3" && ext != ".clap" &&
                ext != ".aaxplugin" && ext != ".aupreset" && !isInfoPlist && !isModuleInfo)
                return;

            var section = AddSection(sections, "Mac audio plug-in");
            var bundlePath = isInfoPlist || isModuleInfo ? FindMacAudioPluginBundlePath(path) : path;
            var bundleName = string.IsNullOrWhiteSpace(bundlePath) ? Path.GetFileName(path) : Path.GetFileName(bundlePath);
            var bundleExt = Path.GetExtension(bundleName).ToLowerInvariant();

            Add(section, "Format hint", MacAudioPluginBundleDescription(bundleExt, ext));
            if (!string.IsNullOrWhiteSpace(bundleName))
                Add(section, "Bundle name", bundleName);
            if (isInfoPlist)
                Add(section, "Metadata file", "Info.plist inside a Mac audio plug-in bundle");
            if (isModuleInfo)
                Add(section, "Metadata file", "VST3 moduleinfo.json inside a Mac audio plug-in bundle");

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

            var moduleInfoText = isModuleInfo && LooksLikeText(header)
                ? Encoding.UTF8.GetString(header, 0, Math.Min(header.Length, 1024 * 1024))
                : TryReadVst3ModuleInfo(bundlePath);
            AddVst3ModuleInfo(section, moduleInfoText);
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

        private static string TryReadVst3ModuleInfo(string bundlePath)
        {
            if (string.IsNullOrWhiteSpace(bundlePath) || !Path.GetExtension(bundlePath).Equals(".vst3", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            var moduleInfo = Path.Combine(bundlePath, "Contents", "Resources", "moduleinfo.json");
            if (!File.Exists(moduleInfo))
                return string.Empty;

            try
            {
                var data = ReadPrefix(moduleInfo, 1024 * 1024);
                return LooksLikeText(data) ? Encoding.UTF8.GetString(data) : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void AddVst3ModuleInfo(ReportSection section, string moduleInfoText)
        {
            if (string.IsNullOrWhiteSpace(moduleInfoText))
                return;

            try
            {
                var serializer = new JavaScriptSerializer();
                var normalizedModuleInfo = Regex.Replace(moduleInfoText, @",\s*([}\]])", "$1");
                var root = serializer.DeserializeObject(normalizedModuleInfo) as Dictionary<string, object>;
                if (root == null)
                    return;

                AddJsonText(section, root, "Name", "VST3 name");
                var factory = GetVst3Dictionary(root, "Factory Info");
                if (factory != null)
                {
                    AddJsonText(section, factory, "Vendor", "Vendor");
                    AddJsonText(section, factory, "URL", "Vendor URL");
                }

                var classes = GetVst3Array(root, "Classes");
                if (classes != null)
                {
                    Add(section, "Class count", classes.Length.ToString(CultureInfo.InvariantCulture));
                    var first = classes.OfType<Dictionary<string, object>>().FirstOrDefault();
                    if (first != null)
                    {
                        AddJsonText(section, first, "Name", "First class");
                        AddJsonText(section, first, "Category", "Category");
                        AddJsonText(section, first, "Sub Categories", "Sub categories");
                        AddJsonText(section, first, "CID", "Class ID");
                        AddJsonText(section, first, "Vendor", "Class vendor");
                        AddJsonText(section, first, "Version", "Class version");
                    }
                }

                AddJsonText(section, root, "Version", "Module version");
                AddJsonText(section, root, "Compatibility", "Compatibility");
                Add(section, "Notes", "VST3 module metadata is read from Steinberg-style moduleinfo.json where present. FileDentify reports plug-in identity and class metadata only; it does not load the plug-in.");
            }
            catch
            {
            }
        }

        private static Dictionary<string, object> GetVst3Dictionary(Dictionary<string, object> root, string key)
        {
            object value;
            return root != null && root.TryGetValue(key, out value) ? value as Dictionary<string, object> : null;
        }

        private static object[] GetVst3Array(Dictionary<string, object> root, string key)
        {
            object value;
            return root != null && root.TryGetValue(key, out value) ? value as object[] : null;
        }

        private static void AddJsonText(ReportSection section, Dictionary<string, object> root, string key, string label)
        {
            object value;
            if (root == null || !root.TryGetValue(key, out value) || value == null)
                return;

            var array = value as object[];
            var text = array == null
                ? Convert.ToString(value, CultureInfo.InvariantCulture)
                : string.Join(", ", array.Select(item => Convert.ToString(item, CultureInfo.InvariantCulture)).Where(item => !string.IsNullOrWhiteSpace(item)).ToArray());
            if (!string.IsNullOrWhiteSpace(text))
                Add(section, label, text);
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
