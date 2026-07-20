using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace FileDentify
{
    internal static partial class FileInspector
    {
        private static string StructuredFormatTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();

            if (ext == ".mum" && XmlHeaderRootIs(header, "assembly", "schemas-microsoft-com:asm.v3"))
                return "Windows servicing package manifest";
            if (ext == ".cdf-ms" && StartsWith(header, Encoding.ASCII.GetBytes("PcmH")))
                return "Windows component definition metadata";
            if (ext == ".admx" && XmlHeaderRootIs(header, "policyDefinitions", null))
                return "Group Policy administrative template";
            if (ext == ".adml" && XmlHeaderRootIs(header, "policyDefinitionResources", null))
                return "Group Policy language resource";
            if (ext == ".resw" && XmlHeaderRootIs(header, "root", null) && IndexOfAscii(header, "Microsoft ResX Schema") >= 0)
                return "Windows XML resource file";
            if (ext == ".diagpkg" && XmlHeaderRootIs(header, "DiagnosticPackage", null))
                return "Windows diagnostic package definition";
            if (ext == ".compositefont" && XmlHeaderRootContains(header, "Font"))
                return "Windows composite font definition";
            if (LooksLikeJmod(header))
                return "Java JMOD module archive";
            if (LooksLikeLuaBytecode(header))
                return "Lua precompiled bytecode";
            if (ext == ".opf" && XmlHeaderRootIs(header, "package", "idpf.org"))
                return "EPUB package document";
            if (ext == ".mtl" && LooksLikeWavefrontMaterial(header))
                return "Wavefront OBJ material library";
            return null;
        }

        private static void AddStructuredFormatInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
        {
            var type = StructuredFormatTypeName(path, header);
            if (type == null)
                return;

            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".mum" || ext == ".cdf-ms" || ext == ".admx" || ext == ".adml" || ext == ".resw" || ext == ".diagpkg" || ext == ".compositefont")
                AddWindowsStructuredInfo(sections, path, header, fileLength, type);
            else if (LooksLikeJmod(header))
                AddJmodInfo(sections, header, fileLength);
            else if (LooksLikeLuaBytecode(header))
                AddLuaBytecodeInfo(sections, header, fileLength);
            else if (ext == ".opf")
                AddOpfInfo(sections, header, fileLength);
            else if (ext == ".mtl")
                AddWavefrontMaterialInfo(sections, header, fileLength);
        }

        private static string StructuredFormatDetectionBasis(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".cdf-ms" && StartsWith(header, Encoding.ASCII.GetBytes("PcmH"))) return ".cdf-ms extension plus PcmH binary header";
            if (ext == ".mum") return ".mum extension plus Microsoft assembly v3 XML root";
            if (ext == ".admx") return ".admx extension plus policyDefinitions XML root";
            if (ext == ".adml") return ".adml extension plus policyDefinitionResources XML root";
            if (ext == ".resw") return ".resw extension plus ResX-schema XML structure";
            if (ext == ".diagpkg") return ".diagpkg extension plus DiagnosticPackage XML root";
            if (ext == ".compositefont") return ".compositefont extension plus font-family XML root";
            if (LooksLikeJmod(header)) return "JM header immediately followed by a ZIP-compatible archive header";
            if (LooksLikeLuaBytecode(header)) return "ESC-Lua precompiled-chunk header";
            if (ext == ".opf") return ".opf extension plus EPUB/IDPF package XML root";
            if (ext == ".mtl") return ".mtl extension plus Wavefront material directives";
            return null;
        }

        private static void AddWindowsStructuredInfo(List<ReportSection> sections, string path, byte[] header, long fileLength, string type)
        {
            var section = AddSection(sections, WindowsStructuredSectionName(Path.GetExtension(path)));
            Add(section, "Format", type);
            Add(section, "File size", FormatBytes(fileLength));

            var ext = Path.GetExtension(path).ToLowerInvariant();
            var document = TryReadXmlDocument(header);
            if (ext == ".cdf-ms")
            {
                Add(section, "Header marker", "PcmH");
                Add(section, "Role", "Binary component-definition metadata used by Windows servicing and component storage.");
            }
            else if (document != null)
            {
                Add(section, "XML root", document.DocumentElement.LocalName);
                if (ext == ".mum")
                {
                    AddXmlAttribute(section, document, "assemblyIdentity", "name", "Package identity");
                    AddXmlAttribute(section, document, "assemblyIdentity", "version", "Package version");
                    AddXmlAttribute(section, document, "assemblyIdentity", "processorArchitecture", "Processor architecture");
                    AddXmlAttribute(section, document, "assemblyIdentity", "language", "Language");
                }
                else if (ext == ".admx" || ext == ".adml")
                {
                    Add(section, "Role", ext == ".admx" ? "Language-neutral registry-based policy definitions." : "Language-specific labels and presentation resources for an ADMX template.");
                    Add(section, "Policy elements", CountXmlLocalName(document, "policy").ToString(CultureInfo.InvariantCulture));
                    Add(section, "Category elements", CountXmlLocalName(document, "category").ToString(CultureInfo.InvariantCulture));
                    Add(section, "String resources", CountXmlLocalName(document, "string").ToString(CultureInfo.InvariantCulture));
                }
                else if (ext == ".resw")
                {
                    Add(section, "Resource entries", CountXmlLocalName(document, "data").ToString(CultureInfo.InvariantCulture));
                    Add(section, "Role", "Source XML for localized Windows application or component resources.");
                }
                else if (ext == ".diagpkg")
                {
                    AddXmlAttribute(section, document, "DiagnosticPackage", "SchemaVersion", "Schema version");
                    AddXmlAttribute(section, document, "DiagnosticPackage", "Localized", "Localized");
                    Add(section, "Troubleshooter interactions", CountXmlLocalName(document, "Interaction").ToString(CultureInfo.InvariantCulture));
                    Add(section, "Role", "Definition and metadata for a Windows troubleshooting or diagnostic package.");
                }
                else if (ext == ".compositefont")
                {
                    Add(section, "Font-family entries", CountXmlLocalName(document, "FontFamily").ToString(CultureInfo.InvariantCulture));
                    Add(section, "Family maps", CountXmlLocalName(document, "FontFamilyMap").ToString(CultureInfo.InvariantCulture));
                    Add(section, "Role", "Maps Unicode ranges and language preferences to fallback font families.");
                }
            }

            Add(section, "Notes", "FileDentify reports safe headers and XML metadata only. It does not install, register, apply, or modify Windows system data.");
        }

        private static string WindowsStructuredSectionName(string extension)
        {
            switch ((extension ?? string.Empty).ToLowerInvariant())
            {
                case ".mum": return "Windows servicing manifest";
                case ".cdf-ms": return "Windows component metadata";
                case ".admx":
                case ".adml": return "Group Policy template";
                case ".resw": return "Windows resource XML";
                case ".diagpkg": return "Windows diagnostic package";
                case ".compositefont": return "Windows composite font";
                default: return "Windows structured data";
            }
        }

        private static bool LooksLikeJmod(byte[] header)
        {
            return header.Length >= 8 && header[0] == (byte)'J' && header[1] == (byte)'M' &&
                header[4] == (byte)'P' && header[5] == (byte)'K' && header[6] == 0x03 && header[7] == 0x04;
        }

        private static void AddJmodInfo(List<ReportSection> sections, byte[] header, long fileLength)
        {
            var section = AddSection(sections, "Java module");
            Add(section, "Format", "JMOD module archive");
            Add(section, "Header", "JM followed by a ZIP-compatible archive");
            Add(section, "JMOD header version", header[2].ToString(CultureInfo.InvariantCulture) + "." + header[3].ToString(CultureInfo.InvariantCulture));
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "Common contents", "Java classes, native libraries, configuration files, headers, legal notices, and module metadata.");
            Add(section, "Notes", "JMOD files are used by the JDK at compile or link time and are not directly executable application archives.");
        }

        private static bool LooksLikeLuaBytecode(byte[] header)
        {
            return header.Length >= 6 && header[0] == 0x1B && header[1] == (byte)'L' && header[2] == (byte)'u' && header[3] == (byte)'a';
        }

        private static void AddLuaBytecodeInfo(List<ReportSection> sections, byte[] header, long fileLength)
        {
            var section = AddSection(sections, "Lua bytecode");
            Add(section, "Format", "Precompiled Lua binary chunk");
            Add(section, "Lua version", LuaVersionName(header[4]));
            Add(section, "Format version", "0x" + header[5].ToString("X2", CultureInfo.InvariantCulture));
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "Notes", "Precompiled Lua chunks are executable program data for a compatible Lua runtime. FileDentify does not decompile or run them.");
        }

        private static string LuaVersionName(byte value)
        {
            var major = value >> 4;
            var minor = value & 0x0F;
            return major.ToString(CultureInfo.InvariantCulture) + "." + minor.ToString(CultureInfo.InvariantCulture) + " (header 0x" + value.ToString("X2", CultureInfo.InvariantCulture) + ")";
        }

        private static void AddOpfInfo(List<ReportSection> sections, byte[] header, long fileLength)
        {
            var section = AddSection(sections, "EPUB package document");
            Add(section, "Format", "Open Packaging Format XML");
            Add(section, "File size", FormatBytes(fileLength));
            var document = TryReadXmlDocument(header);
            if (document != null)
            {
                AddXmlAttribute(section, document, "package", "version", "EPUB package version");
                AddFirstXmlElementText(section, document, "title", "Title");
                AddFirstXmlElementText(section, document, "creator", "Creator");
                AddFirstXmlElementText(section, document, "language", "Language");
                AddFirstXmlElementText(section, document, "identifier", "Identifier");
                Add(section, "Manifest items", CountXmlLocalName(document, "item").ToString(CultureInfo.InvariantCulture));
                Add(section, "Spine items", CountXmlLocalName(document, "itemref").ToString(CultureInfo.InvariantCulture));
            }
            Add(section, "Notes", "An .opf file describes an EPUB publication's metadata, resources, and reading order. It is normally stored inside an EPUB container or an unpacked ebook folder.");
        }

        private static bool LooksLikeWavefrontMaterial(byte[] header)
        {
            var text = DecodeMostlyUtf8(header);
            return text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.TrimStart())
                .Any(line => line.StartsWith("newmtl ", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("Kd ", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("map_Kd ", StringComparison.OrdinalIgnoreCase));
        }

        private static void AddWavefrontMaterialInfo(List<ReportSection> sections, byte[] header, long fileLength)
        {
            var section = AddSection(sections, "3D material library");
            var lines = DecodeMostlyUtf8(header).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()).ToArray();
            var materials = lines.Where(line => line.StartsWith("newmtl ", StringComparison.OrdinalIgnoreCase)).Select(line => line.Substring(7).Trim()).Where(value => value.Length > 0).Take(24).ToArray();
            var textures = lines.Where(line => line.StartsWith("map_", StringComparison.OrdinalIgnoreCase)).Select(line => line.Substring(line.IndexOf(' ') + 1).Trim()).Where(value => value.Length > 0).Take(24).ToArray();
            Add(section, "Format", "Wavefront OBJ material template library");
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "Materials in sample", materials.Length.ToString(CultureInfo.InvariantCulture));
            if (materials.Length > 0) Add(section, "Material names", string.Join(Environment.NewLine, materials));
            if (textures.Length > 0) Add(section, "Referenced textures", string.Join(Environment.NewLine, textures));
            Add(section, "Notes", "MTL files describe surface colors, illumination, transparency, and texture references used by Wavefront OBJ 3D models.");
        }

        private static XmlDocument TryReadXmlDocument(byte[] header)
        {
            if (header == null || header.Length == 0)
                return null;
            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using (var stream = new MemoryStream(header, false))
                using (var reader = XmlReader.Create(stream, settings))
                {
                    var document = new XmlDocument { XmlResolver = null };
                    document.Load(reader);
                    return document;
                }
            }
            catch
            {
                return null;
            }
        }

        private static bool XmlHeaderRootIs(byte[] header, string localName, string namespaceFragment)
        {
            string actualName;
            string actualNamespace;
            return TryReadXmlRootInfo(header, out actualName, out actualNamespace) &&
                string.Equals(actualName, localName, StringComparison.OrdinalIgnoreCase) &&
                (namespaceFragment == null || actualNamespace.IndexOf(namespaceFragment, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool XmlHeaderRootContains(byte[] header, string fragment)
        {
            string actualName;
            string actualNamespace;
            return TryReadXmlRootInfo(header, out actualName, out actualNamespace) &&
                actualName.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryReadXmlRootInfo(byte[] header, out string localName, out string namespaceUri)
        {
            localName = string.Empty;
            namespaceUri = string.Empty;
            if (header == null || header.Length == 0)
                return false;
            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using (var stream = new MemoryStream(header, false))
                using (var reader = XmlReader.Create(stream, settings))
                {
                    reader.MoveToContent();
                    localName = reader.LocalName;
                    namespaceUri = reader.NamespaceURI ?? string.Empty;
                    return localName.Length > 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private static int CountXmlLocalName(XmlDocument document, string localName)
        {
            return document.SelectNodes("//*[local-name()='" + localName + "']").Count;
        }

        private static void AddXmlAttribute(ReportSection section, XmlDocument document, string elementName, string attributeName, string title)
        {
            var element = document.SelectSingleNode("//*[local-name()='" + elementName + "']") as XmlElement;
            if (element == null)
                return;
            var value = element.GetAttribute(attributeName);
            if (!string.IsNullOrWhiteSpace(value))
                Add(section, title, value);
        }

        private static void AddFirstXmlElementText(ReportSection section, XmlDocument document, string elementName, string title)
        {
            var element = document.SelectSingleNode("//*[local-name()='" + elementName + "']") as XmlElement;
            if (element != null && !string.IsNullOrWhiteSpace(element.InnerText))
                Add(section, title, CleanMetadataText(element.InnerText));
        }
    }
}
