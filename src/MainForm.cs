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
{    internal sealed class MainForm : Form
    {
        private readonly TreeView resultsTree;
        private readonly TextBox detailsBox;
        private readonly Button copyButton;
        private readonly Button saveButton;
        private readonly Button viewHtmlButton;
        private readonly Button openFolderButton;
        private readonly Button addFilesButton;
        private readonly Button closeButton;
        private readonly Label statusLabel;
        private readonly ToolTip toolTip;
        private readonly ToolStripMenuItem recentReportsMenu;
        private readonly System.Windows.Forms.Timer updateCheckTimer;
        private readonly System.Windows.Forms.Timer treeShortcutAnnouncementTimer;
        private AppSettings settings;
        private readonly List<string> loadedFiles = new List<string>();
        private readonly List<FileReport> currentReports = new List<FileReport>();
        private readonly List<string> temporaryHtmlReports = new List<string>();
        private readonly Dictionary<string, AdvancedFileViewerState> advancedViewerStates = new Dictionary<string, AdvancedFileViewerState>(StringComparer.OrdinalIgnoreCase);
        private TimeSpan? currentReportElapsed;
        private volatile string reportText = string.Empty;
        private string currentSavedReportPath = string.Empty;
        private string pendingTreeShortcutAnnouncementKey = string.Empty;
        private string pendingTreeShortcutAnnouncementText = string.Empty;

        public MainForm(string[] args)
        {
            settings = AppSettings.Load();
            Text = "FileDentify " + Program.Version;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(820, 560);
            Width = 980;
            Height = 700;
            KeyPreview = true;
            toolTip = new ToolTip();

            var menu = new MenuStrip();
            var fileMenu = new ToolStripMenuItem("&File");
            fileMenu.DropDownItems.Add(CreateShortcutMenuItem("&Open files...", "Ctrl+O", delegate { AddFiles(false); }));
            fileMenu.DropDownItems.Add(CreateShortcutMenuItem("Open fo&lder...", "Ctrl+Shift+L", delegate { OpenFolder(false); }));
            fileMenu.DropDownItems.Add(CreateShortcutMenuItem("&Append files to report...", "Ctrl+Shift+O", delegate { AddFiles(true); }));
            fileMenu.DropDownItems.Add(CreateShortcutMenuItem("Open FileDentify &report...", "Ctrl+R", delegate { OpenSavedReport(); }));
            fileMenu.DropDownItems.Add(CreateShortcutMenuItem("Reopen las&t report", "Ctrl+Shift+T", delegate { OpenLastReport(); }));
            recentReportsMenu = new ToolStripMenuItem("Open recent &items");
            recentReportsMenu.DropDownOpening += delegate { PopulateRecentReportsMenu(); };
            fileMenu.DropDownItems.Add(recentReportsMenu);
            fileMenu.DropDownItems.Add(CreateShortcutMenuItem("&Save report...", "Ctrl+S", delegate { SaveReport(); }));
            fileMenu.DropDownItems.Add(CreateShortcutMenuItem("&View HTML report", "Alt+V", delegate { ViewHtmlReport(); }));
            fileMenu.DropDownItems.Add(CreateShortcutMenuItem("Re&fresh original files", "F5", delegate { RefreshOriginalFiles(); }));
            fileMenu.DropDownItems.Add(CreateShortcutMenuItem("A&dvanced file viewer", "F4", delegate { OpenAdvancedFileViewer(); }));
            fileMenu.DropDownItems.Add(CreateShortcutMenuItem("Open &containing folder", "Alt+L", delegate { OpenContainingFolder(); }));
            fileMenu.DropDownItems.Add(CreateShortcutMenuItem("E&xit", "Esc", delegate { Close(); }));
            var editMenu = new ToolStripMenuItem("&Edit");
            editMenu.DropDownItems.Add(CreateShortcutMenuItem("&Copy selected details", "Ctrl+C", delegate { CopyCurrentSelection(); }));
            editMenu.DropDownItems.Add(CreateShortcutMenuItem("Copy &full report", "Alt+C", delegate { CopyReport(); }));
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            editMenu.DropDownItems.Add(CreateShortcutMenuItem("Co&llapse all", "Ctrl+Shift+Left", delegate { CollapseAllResults(); }));
            editMenu.DropDownItems.Add(CreateShortcutMenuItem("&Expand all", "Ctrl+Shift+Right", delegate { ExpandAllResults(); }));
            editMenu.DropDownItems.Add(CreateShortcutMenuItem("Move section &up", "Ctrl+Up", delegate { MoveSelectedSection(-1); }));
            editMenu.DropDownItems.Add(CreateShortcutMenuItem("Move section &down", "Ctrl+Down", delegate { MoveSelectedSection(1); }));
            editMenu.DropDownItems.Add(CreateShortcutMenuItem("&Previous file", "Alt+Left", delegate { SelectAdjacentFile(-1); }));
            editMenu.DropDownItems.Add(CreateShortcutMenuItem("&Next file", "Alt+Right", delegate { SelectAdjacentFile(1); }));
            editMenu.DropDownItems.Add(CreateShortcutMenuItem("F&irst file", "Alt+PageUp", delegate { SelectFileByPosition(false); }));
            editMenu.DropDownItems.Add(CreateShortcutMenuItem("L&ast file", "Alt+PageDown", delegate { SelectFileByPosition(true); }));
            editMenu.DropDownItems.Add(CreateShortcutMenuItem("Previous &section", "Alt+Up", delegate { SelectAdjacentSection(-1); }));
            editMenu.DropDownItems.Add(CreateShortcutMenuItem("Ne&xt section", "Alt+Down", delegate { SelectAdjacentSection(1); }));
            editMenu.DropDownItems.Add(CreateShortcutMenuItem("Fi&rst section", "Alt+Home", delegate { SelectSectionByPosition(false); }));
            editMenu.DropDownItems.Add(CreateShortcutMenuItem("Las&t section", "Alt+End", delegate { SelectSectionByPosition(true); }));
            var optionsMenu = new ToolStripMenuItem("&Options");
            optionsMenu.DropDownItems.Add(CreateShortcutMenuItem("&Preferences...", "Ctrl+,", delegate { ShowPreferences(); }));
            var helpMenu = new ToolStripMenuItem("&Help");
            helpMenu.DropDownItems.Add(CreateShortcutMenuItem("&Help", "F1", delegate { ShowHelp(); }));
            helpMenu.DropDownItems.Add(CreateShortcutMenuItem("&Check for Updates...", "Shift+F1", delegate { CheckForUpdates(true); }));
            helpMenu.DropDownItems.Add("&Version History...", null, delegate { ShowVersionHistory(); });
            helpMenu.DropDownItems.Add(CreateShortcutMenuItem("&Project page", "Ctrl+F1", delegate { OpenProjectPage(); }));
            helpMenu.DropDownItems.Add("Con&tact", null, delegate { OpenContactPage(); });
            helpMenu.DropDownItems.Add("&Donate", null, delegate { OpenDonatePage(); });
            helpMenu.DropDownItems.Add("Other &software", null, delegate { OpenOtherSoftwarePage(); });
            helpMenu.DropDownItems.Add("Third-party &notices", null, delegate { ShowThirdPartyNotices(); });
            helpMenu.DropDownItems.Add(new ToolStripSeparator());
            helpMenu.DropDownItems.Add("&About FileDentify", null, delegate { ShowAbout(); });
            menu.Items.Add(fileMenu);
            menu.Items.Add(editMenu);
            menu.Items.Add(optionsMenu);
            menu.Items.Add(helpMenu);
            MainMenuStrip = menu;
            Controls.Add(menu);

            var main = new TableLayoutPanel();
            main.Dock = DockStyle.Fill;
            main.ColumnCount = 1;
            main.RowCount = 3;
            main.Padding = new Padding(10, 6, 10, 10);
            main.TabStop = false;
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(main);
            main.BringToFront();

            var split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Vertical;
            split.TabStop = false;
            split.Panel1.TabStop = false;
            split.Panel2.TabStop = false;
            main.Controls.Add(split, 0, 0);

            resultsTree = new TreeView();
            resultsTree.Dock = DockStyle.Fill;
            resultsTree.HideSelection = false;
            resultsTree.AccessibleName = "Identification tree";
            resultsTree.ContextMenuStrip = CreateTreeContextMenu();
            resultsTree.AfterSelect += delegate
            {
                UpdateDetailsFromSelection();
                AnnounceTreeSelectionFromFocus();
            };
            resultsTree.KeyDown += ResultsTree_KeyDown;
            resultsTree.NodeMouseClick += ResultsTree_NodeMouseClick;
            split.Panel1.Controls.Add(resultsTree);

            detailsBox = new TextBox();
            detailsBox.Dock = DockStyle.Fill;
            detailsBox.Multiline = true;
            detailsBox.ScrollBars = ScrollBars.Both;
            detailsBox.ReadOnly = true;
            detailsBox.WordWrap = false;
            detailsBox.AccessibleName = "Selected item details";
            detailsBox.Enter += delegate { ResetTextBoxToTop(detailsBox); };
            detailsBox.KeyDown += TextBoxSelectAll_KeyDown;
            split.Panel2.Controls.Add(detailsBox);

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.AutoSize = true;
            buttons.FlowDirection = FlowDirection.LeftToRight;
            buttons.WrapContents = true;
            buttons.TabStop = false;
            main.Controls.Add(buttons, 0, 1);

            addFilesButton = CreateButton("Open files...", delegate { AddFiles(false); }, "Open files", "Ctrl+O", "Choose one or more files to inspect.");
            var openInputFolderButton = CreateButton("Open folder...", delegate { OpenFolder(false); }, "Open folder", "Ctrl+Shift+L", "Choose a folder and recursively inspect its files.");
            copyButton = CreateButton("&Copy report", delegate { CopyReport(); }, "Copy report", "Alt+C", "Copy the current report text.");
            saveButton = CreateButton("Save report...", delegate { SaveReport(); }, "Save report", "Ctrl+S", "Save the current report as text or HTML.");
            viewHtmlButton = CreateButton("&View HTML report", delegate { ViewHtmlReport(); }, "View HTML report", "Alt+V", "Open a temporary HTML version of the current report in the default browser.");
            openFolderButton = CreateButton("Open containing fo&lder", delegate { OpenContainingFolder(); }, "Open containing folder", "Alt+L", "Open the selected file's folder in File Explorer.");
            buttons.Controls.Add(addFilesButton);
            buttons.Controls.Add(openInputFolderButton);
            buttons.Controls.Add(copyButton);
            buttons.Controls.Add(saveButton);
            buttons.Controls.Add(viewHtmlButton);
            buttons.Controls.Add(openFolderButton);
            var preferencesButton = CreateButton("Preferences...", delegate { ShowPreferences(); }, "Preferences", "Ctrl+,", "Open FileDentify preferences.");
            buttons.Controls.Add(preferencesButton);
            var helpButton = CreateButton("Help", delegate { ShowHelp(); }, "Help", "F1", "Open the FileDentify manual.");
            buttons.Controls.Add(helpButton);
            closeButton = CreateButton("Close", delegate { Close(); }, "Close", "ESC", "Close FileDentify.");
            buttons.Controls.Add(closeButton);

            statusLabel = new Label();
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.AutoSize = true;
            statusLabel.AccessibleName = "Status";
            statusLabel.Text = "Ready.";
            main.Controls.Add(statusLabel, 0, 2);

            KeyDown += MainForm_KeyDown;
            updateCheckTimer = new System.Windows.Forms.Timer();
            updateCheckTimer.Interval = 60 * 60 * 1000;
            updateCheckTimer.Tick += delegate { CheckAutomaticUpdateSchedule(); };
            ConfigureUpdateTimer();
            treeShortcutAnnouncementTimer = new System.Windows.Forms.Timer();
            treeShortcutAnnouncementTimer.Interval = 120;
            treeShortcutAnnouncementTimer.Tick += delegate
            {
                treeShortcutAnnouncementTimer.Stop();
                SpeakPendingTreeShortcutAnnouncement();
            };

            var initial = args.Where(a => !string.IsNullOrWhiteSpace(a)).Select(Path.GetFullPath).ToArray();
            if (initial.Length > 0)
            {
                if (initial.Length == 1 && SavedReportStore.IsSavedReportPath(initial[0]) && File.Exists(initial[0]))
                    LoadSavedReport(initial[0]);
                else
                    LoadFiles(initial, false);
            }
            else
            {
                statusLabel.Text = "Choose one or more files to inspect.";
                resultsTree.Nodes.Add(new TreeNode("No file loaded") { Tag = "Choose Open files to inspect a file. FileDentify combines file/libmagic-style identification with Windows metadata, readable strings, hashes, advanced viewing, and accessible reports. Press F1 for help or Ctrl+comma for Preferences." });
                resultsTree.SelectedNode = resultsTree.Nodes[0];
                addFilesButton.Focus();
            }
            Shown += delegate { ScheduleStartupUpdateCheck(); };
            FormClosed += delegate { DeleteTemporaryHtmlReports(); };
        }

        private Button CreateButton(string text, EventHandler handler, string accessibleName, string shortcutText, string accessibleDescription)
        {
            var button = new ShortcutButton();
            button.Text = text;
            button.AutoSize = true;
            button.Margin = new Padding(0, 8, 8, 0);
            button.AccessibleName = accessibleName;
            ((ShortcutButton)button).ShortcutText = shortcutText;
            button.AccessibleDescription = accessibleDescription;
            if (!string.IsNullOrWhiteSpace(shortcutText))
                toolTip.SetToolTip(button, shortcutText);
            button.Click += handler;
            return button;
        }

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
            if (e.KeyCode == Keys.F1)
            {
                ShowHelp();
                e.Handled = true;
            }
            else if (e.Control && e.Shift && e.KeyCode == Keys.O)
            {
                AddFiles(true);
                e.Handled = true;
            }
            else if (e.Control && e.Shift && e.KeyCode == Keys.L)
            {
                OpenFolder(false);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.O)
            {
                AddFiles(false);
                e.Handled = true;
            }
            else if (e.Control && !e.Shift && e.KeyCode == Keys.R)
            {
                OpenSavedReport();
                e.Handled = true;
            }
            else if (e.Control && e.Shift && e.KeyCode == Keys.T)
            {
                OpenLastReport();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.S)
            {
                SaveReport();
                e.Handled = true;
            }
            else if (!e.Control && !e.Alt && !e.Shift && e.KeyCode == Keys.F5)
            {
                RefreshOriginalFiles();
                e.Handled = true;
            }
            else if (!e.Alt && e.KeyCode == Keys.F4)
            {
                OpenAdvancedFileViewer();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.C)
            {
                CopyCurrentSelection();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.Oemcomma)
            {
                ShowPreferences();
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
            else if (e.Control && e.Shift && e.KeyCode == Keys.Left)
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
            else if (e.Shift && e.KeyCode == Keys.F1)
            {
                CheckForUpdates(true);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.F1)
            {
                OpenProjectPage();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Shift | Keys.O))
            {
                AddFiles(true);
                return true;
            }
            if (keyData == (Keys.Control | Keys.Shift | Keys.L))
            {
                OpenFolder(false);
                return true;
            }
            if (keyData == (Keys.Control | Keys.R))
            {
                OpenSavedReport();
                return true;
            }
            if (keyData == (Keys.Control | Keys.Shift | Keys.T))
            {
                OpenLastReport();
                return true;
            }
            if (keyData == Keys.F5)
            {
                RefreshOriginalFiles();
                return true;
            }
            if (keyData == (Keys.Control | Keys.Shift | Keys.Left))
            {
                CollapseAllResults();
                return true;
            }
            if (keyData == (Keys.Control | Keys.Shift | Keys.Right))
            {
                ExpandAllResults();
                return true;
            }
            if (keyData == (Keys.Control | Keys.Up))
            {
                MoveSelectedSection(-1);
                return true;
            }
            if (keyData == (Keys.Control | Keys.Down))
            {
                MoveSelectedSection(1);
                return true;
            }
            if (keyData == (Keys.Alt | Keys.Left))
            {
                SelectAdjacentFile(-1);
                return true;
            }
            if (keyData == (Keys.Alt | Keys.Right))
            {
                SelectAdjacentFile(1);
                return true;
            }
            if (keyData == (Keys.Alt | Keys.PageUp))
            {
                SelectFileByPosition(false);
                return true;
            }
            if (keyData == (Keys.Alt | Keys.PageDown))
            {
                SelectFileByPosition(true);
                return true;
            }
            if (keyData == (Keys.Alt | Keys.Up))
            {
                SelectAdjacentSection(-1);
                return true;
            }
            if (keyData == (Keys.Alt | Keys.Down))
            {
                SelectAdjacentSection(1);
                return true;
            }
            if (keyData == (Keys.Alt | Keys.Home))
            {
                SelectSectionByPosition(false);
                return true;
            }
            if (keyData == (Keys.Alt | Keys.End))
            {
                SelectSectionByPosition(true);
                return true;
            }
            var modifiers = keyData & Keys.Modifiers;
            var keyCode = keyData & Keys.KeyCode;
            if (modifiers == Keys.Alt && SelectFileByShortcut(keyCode))
                return true;
            if (modifiers == Keys.Control && SelectTreeNodeByShortcut(keyCode, 0))
                return true;
            if (modifiers == (Keys.Control | Keys.Shift) && SelectTreeNodeByShortcut(keyCode, 10))
                return true;
            return base.ProcessCmdKey(ref msg, keyData);
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
                if (detailsBox != null && detailsBox.ContainsFocus)
                {
                    string error;
                    ScreenReaderOutput.TrySpeakForActiveScreenReader(statusLabel.Text, false, out error);
                }
                return true;
            }

            var node = nodes[index];
            var keepDetailsFocus = detailsBox != null && detailsBox.ContainsFocus;
            resultsTree.SelectedNode = node;
            node.EnsureVisible();
            if (keepDetailsFocus)
                detailsBox.Focus();
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

            var keepDetailsFocus = detailsBox != null && detailsBox.ContainsFocus;
            resultsTree.SelectedNode = node;
            node.EnsureVisible();
            if (keepDetailsFocus)
                detailsBox.Focus();
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

            var keepDetailsFocus = detailsBox != null && detailsBox.ContainsFocus;
            resultsTree.SelectedNode = node;
            node.EnsureVisible();
            if (keepDetailsFocus)
                detailsBox.Focus();
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

            var keepDetailsFocus = detailsBox != null && detailsBox.ContainsFocus;
            resultsTree.SelectedNode = nodeToSelect;
            nodeToSelect.EnsureVisible();
            if (keepDetailsFocus)
                detailsBox.Focus();
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

        private void AddFiles(bool append)
        {
            if (!append && !ConfirmReplaceCurrentReport())
                return;

            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = append ? "Append files to FileDentify report" : "Open files to identify";
                dialog.Multiselect = true;
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    LoadFiles(dialog.FileNames, append);
            }
        }

        private void OpenFolder(bool append)
        {
            if (!append && !ConfirmReplaceCurrentReport())
                return;

            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = append ? "Choose a folder to append to the FileDentify report" : "Choose a folder to inspect with FileDentify";
                dialog.ShowNewFolderButton = false;
                if (dialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                    LoadFiles(new[] { dialog.SelectedPath }, append);
            }
        }

        private void OpenSavedReport()
        {
            if (!ConfirmReplaceCurrentReport())
                return;

            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Open FileDentify report";
                dialog.Filter = "FileDentify report (*.fdreport)|*.fdreport|All files (*.*)|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    LoadSavedReport(dialog.FileName);
            }
        }

        private void LoadSavedReport(string path)
        {
            try
            {
                var loaded = SavedReportStore.Load(path);
                loadedFiles.Clear();
                loadedFiles.AddRange(loaded.Reports
                    .Select(report => report.OriginalPath)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase));
                currentSavedReportPath = Path.GetFullPath(path);
                ShowReports(loaded.Reports, loaded.Elapsed ?? TimeSpan.Zero, null, loaded.Selection);
                AddRecentReport(currentSavedReportPath);
                statusLabel.Text = "Opened saved FileDentify report.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Open FileDentify report", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PopulateRecentReportsMenu()
        {
            recentReportsMenu.DropDownItems.Clear();
            var recent = (settings.RecentReports ?? new List<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToArray();
            if (recent.Length == 0)
            {
                recentReportsMenu.DropDownItems.Add(new ToolStripMenuItem("No recent reports") { Enabled = false });
                return;
            }

            foreach (var path in recent)
            {
                var label = RecentReportLabel(path);
                recentReportsMenu.DropDownItems.Add(new ToolStripMenuItem(label, null, delegate { OpenRecentReport(path); }));
            }
        }

        private void OpenLastReport()
        {
            var path = SavedReportStore.AutoSavePath;
            if (!File.Exists(path))
            {
                statusLabel.Text = "No automatically saved last report is available.";
                return;
            }
            if (!ConfirmReplaceCurrentReport())
                return;
            LoadSavedReport(path);
        }

        private void OpenRecentReport(string path)
        {
            if (!File.Exists(path))
            {
                statusLabel.Text = "Recent report not found: " + path;
                RemoveRecentReport(path);
                return;
            }
            if (!ConfirmReplaceCurrentReport())
                return;
            LoadSavedReport(path);
        }

        private string RecentReportLabel(string path)
        {
            if (string.Equals(path, SavedReportStore.AutoSavePath, StringComparison.OrdinalIgnoreCase))
            {
                var reports = currentReports.Count > 0 ? currentReports : TryReadReportsForLabel(path);
                if (reports.Count == 1 && !string.IsNullOrWhiteSpace(reports[0].OriginalPath))
                    return "Last report: " + reports[0].OriginalPath;
                if (reports.Count == 2)
                    return "Last report: " + reports[0].OriginalPath + "; " + reports[1].OriginalPath;
                if (reports.Count > 2)
                    return "Last report: " + reports.Count.ToString(CultureInfo.InvariantCulture) + " files";
                return "Last automatically saved report";
            }
            return path;
        }

        private static List<FileReport> TryReadReportsForLabel(string path)
        {
            try { return SavedReportStore.Load(path).Reports ?? new List<FileReport>(); }
            catch { return new List<FileReport>(); }
        }

        private void AddRecentReport(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            var full = Path.GetFullPath(path);
            var recent = new List<string> { full };
            recent.AddRange((settings.RecentReports ?? new List<string>())
                .Where(item => !string.Equals(item, full, StringComparison.OrdinalIgnoreCase))
                .Take(9));
            settings.RecentReports = recent;
            settings.Save();
        }

        private void RemoveRecentReport(string path)
        {
            settings.RecentReports = (settings.RecentReports ?? new List<string>())
                .Where(item => !string.Equals(item, path, StringComparison.OrdinalIgnoreCase))
                .ToList();
            settings.Save();
        }

        private void RefreshOriginalFiles()
        {
            var paths = currentReports
                .Select(report => report.OriginalPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (paths.Length == 0)
            {
                statusLabel.Text = "This report has no original file paths to refresh.";
                return;
            }

            var existing = paths.Where(File.Exists).ToArray();
            var missing = paths.Where(path => !File.Exists(path)).ToArray();
            if (existing.Length == 0)
            {
                statusLabel.Text = "Cannot refresh. None of the original files are available on this machine.";
                MessageBox.Show(this, "None of the original files in this report are available on this machine, so FileDentify cannot refresh it.", "Refresh original files", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (missing.Length > 0)
            {
                MessageBox.Show(this,
                    missing.Length.ToString(CultureInfo.InvariantCulture) + " original file(s) are not available on this machine. FileDentify will refresh the " + existing.Length.ToString(CultureInfo.InvariantCulture) + " file(s) it can still read.",
                    "Refresh original files",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            LoadFiles(existing, false);
        }

        private bool ConfirmReplaceCurrentReport()
        {
            if (currentReports.Count == 0)
                return true;

            var result = MessageBox.Show(
                this,
                "Opening new files will replace the current report. Save the current report first?",
                "Open files",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);
            if (result == DialogResult.Cancel)
                return false;
            if (result == DialogResult.Yes)
                return SaveReport();
            return true;
        }

        private void LoadFiles(IEnumerable<string> paths, bool append)
        {
            var inputs = paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (inputs.Length == 0)
            {
                if (append && currentReports.Count > 0)
                    statusLabel.Text = "No readable files were selected.";
                else
                    ShowEmptyState("No readable files were selected.", "Choose Open files to inspect a file. Press F1 for help or Ctrl+comma for Preferences.");
                return;
            }

            var existingReports = append ? currentReports.ToList() : new List<FileReport>();
            var existingFiles = append ? loadedFiles.ToList() : new List<string>();
            var existingElapsed = append ? currentReportElapsed : null;
            var restoreFileName = append ? (TopLevelNode(resultsTree.SelectedNode) == null ? null : TopLevelNode(resultsTree.SelectedNode).Text) : null;
            var restoreSectionName = append ? CurrentFileSectionName() : null;
            if (!append)
            {
                currentReports.Clear();
                currentReportElapsed = null;
                currentSavedReportPath = string.Empty;
                resultsTree.Nodes.Clear();
                detailsBox.Clear();
                reportText = string.Empty;
            }
            SetBusy(true, "Finding files...");
            ShowProgressState("Finding files", "Scanning selected files and folders. Large folders can take a moment before inspection starts.");

            var worker = new Thread(delegate()
            {
                var stopwatch = Stopwatch.StartNew();
                var files = ExpandInputPaths(inputs)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (append)
                {
                    var alreadyLoaded = new HashSet<string>(existingFiles, StringComparer.OrdinalIgnoreCase);
                    files = files.Where(path => !alreadyLoaded.Contains(path)).ToArray();
                }
                if (files.Length == 0)
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (append && existingReports.Count > 0)
                        {
                            loadedFiles.Clear();
                            loadedFiles.AddRange(existingFiles);
                            RebuildReportTree(restoreFileName, restoreSectionName);
                            SetBusy(false, "No new readable files were found.");
                            statusLabel.Text = "No new readable files were found.";
                        }
                        else
                        {
                            loadedFiles.Clear();
                            SetBusy(false, "No readable files were found.");
                            ShowEmptyState("No readable files were found.", "The selected input did not contain readable files. Choose Open files to inspect a file, or send a folder that contains files.");
                        }
                    });
                    return;
                }

                BeginInvoke((MethodInvoker)delegate
                {
                    loadedFiles.Clear();
                    loadedFiles.AddRange(append ? existingFiles.Concat(files) : files);
                    if (!append)
                        PruneAdvancedViewerStates(files);
                    SetBusy(true, (append ? "Appending " : "Inspecting ") + files.Length.ToString(CultureInfo.InvariantCulture) + " file(s)...");
                });

                var reports = new List<FileReport>();
                for (var i = 0; i < files.Length; i++)
                {
                    var index = i;
                    if (index == 0 || index % 25 == 0)
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            ShowProgressState("Generating report", "Inspecting file " + (index + 1).ToString(CultureInfo.InvariantCulture) + " of " + files.Length.ToString(CultureInfo.InvariantCulture) + ": " + Path.GetFileName(files[index]));
                            statusLabel.Text = "Generating report. " + (index + 1).ToString(CultureInfo.InvariantCulture) + " of " + files.Length.ToString(CultureInfo.InvariantCulture) + " file(s)...";
                        });
                    }
                    reports.Add(FileInspector.Inspect(files[i]));
                }
                stopwatch.Stop();
                BeginInvoke((MethodInvoker)delegate
                {
                    var combinedReports = append ? existingReports.Concat(reports).ToList() : reports;
                    var combinedElapsed = append && existingElapsed.HasValue ? existingElapsed.Value + stopwatch.Elapsed : stopwatch.Elapsed;
                    ShowReports(combinedReports, combinedElapsed, append ? Path.GetFileName(files[0]) : null);
                });
            });
            worker.IsBackground = true;
            worker.Name = "FileDentify analyzer";
            worker.Start();
        }

        private static IEnumerable<string> ExpandInputPaths(IEnumerable<string> inputs)
        {
            foreach (var input in inputs)
            {
                string path;
                try { path = Path.GetFullPath(input); }
                catch { continue; }

                if (File.Exists(path))
                {
                    yield return path;
                    continue;
                }

                if (!Directory.Exists(path))
                    continue;

                if (FileInspector.IsReportableDirectoryPackage(path))
                {
                    yield return path;
                    continue;
                }

                foreach (var file in SafeEnumerateFiles(path))
                    yield return file;
            }
        }

        private static IEnumerable<string> SafeEnumerateFiles(string root)
        {
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                var directory = pending.Pop();
                string[] files;
                try { files = Directory.GetFiles(directory).OrderBy(path => path, NaturalPathComparer.Instance).ToArray(); }
                catch { files = new string[0]; }
                foreach (var file in files)
                    yield return file;

                string[] directories;
                try { directories = Directory.GetDirectories(directory).OrderBy(path => path, NaturalPathComparer.Instance).ToArray(); }
                catch { directories = new string[0]; }
                foreach (var child in directories.Reverse())
                {
                    if (FileInspector.IsReportableDirectoryPackage(child))
                        yield return child;
                    else
                        pending.Push(child);
                }
            }
        }

        private void ShowEmptyState(string status, string detail)
        {
            resultsTree.BeginUpdate();
            resultsTree.Nodes.Clear();
            var node = new TreeNode("No files loaded") { Tag = detail };
            resultsTree.Nodes.Add(node);
            resultsTree.SelectedNode = node;
            resultsTree.EndUpdate();
            detailsBox.Text = EnsureTrailingBlankLine(detail);
            ResetTextBoxToTop(detailsBox);
            statusLabel.Text = status;
        }

        private void ShowReports(List<FileReport> reports, TimeSpan elapsed)
        {
            ShowReports(reports, elapsed, null, null);
        }

        private void ShowReports(List<FileReport> reports, TimeSpan elapsed, string preferredFileName)
        {
            ShowReports(reports, elapsed, preferredFileName, null);
        }

        private void ShowReports(List<FileReport> reports, TimeSpan elapsed, string preferredFileName, SavedReportSelection selection)
        {
            currentReports.Clear();
            currentReports.AddRange(reports);
            currentReportElapsed = elapsed;
            ReportSectionOrdering.Apply(currentReports, settings.SectionOrder);
            RebuildReportTree(preferredFileName, null, selection);
            AutoSaveCurrentReport();
            SetBusy(false, "Finished. " + currentReports.Count.ToString(CultureInfo.InvariantCulture) + " file(s) inspected in " + FormatElapsed(elapsed) + ".");
        }

        private void RebuildReportTree(string preferredFileName, string preferredSectionTitle)
        {
            RebuildReportTree(preferredFileName, preferredSectionTitle, null);
        }

        private void RebuildReportTree(string preferredFileName, string preferredSectionTitle, SavedReportSelection selection)
        {
            resultsTree.BeginUpdate();
            resultsTree.Nodes.Clear();

            TreeNode preferredNode = null;
            if (currentReports.Count > 1)
            {
                var overviewText = FileInspector.BuildOverviewText(currentReports, currentReportElapsed);
                var overviewNode = new TreeNode("Report overview");
                overviewNode.Tag = overviewText;
                resultsTree.Nodes.Add(overviewNode);
                if (selection != null && selection.ReportOverview)
                    preferredNode = overviewNode;
            }

            foreach (var report in currentReports)
            {
                var root = new TreeNode(report.DisplayName);
                root.Name = report.OriginalPath ?? report.DisplayName;
                root.Tag = report.FullText;
                if (ReportMatchesSelection(report, selection) && string.IsNullOrWhiteSpace(selection.SectionTitle))
                    preferredNode = root;
                if (string.Equals(report.DisplayName, preferredFileName, StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(preferredSectionTitle))
                    preferredNode = root;
                foreach (var section in report.Sections)
                {
                    var sectionNode = new TreeNode(section.Title);
                    sectionNode.Tag = section.DetailText();
                    if (ReportMatchesSelection(report, selection) &&
                        string.Equals(section.Title, selection.SectionTitle, StringComparison.OrdinalIgnoreCase))
                        preferredNode = sectionNode;
                    if (string.Equals(report.DisplayName, preferredFileName, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(section.Title, preferredSectionTitle, StringComparison.OrdinalIgnoreCase))
                        preferredNode = sectionNode;
                    foreach (var item in section.Items)
                    {
                        var itemNode = new TreeNode(item.Title);
                        itemNode.Tag = item.Detail;
                        sectionNode.Nodes.Add(itemNode);
                    }
                    root.Nodes.Add(sectionNode);
                }
                resultsTree.Nodes.Add(root);
                root.Expand();
            }

            resultsTree.EndUpdate();
            reportText = EnsureTrailingBlankLine(FileInspector.BuildCombinedText(currentReports, currentReportElapsed));
            if (preferredNode != null)
                resultsTree.SelectedNode = preferredNode;
            else if (resultsTree.Nodes.Count > 0)
                resultsTree.SelectedNode = resultsTree.Nodes[0];
        }

        private static bool ReportMatchesSelection(FileReport report, SavedReportSelection selection)
        {
            if (report == null || selection == null || selection.ReportOverview)
                return false;
            if (!string.IsNullOrWhiteSpace(selection.OriginalPath) &&
                string.Equals(report.OriginalPath, selection.OriginalPath, StringComparison.OrdinalIgnoreCase))
                return true;
            return !string.IsNullOrWhiteSpace(selection.DisplayName) &&
                string.Equals(report.DisplayName, selection.DisplayName, StringComparison.OrdinalIgnoreCase);
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


        private void ShowProgressState(string title, string detail)
        {
            resultsTree.BeginUpdate();
            resultsTree.Nodes.Clear();
            var node = new TreeNode(title) { Tag = detail };
            resultsTree.Nodes.Add(node);
            resultsTree.SelectedNode = node;
            resultsTree.EndUpdate();
            detailsBox.Text = EnsureTrailingBlankLine(detail);
            ResetTextBoxToTop(detailsBox);
        }

        private void UpdateDetailsFromSelection()
        {
            var node = resultsTree.SelectedNode;
            detailsBox.Text = EnsureTrailingBlankLine(node == null || node.Tag == null ? string.Empty : node.Tag.ToString());
            ResetTextBoxToTop(detailsBox);
        }

        private void OpenAdvancedFileViewer()
        {
            var path = SelectedFilePath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                statusLabel.Text = "Choose a file in the tree before opening the advanced viewer.";
                return;
            }

            AdvancedFileViewerState state;
            if (!advancedViewerStates.TryGetValue(path, out state))
            {
                state = new AdvancedFileViewerState();
                advancedViewerStates[path] = state;
            }

            using (var dialog = new AdvancedFileViewerForm(path, state))
                dialog.ShowDialog(this);
        }

        private void PruneAdvancedViewerStates(IEnumerable<string> inputs)
        {
            var keep = new HashSet<string>(
                inputs.Select(p => Path.GetFullPath(p)),
                StringComparer.OrdinalIgnoreCase);
            var remove = advancedViewerStates.Keys.Where(path => !keep.Contains(path)).ToArray();
            foreach (var path in remove)
                advancedViewerStates.Remove(path);
        }

        private string SelectedFilePath()
        {
            var node = TopLevelNode(resultsTree.SelectedNode);
            if (node == null)
                return loadedFiles.Count == 1 ? loadedFiles[0] : string.Empty;
            if (!string.IsNullOrWhiteSpace(node.Name) && File.Exists(node.Name))
                return node.Name;
            return loadedFiles.FirstOrDefault(path => string.Equals(Path.GetFileName(path), node.Text, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
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

        private void CopyReport()
        {
            if (!string.IsNullOrEmpty(reportText))
            {
                Clipboard.SetText(reportText);
                statusLabel.Text = "Report copied to the clipboard.";
            }
        }

        private void CopyCurrentSelection()
        {
            var text = string.Empty;
            if (detailsBox.Focused && detailsBox.SelectionLength > 0)
                text = detailsBox.SelectedText;
            else if (resultsTree.Focused)
            {
                var node = resultsTree.SelectedNode;
                if (node != null && node.Tag != null)
                    text = node.Tag.ToString();
            }
            else if (detailsBox.Focused)
                text = detailsBox.Text.TrimEnd('\r', '\n');

            if (string.IsNullOrEmpty(text))
            {
                statusLabel.Text = "No selected details to copy.";
                return;
            }

            Clipboard.SetText(text);
            statusLabel.Text = "Selected details copied to the clipboard.";
        }

        private bool SaveReport()
        {
            if (string.IsNullOrEmpty(reportText))
            {
                statusLabel.Text = "No report is available to save.";
                return false;
            }
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Save FileDentify report";
                dialog.Filter = "FileDentify report (*.fdreport)|*.fdreport|Text report (*.txt)|*.txt|HTML report (*.html)|*.html;*.htm|All files (*.*)|*.*";
                dialog.FileName = "FileDentify report.fdreport";
                dialog.DefaultExt = "fdreport";
                dialog.AddExtension = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        if (SavedReportStore.IsSavedReportPath(dialog.FileName))
                        {
                            SavedReportStore.Save(dialog.FileName, currentReports, currentReportElapsed, CurrentSavedReportSelection());
                            currentSavedReportPath = Path.GetFullPath(dialog.FileName);
                            AddRecentReport(currentSavedReportPath);
                        }
                        else if (IsHtmlReportPath(dialog.FileName))
                            File.WriteAllText(dialog.FileName, FileInspector.BuildCombinedHtml(currentReports, "FileDentify report", currentReportElapsed), Encoding.UTF8);
                        else
                            File.WriteAllText(dialog.FileName, reportText, Encoding.UTF8);
                        statusLabel.Text = "Report saved.";
                        return true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, ex.Message, "Save report", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }
            }
            return false;
        }

        private void AutoSaveCurrentReport()
        {
            if (!settings.AutoSaveLastReport || currentReports.Count == 0)
                return;
            try
            {
                SavedReportStore.Save(SavedReportStore.AutoSavePath, currentReports, currentReportElapsed, CurrentSavedReportSelection());
                AddRecentReport(SavedReportStore.AutoSavePath);
            }
            catch
            {
            }
        }

        private SavedReportSelection CurrentSavedReportSelection()
        {
            var selected = resultsTree.SelectedNode;
            if (IsReportOverviewNode(selected))
                return new SavedReportSelection { ReportOverview = true };

            var fileNode = TopLevelNode(selected);
            if (fileNode == null || fileNode.Nodes.Count == 0)
                return null;

            var report = currentReports.FirstOrDefault(item =>
                string.Equals(item.OriginalPath, fileNode.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.DisplayName, fileNode.Text, StringComparison.OrdinalIgnoreCase));
            return new SavedReportSelection
            {
                OriginalPath = report == null ? fileNode.Name : report.OriginalPath,
                DisplayName = report == null ? fileNode.Text : report.DisplayName,
                SectionTitle = CurrentFileSectionName()
            };
        }

        private void ViewHtmlReport()
        {
            if (currentReports.Count == 0)
            {
                statusLabel.Text = "No report is available to view.";
                return;
            }

            try
            {
                var dir = Path.Combine(Path.GetTempPath(), "FileDentify-html-reports");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "FileDentify-report-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + ".html");
                File.WriteAllText(path, FileInspector.BuildCombinedHtml(currentReports, "FileDentify report", currentReportElapsed), Encoding.UTF8);
                temporaryHtmlReports.Add(path);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                statusLabel.Text = "HTML report opened in the default browser.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "View HTML report", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeleteTemporaryHtmlReports()
        {
            foreach (var path in temporaryHtmlReports.ToArray())
            {
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch
                {
                }
            }
        }

        private static bool IsHtmlReportPath(string path)
        {
            var extension = Path.GetExtension(path);
            return string.Equals(extension, ".html", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".htm", StringComparison.OrdinalIgnoreCase);
        }

        private void OpenContainingFolder()
        {
            var path = SelectedFilePath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                statusLabel.Text = "The selected file is not available on this machine.";
                return;
            }
            Process.Start("explorer.exe", "/select,\"" + path + "\"");
        }

        private void ShowPreferences()
        {
            using (var dialog = new PreferencesForm(settings))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    var oldSendTo = settings.SendToEnabled;
                    var oldDesktopShortcut = settings.DesktopShortcutEnabled;
                    var oldFileAssociation = settings.FileAssociationEnabled;
                    dialog.ApplyTo(settings);
                    settings.Save();
                    if (oldSendTo != settings.SendToEnabled)
                    {
                        try { SendToInstaller.SetInstalled(settings.SendToEnabled); }
                        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Send To menu", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    }
                    if (oldDesktopShortcut != settings.DesktopShortcutEnabled)
                    {
                        try { DesktopShortcutInstaller.SetInstalled(settings.DesktopShortcutEnabled); }
                        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Desktop shortcut", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    }
                    if (oldFileAssociation != settings.FileAssociationEnabled)
                    {
                        try { FileAssociationInstaller.SetInstalled(settings.FileAssociationEnabled); }
                        catch (Exception ex) { MessageBox.Show(this, ex.Message, "FileDentify report association", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    }
                    if (settings.AutoSaveLastReport)
                        AutoSaveCurrentReport();
                    ConfigureUpdateTimer();
                    statusLabel.Text = "Preferences saved.";
                }
            }
        }

        private void ConfigureUpdateTimer()
        {
            if (updateCheckTimer == null)
                return;
            updateCheckTimer.Stop();
            if (UpdateService.AutomaticUpdateInterval(settings.UpdateCheckFrequency).HasValue)
                updateCheckTimer.Start();
        }

        private void CheckAutomaticUpdateSchedule()
        {
            var normalized = UpdateService.NormalizeUpdateCheckFrequency(settings.UpdateCheckFrequency);
            if (normalized.Equals("Never", StringComparison.OrdinalIgnoreCase))
                return;
            var interval = normalized.Equals("Startup", StringComparison.OrdinalIgnoreCase) ? TimeSpan.Zero : UpdateService.AutomaticUpdateInterval(normalized);
            if (interval.HasValue && settings.LastAutomaticUpdateCheckUtc.HasValue && DateTime.UtcNow - settings.LastAutomaticUpdateCheckUtc.Value < interval.Value)
                return;
            settings.LastAutomaticUpdateCheckUtc = DateTime.UtcNow;
            settings.Save();
            CheckForUpdates(false);
        }

        private void ScheduleStartupUpdateCheck()
        {
            if (!UpdateService.NormalizeUpdateCheckFrequency(settings.UpdateCheckFrequency).Equals("Startup", StringComparison.OrdinalIgnoreCase))
                return;

            var timer = new System.Windows.Forms.Timer();
            timer.Interval = 5000;
            timer.Tick += delegate
            {
                timer.Stop();
                timer.Dispose();
                CheckAutomaticUpdateSchedule();
            };
            timer.Start();
        }

        private void CheckForUpdates(bool showCurrent)
        {
            try
            {
                var releases = UpdateService.FetchReleases(Program.ProjectUrl, Program.Version);
                var release = UpdateService.LatestVersionedRelease(releases) ?? UpdateService.FetchLatestRelease(Program.ProjectUrl, Program.Version);
                var latest = release == null ? string.Empty : (release.tag_name ?? string.Empty).Trim();
                var current = new Version(Program.Version);
                Version remote;
                if (!Version.TryParse(latest.TrimStart('v', 'V'), out remote) || remote <= current)
                {
                    if (showCurrent)
                        MessageBox.Show(this, "FileDentify is up to date.", "Check for Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (settings.InstallUpdatesQuietly && TryStartUpdate(release, false))
                    return;
                ShowUpdateAvailableDialog(release, latest, UpdateService.BuildReleaseNotes(releases, current, remote, Program.Version));
            }
            catch (Exception ex)
            {
                if (showCurrent)
                    MessageBox.Show(this, "Could not check for updates. GitHub releases may not exist yet, or the network request failed." + Environment.NewLine + Environment.NewLine + ex.Message, "Check for Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ShowUpdateAvailableDialog(GitHubReleaseInfo release, string latest, string releaseNotes)
        {
            var releaseUrl = release == null || string.IsNullOrWhiteSpace(release.html_url) ? Program.ProjectUrl + "/releases" : release.html_url;
            var zipAsset = UpdateService.FindPortableZipAsset(release);
            using (var dialog = new Form())
            {
                dialog.Text = "FileDentify update available";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Size = new Size(620, 430);
                dialog.MinimumSize = new Size(520, 340);
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                dialog.Controls.Add(layout);
                layout.Controls.Add(new Label { Text = "FileDentify " + latest + " is available.", AutoSize = true }, 0, 0);
                var notesBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Text = EnsureTrailingBlankLine(releaseNotes), AccessibleName = "Release notes" };
                notesBox.Enter += delegate { ResetTextBoxToTop(notesBox); };
                notesBox.KeyDown += TextBoxSelectAll_KeyDown;
                layout.Controls.Add(notesBox, 0, 1);
                var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
                var close = new ShortcutButton { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel, AccessibleName = "Close", AccessibleDescription = "Close this dialog.", ShortcutText = "ESC" };
                toolTip.SetToolTip(close, "ESC");
                buttons.Controls.Add(close);
                var releaseButton = new ShortcutButton { Text = "Open &release page", AutoSize = true, AccessibleName = "Open release page", AccessibleDescription = "Open the release page in the default browser.", ShortcutText = "Alt+R" };
                toolTip.SetToolTip(releaseButton, "Alt+R");
                releaseButton.Click += delegate { Process.Start(new ProcessStartInfo { FileName = releaseUrl, UseShellExecute = true }); };
                buttons.Controls.Add(releaseButton);
                if (zipAsset != null)
                {
                    var install = new ShortcutButton { Text = "&Download and install", AutoSize = true, AccessibleName = "Download and install", AccessibleDescription = "Download and install this update.", ShortcutText = "Alt+D" };
                    toolTip.SetToolTip(install, "Alt+D");
                    install.Click += delegate { if (TryStartUpdate(release, true)) dialog.Close(); };
                    buttons.Controls.Add(install);
                }
                layout.Controls.Add(buttons, 0, 2);
                dialog.CancelButton = close;
                dialog.Shown += delegate { ResetTextBoxToTop(notesBox); };
                dialog.ShowDialog(this);
            }
        }

        private void ShowVersionHistory()
        {
            try
            {
                var releases = UpdateService.FetchReleases(Program.ProjectUrl, Program.Version);
                var release = UpdateService.LatestVersionedRelease(releases) ?? UpdateService.FetchLatestRelease(Program.ProjectUrl, Program.Version);
                var version = release == null ? Program.Version : (release.tag_name ?? Program.Version).Trim().TrimStart('v', 'V');
                var notes = UpdateService.FormatReleaseNotesForDialog(release == null ? string.Empty : release.body, "No release notes were provided.");
                ShowTextDialog("Version History - " + version, "Latest release: " + version + Environment.NewLine + Environment.NewLine + notes);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not check updates. GitHub releases may not exist yet, or the network request failed." + Environment.NewLine + Environment.NewLine + ex.Message, "Version History", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ShowTextDialog(string title, string text)
        {
            using (var dialog = new Form())
            {
                dialog.Text = title;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Size = new Size(620, 430);
                var box = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Text = EnsureTrailingBlankLine(text) };
                box.Enter += delegate { ResetTextBoxToTop(box); };
                box.KeyDown += TextBoxSelectAll_KeyDown;
                dialog.Controls.Add(box);
                var close = new ShortcutButton { Text = "Close", DialogResult = DialogResult.Cancel, AccessibleName = "Close", AccessibleDescription = "Close this dialog.", ShortcutText = "ESC" };
                close.SetBounds(-100, -100, 1, 1);
                dialog.Controls.Add(close);
                dialog.CancelButton = close;
                dialog.Shown += delegate
                {
                    box.Focus();
                    BeginInvoke((MethodInvoker)delegate { ResetTextBoxToTop(box); });
                };
                dialog.ShowDialog(this);
            }
        }

        private bool TryStartUpdate(GitHubReleaseInfo release, bool showErrors)
        {
            var zipAsset = UpdateService.FindPortableZipAsset(release);
            if (zipAsset == null)
            {
                if (showErrors)
                    MessageBox.Show(this, "This GitHub release does not include a downloadable ZIP package. Please open the release page instead.", "Check for Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            try
            {
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var exePath = Application.ExecutablePath;
                var updaterTempDir = UpdateService.GetUpdaterTempDirectory(appDir);
                var scriptPath = Path.Combine(updaterTempDir, "FileDentifyUpdater-" + Guid.NewGuid().ToString("N") + ".ps1");
                File.WriteAllText(scriptPath, UpdateService.BuildUpdaterScript(zipAsset.browser_download_url, appDir, exePath, updaterTempDir, Process.GetCurrentProcess().Id, Program.Version), Encoding.UTF8);
                Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \"" + scriptPath + "\"") { UseShellExecute = false, CreateNoWindow = true });
                Close();
                return true;
            }
            catch (Exception ex)
            {
                if (showErrors)
                    MessageBox.Show(this, ex.Message, "Could not start updater", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void OpenProjectPage()
        {
            OpenUrl(Program.ProjectUrl);
        }

        private void OpenContactPage()
        {
            OpenUrl("https://onj.me/contact");
        }

        private void OpenDonatePage()
        {
            OpenUrl("https://onj.me/donate");
        }

        private void OpenOtherSoftwarePage()
        {
            OpenUrl("https://onj.me/software");
        }

        private static void OpenUrl(string url)
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }

        private void ShowHelp()
        {
            ManualService.OpenManual(this);
        }

        private void ShowAbout()
        {
            MessageBox.Show(this,
                "FileDentify " + Program.Version + " inspects selected files using bounded local reads, common binary signatures, text/header checks, ZIP container hints, embedded Unix file/libmagic, and bundled tools such as ffprobe when available.\r\n\r\nIt is designed as a SendTo-friendly, keyboard-first file identification utility.",
                "About FileDentify",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ShowThirdPartyNotices()
        {
            ShowThirdPartyNoticesDialog();
        }

        private void ShowThirdPartyNoticesDialog()
        {
            using (var dialog = new Form())
            {
                dialog.Text = "Third-party notices";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Size = new Size(760, 560);
                dialog.MinimumSize = new Size(520, 360);
                dialog.KeyPreview = true;

                var layout = new TableLayoutPanel();
                layout.Dock = DockStyle.Fill;
                layout.ColumnCount = 1;
                layout.RowCount = 3;
                layout.Padding = new Padding(8);
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                dialog.Controls.Add(layout);

                var notices = LibmagicProbe.NoticeEntries();
                var selector = new ComboBox();
                selector.DropDownStyle = ComboBoxStyle.DropDownList;
                selector.Dock = DockStyle.Top;
                selector.AccessibleName = "Notice";
                selector.AccessibleDescription = "Choose a third-party notice to review.";
                selector.Margin = new Padding(0, 0, 0, 8);
                selector.Items.AddRange(notices);
                layout.Controls.Add(selector, 0, 0);

                var noticeBox = new TextBox();
                noticeBox.Dock = DockStyle.Fill;
                noticeBox.Multiline = true;
                noticeBox.ReadOnly = true;
                noticeBox.ScrollBars = ScrollBars.Both;
                noticeBox.WordWrap = false;
                noticeBox.AccessibleName = "Notice text";
                noticeBox.AccessibleDescription = "Read-only text for the selected third-party notice.";
                noticeBox.KeyDown += TextBoxSelectAll_KeyDown;
                layout.Controls.Add(noticeBox, 0, 1);

                var close = new ShortcutButton();
                close.Text = "Close";
                close.AutoSize = true;
                close.Anchor = AnchorStyles.Right;
                close.DialogResult = DialogResult.Cancel;
                close.AccessibleName = "Close";
                close.AccessibleDescription = "Close this dialog.";
                close.ShortcutText = "ESC";
                toolTip.SetToolTip(close, "ESC");
                layout.Controls.Add(close, 0, 2);
                dialog.CancelButton = close;

                EventHandler updateNotice = delegate
                {
                    var selected = selector.SelectedItem as ThirdPartyNotice;
                    noticeBox.Text = EnsureTrailingBlankLine(selected == null ? string.Empty : selected.Text);
                    ResetTextBoxToTop(noticeBox);
                };
                selector.SelectedIndexChanged += updateNotice;

                dialog.KeyDown += delegate(object sender, KeyEventArgs e)
                {
                    if (e.KeyCode == Keys.Escape)
                    {
                        dialog.DialogResult = DialogResult.Cancel;
                        dialog.Close();
                        e.Handled = true;
                    }
                };

                dialog.Shown += delegate
                {
                    if (selector.Items.Count > 0)
                        selector.SelectedIndex = 0;
                    selector.Focus();
                };

                dialog.ShowDialog(this);
            }
        }

        private void SetBusy(bool busy, string status)
        {
            UseWaitCursor = busy;
            addFilesButton.Enabled = !busy;
            copyButton.Enabled = !busy;
            saveButton.Enabled = !busy;
            viewHtmlButton.Enabled = !busy;
            openFolderButton.Enabled = !busy;
            closeButton.Enabled = true;
            statusLabel.Text = status;
        }

        private static string FormatElapsed(TimeSpan elapsed)
        {
            return elapsed.TotalSeconds < 60
                ? elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " seconds"
                : elapsed.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
        }
    }

}

