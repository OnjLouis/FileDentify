using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Text.RegularExpressions;

namespace FileDentify
{
    internal static partial class FileInspector
    {
        private static string DeveloperAppResourceTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".asar" && LooksLikeElectronAsar(header))
                return "Electron ASAR application archive";
            if (ext == ".nupkg" && IsZipHeader(header))
                return "NuGet package";
            if (IsChromiumHyphenationDictionary(path, header))
                return "Chromium hyphenation dictionary";
            if (IsLibreOfficeResource(path))
                return "LibreOffice application resource";
            if (IsWinampWasabiPlugin(path, header))
                return "Winamp system plug-in";
            if (IsWinampLanguagePack(path, header))
                return "Winamp language pack";
            if (IsWinampDspPreset(path, header))
                return "Winamp DSP preset";
            return null;
        }

        private static void AddDeveloperAppResourceInfo(List<ReportSection> sections, string path, byte[] header)
        {
            var type = DeveloperAppResourceTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Developer/app resources");
            Add(section, "Format hint", type);
            Add(section, "File name", Path.GetFileName(path));

            if (LooksLikeElectronAsar(header))
                AddElectronAsarInfo(section, path, header);
            else if (Path.GetExtension(path).Equals(".nupkg", StringComparison.OrdinalIgnoreCase))
                AddNuGetPackageInfo(section, path);
            else if (IsChromiumHyphenationDictionary(path, header))
                AddChromiumHyphenationInfo(section, path, header);
            else if (IsLibreOfficeResource(path))
                AddLibreOfficeResourceInfo(section, path, header);
            else if (IsWinampWasabiPlugin(path, header))
                AddWinampWasabiPluginInfo(section, path, header);
            else if (IsWinampLanguagePack(path, header))
                AddWinampLanguagePackInfo(section, path, header);
            else if (IsWinampDspPreset(path, header))
                AddWinampDspPresetInfo(section, path, header);
        }

        private static bool IsChromiumHyphenationDictionary(string path, byte[] header)
        {
            return string.Equals(Path.GetExtension(path), ".hyb", StringComparison.OrdinalIgnoreCase) &&
                (StartsWith(header, new byte[] { 0x68, 0x79, 0xAD, 0x62 }) ||
                 path.IndexOf("\\hyphen-data\\", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsLibreOfficeResource(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".soc" && ext != ".sod" && ext != ".soe" && ext != ".sog" && ext != ".soh" && ext != ".sor" && ext != ".sdg" && ext != ".sdv" && ext != ".rdb" && ext != ".xdl")
                return false;
            return path.IndexOf("\\LibreOfficePortable\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\libreoffice\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\openoffice", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsWinampWasabiPlugin(string path, byte[] header)
        {
            return string.Equals(Path.GetExtension(path), ".w5s", StringComparison.OrdinalIgnoreCase) &&
                (StartsWith(header, Encoding.ASCII.GetBytes("MZ")) ||
                 path.IndexOf("\\Winamp\\System\\", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsWinampLanguagePack(string path, byte[] header)
        {
            return string.Equals(Path.GetExtension(path), ".wlz", StringComparison.OrdinalIgnoreCase) &&
                (IsZipHeader(header) || path.IndexOf("\\Winamp\\Lang\\", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsWinampDspPreset(string path, byte[] header)
        {
            return string.Equals(Path.GetExtension(path), ".sps", StringComparison.OrdinalIgnoreCase) &&
                DecodeTextSample(header, 4096).IndexOf("[SPS PRESET]", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddChromiumHyphenationInfo(ReportSection section, string path, byte[] header)
        {
            Add(section, "Role", "Hyphenation dictionary used by Chromium-based browsers and Electron apps.");
            Add(section, "Language", ChromiumHyphenLanguageFromName(path));
            if (StartsWith(header, new byte[] { 0x68, 0x79, 0xAD, 0x62 }))
                Add(section, "Header marker", "hy AD 62");
            Add(section, "Notes", "Chromium .hyb files are browser/app text-layout dictionaries. FileDentify identifies their role and language clue only; it does not expand or validate the dictionary data.");
        }

        private static string ChromiumHyphenLanguageFromName(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
            if (name.StartsWith("hyph-", StringComparison.OrdinalIgnoreCase))
                return name.Substring(5);
            return string.Empty;
        }

        private static void AddLibreOfficeResourceInfo(ReportSection section, string path, byte[] header)
        {
            Add(section, "Role", LibreOfficeResourceRole(path));
            Add(section, "File name", Path.GetFileName(path));
            if (LooksLikeText(header))
                Add(section, "First line", FirstNonEmptyLine(DecodeTextSample(header, 8192)));
            Add(section, "Notes", "LibreOffice and OpenOffice support-resource files describe palettes, galleries, hatches, gradients, styles, and number-text rules. FileDentify reports the resource role and sampled text only; it does not import the resource into an office suite.");
        }

        private static string LibreOfficeResourceRole(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".soc": return "Color palette";
                case ".sod": return "Drawing/style resource";
                case ".soe": return "Line-end or arrowhead resource";
                case ".sog": return "Gradient resource";
                case ".soh": return "Hatch pattern resource";
                case ".sor": return "Number-text spelling/rules resource";
                case ".sdg": return "Gallery binary data";
                case ".sdv": return "Gallery index or preview data";
                case ".rdb": return "UNO component registry";
                case ".xdl": return "LibreOffice Basic dialog";
                default: return "LibreOffice/OpenOffice resource";
            }
        }

        private static void AddWinampWasabiPluginInfo(ReportSection section, string path, byte[] header)
        {
            Add(section, "Role", "Winamp 5 / Wasabi system plug-in module");
            Add(section, "Module", Path.GetFileName(path));
            if (StartsWith(header, Encoding.ASCII.GetBytes("MZ")))
                Add(section, "Container", "Windows executable/DLL-style module");
            Add(section, "Notes", "Winamp .w5s files are application plug-in modules. FileDentify reports module identity and PE-style container clues only; it does not load or execute the plug-in.");
        }

        private static void AddWinampLanguagePackInfo(ReportSection section, string path, byte[] header)
        {
            Add(section, "Role", "Winamp language/localization package");
            Add(section, "Language clue", Path.GetFileNameWithoutExtension(path));
            if (IsZipHeader(header))
                Add(section, "Container", "ZIP-compatible package");
            Add(section, "Notes", "Winamp .wlz files are localization packages. FileDentify identifies package role and container clues only; it does not install or load language resources.");
        }

        private static void AddWinampDspPresetInfo(ReportSection section, string path, byte[] header)
        {
            Add(section, "Role", "Winamp Signal Processing Studio DSP preset");
            Add(section, "Preset", Path.GetFileNameWithoutExtension(path));
            var text = DecodeTextSample(header, 16384);
            Add(section, "Header", FirstNonEmptyLine(text));
            Add(section, "Slider fields", Regex.Matches(text ?? string.Empty, @"(?im)^slider\d+=", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Code blocks", Regex.Matches(text ?? string.Empty, @"(?im)^code\d+_data=", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Notes", "Winamp SPS files are text DSP presets/scripts. FileDentify reports preset structure only; it does not run DSP code.");
        }

        private static bool LooksLikeElectronAsar(byte[] header)
        {
            if (header.Length < 24)
                return false;
            var jsonOffset = 16;
            var declaredLength = ReadUInt32LittleEndian(header, 12);
            return declaredLength > 8 &&
                jsonOffset + declaredLength <= header.Length &&
                header[jsonOffset] == (byte)'{' &&
                AsciiPreview(header.Skip(jsonOffset).Take(Math.Min((int)declaredLength, 128)).ToArray(), 128).IndexOf("\"files\"", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddElectronAsarInfo(ReportSection section, string path, byte[] header)
        {
            var jsonLength = (int)Math.Min(ReadUInt32LittleEndian(header, 12), (uint)Math.Max(0, header.Length - 16));
            Add(section, "Container", "Electron ASAR archive");
            Add(section, "Header JSON size", FormatBytes(jsonLength));

            try
            {
                var json = Encoding.UTF8.GetString(header, 16, jsonLength);
                var serializer = new JavaScriptSerializer { MaxJsonLength = Math.Max(json.Length + 1024, 1024 * 1024) };
                var root = serializer.DeserializeObject(json) as Dictionary<string, object>;
                var files = GetObjectDictionary(root, "files");
                if (files == null)
                    return;

                Add(section, "Top-level entries", files.Count.ToString(CultureInfo.InvariantCulture));
                var firstEntries = files.Keys.Where(k => !string.IsNullOrWhiteSpace(k)).Take(30).ToArray();
                if (firstEntries.Length > 0)
                    Add(section, "First top-level entries", string.Join(Environment.NewLine, firstEntries));
                var package = GetObjectDictionary(files, "package.json");
                if (package != null)
                {
                    Add(section, "Contains package.json", "yes");
                    AddAsarEntrySize(section, package, "package.json size");
                }
                Add(section, "Directory entries in header", CountAsarDirectories(files).ToString(CultureInfo.InvariantCulture));
                Add(section, "File entries in header", CountAsarFiles(files).ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                Add(section, "Header parse note", ex.Message);
            }

            Add(section, "Notes", "ASAR is Electron's application resource archive. FileDentify reads the small JSON index and does not extract or run application code.");
        }

        private static void AddNuGetPackageInfo(ReportSection section, string path)
        {
            Add(section, "Container", "ZIP-based NuGet package");
            try
            {
                using (var archive = ZipFile.OpenRead(path))
                {
                    Add(section, "Archive entries", archive.Entries.Count.ToString(CultureInfo.InvariantCulture));
                    var nuspec = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
                    if (nuspec != null)
                    {
                        Add(section, "Nuspec", nuspec.FullName);
                        var text = ReadZipEntryText(nuspec, 1024 * 1024);
                        AddSimpleXmlTagValue(section, text, "id", "Package id");
                        AddSimpleXmlTagValue(section, text, "version", "Package version");
                        AddSimpleXmlTagValue(section, text, "authors", "Authors");
                        AddSimpleXmlTagValue(section, text, "description", "Description");
                        var dependencyCount = Regex.Matches(text ?? string.Empty, "<\\s*dependency\\b", RegexOptions.IgnoreCase).Count;
                        if (dependencyCount > 0)
                            Add(section, "Dependency entries", dependencyCount.ToString(CultureInfo.InvariantCulture));
                    }
                    var libEntries = archive.Entries.Count(e => e.FullName.StartsWith("lib/", StringComparison.OrdinalIgnoreCase));
                    var contentEntries = archive.Entries.Count(e => e.FullName.StartsWith("content", StringComparison.OrdinalIgnoreCase));
                    if (libEntries > 0)
                        Add(section, "lib entries", libEntries.ToString(CultureInfo.InvariantCulture));
                    if (contentEntries > 0)
                        Add(section, "content entries", contentEntries.ToString(CultureInfo.InvariantCulture));
                }
            }
            catch (Exception ex)
            {
                Add(section, "Package read note", ex.Message);
            }
        }

        private static Dictionary<string, object> GetObjectDictionary(Dictionary<string, object> owner, string key)
        {
            if (owner == null || !owner.ContainsKey(key))
                return null;
            return owner[key] as Dictionary<string, object>;
        }

        private static int CountAsarFiles(Dictionary<string, object> files)
        {
            if (files == null)
                return 0;
            var count = 0;
            foreach (var entry in files.Values.OfType<Dictionary<string, object>>())
            {
                if (entry.ContainsKey("files"))
                    count += CountAsarFiles(entry["files"] as Dictionary<string, object>);
                else
                    count++;
            }
            return count;
        }

        private static int CountAsarDirectories(Dictionary<string, object> files)
        {
            if (files == null)
                return 0;
            var count = 0;
            foreach (var entry in files.Values.OfType<Dictionary<string, object>>())
            {
                var children = entry.ContainsKey("files") ? entry["files"] as Dictionary<string, object> : null;
                if (children == null)
                    continue;
                count++;
                count += CountAsarDirectories(children);
            }
            return count;
        }

        private static void AddAsarEntrySize(ReportSection section, Dictionary<string, object> entry, string label)
        {
            if (entry == null || !entry.ContainsKey("size"))
                return;
            long size;
            if (long.TryParse(Convert.ToString(entry["size"], CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out size))
                Add(section, label, FormatBytes(size));
        }

        private static void AddSimpleXmlTagValue(ReportSection section, string text, string tag, string label)
        {
            var match = Regex.Match(text ?? string.Empty, "<\\s*" + Regex.Escape(tag) + "(?:\\s[^>]*)?>(?<value>.*?)</\\s*" + Regex.Escape(tag) + "\\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!match.Success)
                return;
            var value = CleanMetadataText(Regex.Replace(match.Groups["value"].Value, "\\s+", " "));
            if (!string.IsNullOrWhiteSpace(value))
                Add(section, label, value);
        }
    }
}
