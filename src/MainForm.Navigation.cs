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

        private ContextMenuStrip CreateTreeContextMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add(CreateShortcutMenuItem("&Copy selected details", "Ctrl+C", delegate { CopyCurrentSelection(); }));
            menu.Items.Add(CreateShortcutMenuItem("Copy full report", "Alt+C", delegate { CopyReport(); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(CreateShortcutMenuItem("Co&llapse all", "Ctrl+Shift+Left", delegate { CollapseAllResults(); }));
            menu.Items.Add(CreateShortcutMenuItem("&Expand all", "Ctrl+Shift+Right", delegate { ExpandAllResults(); }));
            menu.Items.Add(CreateShortcutMenuItem("Move section &up", "Ctrl+Up", delegate { MoveSelectedSection(-1); }));
            menu.Items.Add(CreateShortcutMenuItem("Move section &down", "Ctrl+Down", delegate { MoveSelectedSection(1); }));
            menu.Items.Add(CreateShortcutMenuItem("Report overvie&w", "Alt+Backspace", delegate { SelectReportOverview(); }));
            menu.Items.Add(CreateShortcutMenuItem("Pre&vious file", "Alt+Left", delegate { SelectAdjacentFile(-1); }));
            menu.Items.Add(CreateShortcutMenuItem("Next f&ile", "Alt+Right", delegate { SelectAdjacentFile(1); }));
            menu.Items.Add(CreateShortcutMenuItem("First file", "Alt+PageUp", delegate { SelectFileByPosition(false); }));
            menu.Items.Add(CreateShortcutMenuItem("Last file", "Alt+PageDown", delegate { SelectFileByPosition(true); }));
            menu.Items.Add(CreateShortcutMenuItem("&Previous section", "Alt+Up", delegate { SelectAdjacentSection(-1); }));
            menu.Items.Add(CreateShortcutMenuItem("Ne&xt section", "Alt+Down", delegate { SelectAdjacentSection(1); }));
            menu.Items.Add(CreateShortcutMenuItem("First sectio&n", "Alt+Home", delegate { SelectSectionByPosition(false); }));
            menu.Items.Add(CreateShortcutMenuItem("Las&t section", "Alt+End", delegate { SelectSectionByPosition(true); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(CreateShortcutMenuItem("Open FileDentify &report...", "Ctrl+R", delegate { OpenSavedReport(); }));
            menu.Items.Add(CreateShortcutMenuItem("&Open folder...", "Ctrl+Shift+L", delegate { OpenFolder(false); }));
            menu.Items.Add(CreateShortcutMenuItem("View &HTML report", "Alt+V", delegate { ViewHtmlReport(); }));
            menu.Items.Add(CreateShortcutMenuItem("&Save report...", "Ctrl+S", delegate { SaveReport(); }));
            menu.Items.Add(CreateShortcutMenuItem("Refresh original files", "F5", delegate { RefreshOriginalFiles(); }));
            menu.Items.Add(CreateShortcutMenuItem("Open containing folder", "Alt+L", delegate { OpenContainingFolder(); }));
            menu.Items.Add(CreateShortcutMenuItem("&Advanced file viewer", "F4", delegate { OpenAdvancedFileViewer(); }));
            return menu;
        }


        private static ToolStripMenuItem CreateShortcutMenuItem(string text, string shortcutText, EventHandler handler)
        {
            return new ToolStripMenuItem(WithShortcutText(text, shortcutText), null, handler)
            {
                ShortcutKeyDisplayString = shortcutText,
                ShowShortcutKeys = false
            };
        }


        private static string WithShortcutText(string text, string shortcutText)
        {
            if (string.IsNullOrWhiteSpace(shortcutText) || text.IndexOf('\t') >= 0)
                return text;
            return text + "\t" + shortcutText;
        }


        private void ResultsTree_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right && e.Node != null)
                resultsTree.SelectedNode = e.Node;
        }


        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            bool suppressKeyPress;
            if (!TryHandleGlobalShortcut(e.KeyData, false, out suppressKeyPress))
                return;

            e.Handled = true;
            if (suppressKeyPress)
                e.SuppressKeyPress = true;
        }


        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            bool suppressKeyPress;
            if (TryHandleGlobalShortcut(keyData, false, out suppressKeyPress))
                return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }


        private bool TryHandleGlobalShortcut(Keys keyData, bool fromHtmlDetails, out bool suppressKeyPress)
        {
            suppressKeyPress = false;
            var modifiers = keyData & Keys.Modifiers;
            var keyCode = keyData & Keys.KeyCode;

            if (fromHtmlDetails && keyCode == Keys.Tab && (modifiers == Keys.None || modifiers == Keys.Shift))
            {
                FocusHtmlDetailsDocument();
                return true;
            }
            if (fromHtmlDetails && keyCode == Keys.Escape && modifiers == Keys.None)
            {
                htmlDetailsWantsFocus = false;
                resultsTree.Focus();
                statusLabel.Text = "Tree focused.";
                return true;
            }
            if (!fromHtmlDetails && keyCode == Keys.Escape && modifiers == Keys.None)
            {
                if (ReturnToHtmlDetailsAfterMenuEscape())
                {
                    suppressKeyPress = true;
                    return true;
                }
                Close();
                return true;
            }

            if (keyCode == Keys.F1 && modifiers == Keys.None)
            {
                ShowHelp();
                return true;
            }
            if (keyCode == Keys.F1 && modifiers == Keys.Shift)
            {
                CheckForUpdates(true);
                return true;
            }
            if (keyCode == Keys.F1 && modifiers == Keys.Control)
            {
                OpenProjectPage();
                return true;
            }
            if (keyCode == Keys.F7 && modifiers == Keys.None)
            {
                ToggleHtmlDetailsView();
                return true;
            }
            if (keyCode == Keys.F6 && modifiers == Keys.None && settings.HtmlDetailsView)
            {
                ToggleHtmlDetailsFocus();
                return true;
            }
            if (keyCode == Keys.F5 && modifiers == Keys.None)
            {
                RefreshOriginalFiles();
                return true;
            }
            if (keyCode == Keys.F4 && modifiers == Keys.None)
            {
                OpenAdvancedFileViewer();
                return true;
            }

            if (keyCode == Keys.N && modifiers == Keys.Control)
            {
                NewReport();
                return true;
            }
            if (keyCode == Keys.O && modifiers == Keys.Control)
            {
                AddFiles(false);
                return true;
            }
            if (keyCode == Keys.O && modifiers == (Keys.Control | Keys.Shift))
            {
                AddFiles(true);
                return true;
            }
            if (keyCode == Keys.L && modifiers == (Keys.Control | Keys.Shift))
            {
                OpenFolder(false);
                return true;
            }
            if (keyCode == Keys.R && modifiers == Keys.Control)
            {
                OpenSavedReport();
                return true;
            }
            if (keyCode == Keys.T && modifiers == (Keys.Control | Keys.Shift))
            {
                OpenLastReport();
                return true;
            }
            if (keyCode == Keys.S && modifiers == Keys.Control)
            {
                SaveReport();
                return true;
            }
            if (keyCode == Keys.C && modifiers == Keys.Control)
            {
                CopyCurrentSelection();
                return true;
            }
            if (keyCode == Keys.Oemcomma && modifiers == Keys.Control)
            {
                ShowPreferences();
                return true;
            }
            if (keyCode == Keys.C && modifiers == Keys.Alt)
            {
                CopyReport();
                return true;
            }
            if (keyCode == Keys.L && modifiers == Keys.Alt)
            {
                OpenContainingFolder();
                return true;
            }
            if (keyCode == Keys.V && modifiers == Keys.Alt)
            {
                ViewHtmlReport();
                return true;
            }

            if (keyCode == Keys.Left && modifiers == (Keys.Control | Keys.Shift))
            {
                CollapseAllResults();
                suppressKeyPress = true;
                return true;
            }
            if (keyCode == Keys.Right && modifiers == (Keys.Control | Keys.Shift))
            {
                ExpandAllResults();
                suppressKeyPress = true;
                return true;
            }
            if (keyCode == Keys.Up && modifiers == Keys.Control)
            {
                MoveSelectedSection(-1);
                suppressKeyPress = true;
                return true;
            }
            if (keyCode == Keys.Down && modifiers == Keys.Control)
            {
                MoveSelectedSection(1);
                suppressKeyPress = true;
                return true;
            }

            if (modifiers == Keys.Control && SelectTreeNodeByShortcut(keyCode, 0))
            {
                if (fromHtmlDetails)
                    PrepareHtmlBrowserKeyboardNavigation();
                suppressKeyPress = true;
                return true;
            }
            if (modifiers == (Keys.Control | Keys.Shift) && SelectTreeNodeByShortcut(keyCode, 10))
            {
                if (fromHtmlDetails)
                    PrepareHtmlBrowserKeyboardNavigation();
                suppressKeyPress = true;
                return true;
            }
            if (modifiers != Keys.Alt)
                return false;

            if (keyCode == Keys.Back)
            {
                if (fromHtmlDetails)
                    PrepareHtmlBrowserKeyboardNavigation();
                SelectReportOverview();
                suppressKeyPress = true;
                return true;
            }
            if (keyCode == Keys.Left)
            {
                if (fromHtmlDetails)
                    PrepareHtmlBrowserKeyboardNavigation();
                SelectAdjacentFile(-1);
                suppressKeyPress = true;
                return true;
            }
            if (keyCode == Keys.Right)
            {
                if (fromHtmlDetails)
                    PrepareHtmlBrowserKeyboardNavigation();
                SelectAdjacentFile(1);
                suppressKeyPress = true;
                return true;
            }
            if (keyCode == Keys.PageUp)
            {
                if (fromHtmlDetails)
                    PrepareHtmlBrowserKeyboardNavigation();
                SelectFileByPosition(false);
                suppressKeyPress = true;
                return true;
            }
            if (keyCode == Keys.PageDown)
            {
                if (fromHtmlDetails)
                    PrepareHtmlBrowserKeyboardNavigation();
                SelectFileByPosition(true);
                suppressKeyPress = true;
                return true;
            }
            if (keyCode == Keys.Up)
            {
                if (fromHtmlDetails)
                    PrepareHtmlBrowserKeyboardNavigation();
                SelectAdjacentSection(-1);
                suppressKeyPress = true;
                return true;
            }
            if (keyCode == Keys.Down)
            {
                if (fromHtmlDetails)
                    PrepareHtmlBrowserKeyboardNavigation();
                SelectAdjacentSection(1);
                suppressKeyPress = true;
                return true;
            }
            if (keyCode == Keys.Home)
            {
                if (fromHtmlDetails)
                    PrepareHtmlBrowserKeyboardNavigation();
                SelectSectionByPosition(false);
                suppressKeyPress = true;
                return true;
            }
            if (keyCode == Keys.End)
            {
                if (fromHtmlDetails)
                    PrepareHtmlBrowserKeyboardNavigation();
                SelectSectionByPosition(true);
                suppressKeyPress = true;
                return true;
            }
            if (SelectFileByShortcut(keyCode))
            {
                if (fromHtmlDetails)
                    PrepareHtmlBrowserKeyboardNavigation();
                suppressKeyPress = true;
                return true;
            }

            return false;
        }


        public bool PreFilterMessage(ref Message m)
        {
            const int wmKeyDown = 0x0100;
            const int wmSysKeyDown = 0x0104;

            if (!settings.HtmlDetailsView || detailsBrowser == null || !detailsBrowser.ContainsFocus)
                return false;
            if (m.Msg != wmKeyDown && m.Msg != wmSysKeyDown)
                return false;

            var keyData = ((Keys)((int)m.WParam & 0xffff)) | Control.ModifierKeys;
            bool suppressKeyPress;
            return TryHandleGlobalShortcut(keyData, true, out suppressKeyPress);
        }


        private bool ReturnToHtmlDetailsAfterMenuEscape()
        {
            if (!settings.HtmlDetailsView || detailsBrowser == null || !htmlDetailsWantsFocus)
                return false;
            if (detailsBrowser.ContainsFocus)
                return false;
            BeginInvoke((MethodInvoker)FocusHtmlDetailsDocument);
            statusLabel.Text = "HTML details focused.";
            return true;
        }


        private void PrepareHtmlBrowserKeyboardNavigation()
        {
            if (settings.HtmlDetailsView && detailsBrowser != null && detailsBrowser.ContainsFocus)
            {
                htmlDetailsWantsFocus = true;
                resultsTree.Focus();
            }
        }


        private void FocusHtmlDetailsDocument()
        {
            if (!settings.HtmlDetailsView || detailsBrowser == null)
                return;
            if (WindowState == FormWindowState.Minimized || !Visible)
                return;
            if (!ContainsFocus && Form.ActiveForm != this)
                return;

            detailsBrowser.Focus();
            try
            {
                if (detailsBrowser.Document != null && detailsBrowser.Document.Body != null)
                    detailsBrowser.Document.Body.Focus();
            }
            catch
            {
            }
        }


        private bool CanRestoreHtmlDetailsFocus()
        {
            return settings.HtmlDetailsView &&
                htmlDetailsWantsFocus &&
                detailsBrowser != null &&
                WindowState != FormWindowState.Minimized &&
                Visible &&
                (ContainsFocus || Form.ActiveForm == this);
        }


        private void ResultsTree_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.Shift && e.KeyCode == Keys.Left)
            {
                CollapseAllResults();
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.Control && e.Shift && e.KeyCode == Keys.Right)
            {
                ExpandAllResults();
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.Control && !e.Shift && e.KeyCode == Keys.Up)
            {
                MoveSelectedSection(-1);
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.Control && !e.Shift && e.KeyCode == Keys.Down)
            {
                MoveSelectedSection(1);
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.Alt && e.KeyCode == Keys.Left)
            {
                SelectAdjacentFile(-1);
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.Alt && e.KeyCode == Keys.Back)
            {
                SelectReportOverview();
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.Alt && e.KeyCode == Keys.Right)
            {
                SelectAdjacentFile(1);
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.Alt && e.KeyCode == Keys.PageUp)
            {
                SelectFileByPosition(false);
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.Alt && e.KeyCode == Keys.PageDown)
            {
                SelectFileByPosition(true);
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.Alt && e.KeyCode == Keys.Up)
            {
                SelectAdjacentSection(-1);
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.Alt && e.KeyCode == Keys.Down)
            {
                SelectAdjacentSection(1);
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.Alt && e.KeyCode == Keys.Home)
            {
                SelectSectionByPosition(false);
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.Alt && e.KeyCode == Keys.End)
            {
                SelectSectionByPosition(true);
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.Alt && !e.Control && !e.Shift && SelectFileByShortcut(e.KeyCode))
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
        }


        private void CollapseAllResults()
        {
            if (resultsTree.Nodes.Count == 0)
                return;
            var selected = TopLevelNode(resultsTree.SelectedNode) ?? resultsTree.Nodes[0];
            resultsTree.BeginUpdate();
            resultsTree.CollapseAll();
            resultsTree.SelectedNode = selected;
            resultsTree.EndUpdate();
            selected.EnsureVisible();
            statusLabel.Text = "Report tree collapsed.";
        }


        private void ExpandAllResults()
        {
            if (resultsTree.Nodes.Count == 0)
                return;
            var selected = resultsTree.SelectedNode;
            resultsTree.BeginUpdate();
            resultsTree.ExpandAll();
            resultsTree.EndUpdate();
            if (selected != null)
            {
                resultsTree.SelectedNode = selected;
                selected.EnsureVisible();
            }
            statusLabel.Text = "Report tree expanded.";
        }


        private static TreeNode TopLevelNode(TreeNode node)
        {
            if (node == null)
                return null;
            while (node.Parent != null)
                node = node.Parent;
            return node;
        }


        private bool SelectTreeNodeByShortcut(Keys keyCode, int offset)
        {
            var digit = DigitFromShortcutKey(keyCode);
            if (digit < 0 || resultsTree.Nodes.Count == 0)
                return false;

            var nodes = ShortcutScopeNodes();
            var index = offset + digit;
            if (index < 0 || index >= nodes.Count)
            {
                statusLabel.Text = "No file section for " + ShortcutText(index) + ".";
                if (DetailsPaneContainsFocus())
                {
                    string error;
                    ScreenReaderOutput.TrySpeakForActiveScreenReader(statusLabel.Text, false, out error);
                }
                return true;
            }

            var node = nodes[index];
            var keepDetailsFocus = DetailsPaneContainsFocus();
            resultsTree.SelectedNode = node;
            node.EnsureVisible();
            if (keepDetailsFocus)
                FocusDetailsPane();
            else
                resultsTree.Focus();
            var message = node.Text;
            statusLabel.Text = message;
            if (keepDetailsFocus)
            {
                string error;
                ScreenReaderOutput.TrySpeakForActiveScreenReader(message, false, out error);
            }
            return true;
        }


        private List<TreeNode> ShortcutScopeNodes()
        {
            var selected = resultsTree.SelectedNode;
            if (selected != null)
            {
                if (selected.Parent == null && selected.Nodes.Count > 0)
                    return selected.Nodes.Cast<TreeNode>().ToList();

                if (selected.Parent != null)
                {
                    var fileNode = selected.Parent.Parent ?? selected.Parent;
                    if (fileNode.Nodes.Count > 0)
                        return fileNode.Nodes.Cast<TreeNode>().ToList();
                }
            }

            return new List<TreeNode>();
        }


        private void SelectAdjacentSection(int direction)
        {
            var selected = resultsTree.SelectedNode;
            if (IsReportOverviewNode(selected))
            {
                if (direction > 0)
                {
                    var firstFile = FileRootNodes().FirstOrDefault();
                    if (firstFile != null)
                    {
                        SelectFileOverviewNode(firstFile);
                        return;
                    }
                }

                statusLabel.Text = "Start of report";
                string reportStartError;
                ScreenReaderOutput.TrySpeakForActiveScreenReader(statusLabel.Text, false, out reportStartError);
                return;
            }

            var nodes = ShortcutScopeNodes();
            if (nodes.Count == 0)
            {
                statusLabel.Text = "No file sections are available.";
                return;
            }

            var current = CurrentSectionNodeForNavigation();
            var currentIndex = current == null ? -1 : nodes.IndexOf(current);
            if (currentIndex < 0)
            {
                if (direction < 0)
                {
                    var reportOverview = ReportOverviewNode();
                    if (reportOverview != null && selected != null && selected.Parent == null && selected == FileRootNodes().FirstOrDefault())
                    {
                        SelectFileOverviewNode(reportOverview);
                        return;
                    }

                    statusLabel.Text = "Start of sections";
                    string startError;
                    ScreenReaderOutput.TrySpeakForActiveScreenReader(statusLabel.Text, false, out startError);
                    return;
                }

                currentIndex = -1;
            }
            else if (direction < 0 && currentIndex == 0)
            {
                var fileNode = TopLevelNode(current);
                if (fileNode != null)
                {
                    SelectFileOverviewNode(fileNode);
                    return;
                }
            }

            var targetIndex = currentIndex + direction;
            if (targetIndex < 0 || targetIndex >= nodes.Count)
            {
                statusLabel.Text = direction < 0 ? "Start of sections" : "End of sections";
                string boundaryError;
                ScreenReaderOutput.TrySpeakForActiveScreenReader(statusLabel.Text, false, out boundaryError);
                return;
            }

            SelectSectionNode(nodes[targetIndex]);
        }


        private TreeNode ReportOverviewNode()
        {
            return resultsTree.Nodes
                .Cast<TreeNode>()
                .FirstOrDefault(IsReportOverviewNode);
        }


        private static bool IsReportOverviewNode(TreeNode node)
        {
            return node != null &&
                node.Parent == null &&
                string.Equals(node.Text, "Report overview", StringComparison.OrdinalIgnoreCase);
        }


        private void SelectFileOverviewNode(TreeNode node)
        {
            if (node == null)
                return;

            var keepDetailsFocus = DetailsPaneContainsFocus();
            resultsTree.SelectedNode = node;
            node.EnsureVisible();
            if (keepDetailsFocus)
                FocusDetailsPane();
            else
                resultsTree.Focus();

            var message = node.Text;
            statusLabel.Text = message;
            string error;
            ScreenReaderOutput.TrySpeakForActiveScreenReader(message, false, out error);
        }


        private void SelectSectionByPosition(bool last)
        {
            var nodes = ShortcutScopeNodes();
            if (nodes.Count == 0)
            {
                statusLabel.Text = "No file sections are available.";
                return;
            }

            SelectSectionNode(nodes[last ? nodes.Count - 1 : 0]);
        }


        private void SelectSectionNode(TreeNode node)
        {
            if (node == null)
                return;

            var keepDetailsFocus = DetailsPaneContainsFocus();
            resultsTree.SelectedNode = node;
            node.EnsureVisible();
            if (keepDetailsFocus)
                FocusDetailsPane();
            else
                resultsTree.Focus();

            var message = node.Text;
            statusLabel.Text = message;
            string error;
            ScreenReaderOutput.TrySpeakForActiveScreenReader(message, false, out error);
        }


        private TreeNode CurrentSectionNodeForNavigation()
        {
            var selected = resultsTree.SelectedNode;
            if (selected == null || selected.Parent == null)
                return null;

            var fileNode = TopLevelNode(selected);
            var section = selected;
            while (section.Parent != null && section.Parent != fileNode)
                section = section.Parent;
            return section.Parent == fileNode ? section : null;
        }


        private void AnnounceTreeSelectionFromFocus()
        {
            if (treeShortcutAnnouncementTimer != null)
                treeShortcutAnnouncementTimer.Stop();
            pendingTreeShortcutAnnouncementKey = string.Empty;
            pendingTreeShortcutAnnouncementText = string.Empty;

            if (resultsTree == null || !resultsTree.ContainsFocus || resultsTree.SelectedNode == null)
                return;

            var shortcut = TreeSelectionShortcutText(resultsTree.SelectedNode);
            if (string.IsNullOrWhiteSpace(shortcut))
                return;

            pendingTreeShortcutAnnouncementKey = TreeNodeStableKey(resultsTree.SelectedNode);
            pendingTreeShortcutAnnouncementText = shortcut;
            if (treeShortcutAnnouncementTimer != null)
                treeShortcutAnnouncementTimer.Start();
        }


        private void SpeakPendingTreeShortcutAnnouncement()
        {
            if (resultsTree == null || resultsTree.SelectedNode == null || !resultsTree.ContainsFocus)
            {
                pendingTreeShortcutAnnouncementKey = string.Empty;
                pendingTreeShortcutAnnouncementText = string.Empty;
                return;
            }

            if (string.IsNullOrWhiteSpace(pendingTreeShortcutAnnouncementKey) || string.IsNullOrWhiteSpace(pendingTreeShortcutAnnouncementText))
                return;

            if (!string.Equals(TreeNodeStableKey(resultsTree.SelectedNode), pendingTreeShortcutAnnouncementKey, StringComparison.Ordinal))
            {
                pendingTreeShortcutAnnouncementKey = string.Empty;
                pendingTreeShortcutAnnouncementText = string.Empty;
                return;
            }

            var text = pendingTreeShortcutAnnouncementText;
            pendingTreeShortcutAnnouncementKey = string.Empty;
            pendingTreeShortcutAnnouncementText = string.Empty;
            string error;
            ScreenReaderOutput.TrySpeakForActiveScreenReader(text, false, out error);
        }


        private string TreeSelectionShortcutText(TreeNode node)
        {
            if (node == null)
                return string.Empty;

            var scope = ShortcutScopeNodes();
            var index = scope.IndexOf(node);
            if (index < 0 && node.Parent == null && node.Nodes.Count > 0)
                index = 0;
            if (index < 0 && node.Parent != null && node.Parent.Parent != null)
            {
                index = scope.IndexOf(node.Parent);
            }

            if (index >= 0)
            {
                var shortcut = ShortcutText(index);
                if (!string.IsNullOrWhiteSpace(shortcut))
                    return shortcut;
            }

            return string.Empty;
        }


        private static string TreeNodeStableKey(TreeNode node)
        {
            if (node == null)
                return string.Empty;
            return string.IsNullOrWhiteSpace(node.Name) ? node.FullPath : node.Name;
        }


        private static int DigitFromShortcutKey(Keys keyCode)
        {
            if (keyCode >= Keys.D0 && keyCode <= Keys.D9)
                return keyCode - Keys.D0;
            if (keyCode >= Keys.NumPad0 && keyCode <= Keys.NumPad9)
                return keyCode - Keys.NumPad0;
            return -1;
        }


        private static string ShortcutText(int index)
        {
            if (index >= 0 && index <= 9)
                return "Ctrl+" + index.ToString(CultureInfo.InvariantCulture);
            if (index >= 10 && index <= 19)
                return "Ctrl+Shift+" + (index - 10).ToString(CultureInfo.InvariantCulture);
            return string.Empty;
        }


        private static int FileIndexFromShortcutKey(Keys keyCode)
        {
            var digit = DigitFromShortcutKey(keyCode);
            if (digit < 0)
                return -1;
            return digit == 0 ? 9 : digit - 1;
        }


        private bool SelectFileByShortcut(Keys keyCode)
        {
            var targetIndex = FileIndexFromShortcutKey(keyCode);
            if (targetIndex < 0)
                return false;
            return SelectFileByIndex(targetIndex, "No file " + (targetIndex + 1).ToString(CultureInfo.InvariantCulture) + ".");
        }


        private void SelectAdjacentFile(int direction)
        {
            var fileNodes = FileRootNodes();
            if (fileNodes.Count == 0)
            {
                statusLabel.Text = "No files are loaded.";
                return;
            }

            var current = TopLevelNode(resultsTree.SelectedNode);
            var currentIndex = fileNodes.IndexOf(current);
            if (currentIndex < 0)
            {
                if (direction < 0)
                {
                    statusLabel.Text = "Start of file list";
                    string startError;
                    ScreenReaderOutput.TrySpeakForActiveScreenReader(statusLabel.Text, false, out startError);
                    return;
                }

                currentIndex = -1;
            }

            var targetIndex = currentIndex + direction;
            SelectFileByIndex(targetIndex, direction < 0 ? "Start of file list" : "End of file list", fileNodes);
        }


        private void SelectFileByPosition(bool last)
        {
            var fileNodes = FileRootNodes();
            if (fileNodes.Count == 0)
            {
                statusLabel.Text = "No files are loaded.";
                return;
            }

            SelectFileByIndex(last ? fileNodes.Count - 1 : 0, last ? "End of file list" : "Start of file list", fileNodes);
        }


        private void SelectReportOverview()
        {
            var target = ReportOverviewNode() ?? resultsTree.Nodes.Cast<TreeNode>().FirstOrDefault();
            if (target == null)
            {
                statusLabel.Text = "No report is loaded.";
                return;
            }

            var keepDetailsFocus = DetailsPaneContainsFocus();
            resultsTree.SelectedNode = target;
            target.EnsureVisible();
            if (keepDetailsFocus)
                FocusDetailsPane();
            else
                resultsTree.Focus();

            statusLabel.Text = target.Text;
        }


        private bool SelectFileByIndex(int targetIndex, string unavailableMessage)
        {
            return SelectFileByIndex(targetIndex, unavailableMessage, FileRootNodes());
        }


        private bool SelectFileByIndex(int targetIndex, string unavailableMessage, List<TreeNode> fileNodes)
        {
            if (fileNodes.Count == 0)
            {
                statusLabel.Text = "No files are loaded.";
                return true;
            }

            if (targetIndex < 0 || targetIndex >= fileNodes.Count)
            {
                statusLabel.Text = unavailableMessage;
                string unavailableError;
                ScreenReaderOutput.TrySpeakForActiveScreenReader(statusLabel.Text, false, out unavailableError);
                return true;
            }

            var preferredSection = CurrentFileSectionName();
            var target = fileNodes[targetIndex];
            var nodeToSelect = string.IsNullOrWhiteSpace(preferredSection)
                ? target
                : (FindDirectChildSection(target, preferredSection) ?? FindDirectChildSection(target, "Summary") ?? target);

            var keepDetailsFocus = DetailsPaneContainsFocus();
            resultsTree.SelectedNode = nodeToSelect;
            nodeToSelect.EnsureVisible();
            if (keepDetailsFocus)
                FocusDetailsPane();
            else
                resultsTree.Focus();

            var message = target.Text + " " + (targetIndex + 1).ToString(CultureInfo.InvariantCulture) + " of " + fileNodes.Count.ToString(CultureInfo.InvariantCulture);
            statusLabel.Text = message;
            string error;
            ScreenReaderOutput.TrySpeakForActiveScreenReader(message, false, out error);
            return true;
        }


        private string CurrentFileSectionName()
        {
            var selected = resultsTree.SelectedNode;
            if (selected == null || selected.Parent == null)
                return string.Empty;

            var fileNode = TopLevelNode(selected);
            var section = selected;
            while (section.Parent != null && section.Parent != fileNode)
                section = section.Parent;
            return section.Parent == fileNode ? section.Text : string.Empty;
        }


        private string CurrentFileItemName()
        {
            var selected = resultsTree.SelectedNode;
            if (selected == null || selected.Parent == null)
                return string.Empty;

            var fileNode = TopLevelNode(selected);
            return selected.Parent != null && selected.Parent.Parent == fileNode ? selected.Text : string.Empty;
        }


        private static TreeNode FindDirectChildSection(TreeNode fileNode, string sectionName)
        {
            if (fileNode == null || string.IsNullOrWhiteSpace(sectionName))
                return null;
            return fileNode.Nodes
                .Cast<TreeNode>()
                .FirstOrDefault(node => string.Equals(node.Text, sectionName, StringComparison.OrdinalIgnoreCase));
        }


        private List<TreeNode> FileRootNodes()
        {
            return resultsTree.Nodes
                .Cast<TreeNode>()
                .Where(node => node.Nodes.Count > 0 && !string.Equals(node.Text, "Report overview", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }


        private void MoveSelectedSection(int direction)
        {
            if (currentReports.Count == 0 || resultsTree.SelectedNode == null)
            {
                statusLabel.Text = "No report section is selected.";
                return;
            }

            var sectionNode = SelectedSectionNode();
            if (sectionNode == null)
            {
                statusLabel.Text = "Choose a report section before moving section order. File order and Report overview stay fixed.";
                return;
            }

            if (ReportSectionOrdering.IsPinnedSection(sectionNode.Text))
            {
                statusLabel.Text = sectionNode.Text + " is pinned and cannot be moved.";
                return;
            }

            var order = CurrentSectionOrder();
            var index = order.FindIndex(sectionTitle => string.Equals(sectionTitle, sectionNode.Text, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                statusLabel.Text = "Could not find " + sectionNode.Text + " in the current section order.";
                return;
            }

            var target = index + direction;
            if (target < 0 || target >= order.Count)
            {
                statusLabel.Text = sectionNode.Text + " is already " + (direction < 0 ? "as high" : "as low") + " as it can go.";
                return;
            }

            var movedTitle = order[index];
            order.RemoveAt(index);
            order.Insert(target, movedTitle);
            settings.SectionOrder = order;
            settings.Save();
            ReportSectionOrdering.Apply(currentReports, settings.SectionOrder);

            var fileNode = TopLevelNode(sectionNode);
            var fileName = fileNode == null ? null : fileNode.Text;
            RebuildReportTree(fileName, movedTitle);
            var shortcut = ShortcutText(Math.Max(0, target + 1));
            var message = string.IsNullOrWhiteSpace(shortcut)
                ? "Moved " + movedTitle + "."
                : "Moved " + movedTitle + ". " + shortcut + ".";
            statusLabel.Text = message;
            ScreenReaderOutput.TrySpeakForActiveScreenReader(message);
        }


        private TreeNode SelectedSectionNode()
        {
            var node = resultsTree.SelectedNode;
            if (node == null)
                return null;
            if (node.Parent == null)
                return null;
            if (node.Parent.Parent == null)
                return node;
            return node.Parent;
        }


        private List<string> CurrentSectionOrder()
        {
            var available = currentReports
                .SelectMany(report => report.Sections)
                .Select(section => section.Title)
                .Where(title => !ReportSectionOrdering.IsPinnedSection(title))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var configured = (settings.SectionOrder ?? new List<string>())
                .Where(title => available.Contains(title, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            configured.AddRange(available.Where(title => !configured.Contains(title, StringComparer.OrdinalIgnoreCase)));
            return configured;
        }
    }
}

