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
            if (ext == ".deb" && StartsWith(header, Encoding.ASCII.GetBytes("!<arch>\n")))
                return "Debian package";
            if (ext == ".crx3" || StartsWith(header, Encoding.ASCII.GetBytes("Cr24")))
                return "Chrome extension package";
            if (ext == ".fbx" && StartsWith(header, Encoding.ASCII.GetBytes("Kaydara FBX Binary")))
                return "Autodesk FBX 3D model";
            if (ext == ".mgc")
                return "compiled file/libmagic database";
            if (ext == ".enc" && IsTclEncodingMap(path, header))
                return "Tcl encoding map";
            if (LooksLikeAntivirusIdeFile(path, header))
                return "antivirus identity/signature file";
            if (LooksLikeHtmlAppCache(path, header))
                return "HTML5 application cache manifest";
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
            var generic = GenericDeveloperResourceTypeName(path, header);
            if (generic != null)
                return generic;
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
            else if (Path.GetExtension(path).Equals(".deb", StringComparison.OrdinalIgnoreCase))
                AddDebianPackageInfo(section, path, header);
            else if (Path.GetExtension(path).Equals(".crx3", StringComparison.OrdinalIgnoreCase) || StartsWith(header, Encoding.ASCII.GetBytes("Cr24")))
                AddChromeExtensionPackageInfo(section, path, header);
            else if (Path.GetExtension(path).Equals(".fbx", StringComparison.OrdinalIgnoreCase))
                AddFbxInfo(section, path, header);
            else if (Path.GetExtension(path).Equals(".mgc", StringComparison.OrdinalIgnoreCase))
                AddMagicDatabaseInfo(section, path, header);
            else if (Path.GetExtension(path).Equals(".enc", StringComparison.OrdinalIgnoreCase) && IsTclEncodingMap(path, header))
                AddTclEncodingMapInfo(section, path, header);
            else if (LooksLikeAntivirusIdeFile(path, header))
                AddAntivirusIdeInfo(section, path, header);
            else if (LooksLikeHtmlAppCache(path, header))
                AddHtmlAppCacheInfo(section, path, header);
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
            else
                AddGenericDeveloperResourceInfo(section, path, header, type);
        }

        private static string GenericDeveloperResourceTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".h":
                case ".hh":
                case ".hpp":
                case ".hxx":
                case ".c":
                case ".cc":
                case ".cpp":
                case ".cxx":
                case ".ipp":
                case ".inl":
                case ".idl":
                case ".def":
                case ".asm":
                case ".rc":
                    return "C/C++ or Windows SDK source/resource file";
                case ".map":
                    return "source map or linker map";
                case ".rst":
                    return "reStructuredText documentation";
                case ".cmake":
                    return "CMake build script/module";
                case ".zig":
                    return "Zig source file";
                case ".pyi":
                    return "Python type stub";
                case ".pyx":
                case ".pxd":
                case ".pxi":
                    return "Cython source/include file";
                case ".properties":
                    return "Java/application properties file";
                case ".ui":
                    return "Qt/GTK/LibreOffice UI definition";
                case ".bas":
                    return "BASIC source file";
                case ".nsi":
                case ".nsh":
                    return "NSIS installer script";
                case ".sh":
                    return "Unix shell script";
                case ".f":
                case ".f90":
                    return "Fortran source file";
                case ".glsl":
                case ".hlsl":
                case ".fx":
                    return "shader/effect source file";
                case ".dtd":
                    return "XML document type definition";
                case ".yang":
                    return "YANG network data model";
                case ".po":
                    return "gettext translation catalogue";
                case ".afm":
                    return "Adobe Font Metrics file";
                case ".lib":
                    return "static/import library";
                case ".obj":
                    return "compiled object file";
                case ".a":
                    return "Unix static library archive";
                case ".vim":
                    return "Vim script";
                case ".pm":
                    return "Perl module";
                case ".pl":
                    return "Perl script";
                case ".tcl":
                    return "Tcl script";
                case ".lua":
                    return "Lua script";
                case ".nse":
                    return "Nmap Scripting Engine Lua script";
                case ".adoc":
                    return "AsciiDoc document";
                case ".props":
                case ".targets":
                    return "MSBuild project support file";
                case ".resx":
                    return ".NET XML resource file";
                case ".xsd":
                    return "XML schema";
                case ".xsl":
                case ".xslt":
                    return "XSLT stylesheet";
                case ".qml":
                case ".qmltypes":
                    return "Qt QML resource";
                case ".bcmap":
                    return "PDF.js binary CMap";
                case ".cjs":
                case ".mjs":
                    return "JavaScript module";
                case ".node":
                    return "Node.js native add-on";
                case ".pyd":
                    return "Python native extension module";
                case ".natvis":
                    return "Visual Studio debugger visualizer";
                case ".version":
                    return "application version marker";
                case ".str":
                    if (path.IndexOf("\\MAXON\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        path.IndexOf("\\Cinebench\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        path.IndexOf("\\Cinema 4D\\", StringComparison.OrdinalIgnoreCase) >= 0)
                        return "MAXON/Cinema 4D string resource";
                    return "application string resource";
                case ".c4d":
                case ".c4dnodes":
                case ".c4dnodedesc":
                case ".c4dnodectx":
                    return "MAXON/Cinema 4D resource";
                case ".nvi":
                case ".nvx":
                case ".nifx":
                case ".forms":
                    if (path.IndexOf("\\NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0)
                        return "NVIDIA installer/resource manifest";
                    return "application resource manifest";
                case ".arguments":
                case ".files":
                case ".ignore":
                    if (path.IndexOf("\\chocolatey\\", StringComparison.OrdinalIgnoreCase) >= 0)
                        return "Chocolatey package support file";
                    break;
                case ".mxmd":
                case ".mxdh":
                case ".mxdk":
                case ".mxdp":
                case ".sfexp":
                case ".bj":
                    return "MAGIX/Sound Forge application resource";
            }

            if (LooksLikeText(header) && IsLikelyDeveloperResourcePath(path))
                return "developer/app text resource";
            return null;
        }

        private static bool LooksLikeAntivirusIdeFile(string path, byte[] header)
        {
            if (!Path.GetExtension(path).Equals(".ide", StringComparison.OrdinalIgnoreCase))
                return false;
            var text = DecodeTextSample(header, 8192);
            return path.IndexOf("Sophos", StringComparison.OrdinalIgnoreCase) >= 0 ||
                Regex.IsMatch(text ?? string.Empty, @"(?im)^[0-9A-F]{4,}\s+[0-9A-F]{4,}", RegexOptions.CultureInvariant);
        }

        private static void AddAntivirusIdeInfo(ReportSection section, string path, byte[] header)
        {
            var text = DecodeTextSample(header, 64 * 1024);
            Add(section, "Role detail", "Antivirus identity/update signature data.");
            Add(section, "Vendor/path hint", path.IndexOf("Sophos", StringComparison.OrdinalIgnoreCase) >= 0 ? "Sophos or Sophos-derived tool" : "IDE-style antivirus identity file");
            Add(section, "Signature-like lines in sample", Regex.Matches(text ?? string.Empty, @"(?im)^[0-9A-F]{4,}\s+[0-9A-F]{4,}", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Safety note", "FileDentify identifies antivirus signature/update files only. It does not evaluate whether the signatures are current and does not scan files for malware.");
        }

        private static bool LooksLikeHtmlAppCache(string path, byte[] header)
        {
            if (!Path.GetExtension(path).Equals(".appcache", StringComparison.OrdinalIgnoreCase))
                return false;
            var text = DecodeTextSample(header, 8192).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
            return text.StartsWith("CACHE MANIFEST", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddHtmlAppCacheInfo(ReportSection section, string path, byte[] header)
        {
            var text = DecodeTextSample(header, 128 * 1024);
            Add(section, "Role detail", "Legacy HTML5 offline application cache manifest.");
            Add(section, "Cache entries in sample", text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Count(line => !line.TrimStart().StartsWith("#", StringComparison.Ordinal) && line.IndexOf(':') < 0).ToString(CultureInfo.InvariantCulture));
            Add(section, "Notes", "AppCache manifests were used by older web applications to list offline resources. They are deprecated on the modern web, but remain useful clues in old application bundles.");
        }

        private static bool IsLikelyDeveloperResourcePath(string path)
        {
            return path.IndexOf("\\Program Files\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\Program Files (x86)\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\Windows Kits\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\Microsoft Visual Studio\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\Git\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\Nmap\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\node_modules\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddGenericDeveloperResourceInfo(ReportSection section, string path, byte[] header, string type)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            Add(section, "Role", type + ".");
            Add(section, "Resource", Path.GetFileName(path));
            AddDeveloperPathHint(section, path);

            if (ext == ".node" || ext == ".pyd")
            {
                if (StartsWith(header, Encoding.ASCII.GetBytes("MZ")))
                    Add(section, "Container", "Windows PE native module");
                Add(section, "Safety note", "Native extension modules are identified only. FileDentify does not load them into Node.js or Python.");
                return;
            }

            if (ext == ".bcmap")
            {
                Add(section, "Role detail", "Compact binary character map used by PDF.js for PDF text extraction/rendering.");
                return;
            }

            if (StartsWith(header, Encoding.ASCII.GetBytes("MZ")) && (ext == ".acm" || ext == ".ax"))
                Add(section, "Container", "Windows PE module");

            var text = DecodeTextSample(header, 256 * 1024);
            if (!string.IsNullOrWhiteSpace(text))
            {
                var firstLine = FirstNonEmptyLine(text);
                if (!string.IsNullOrWhiteSpace(firstLine))
                    Add(section, "First non-empty line", firstLine);
                Add(section, "Non-empty lines in sample", text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Count(line => !string.IsNullOrWhiteSpace(line)).ToString(CultureInfo.InvariantCulture));
                AddDeveloperTextCounters(section, ext, text);
            }

            Add(section, "Notes", "Developer and application resource files are summarized as text/header evidence only. FileDentify does not compile, run, import, or load them.");
        }

        private static void AddDeveloperPathHint(ReportSection section, string path)
        {
            if (path.IndexOf("\\Git\\", StringComparison.OrdinalIgnoreCase) >= 0)
                Add(section, "Product/path hint", "Git for Windows or bundled Unix-style runtime");
            else if (path.IndexOf("\\Nmap\\", StringComparison.OrdinalIgnoreCase) >= 0)
                Add(section, "Product/path hint", "Nmap");
            else if (path.IndexOf("\\MAXON\\", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("\\Cinema 4D\\", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("\\Cinebench\\", StringComparison.OrdinalIgnoreCase) >= 0)
                Add(section, "Product/path hint", "MAXON / Cinema 4D / Cinebench");
            else if (path.IndexOf("\\NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0)
                Add(section, "Product/path hint", "NVIDIA software/driver installer resources");
            else if (path.IndexOf("\\chocolatey\\", StringComparison.OrdinalIgnoreCase) >= 0)
                Add(section, "Product/path hint", "Chocolatey package manager");
            else if (path.IndexOf("\\Windows Kits\\", StringComparison.OrdinalIgnoreCase) >= 0)
                Add(section, "Product/path hint", "Windows SDK / Windows Kits");
            else if (path.IndexOf("\\Microsoft Visual Studio\\", StringComparison.OrdinalIgnoreCase) >= 0)
                Add(section, "Product/path hint", "Microsoft Visual Studio");
            else if (path.IndexOf("\\MAGIX\\", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("\\Sound Forge", StringComparison.OrdinalIgnoreCase) >= 0)
                Add(section, "Product/path hint", "MAGIX / Sound Forge");
        }

        private static void AddDeveloperTextCounters(ReportSection section, string ext, string text)
        {
            switch (ext)
            {
                case ".h":
                case ".hh":
                case ".hpp":
                case ".hxx":
                case ".c":
                case ".cc":
                case ".cpp":
                case ".cxx":
                case ".idl":
                case ".rc":
                    Add(section, "Include/import lines", Regex.Matches(text, @"(?im)^\s*#\s*(include|import)\b", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Defines", Regex.Matches(text, @"(?im)^\s*#\s*define\b", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Class/interface markers", Regex.Matches(text, @"(?im)^\s*(class|interface|struct)\s+\w+", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    break;
                case ".map":
                    Add(section, "Source map marker", text.IndexOf("\"mappings\"", StringComparison.OrdinalIgnoreCase) >= 0 ? "Present" : "Not seen in sample");
                    Add(section, "Linker-map public symbols", Regex.Matches(text, @"(?im)\b(publics by value|Address\s+Publics by Value)\b", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    break;
                case ".rst":
                    Add(section, "Headings", Regex.Matches(text, @"(?m)^[^\r\n]+\r?\n[=\-~`#^""*+]{3,}\s*$", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Directives", Regex.Matches(text, @"(?m)^\s*\.\.\s+\w+::", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    break;
                case ".cmake":
                    Add(section, "CMake commands", Regex.Matches(text, @"(?im)^\s*[A-Za-z_][A-Za-z0-9_]*\s*\(", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    Add(section, "CMake function/macro blocks", Regex.Matches(text, @"(?im)^\s*(function|macro)\s*\(", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    break;
                case ".zig":
                    Add(section, "Imports", Regex.Matches(text, @"@import\s*\(", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Public declarations", Regex.Matches(text, @"(?m)^\s*pub\s+(const|fn|var)\b", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    break;
                case ".pyi":
                case ".pyx":
                case ".pxd":
                case ".pxi":
                    Add(section, "Python imports", Regex.Matches(text, @"(?im)^\s*(from\s+\S+\s+import|import\s+)", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Function/class declarations", Regex.Matches(text, @"(?im)^\s*(def|class|cdef|cpdef)\s+", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    break;
                case ".properties":
                    Add(section, "Property assignments", Regex.Matches(text, @"(?m)^\s*[^#!\s][^:=\r\n]*\s*[:=]", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    break;
                case ".ui":
                    Add(section, "UI object tags", Regex.Matches(text, @"<object\b|<widget\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    Add(section, "UI property tags", Regex.Matches(text, @"<property\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    break;
                case ".bas":
                    Add(section, "Numbered BASIC lines", Regex.Matches(text, @"(?m)^\s*\d+\s+", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Sub/function declarations", Regex.Matches(text, @"(?im)^\s*(sub|function)\s+", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    break;
                case ".nsi":
                case ".nsh":
                    Add(section, "NSIS sections", Regex.Matches(text, @"(?im)^\s*Section\b", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    Add(section, "NSIS functions", Regex.Matches(text, @"(?im)^\s*Function\b", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    break;
                case ".po":
                    Add(section, "Message ids", Regex.Matches(text, @"(?m)^msgid\s+", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Translated strings", Regex.Matches(text, @"(?m)^msgstr\s+", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    break;
                case ".vim":
                    Add(section, "Vim functions", Regex.Matches(text, @"(?im)^\s*fu(nction)?!?\s+", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Vim commands", Regex.Matches(text, @"(?im)^\s*com(mand)?!?\s+", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    break;
                case ".pm":
                case ".pl":
                    Add(section, "Perl packages", Regex.Matches(text, @"(?im)^\s*package\s+[\w:]+", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Perl subroutines", Regex.Matches(text, @"(?im)^\s*sub\s+\w+", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    break;
                case ".tcl":
                    Add(section, "Tcl procedures", Regex.Matches(text, @"(?im)^\s*proc\s+\S+", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    break;
                case ".lua":
                case ".nse":
                    Add(section, "Lua functions", Regex.Matches(text, @"(?im)\bfunction\s+[\w.:]+", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    Add(section, "Lua require calls", Regex.Matches(text, @"(?im)\brequire\s*[\(\x20]", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    break;
                case ".adoc":
                    Add(section, "AsciiDoc headings", Regex.Matches(text, @"(?im)^={1,6}\s+\S", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    break;
                case ".props":
                case ".targets":
                case ".resx":
                case ".xsd":
                case ".xsl":
                case ".xslt":
                case ".qml":
                case ".qmltypes":
                case ".natvis":
                    Add(section, "XML/QML element-like lines", Regex.Matches(text, @"(?im)^\s*<\w+|^\s*[A-Z]\w+\s*\{", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
                    break;
            }
        }

        private static bool IsChromiumHyphenationDictionary(string path, byte[] header)
        {
            return string.Equals(Path.GetExtension(path), ".hyb", StringComparison.OrdinalIgnoreCase) &&
                (StartsWith(header, new byte[] { 0x68, 0x79, 0xAD, 0x62 }) ||
                 path.IndexOf("\\hyphen-data\\", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void AddDebianPackageInfo(ReportSection section, string path, byte[] header)
        {
            Add(section, "Role", "Debian binary package archive.");
            Add(section, "Package file", Path.GetFileName(path));
            Add(section, "Container", "Unix ar archive with debian-binary, control archive, and data archive members.");
            var headerText = Encoding.ASCII.GetString(header.Take(Math.Min(header.Length, 512)).ToArray());
            var members = Regex.Matches(headerText, @"(?m)(debian-binary|control\.tar\.[A-Za-z0-9]+|data\.tar\.[A-Za-z0-9]+)")
                .Cast<Match>()
                .Select(match => match.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (members.Length > 0)
                Add(section, "Visible members", string.Join("\r\n", members));
            Add(section, "Notes", "Debian .deb files are Linux software packages. FileDentify identifies the archive structure only; it does not install packages, run maintainer scripts, or unpack payloads.");
        }

        private static void AddChromeExtensionPackageInfo(ReportSection section, string path, byte[] header)
        {
            Add(section, "Role", "Chrome/Chromium extension package.");
            Add(section, "Package file", Path.GetFileName(path));
            if (StartsWith(header, Encoding.ASCII.GetBytes("Cr24")) && header.Length >= 12)
            {
                Add(section, "Header marker", "Cr24");
                Add(section, "CRX version", ReadUInt32LittleEndian(header, 4).ToString(CultureInfo.InvariantCulture));
                Add(section, "Header size", FormatBytes(ReadUInt32LittleEndian(header, 8)));
            }
            Add(section, "Notes", "Chrome .crx/.crx3 files package browser extensions. FileDentify reports the package header only; it does not install, load, or execute extension code.");
        }

        private static void AddFbxInfo(ReportSection section, string path, byte[] header)
        {
            Add(section, "Role", "Autodesk/Kaydara FBX 3D model or scene asset.");
            Add(section, "Model file", Path.GetFileName(path));
            Add(section, "Header marker", StartsWith(header, Encoding.ASCII.GetBytes("Kaydara FBX Binary")) ? "Kaydara FBX Binary" : "FBX extension");
            var text = DecodeTextSample(header, 64 * 1024);
            var version = Regex.Match(text, @"FBXVersion\\?\0*(?<version>\d{4,})");
            if (version.Success)
                Add(section, "Visible FBX version", version.Groups["version"].Value);
            Add(section, "Notes", "FBX files store 3D model, animation, and scene data used by game engines and digital-content tools. FileDentify reports header markers only; it does not render or import the model.");
        }

        private static void AddMagicDatabaseInfo(ReportSection section, string path, byte[] header)
        {
            Add(section, "Role", "Compiled file/libmagic magic database.");
            Add(section, "Database file", Path.GetFileName(path));
            if (header.Length >= 8)
                Add(section, "Header bytes", BitConverter.ToString(header.Take(8).ToArray()).Replace("-", " "));
            Add(section, "Notes", "magic.mgc files are compiled signature databases used by the Unix file command and libmagic. FileDentify identifies the database container only; it does not import or execute external magic rules.");
        }

        private static bool IsTclEncodingMap(string path, byte[] header)
        {
            return path.IndexOf("\\encoding\\", StringComparison.OrdinalIgnoreCase) >= 0 &&
                DecodeTextSample(header, 4096).IndexOf("Encoding file:", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddTclEncodingMapInfo(ReportSection section, string path, byte[] header)
        {
            var text = DecodeTextSample(header, 64 * 1024);
            Add(section, "Role", "Tcl character-encoding map.");
            Add(section, "Encoding", Path.GetFileNameWithoutExtension(path));
            Add(section, "First line", FirstNonEmptyLine(text));
            Add(section, "Mapping lines in sample", Regex.Matches(text, @"(?m)^[0-9A-Fa-f]{2,6}\s+", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Notes", "Tcl .enc files map byte/codepoint values for Tcl's character encoding support. FileDentify reports the map identity and counts only; it does not compile or load the encoding.");
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
