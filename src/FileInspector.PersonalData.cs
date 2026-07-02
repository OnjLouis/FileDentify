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
        private static string PersonalDataTypeName(string path, byte[] header)
        {
            var text = DecodePersonalText(header);
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (LooksLikeNokiaVmg(ext, text)) return "Nokia/Symbian VMG message";
            if (LooksLikeVCard(ext, text)) return "vCard contact";
            if (LooksLikeICalendar(ext, text)) return "iCalendar data";
            if (LooksLikeOpml(ext, text)) return "OPML subscription list";
            if (LooksLikeEmailMessage(ext, text)) return "Email message";
            if (LooksLikeWindowsMediaEncoder(ext, text)) return "Windows Media Encoder session";
            if (LooksLikeWindowsMediaPlaylist(ext, text)) return "Windows Media Player playlist";
            if (LooksLikeM3uPlaylist(ext, text)) return "M3U/M3U8 media playlist";
            return null;
        }

        private static void AddPersonalDataInfo(List<ReportSection> sections, string path, byte[] header)
        {
            var text = DecodePersonalText(header);
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (LooksLikeNokiaVmg(ext, text))
                AddNokiaVmgInfo(sections, text);
            else if (LooksLikeVCard(ext, text))
                AddVCardInfo(sections, text);
            else if (LooksLikeICalendar(ext, text))
                AddICalendarInfo(sections, text);
            else if (LooksLikeOpml(ext, text))
                AddOpmlInfo(sections, text);
            else if (LooksLikeEmailMessage(ext, text))
                AddEmailInfo(sections, text);
            else if (LooksLikeWindowsMediaEncoder(ext, text))
                AddWindowsMediaEncoderInfo(sections, text);
            else if (LooksLikeWindowsMediaPlaylist(ext, text))
                AddWindowsMediaPlaylistInfo(sections, text);
            else if (LooksLikeM3uPlaylist(ext, text))
                AddM3uPlaylistInfo(sections, path, text);
        }

        private static bool LooksLikeNokiaVmg(string ext, string text)
        {
            return ext == ".vmg" && text.IndexOf("BEGIN:VMSG", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddNokiaVmgInfo(List<ReportSection> sections, string text)
        {
            var section = AddSection(sections, "Message/contact data");
            Add(section, "Format", "Nokia/Symbian VMG saved message");
            Add(section, "Version", ValueOrNotReported(FindCalendarValue(text, "VERSION")));
            Add(section, "Status", ValueOrNotReported(FindCalendarValue(text, "X-IRMC-STATUS")));
            Add(section, "Box/folder", ValueOrNotReported(FindCalendarValue(text, "X-IRMC-BOX")));
            Add(section, "Message timestamp", ValueOrNotReported(FindCalendarValue(text, "X-NOK-DT")));
            Add(section, "Embedded contacts", CountToken(text, "BEGIN:VCARD").ToString(CultureInfo.InvariantCulture));
            Add(section, "Body blocks", CountToken(text, "BEGIN:VBODY").ToString(CultureInfo.InvariantCulture));
            Add(section, "Phone fields", CountLinePrefix(text, "TEL").ToString(CultureInfo.InvariantCulture));
            Add(section, "Privacy note", "VMG files can contain phone numbers and full message bodies. FileDentify reports structure and selected metadata here; review full readable text before sharing reports.");
        }

        private static bool LooksLikeVCard(string ext, string text)
        {
            return ext == ".vcf" && text.IndexOf("BEGIN:VCARD", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddVCardInfo(List<ReportSection> sections, string text)
        {
            var unfolded = UnfoldStructuredLines(text);
            var section = AddSection(sections, "Message/contact data");
            Add(section, "Format", "vCard contact");
            Add(section, "Cards", CountToken(unfolded, "BEGIN:VCARD").ToString(CultureInfo.InvariantCulture));
            Add(section, "Version", ValueOrNotReported(FindCalendarValue(unfolded, "VERSION")));
            Add(section, "Display name", ValueOrNotReported(FindCalendarValue(unfolded, "FN")));
            Add(section, "Email fields", CountLinePrefix(unfolded, "EMAIL").ToString(CultureInfo.InvariantCulture));
            Add(section, "Telephone fields", CountLinePrefix(unfolded, "TEL").ToString(CultureInfo.InvariantCulture));
            Add(section, "Address fields", CountLinePrefix(unfolded, "ADR").ToString(CultureInfo.InvariantCulture));
            Add(section, "Organization fields", CountLinePrefix(unfolded, "ORG").ToString(CultureInfo.InvariantCulture));
            Add(section, "Privacy note", "vCard files often contain names, phone numbers, email addresses, postal addresses, and notes.");
        }

        private static bool LooksLikeICalendar(string ext, string text)
        {
            return ext == ".ics" && text.IndexOf("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddICalendarInfo(List<ReportSection> sections, string text)
        {
            var unfolded = UnfoldStructuredLines(text);
            var section = AddSection(sections, "Message/contact data");
            Add(section, "Format", "iCalendar");
            Add(section, "Product", ValueOrNotReported(FindCalendarValue(unfolded, "PRODID")));
            Add(section, "Version", ValueOrNotReported(FindCalendarValue(unfolded, "VERSION")));
            Add(section, "Calendar scale", ValueOrNotReported(FindCalendarValue(unfolded, "CALSCALE")));
            Add(section, "Events", CountToken(unfolded, "BEGIN:VEVENT").ToString(CultureInfo.InvariantCulture));
            Add(section, "Todos/reminders", CountToken(unfolded, "BEGIN:VTODO").ToString(CultureInfo.InvariantCulture));
            Add(section, "Alarms", CountToken(unfolded, "BEGIN:VALARM").ToString(CultureInfo.InvariantCulture));
            Add(section, "Attendee fields", CountLinePrefix(unfolded, "ATTENDEE").ToString(CultureInfo.InvariantCulture));
            Add(section, "Privacy note", "Calendar files may contain attendees, email addresses, locations, notes, and meeting URLs.");
        }

        private static bool LooksLikeOpml(string ext, string text)
        {
            return ext == ".opml" && text.IndexOf("<opml", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddOpmlInfo(List<ReportSection> sections, string text)
        {
            var section = AddSection(sections, "Message/contact data");
            Add(section, "Format", "OPML outline/subscription list");
            Add(section, "Title", ValueOrNotReported(FirstXmlText(text, "title")));
            Add(section, "Outline entries", Regex.Matches(text, "<outline\\b", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Feed URL entries", Regex.Matches(text, "\\bxmlUrl\\s*=", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
            var examples = Regex.Matches(text, "\\btext\\s*=\\s*\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase)
                .Cast<Match>()
                .Select(match => CleanMetadataText(match.Groups["value"].Value))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Take(12)
                .ToArray();
            if (examples.Length > 0)
                Add(section, "Example outline labels", string.Join("\r\n", examples));
            Add(section, "Privacy note", "OPML files commonly contain RSS/feed subscriptions or reading lists.");
        }

        private static bool LooksLikeEmailMessage(string ext, string text)
        {
            if (ext != ".eml")
                return false;
            return Regex.IsMatch(text, @"(?im)^(From|To|Subject|Date|Received):\s+");
        }

        private static void AddEmailInfo(List<ReportSection> sections, string text)
        {
            var section = AddSection(sections, "Message/contact data");
            Add(section, "Format", "RFC 822 / MIME email message");
            Add(section, "From", ValueOrNotReported(FindHeaderValue(text, "From")));
            Add(section, "To", ValueOrNotReported(FindHeaderValue(text, "To")));
            Add(section, "Date", ValueOrNotReported(FindHeaderValue(text, "Date")));
            Add(section, "Subject", ValueOrNotReported(FindHeaderValue(text, "Subject")));
            Add(section, "Received headers", CountLinePrefix(text, "Received").ToString(CultureInfo.InvariantCulture));
            Add(section, "MIME parts", CountToken(text, "Content-Type:").ToString(CultureInfo.InvariantCulture));
            Add(section, "Attachments hinted", CountToken(text, "Content-Disposition: attachment").ToString(CultureInfo.InvariantCulture));
            Add(section, "Privacy note", "Email files may contain private addresses, subjects, headers, and message bodies.");
        }

        private static bool LooksLikeWindowsMediaEncoder(string ext, string text)
        {
            return ext == ".wme" && text.IndexOf("<WMEncoder", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool LooksLikeWindowsMediaPlaylist(string ext, string text)
        {
            return ext == ".wpl" && (text.IndexOf("<?wpl", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("<smil", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool LooksLikeM3uPlaylist(string ext, string text)
        {
            if (ext != ".m3u" && ext != ".m3u8")
                return false;
            if (text.IndexOf("#EXTM3U", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return PlaylistSourceLines(text).Any();
        }

        private static void AddWindowsMediaEncoderInfo(List<ReportSection> sections, string text)
        {
            var section = AddSection(sections, "Message/contact data");
            Add(section, "Format", "Windows Media Encoder session");
            Add(section, "Session name", ValueOrNotReported(FirstXmlAttribute(text, "WMEncoder", "Name")));
            Add(section, "Author", ValueOrNotReported(FirstXmlAttribute(text, "Description", "Author")));
            Add(section, "Title", ValueOrNotReported(FirstXmlAttribute(text, "Description", "Title")));
            Add(section, "Source groups", Regex.Matches(text, "<SourceGroup\\b", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Sources", Regex.Matches(text, "<Source\\b", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Encoder profiles", Regex.Matches(text, "<EncoderProfile\\b|<WMEncoder_Profile\\b", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
            Add(section, "Broadcast port", ValueOrNotReported(FirstXmlAttribute(text, "Broadcast", "Http")));
            Add(section, "Privacy note", "Windows Media Encoder sessions can contain device names, paths, URLs, and broadcast settings.");
        }

        private static void AddWindowsMediaPlaylistInfo(List<ReportSection> sections, string text)
        {
            var section = AddSection(sections, "Message/contact data");
            Add(section, "Format", "Windows Media Player playlist");
            Add(section, "Generator", ValueOrNotReported(FirstXmlMetaContent(text, "Generator")));
            Add(section, "Title", ValueOrNotReported(FirstXmlText(text, "title")));
            Add(section, "Media entries", Regex.Matches(text ?? string.Empty, "<media\\b", RegexOptions.IgnoreCase).Count.ToString(CultureInfo.InvariantCulture));
            var sources = Regex.Matches(text ?? string.Empty, "<media\\b[^>]*\\bsrc\\s*=\\s*\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Singleline)
                .Cast<Match>()
                .Select(match => CleanMetadataText(match.Groups["value"].Value))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Take(12)
                .ToArray();
            if (sources.Length > 0)
                Add(section, "Media sources", string.Join("\r\n", sources));
            Add(section, "Privacy note", "Playlists can contain local paths, network URLs, and library names. Review reports before sharing them.");
        }

        private static void AddM3uPlaylistInfo(List<ReportSection> sections, string path, string text)
        {
            var section = AddSection(sections, "Message/contact data");
            var ext = Path.GetExtension(path).ToLowerInvariant();
            Add(section, "Format", ext == ".m3u8" ? "M3U8 media playlist" : "M3U media playlist");
            Add(section, "Encoding hint", ext == ".m3u8" ? "UTF-8 by convention" : "M3U text playlist; encoding varies in older files");
            Add(section, "Extended marker", text.IndexOf("#EXTM3U", StringComparison.OrdinalIgnoreCase) >= 0 ? "Present" : "Not seen in sample");
            Add(section, "EXTINF entries", Regex.Matches(text ?? string.Empty, @"(?im)^\s*#EXTINF\b", RegexOptions.CultureInvariant).Count.ToString(CultureInfo.InvariantCulture));
            var sources = PlaylistSourceLines(text).Take(20).ToArray();
            Add(section, "Media entries in sample", sources.Length.ToString(CultureInfo.InvariantCulture));
            if (sources.Length > 0)
                Add(section, "Media sources", string.Join(Environment.NewLine, sources));
            Add(section, "Privacy note", "M3U/M3U8 playlists can contain local paths, network shares, URLs, and track titles. FileDentify lists a bounded sample so reports remain readable.");
        }

        private static IEnumerable<string> PlaylistSourceLines(string text)
        {
            return (text ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => CleanMetadataText(line.Trim('\uFEFF', ' ', '\t')))
                .Where(line => line.Length > 0 && !line.StartsWith("#", StringComparison.Ordinal))
                .Where(line => line.IndexOf("://", StringComparison.Ordinal) > 0 ||
                    line.IndexOf('\\') >= 0 ||
                    line.IndexOf('/') >= 0 ||
                    Path.HasExtension(line));
        }

        private static string DecodePersonalText(byte[] data)
        {
            if (data == null || data.Length == 0)
                return string.Empty;
            var sample = data.Take(Math.Min(data.Length, 1024 * 1024)).ToArray();
            var oddNulls = 0;
            for (var i = 1; i < sample.Length; i += 2)
                if (sample[i] == 0)
                    oddNulls++;
            if (sample.Length > 4 && oddNulls > sample.Length / 4)
                return Encoding.Unicode.GetString(sample);
            return Encoding.UTF8.GetString(sample);
        }

        private static string UnfoldStructuredLines(string text)
        {
            return Regex.Replace(text ?? string.Empty, @"\r?\n[ \t]", string.Empty);
        }

        private static string FindCalendarValue(string text, string key)
        {
            var match = Regex.Match(text ?? string.Empty, @"(?im)^" + Regex.Escape(key) + @"(?:;[^:]*)?:(?<value>.*)$");
            return match.Success ? CleanMetadataText(match.Groups["value"].Value) : string.Empty;
        }

        private static string FindHeaderValue(string text, string key)
        {
            var unfolded = Regex.Replace(text ?? string.Empty, @"\r?\n[ \t]+", " ");
            var match = Regex.Match(unfolded, @"(?im)^" + Regex.Escape(key) + @":\s*(?<value>.*)$");
            return match.Success ? CleanMetadataText(match.Groups["value"].Value) : string.Empty;
        }

        private static int CountLinePrefix(string text, string key)
        {
            return Regex.Matches(text ?? string.Empty, @"(?im)^" + Regex.Escape(key) + @"(?:;|:)", RegexOptions.CultureInvariant).Count;
        }

        private static int CountToken(string text, string token)
        {
            return Regex.Matches(text ?? string.Empty, Regex.Escape(token), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count;
        }

        private static string FirstXmlText(string text, string tag)
        {
            var match = Regex.Match(text ?? string.Empty, "<" + Regex.Escape(tag) + @"[^>]*>(?<value>.*?)</" + Regex.Escape(tag) + ">", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return match.Success ? CleanMetadataText(Regex.Replace(match.Groups["value"].Value, "<.*?>", string.Empty)) : string.Empty;
        }

        private static string FirstXmlAttribute(string text, string tag, string attribute)
        {
            var match = Regex.Match(text ?? string.Empty, "<" + Regex.Escape(tag) + @"\b[^>]*\b" + Regex.Escape(attribute) + "\\s*=\\s*\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return match.Success ? CleanMetadataText(match.Groups["value"].Value) : string.Empty;
        }

        private static string FirstXmlMetaContent(string text, string name)
        {
            var match = Regex.Match(text ?? string.Empty, @"<meta\b(?=[^>]*\bname\s*=\s*""" + Regex.Escape(name) + @""")(?=[^>]*\bcontent\s*=\s*""(?<value>[^""]*)"")[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return match.Success ? CleanMetadataText(match.Groups["value"].Value) : string.Empty;
        }
    }
}
