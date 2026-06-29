using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace FileDentify
{    internal sealed class FileReport
    {
        public string DisplayName;
        public string OriginalPath;
        public string FullText;
        public readonly List<ReportSection> Sections = new List<ReportSection>();
    }

    internal sealed class ReportSection
    {
        public string Title;
        public readonly List<ReportItem> Items = new List<ReportItem>();

        public string DetailText()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Title);
            foreach (var item in Items)
            {
                var title = item.Title ?? string.Empty;
                var detail = NormalizeDetailText(item.Detail);
                if (string.Equals(title.Trim(), detail.Trim(), StringComparison.Ordinal))
                    sb.AppendLine(title);
                else
                {
                    sb.AppendLine(title + ":");
                    if (!string.IsNullOrWhiteSpace(detail))
                        sb.AppendLine(detail);
                }
            }
            return sb.ToString().TrimEnd() + Environment.NewLine + Environment.NewLine;
        }

        internal static string NormalizeDetailText(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var normalized = value
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\n", Environment.NewLine)
                .TrimEnd();
            return BreakLongDetailLines(normalized);
        }

        private static string BreakLongDetailLines(string value)
        {
            var lines = value.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var changed = false;
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Length < 120)
                    continue;
                if (CountOccurrences(line, " | ") >= 2)
                {
                    lines[i] = line.Replace(" | ", Environment.NewLine);
                    changed = true;
                }
                else if (line.Length > 160 && LooksLikeProse(line))
                {
                    lines[i] = line.Replace(". ", "." + Environment.NewLine);
                    changed = true;
                }
                else if (line.Length > 180 && LooksLikeCommaHeavyDescription(line))
                {
                    lines[i] = line.Replace(", ", "," + Environment.NewLine);
                    changed = true;
                }
            }
            return changed ? string.Join(Environment.NewLine, lines) : value;
        }

        private static bool LooksLikeProse(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;
            if (line.IndexOf(":\\", StringComparison.Ordinal) >= 0 || line.StartsWith("\\\\", StringComparison.Ordinal))
                return false;
            if (line.StartsWith("0000", StringComparison.Ordinal) || line.IndexOf("  ", StringComparison.Ordinal) >= 0 && line.IndexOf("0x", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            return line.IndexOf(". ", StringComparison.Ordinal) >= 0 &&
                CountOccurrences(line, " ") >= 12 &&
                CountOccurrences(line, ". ") <= 4;
        }

        private static bool LooksLikeCommaHeavyDescription(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;
            if (line.IndexOf(":\\", StringComparison.Ordinal) >= 0 || line.StartsWith("\\\\", StringComparison.Ordinal))
                return false;
            if (line.StartsWith("0000", StringComparison.Ordinal) || line.IndexOf("....", StringComparison.Ordinal) >= 0)
                return false;
            return CountOccurrences(line, ", ") >= 4 && CountOccurrences(line, " ") >= 12;
        }

        private static int CountOccurrences(string value, string token)
        {
            var count = 0;
            var index = 0;
            while ((index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }
            return count;
        }
    }

    internal sealed class ReportItem
    {
        public string Title;
        public string Detail;
    }

    internal sealed class ShortcutButton : Button
    {
        public string ShortcutText { get; set; }

        public ShortcutButton()
        {
            AccessibleRole = AccessibleRole.PushButton;
        }

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new ShortcutButtonAccessibleObject(this);
        }

        private sealed class ShortcutButtonAccessibleObject : Control.ControlAccessibleObject
        {
            private readonly ShortcutButton owner;

            public ShortcutButtonAccessibleObject(ShortcutButton owner)
                : base(owner)
            {
                this.owner = owner;
            }

            public override string KeyboardShortcut
            {
                get
                {
                    return string.IsNullOrWhiteSpace(owner.ShortcutText)
                        ? base.KeyboardShortcut
                        : owner.ShortcutText;
                }
            }
        }
    }

}

