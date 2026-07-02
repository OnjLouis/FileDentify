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
        private static string BrowserProfileTypeName(string path, byte[] header)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".wdseml")
                return "Thunderbird search-index email snippet";
            if (ext == ".msf")
                return "Mozilla/Thunderbird mail summary index";
            if (ext == ".jsonlz4" || StartsWith(header, Encoding.ASCII.GetBytes("mozLz40\0")))
                return "Firefox/Thunderbird LZ4-compressed JSON profile data";
            if (ext == ".sqlite-wal" || ext == ".sqlite-shm" || ext == ".db-wal" || ext == ".db-shm")
                return "SQLite sidecar file";
            if (LooksLikeChromiumSafeBrowsingStore(path))
                return "Chromium Safe Browsing store";
            return null;
        }

        private static void AddBrowserProfileInfo(List<ReportSection> sections, string path, byte[] header, byte[] sample, long fileLength)
        {
            var type = BrowserProfileTypeName(path, header);
            if (type == null)
                return;

            var section = AddSection(sections, "Browser/profile data");
            var ext = Path.GetExtension(path).ToLowerInvariant();
            Add(section, "Format hint", type);
            Add(section, "File size", FormatBytes(fileLength));
            Add(section, "Profile context", BrowserProfileContext(path));

            if (ext == ".wdseml")
            {
                var text = DecodePersonalText(sample);
                Add(section, "Role", "Search/index copy of an email message stored by Thunderbird/SeaMonkey-style .mozmsgs folders.");
                Add(section, "Subject", ValueOrNotReported(FindHeaderValue(text, "Subject")));
                Add(section, "From", ValueOrNotReported(FindHeaderValue(text, "From")));
                Add(section, "Date", ValueOrNotReported(FindHeaderValue(text, "Date")));
                Add(section, "MIME parts", CountToken(text, "Content-Type:").ToString(CultureInfo.InvariantCulture));
            }
            else if (ext == ".msf")
            {
                Add(section, "Role", "Mozilla mailbox summary/index file. The matching mailbox usually has the same name without .msf.");
                var strings = FindAsciiStrings(sample, 4, 90)
                    .Select(item => item.Value.Trim())
                    .Where(value => value.IndexOf("subject", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        value.IndexOf("message-id", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        value.IndexOf("X-Mozilla", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(12)
                    .ToArray();
                if (strings.Length > 0)
                    Add(section, "Visible index strings", string.Join(Environment.NewLine, strings));
            }
            else if (ext == ".jsonlz4" || StartsWith(header, Encoding.ASCII.GetBytes("mozLz40\0")))
            {
                Add(section, "Role", MozillaJsonLz4Role(path));
                Add(section, "Header marker", StartsWith(header, Encoding.ASCII.GetBytes("mozLz40\0")) ? "mozLz40" : "Not seen in sampled header");
            }
            else if (LooksLikeChromiumSafeBrowsingStore(path))
            {
                Add(section, "Role", "Chromium Safe Browsing local threat-list store.");
                Add(section, "Store name", ChromiumSafeBrowsingStoreName(path));
                Add(section, "Privacy note", "Safe Browsing stores are browser security data, not browsing history, but they are still profile-internal files.");
            }
            else
            {
                Add(section, "Role", "SQLite write-ahead-log or shared-memory sidecar. It belongs with the base database and is not normally opened by itself.");
            }

            Add(section, "Notes", "Browser and mail profile files can contain private browsing, session, account, search-index, or email metadata. FileDentify reports file role and bounded visible metadata only; it does not reconstruct mailboxes or decompress private profile content.");
        }

        private static string BrowserProfileContext(string path)
        {
            if (path.IndexOf("Thunderbird", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Thunderbird";
            if (path.IndexOf("Firefox", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Firefox";
            if (path.IndexOf("Chrome", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("Chromium", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("\\Safe Browsing\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Chromium/Chrome";
            if (path.IndexOf("\\ImapMail\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Mozilla IMAP mail profile";
            if (path.IndexOf("\\Mail\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Mozilla local mail profile";
            return "Mozilla-compatible profile or SQLite database sidecar";
        }

        private static string MozillaJsonLz4Role(string path)
        {
            var file = Path.GetFileName(path);
            if (file.IndexOf("sessionstore", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Saved browser session/window/tab state.";
            if (path.IndexOf("\\datareporting\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Firefox/Thunderbird telemetry or health-report archive.";
            if (file.IndexOf("recovery", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Session recovery data.";
            return "Mozilla JSON data compressed with Mozilla's LZ4 wrapper.";
        }

        private static bool LooksLikeChromiumSafeBrowsingStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;
            return path.IndexOf("\\Safe Browsing\\", StringComparison.OrdinalIgnoreCase) >= 0 &&
                Regex.IsMatch(Path.GetFileName(path) ?? string.Empty, @"\.store\.\d+_", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static string ChromiumSafeBrowsingStoreName(string path)
        {
            var file = Path.GetFileName(path) ?? string.Empty;
            var match = Regex.Match(file, @"^(?<name>.+?)\.store\.", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return match.Success ? match.Groups["name"].Value : file;
        }
    }
}
