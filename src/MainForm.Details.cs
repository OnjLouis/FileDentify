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
{
    internal sealed partial class MainForm
    {

        private void UpdateDetailsFromSelection()
        {
            var node = resultsTree.SelectedNode;
            detailsBox.Text = EnsureTrailingBlankLine(node == null || node.Tag == null ? string.Empty : node.Tag.ToString());
            UpdateDetailsBrowser(node == null ? "Details" : node.Text, BuildSelectedNodeHtml(node));
            ResetTextBoxToTop(detailsBox);
        }


        private void ToggleHtmlDetailsView()
        {
            var focusDetails = detailsBox.ContainsFocus || detailsBrowser.ContainsFocus;
            settings.HtmlDetailsView = !settings.HtmlDetailsView;
            settings.Save();
            ApplyDetailsViewMode(focusDetails);
            statusLabel.Text = settings.HtmlDetailsView ? "HTML details view enabled." : "Text details view enabled.";
        }


        private void ToggleHtmlDetailsFocus()
        {
            if (!settings.HtmlDetailsView)
                return;

            if (detailsBrowser.ContainsFocus)
            {
                htmlDetailsWantsFocus = false;
                resultsTree.Focus();
                statusLabel.Text = "Tree focused.";
            }
            else
            {
                htmlDetailsWantsFocus = true;
                BeginInvoke((MethodInvoker)FocusHtmlDetailsDocument);
                statusLabel.Text = "HTML details focused. Press F6 to return to the tree.";
            }
        }


        private void DetailsBrowser_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (IsHtmlDetailsGlobalShortcut(e.KeyData))
                e.IsInputKey = true;
        }


        private bool IsHtmlDetailsGlobalShortcut(Keys keyData)
        {
            var modifiers = keyData & Keys.Modifiers;
            var keyCode = keyData & Keys.KeyCode;

            if (modifiers == Keys.None && (keyCode == Keys.F1 || keyCode == Keys.F4 || keyCode == Keys.F5 || keyCode == Keys.F6 || keyCode == Keys.F7 || keyCode == Keys.Escape || keyCode == Keys.Tab))
                return true;
            if (modifiers == Keys.Shift && (keyCode == Keys.F1 || keyCode == Keys.Tab))
                return true;
            if (modifiers == Keys.Control && (keyCode == Keys.F1 || keyCode == Keys.N || keyCode == Keys.O || keyCode == Keys.R || keyCode == Keys.S || keyCode == Keys.C || keyCode == Keys.Oemcomma || keyCode == Keys.Up || keyCode == Keys.Down || DigitFromShortcutKey(keyCode) >= 0))
                return true;
            if (modifiers == (Keys.Control | Keys.Shift) && (keyCode == Keys.O || keyCode == Keys.L || keyCode == Keys.T || keyCode == Keys.Left || keyCode == Keys.Right || DigitFromShortcutKey(keyCode) >= 0))
                return true;
            if (modifiers == Keys.Alt && (keyCode == Keys.Left || keyCode == Keys.Right || keyCode == Keys.Up || keyCode == Keys.Down || keyCode == Keys.Home || keyCode == Keys.End || keyCode == Keys.PageUp || keyCode == Keys.PageDown || keyCode == Keys.Back || keyCode == Keys.C || keyCode == Keys.L || keyCode == Keys.V || FileIndexFromShortcutKey(keyCode) >= 0))
                return true;

            return false;
        }


        private void ApplyDetailsViewMode(bool focusDetails)
        {
            htmlDetailsMenuItem.Checked = settings.HtmlDetailsView;
            enterReportButton.Visible = settings.HtmlDetailsView;
            detailsBrowser.Visible = settings.HtmlDetailsView;
            detailsBox.Visible = !settings.HtmlDetailsView;
            if (settings.HtmlDetailsView)
            {
                detailsBrowser.BringToFront();
                UpdateDetailsBrowser(resultsTree.SelectedNode == null ? "Details" : resultsTree.SelectedNode.Text, BuildSelectedNodeHtml(resultsTree.SelectedNode));
                if (focusDetails)
                {
                    htmlDetailsWantsFocus = true;
                    BeginInvoke((MethodInvoker)FocusHtmlDetailsDocument);
                }
            }
            else
            {
                htmlDetailsWantsFocus = false;
                detailsBox.BringToFront();
                if (focusDetails)
                    detailsBox.Focus();
            }
        }


        private void UpdateDetailsBrowser(string title, string html)
        {
            if (detailsBrowser == null)
                return;
            detailsBrowser.DocumentText = WrapDetailsHtml(title, html);
        }


        private bool DetailsPaneContainsFocus()
        {
            return (detailsBox != null && detailsBox.ContainsFocus) || (detailsBrowser != null && detailsBrowser.ContainsFocus);
        }


        private void FocusDetailsPane()
        {
            if (settings.HtmlDetailsView && detailsBrowser != null)
            {
                htmlDetailsWantsFocus = true;
                BeginInvoke((MethodInvoker)FocusHtmlDetailsDocument);
            }
            else if (detailsBox != null)
                detailsBox.Focus();
        }


        private string BuildSelectedNodeHtml(TreeNode node)
        {
            if (node == null)
                return "<h1>Details</h1>";

            if (IsReportOverviewNode(node))
                return TextDetailsToHtml("Report overview", node.Tag == null ? string.Empty : node.Tag.ToString());

            var report = ReportForNode(node);
            if (report == null)
                return TextDetailsToHtml(node.Text, node.Tag == null ? string.Empty : node.Tag.ToString());

            if (node.Parent == null)
                return ReportToDetailsHtml(report);

            if (node.Parent.Parent == null)
            {
                var section = report.Sections.FirstOrDefault(s => string.Equals(s.Title, node.Text, StringComparison.OrdinalIgnoreCase));
                return section == null ? TextDetailsToHtml(node.Text, node.Tag == null ? string.Empty : node.Tag.ToString()) : SectionToDetailsHtml(section, report.DisplayName);
            }

            var parentSection = report.Sections.FirstOrDefault(s => string.Equals(s.Title, node.Parent.Text, StringComparison.OrdinalIgnoreCase));
            if (parentSection != null)
            {
                var item = parentSection.Items.FirstOrDefault(i => string.Equals(i.Title, node.Text, StringComparison.OrdinalIgnoreCase));
                if (item != null)
                    return ItemToDetailsHtml(item, parentSection.Title, report.DisplayName);
            }

            return TextDetailsToHtml(node.Text, node.Tag == null ? string.Empty : node.Tag.ToString());
        }


        private FileReport ReportForNode(TreeNode node)
        {
            var root = TopLevelNode(node);
            if (root == null || IsReportOverviewNode(root))
                return null;
            return currentReports.FirstOrDefault(report =>
                string.Equals(report.OriginalPath, root.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(report.DisplayName, root.Text, StringComparison.OrdinalIgnoreCase));
        }


        private static string ReportToDetailsHtml(FileReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<h1>" + Html(report.DisplayName) + "</h1>");
            foreach (var section in report.Sections)
                AppendSectionHtml(sb, section, 2);
            return sb.ToString();
        }


        private static string SectionToDetailsHtml(ReportSection section, string fileName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<h1>" + Html(fileName) + "</h1>");
            AppendSectionHtml(sb, section, 2);
            return sb.ToString();
        }


        private static string ItemToDetailsHtml(ReportItem item, string sectionTitle, string fileName)
        {
            var detail = ReportSection.NormalizeDetailText(item.Detail);
            var sb = new StringBuilder();
            sb.AppendLine("<h1>" + Html(fileName) + "</h1>");
            sb.AppendLine("<h2>" + Html(sectionTitle) + "</h2>");
            sb.AppendLine("<h3>" + Html(item.Title) + "</h3>");
            sb.AppendLine("<pre>" + Html(detail) + "</pre>");
            return sb.ToString();
        }


        private static void AppendSectionHtml(StringBuilder sb, ReportSection section, int headingLevel)
        {
            var h = Math.Max(1, Math.Min(6, headingLevel));
            sb.AppendLine("<section>");
            sb.AppendLine("<h" + h.ToString(CultureInfo.InvariantCulture) + ">" + Html(section.Title) + "</h" + h.ToString(CultureInfo.InvariantCulture) + ">");
            if (ShouldRenderSectionAsPlainTextHtml(section))
            {
                var readableText = new List<string>();
                foreach (var item in section.Items)
                {
                    var detail = ReportSection.NormalizeDetailText(item.Detail);
                    if (string.IsNullOrWhiteSpace(detail))
                        continue;

                    if (IsHtmlNavigationHeadingItem(item.Title))
                    {
                        FlushReadableTextHtml(sb, readableText);
                        sb.AppendLine("<h3>" + Html((item.Title ?? string.Empty).Trim()) + "</h3>");
                        sb.AppendLine("<pre>" + Html(detail) + "</pre>");
                    }
                    else
                    {
                        readableText.Add(detail);
                    }
                }
                FlushReadableTextHtml(sb, readableText);
                sb.AppendLine("</section>");
                return;
            }
            var tableOpen = false;
            foreach (var item in section.Items)
            {
                var detail = ReportSection.NormalizeDetailText(item.Detail);
                if (IsHtmlNavigationHeadingItem(item.Title))
                {
                    if (tableOpen)
                    {
                        EndDetailsTable(sb);
                        tableOpen = false;
                    }
                    sb.AppendLine("<h3>" + Html((item.Title ?? string.Empty).Trim()) + "</h3>");
                    sb.AppendLine("<pre>" + Html(detail) + "</pre>");
                    continue;
                }

                if (!tableOpen)
                {
                    StartDetailsTable(sb);
                    tableOpen = true;
                }

                if (string.Equals((item.Title ?? string.Empty).Trim(), detail.Trim(), StringComparison.Ordinal))
                    sb.AppendLine("<tr><td colspan=\"2\"><pre>" + Html(detail) + "</pre></td></tr>");
                else
                    sb.AppendLine("<tr><td>" + Html(item.Title) + "</td><td><pre>" + Html(detail) + "</pre></td></tr>");
            }
            if (tableOpen)
                EndDetailsTable(sb);
            sb.AppendLine("</section>");
        }


        private static void StartDetailsTable(StringBuilder sb)
        {
            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr><th scope=\"col\">Item</th><th scope=\"col\">Details</th></tr></thead>");
            sb.AppendLine("<tbody>");
        }


        private static void EndDetailsTable(StringBuilder sb)
        {
            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");
        }


        private static bool IsHtmlNavigationHeadingItem(string title)
        {
            var text = (title ?? string.Empty).Trim();
            return string.Equals(text, "Notes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "Scan note", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "Section end", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "End of section", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "Information", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "Info", StringComparison.OrdinalIgnoreCase) ||
                text.EndsWith(" note", StringComparison.OrdinalIgnoreCase) ||
                text.EndsWith(" notes", StringComparison.OrdinalIgnoreCase) ||
                text.EndsWith(" information", StringComparison.OrdinalIgnoreCase);
        }


        private static bool ShouldRenderSectionAsPlainTextHtml(ReportSection section)
        {
            if (section == null)
                return false;
            if (IsPlainTextHtmlSection(section.Title))
                return true;
            return section.Items.Any(item => HasMultipleHtmlDetailLines(ReportSection.NormalizeDetailText(item.Detail)));
        }


        private static bool IsPlainTextHtmlSection(string title)
        {
            var text = (title ?? string.Empty).Trim();
            return string.Equals(text, "Readable text", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "ffprobe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "opusinfo", StringComparison.OrdinalIgnoreCase);
        }


        private static bool HasMultipleHtmlDetailLines(string detail)
        {
            if (string.IsNullOrWhiteSpace(detail))
                return false;
            var normalized = NormalizeLineEndings(detail);
            return normalized.IndexOf(Environment.NewLine, StringComparison.Ordinal) >= 0;
        }


        private static void FlushReadableTextHtml(StringBuilder sb, List<string> readableText)
        {
            if (readableText == null || readableText.Count == 0)
                return;

            sb.AppendLine("<pre>" + Html(string.Join(Environment.NewLine, readableText)) + "</pre>");
            readableText.Clear();
        }


        private static string TextDetailsToHtml(string title, string text)
        {
            var sb = new StringBuilder();
            var safeTitle = title ?? "Details";
            sb.AppendLine("<h1>" + Html(safeTitle) + "</h1>");
            var lines = NormalizeLineEndings(text ?? string.Empty).Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var paragraph = new StringBuilder();
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i] ?? string.Empty;
                var next = i + 1 < lines.Length ? lines[i + 1] ?? string.Empty : string.Empty;
                if (IsUnderline(next, line.Length))
                {
                    if (i == 0 && string.Equals(line.Trim(), safeTitle, StringComparison.OrdinalIgnoreCase))
                    {
                        i++;
                        continue;
                    }
                    FlushParagraph(sb, paragraph);
                    var level = next.StartsWith("=", StringComparison.Ordinal) ? 2 : 3;
                    sb.AppendLine("<h" + level.ToString(CultureInfo.InvariantCulture) + ">" + Html(line.Trim()) + "</h" + level.ToString(CultureInfo.InvariantCulture) + ">");
                    i++;
                    continue;
                }

                if (line.EndsWith(":", StringComparison.Ordinal) && line.Length < 80)
                {
                    FlushParagraph(sb, paragraph);
                    sb.AppendLine("<h3>" + Html(line.TrimEnd(':')) + "</h3>");
                    continue;
                }

                if (IsHtmlNavigationHeadingItem(line))
                {
                    FlushParagraph(sb, paragraph);
                    sb.AppendLine("<h3>" + Html(line.Trim()) + "</h3>");
                    continue;
                }

                paragraph.AppendLine(line);
            }
            FlushParagraph(sb, paragraph);
            return sb.ToString();
        }


        private static bool IsUnderline(string line, int headingLength)
        {
            if (string.IsNullOrWhiteSpace(line) || headingLength <= 0)
                return false;
            var trimmed = line.Trim();
            if (trimmed.Length < Math.Min(3, headingLength))
                return false;
            return trimmed.All(ch => ch == '=') || trimmed.All(ch => ch == '-');
        }


        private static void FlushParagraph(StringBuilder sb, StringBuilder paragraph)
        {
            var text = paragraph.ToString().TrimEnd();
            paragraph.Length = 0;
            if (!string.IsNullOrWhiteSpace(text))
                sb.AppendLine("<pre>" + Html(text) + "</pre>");
        }


        private static string WrapDetailsHtml(string title, string body)
        {
            return "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\"><title>" +
                Html(title) +
                "</title><style>body{font-family:Segoe UI,Arial,sans-serif;font-size:10pt;line-height:1.45;color:#111;background:#fff;margin:.75rem}h1{font-size:1.25rem;margin:0 0 .75rem}h2{font-size:1.1rem;margin:1rem 0 .35rem}h3{font-size:1rem;margin:.85rem 0 .25rem}table{border-collapse:collapse;width:100%;margin:.25rem 0 1rem}th,td{border:1px solid #aaa;padding:.35rem .5rem;text-align:left;vertical-align:top}th{background:#f2f2f2}td:first-child{width:15rem;font-weight:600}pre{white-space:pre-wrap;font-family:Consolas,monospace;margin:0}</style></head><body>" +
                (body ?? string.Empty) +
                "</body></html>";
        }


        private static string Html(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }


        private static string EnsureTrailingBlankLine(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Environment.NewLine;
            var trimmed = NormalizeLineEndings(text).TrimEnd('\r', '\n');
            return trimmed + Environment.NewLine + Environment.NewLine;
        }


        private static string NormalizeLineEndings(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            return text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);
        }


        private static void ResetTextBoxToTop(TextBox box)
        {
            if (box == null)
                return;
            box.SelectionStart = 0;
            box.SelectionLength = 0;
            box.ScrollToCaret();
        }


        private static void TextBoxSelectAll_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                var box = sender as TextBox;
                if (box != null)
                    box.SelectAll();
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
        }
    }
}

