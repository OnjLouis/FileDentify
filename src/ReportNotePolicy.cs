using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FileDentify
{
    internal static class ReportNotePolicy
    {
        private static readonly string[] CanonicalTitles =
        {
            "About this format",
            "Limitations",
            "Compatibility",
            "Privacy",
            "Safety",
            "Uncertainty",
            "Advice",
            "Viewing note"
        };

        internal static bool IsLegacyNoteTitle(string title)
        {
            var text = (title ?? string.Empty).Trim();
            return string.Equals(text, "Notes", StringComparison.OrdinalIgnoreCase) ||
                text.EndsWith(" note", StringComparison.OrdinalIgnoreCase) ||
                text.EndsWith(" notes", StringComparison.OrdinalIgnoreCase);
        }

        internal static string Categorize(string title, string detail)
        {
            var label = (title ?? string.Empty).Trim().ToLowerInvariant();
            var text = (detail ?? string.Empty).Trim();
            var lower = text.ToLowerInvariant();

            if (CanonicalTitles.Any(value => string.Equals(value, title, StringComparison.OrdinalIgnoreCase)))
                return CanonicalTitles.First(value => string.Equals(value, title, StringComparison.OrdinalIgnoreCase));
            if (label.Contains("privacy") || label.Contains("credential"))
                return "Privacy";
            if (label.Contains("safety") || label.Contains("warning") || label.Contains("executable"))
                return "Safety";
            if (label.Contains("compatibility") || label.Contains("platform"))
                return "Compatibility";
            if (label.Contains("recommendation"))
                return "Advice";
            if (label.Contains("scan note"))
                return "Viewing note";
            if (label.Contains("json note") || label.Contains("parsing note") || label.Contains("parse note"))
                return "Uncertainty";
            if (label.Contains("read note") || label.Contains("decompression note"))
                return "Limitations";
            if (!string.Equals(label, "notes", StringComparison.OrdinalIgnoreCase))
                return "About this format";

            if (StartsWithAny(lower, "keep ", "place ", "confirm ", "treat ", "use ", "review ", "verify "))
                return "Advice";
            if (ContainsAny(lower, "private key", "treat it as private", "treat them as private", "treat this as confidential", "before sharing"))
                return "Privacy";
            if (ContainsAny(lower, "not proof", "cannot confirm", "could not confirm", "may still be", "extension is shared", "extension is generic", "treat this as a hint"))
                return "Uncertainty";
            if (StartsWithAny(lower, "filedentify reports", "header-level", "header and file-table", "support is identification-level") ||
                StartsWithAny(lower, "installer support", "firmware and device images", "ebook and help files"))
                return "Limitations";
            if (ContainsAny(lower, "normally depends on a companion", "requires a companion", "requires the ", "compatible family", "minimum version"))
                return "Compatibility";
            return "About this format";
        }

        internal static string MergeDistinct(string existing, string addition)
        {
            var blocks = SplitBlocks(existing).ToList();
            var seen = new HashSet<string>(blocks.Select(NormalizeForComparison), StringComparer.OrdinalIgnoreCase);
            foreach (var block in SplitBlocks(addition))
            {
                var key = NormalizeForComparison(block);
                if (key.Length > 0 && seen.Add(key))
                    blocks.Add(block.Trim());
            }
            return string.Join(Environment.NewLine + Environment.NewLine, blocks.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray());
        }

        private static IEnumerable<string> SplitBlocks(string value)
        {
            return Regex.Split((value ?? string.Empty).Trim(), "(?:\\r?\\n){2,}")
                .Where(block => !string.IsNullOrWhiteSpace(block));
        }

        private static string NormalizeForComparison(string value)
        {
            return Regex.Replace((value ?? string.Empty).Trim(), "\\s+", " ");
        }

        private static bool StartsWithAny(string value, params string[] prefixes)
        {
            return prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        private static bool ContainsAny(string value, params string[] fragments)
        {
            return fragments.Any(fragment => value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
