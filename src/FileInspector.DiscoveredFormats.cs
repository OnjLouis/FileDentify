using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace FileDentify
{
    internal static partial class FileInspector
    {
        private static string DiscoveredFormatTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".contact" && XmlHeaderRootIs(header, "contact", "schemas.microsoft.com/Contact")) return "Windows Contact file";
            if (ext == ".smil" && XmlHeaderRootIs(header, "smil", null)) return "SMIL synchronized multimedia document";
            if (ext == ".acsm" && XmlHeaderRootIs(header, "fulfillmentToken", "ns.adobe.com/adept")) return "Adobe ebook fulfillment token";
            if (ext == ".aup" && XmlHeaderRootIs(header, "project", "audacity.sourceforge.net/xml")) return "Audacity legacy project";
            if (ext == ".kmmacros" && LooksLikeKeyboardMaestroMacros(header)) return "Keyboard Maestro macro library";
            if (ext == ".mamd" && LooksLikeLogicMamd(header)) return "Logic Pro audio metadata sidecar";
            if (ext == ".zdt" && StartsWith(header, Encoding.ASCII.GetBytes("ZOOM L-20    PROJECT DATA VER"))) return "Zoom LiveTrak L-20 project data";
            if (ext == ".dbb" && StartsWith(header, Encoding.ASCII.GetBytes("l33l"))) return "Legacy Skype database";
            if (ext == ".scpt" && StartsWith(header, Encoding.ASCII.GetBytes("FasdUAS"))) return "Compiled AppleScript";
            if (ext == ".pdd" && LooksLikeSymbianDriver(header)) return "Symbian physical device driver";
            if (ext == ".kbd" && LooksLikeSymbianKeyboardLayout(header)) return "Symbian keyboard layout";
            if (ext == ".pml" && LooksLikePmmlMusicSource(header)) return "PMML music macro source";
            if (ext == ".thm" && StartsWith(header, new byte[] { 0xFF, 0xD8, 0xFF })) return "Camera thumbnail JPEG image";
            if (ext == ".srt" && LooksLikeSubRipSubtitles(header)) return "SubRip subtitle file";
            if (ext == ".tga" && LooksLikeTargaImage(header)) return "Truevision Targa image";
            if (ext == ".wmf" && LooksLikeWindowsMetafile(header)) return "Windows Metafile image";
            return null;
        }

        private static string DiscoveredFormatDetectionBasis(string path, byte[] header)
        {
            var type = DiscoveredFormatTypeName(path, header);
            if (type == null)
                return null;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".contact": return ".contact extension plus Microsoft Contact XML namespace";
                case ".smil": return ".smil extension plus SMIL XML root";
                case ".acsm": return ".acsm extension plus Adobe ADEPT fulfillment-token XML root";
                case ".aup": return ".aup extension plus Audacity project XML namespace";
                case ".kmmacros": return ".kmmacros extension plus Keyboard Maestro property-list markers";
                case ".mamd": return ".mamd extension plus FORM/AIFF and Logic metadata chunks";
                case ".zdt": return ".zdt extension plus fixed ZOOM L-20 project header";
                case ".dbb": return ".dbb extension plus legacy Skype l33l database marker";
                case ".scpt": return ".scpt extension plus FasdUAS compiled-script header";
                case ".pdd": return ".pdd extension plus Symbian UID header fields";
                case ".kbd": return ".kbd extension plus Symbian UID and EPOC markers";
                case ".pml": return ".pml extension plus PMML music directives";
                case ".thm": return ".thm extension plus JPEG image header";
                case ".srt": return ".srt extension plus numbered SubRip timing blocks";
                case ".tga": return ".tga extension plus internally consistent Targa image header";
                case ".wmf": return ".wmf extension plus placeable or standard Windows Metafile header";
                default: return ext + " extension plus expected format marker";
            }
        }

        private static void AddDiscoveredFormatInfo(List<ReportSection> sections, string path, byte[] header, long fileLength)
        {
            var type = DiscoveredFormatTypeName(path, header);
            if (type == null)
                return;

            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".contact") AddWindowsContactInfo(sections, header, fileLength);
            else if (ext == ".smil") AddSmilInfo(sections, header, fileLength);
            else if (ext == ".acsm") AddAcsmInfo(sections, header, fileLength);
            else if (ext == ".aup") AddAudacityProjectInfo(sections, header, fileLength);
            else if (ext == ".kmmacros") AddKeyboardMaestroInfo(sections, header, fileLength);
            else if (ext == ".mamd") AddLogicMamdInfo(sections, header, fileLength);
            else if (ext == ".zdt") AddZoomProjectInfo(sections, header, fileLength);
            else if (ext == ".dbb") AddLegacySkypeInfo(sections, path, fileLength);
            else if (ext == ".scpt") AddCompiledAppleScriptInfo(sections, header, fileLength);
            else if (ext == ".pdd" || ext == ".kbd") AddSymbianSpecialInfo(sections, path, header, fileLength, type);
            else if (ext == ".pml") AddPmmlInfo(sections, header, fileLength);
            else if (ext == ".thm") AddCameraThumbnailInfo(sections, fileLength);
            else if (ext == ".srt") AddSubRipInfo(sections, header, fileLength);
            else if (ext == ".tga") AddTargaInfo(sections, header, fileLength);
            else if (ext == ".wmf") AddWindowsMetafileInfo(sections, header, fileLength);
        }

        private static void AddWindowsContactInfo(List<ReportSection> sections, byte[] header, long fileLength)
        {
            var section = AddSection(sections, "Message/contact data");
            var document = TryReadXmlDocument(header);
            Add(section, "Format", "Windows Contact XML");
            Add(section, "File size", FormatBytes(fileLength));
            if (document != null)
            {
                AddFirstXmlElementText(section, document, "FormattedName", "Display name");
                Add(section, "Email addresses", CountXmlLocalName(document, "EmailAddress").ToString(CultureInfo.InvariantCulture));
                Add(section, "Telephone numbers", CountXmlLocalName(document, "PhoneNumber").ToString(CultureInfo.InvariantCulture));
                Add(section, "Postal addresses", CountXmlLocalName(document, "Address").ToString(CultureInfo.InvariantCulture));
                Add(section, "Embedded photos", CountXmlLocalName(document, "Photo").ToString(CultureInfo.InvariantCulture));
            }
            Add(section, "Privacy note", "Windows Contact files can contain names, addresses, phone numbers, email addresses, notes, and photographs. Review reports before sharing them.");
        }

        private static void AddSmilInfo(List<ReportSection> sections, byte[] header, long fileLength)
        {
            var section = AddSection(sections, "Synchronized multimedia");
            var document = TryReadXmlDocument(header);
            Add(section, "Format", "Synchronized Multimedia Integration Language (SMIL)");
            Add(section, "File size", FormatBytes(fileLength));
            if (document != null)
            {
                Add(section, "Sequences", CountXmlLocalName(document, "seq").ToString(CultureInfo.InvariantCulture));
                Add(section, "Parallel groups", CountXmlLocalName(document, "par").ToString(CultureInfo.InvariantCulture));
                Add(section, "Audio references", CountXmlLocalName(document, "audio").ToString(CultureInfo.InvariantCulture));
                Add(section, "Video references", CountXmlLocalName(document, "video").ToString(CultureInfo.InvariantCulture));
                Add(section, "Text references", CountXmlLocalName(document, "text").ToString(CultureInfo.InvariantCulture));
            }
            Add(section, "Notes", "SMIL coordinates timed text, audio, video, and images. It is used by accessible ebooks, multimedia presentations, and some messaging systems; FileDentify does not fetch referenced media.");
        }

        private static void AddAcsmInfo(List<ReportSection> sections, byte[] header, long fileLength)
        {
            var section = AddSection(sections, "Ebook fulfillment token");
            var document = TryReadXmlDocument(header);
            Add(section, "Format", "Adobe Content Server Message (ACSM)");
            Add(section, "File size", FormatBytes(fileLength));
            if (document != null)
            {
                AddFirstXmlElementText(section, document, "title", "Title");
                AddFirstXmlElementText(section, document, "creator", "Creator");
                AddFirstXmlElementText(section, document, "publisher", "Publisher");
                AddFirstXmlElementText(section, document, "expiration", "Expiration");
                AddFirstXmlElementText(section, document, "format", "Publication format");
            }
            Add(section, "Privacy note", "An ACSM file is a small license and fulfillment token, not the ebook itself. FileDentify deliberately omits account, operator, resource, HMAC, and license-token values and does not activate or download the publication.");
        }

        private static void AddAudacityProjectInfo(List<ReportSection> sections, byte[] header, long fileLength)
        {
            var section = AddSection(sections, "Audacity project");
            var document = TryReadXmlDocument(header);
            Add(section, "Format", "Legacy Audacity XML project");
            Add(section, "File size", FormatBytes(fileLength));
            if (document != null && document.DocumentElement != null)
            {
                AddXmlRootAttribute(section, document, "audacityversion", "Audacity version");
                AddXmlRootAttribute(section, document, "version", "Project format version");
                AddXmlRootAttribute(section, document, "projname", "Project data folder");
                AddXmlRootAttribute(section, document, "rate", "Project sample rate");
                Add(section, "Wave tracks", CountXmlLocalName(document, "wavetrack").ToString(CultureInfo.InvariantCulture));
                Add(section, "Wave clips", CountXmlLocalName(document, "waveclip").ToString(CultureInfo.InvariantCulture));
                Add(section, "Audio blocks", CountXmlLocalName(document, "simpleblockfile").ToString(CultureInfo.InvariantCulture));
            }
            Add(section, "Notes", "An .aup file is the project description for older Audacity releases and normally depends on a companion _data folder containing audio blocks. FileDentify does not open or render the project.");
        }

        private static void AddXmlRootAttribute(ReportSection section, XmlDocument document, string attributeName, string title)
        {
            var value = document.DocumentElement == null ? string.Empty : document.DocumentElement.GetAttribute(attributeName);
            if (!string.IsNullOrWhiteSpace(value))
                Add(section, title, CleanMetadataText(value));
        }

        private static bool LooksLikeKeyboardMaestroMacros(byte[] header)
        {
            return XmlHeaderRootIs(header, "plist", null) &&
                (IndexOfAscii(header, "Keyboard Maestro") >= 0 || IndexOfAscii(header, "<key>Macros</key>") >= 0);
        }

        private static void AddKeyboardMaestroInfo(List<ReportSection> sections, byte[] header, long fileLength)
        {
            var text = DecodeMostlyUtf8(header);
            var section = AddSection(sections, "Keyboard Maestro macros");
            Add(section, "Format", "Keyboard Maestro macro-library property list");
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "Macro collections in sample", CountToken(text, "<key>Macros</key>").ToString(CultureInfo.InvariantCulture));
            Add(section, "Enabled-state fields in sample", CountToken(text, "<key>IsActive</key>").ToString(CultureInfo.InvariantCulture));
            Add(section, "Privacy note", "Macro libraries can contain typed text, paths, URLs, application names, scripts, and automation actions. FileDentify reports structure rather than macro contents.");
        }

        private static bool LooksLikeLogicMamd(byte[] header)
        {
            return header != null && header.Length >= 16 && StartsWith(header, Encoding.ASCII.GetBytes("FORM")) &&
                header[8] == (byte)'A' && header[9] == (byte)'I' && header[10] == (byte)'F' && header[11] == (byte)'F' &&
                (IndexOfAscii(header, "LGBM") >= 0 || IndexOfAscii(header, "Logic Pro") >= 0);
        }

        private static void AddLogicMamdInfo(List<ReportSection> sections, byte[] header, long fileLength)
        {
            var section = AddSection(sections, "Logic Pro audio metadata");
            Add(section, "Format", "Logic Pro MAMD metadata/analysis sidecar");
            Add(section, "Container", "FORM/AIFF-style chunk container");
            Add(section, "File size", FormatBytes(fileLength));
            var chunks = new[] { "COMT", "COMM", "CHAN", "SSND", "LGBM", "ResU" }.Where(name => IndexOfAscii(header, name) >= 0).ToArray();
            if (chunks.Length > 0) Add(section, "Observed chunks", string.Join(Environment.NewLine, chunks));
            var creator = FindReadableTextLines(header, 4, 120).FirstOrDefault(value => value.IndexOf("Creator:", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrWhiteSpace(creator)) Add(section, "Creator marker", CleanMetadataText(creator));
            Add(section, "Notes", "MAMD files accompany Logic Pro media and can store analysis and metadata. Their AIFF-style structure does not mean they are independently playable audio files.");
        }

        private static void AddZoomProjectInfo(List<ReportSection> sections, byte[] header, long fileLength)
        {
            var text = DecodeMostlyUtf8(header);
            var match = Regex.Match(text, @"ZOOM L-20\s+PROJECT DATA VER(?<version>[0-9]+)", RegexOptions.CultureInvariant);
            var section = AddSection(sections, "Zoom LiveTrak project");
            Add(section, "Format", "Zoom LiveTrak L-20 project data");
            Add(section, "Project-data version", match.Success ? match.Groups["version"].Value : "Not reported");
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "Channel labels in sample", Regex.Matches(text, @"\bCH[0-9]{1,2}\b", RegexOptions.CultureInvariant).Cast<Match>().Select(item => item.Value).Distinct(StringComparer.OrdinalIgnoreCase).Count().ToString(CultureInfo.InvariantCulture));
            Add(section, "Notes", "This proprietary project file stores settings for a Zoom LiveTrak L-20 recording/mixing session. FileDentify reports the fixed product header and safe visible metadata without modifying the project.");
        }

        private static void AddLegacySkypeInfo(List<ReportSection> sections, string path, long fileLength)
        {
            var section = AddSection(sections, "Legacy Skype database");
            Add(section, "Format", "Legacy Skype l33l database/cache");
            Add(section, "Database role hint", LegacySkypeRole(Path.GetFileNameWithoutExtension(path)));
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "Privacy note", "Legacy Skype databases can contain account identifiers, contacts, call records, chat metadata, and message text. FileDentify does not expose database strings in this section; review any full report before sharing it.");
        }

        private static string LegacySkypeRole(string name)
        {
            var value = (name ?? string.Empty).ToLowerInvariant();
            if (value.Contains("chatmsg")) return "Chat-message database";
            if (value.Contains("chat")) return "Chat database";
            if (value.Contains("call")) return "Call database";
            if (value.Contains("contact")) return "Contact database";
            return "Skype application database or cache";
        }

        private static void AddCompiledAppleScriptInfo(List<ReportSection> sections, byte[] header, long fileLength)
        {
            var marker = Encoding.ASCII.GetString(header, 0, Math.Min(header.Length, 32)).TrimEnd('\0', ' ');
            var section = AddSection(sections, "AppleScript");
            Add(section, "Format", "Compiled AppleScript");
            Add(section, "Header marker", CleanMetadataText(marker));
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "Notes", "Compiled AppleScript files contain executable automation instructions for macOS. FileDentify identifies the compiled-script header but does not run or decompile the script.");
        }

        private static bool LooksLikeSymbianDriver(byte[] header)
        {
            return header != null && header.Length >= 16 && ReadUInt32LittleEndian(header, 0) == 0x10000079 && HasSymbianUidFields(header);
        }

        private static bool LooksLikeSymbianKeyboardLayout(byte[] header)
        {
            return LooksLikeSymbianDriver(header) && IndexOfAscii(header, "EPOC") >= 0;
        }

        private static void AddSymbianSpecialInfo(List<ReportSection> sections, string path, byte[] header, long fileLength, string type)
        {
            var section = AddSection(sections, "Symbian app/resource");
            Add(section, "Format hint", type);
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "Item name", Path.GetFileName(path));
            Add(section, "UID1", "0x" + ReadUInt32LittleEndian(header, 0).ToString("X8", CultureInfo.InvariantCulture));
            Add(section, "UID2", "0x" + ReadUInt32LittleEndian(header, 4).ToString("X8", CultureInfo.InvariantCulture));
            Add(section, "UID3", "0x" + ReadUInt32LittleEndian(header, 8).ToString("X8", CultureInfo.InvariantCulture));
            Add(section, "Notes", Path.GetExtension(path).Equals(".pdd", StringComparison.OrdinalIgnoreCase)
                ? "A PDD is a low-level Symbian physical device driver. FileDentify reports its UID header without loading or disassembling driver code."
                : "This file maps hardware keyboard input for Symbian/EPOC devices. FileDentify reports identity fields without applying the layout.");
        }

        private static bool LooksLikePmmlMusicSource(byte[] header)
        {
            var text = DecodeMostlyUtf8(header);
            return text.IndexOf("newqwstrack", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (Regex.IsMatch(text, @"(?im)^\s*include\s*\(") && Regex.IsMatch(text, @"(?im)^\s*(tempo|title)\s*\("));
        }

        private static void AddPmmlInfo(List<ReportSection> sections, byte[] header, long fileLength)
        {
            var text = DecodeMostlyUtf8(header);
            var section = AddSection(sections, "Music macro source");
            Add(section, "Format", "PMML text music-macro source");
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "Track declarations in sample", Regex.Matches(text, @"\bnewqwstrack\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Tempo directives in sample", Regex.Matches(text, @"(?im)^\s*tempo\s*\(", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Notes", "The .pml extension is shared by unrelated formats. FileDentify labels this as music source only when PMML-style track or score directives are present; it does not render or execute the source.");
        }

        private static void AddCameraThumbnailInfo(List<ReportSection> sections, long fileLength)
        {
            var section = AddSection(sections, "Camera thumbnail");
            Add(section, "Format", "JPEG camera/video thumbnail");
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "Role", "Small preview image stored beside camera video or media files.");
            Add(section, "Notes", "THM files commonly use ordinary JPEG encoding with a camera-specific extension. Image and EXIF sections may reveal dimensions, camera model, capture time, and firmware metadata when available.");
        }

        private static bool LooksLikeSubRipSubtitles(byte[] header)
        {
            var text = DecodeMostlyUtf8(header).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
            return Regex.IsMatch(text, @"(?m)^\s*\d+\s*\r?$\n\s*\d{1,2}:\d{2}:\d{2}[,.]\d{3}\s*-->\s*\d{1,2}:\d{2}:\d{2}[,.]\d{3}", RegexOptions.CultureInvariant);
        }

        private static void AddSubRipInfo(List<ReportSection> sections, byte[] header, long fileLength)
        {
            var text = DecodeMostlyUtf8(header);
            var timings = Regex.Matches(text, @"(?m)^\s*(?<start>\d{1,2}:\d{2}:\d{2}[,.]\d{3})\s*-->\s*(?<end>\d{1,2}:\d{2}:\d{2}[,.]\d{3})", RegexOptions.CultureInvariant);
            var section = AddSection(sections, "Subtitles");
            Add(section, "Format", "SubRip timed-text subtitles");
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "Cue timing lines in sample", timings.Count.ToString(CultureInfo.InvariantCulture));
            if (timings.Count > 0)
            {
                Add(section, "First cue starts", timings[0].Groups["start"].Value);
                Add(section, "Last sampled cue ends", timings[timings.Count - 1].Groups["end"].Value);
            }
            Add(section, "Notes", "SubRip files contain timed subtitle text and may include speaker names or dialogue. FileDentify reports timing structure without interpreting or translating the content.");
        }

        private static bool LooksLikeTargaImage(byte[] header)
        {
            if (header == null || header.Length < 18)
                return false;
            var imageType = header[2];
            if (!(imageType == 1 || imageType == 2 || imageType == 3 || imageType == 9 || imageType == 10 || imageType == 11))
                return false;
            var width = ReadUInt16LittleEndian(header, 12);
            var height = ReadUInt16LittleEndian(header, 14);
            var depth = header[16];
            return width > 0 && height > 0 && (depth == 8 || depth == 15 || depth == 16 || depth == 24 || depth == 32) && header[1] <= 1;
        }

        private static void AddTargaInfo(List<ReportSection> sections, byte[] header, long fileLength)
        {
            var section = AddSection(sections, "Image");
            Add(section, "Format", "Truevision Targa (TGA)");
            Add(section, "Dimensions", ReadUInt16LittleEndian(header, 12).ToString(CultureInfo.InvariantCulture) + " x " + ReadUInt16LittleEndian(header, 14).ToString(CultureInfo.InvariantCulture));
            Add(section, "Bits per pixel", header[16].ToString(CultureInfo.InvariantCulture));
            Add(section, "Image encoding", TargaImageTypeName(header[2]));
            Add(section, "Color map", header[1] == 1 ? "Present" : "Not present");
            Add(section, "File size", FormatBytes(fileLength));
        }

        private static string TargaImageTypeName(byte imageType)
        {
            switch (imageType)
            {
                case 1: return "Uncompressed color-mapped";
                case 2: return "Uncompressed true-color";
                case 3: return "Uncompressed grayscale";
                case 9: return "RLE color-mapped";
                case 10: return "RLE true-color";
                case 11: return "RLE grayscale";
                default: return "Type " + imageType.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static bool LooksLikeWindowsMetafile(byte[] header)
        {
            if (header == null || header.Length < 18)
                return false;
            if (header.Length >= 22 && ReadUInt32LittleEndian(header, 0) == 0x9AC6CDD7)
                return true;
            var type = ReadUInt16LittleEndian(header, 0);
            var headerWords = ReadUInt16LittleEndian(header, 2);
            var version = ReadUInt16LittleEndian(header, 4);
            return (type == 1 || type == 2) && headerWords == 9 && (version == 0x0100 || version == 0x0300);
        }

        private static void AddWindowsMetafileInfo(List<ReportSection> sections, byte[] header, long fileLength)
        {
            var placeable = header.Length >= 22 && ReadUInt32LittleEndian(header, 0) == 0x9AC6CDD7;
            var section = AddSection(sections, "Image");
            Add(section, "Format", placeable ? "Aldus placeable Windows Metafile" : "Windows Metafile");
            Add(section, "File size", FormatBytes(fileLength));
            if (placeable)
            {
                var left = (short)ReadUInt16LittleEndian(header, 6);
                var top = (short)ReadUInt16LittleEndian(header, 8);
                var right = (short)ReadUInt16LittleEndian(header, 10);
                var bottom = (short)ReadUInt16LittleEndian(header, 12);
                Add(section, "Logical bounds", left.ToString(CultureInfo.InvariantCulture) + ", " + top.ToString(CultureInfo.InvariantCulture) + " to " + right.ToString(CultureInfo.InvariantCulture) + ", " + bottom.ToString(CultureInfo.InvariantCulture));
                Add(section, "Units per inch", ReadUInt16LittleEndian(header, 14).ToString(CultureInfo.InvariantCulture));
            }
            Add(section, "Notes", "WMF is an older Windows vector/graphics command format. FileDentify reports header geometry only and does not render or execute embedded drawing records.");
        }
    }
}
