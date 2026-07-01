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
{    internal sealed partial class MainForm : Form, IMessageFilter
    {
        private readonly TreeView resultsTree;
        private readonly TextBox detailsBox;
        private readonly WebBrowser detailsBrowser;
        private readonly Button enterReportButton;
        private readonly Button copyButton;
        private readonly Button saveButton;
        private readonly Button viewHtmlButton;
        private readonly Button openFolderButton;
        private readonly Button addFilesButton;
        private readonly Button closeButton;
        private readonly Label statusLabel;
        private readonly ToolTip toolTip;
        private readonly ToolStripMenuItem recentReportsMenu;
        private readonly ToolStripMenuItem htmlDetailsMenuItem;
        private readonly System.Windows.Forms.Timer updateCheckTimer;
        private readonly System.Windows.Forms.Timer treeShortcutAnnouncementTimer;
        private readonly SingleInstanceService singleInstanceService;
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
        private bool htmlDetailsWantsFocus;

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
            fileMenu.DropDownItems.Add(CreateShortcutMenuItem("&New report", "Ctrl+N", delegate { NewReport(); }));
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
            editMenu.DropDownItems.Add(CreateShortcutMenuItem("Report overvie&w", "Alt+Backspace", delegate { SelectReportOverview(); }));
            editMenu.DropDownItems.Add(CreateShortcutMenuItem("&Previous file", "Alt+Left", delegate { SelectAdjacentFile(-1); }));
            editMenu.DropDownItems.Add(CreateShortcutMenuItem("&Next file", "Alt+Right", delegate { SelectAdjacentFile(1); }));
            editMenu.DropDownItems.Add(CreateShortcutMenuItem("F&irst file", "Alt+PageUp", delegate { SelectFileByPosition(false); }));
            editMenu.DropDownItems.Add(CreateShortcutMenuItem("L&ast file", "Alt+PageDown", delegate { SelectFileByPosition(true); }));
            editMenu.DropDownItems.Add(CreateShortcutMenuItem("Previous &section", "Alt+Up", delegate { SelectAdjacentSection(-1); }));
            editMenu.DropDownItems.Add(CreateShortcutMenuItem("Ne&xt section", "Alt+Down", delegate { SelectAdjacentSection(1); }));
            editMenu.DropDownItems.Add(CreateShortcutMenuItem("Fi&rst section", "Alt+Home", delegate { SelectSectionByPosition(false); }));
            editMenu.DropDownItems.Add(CreateShortcutMenuItem("Las&t section", "Alt+End", delegate { SelectSectionByPosition(true); }));
            var viewMenu = new ToolStripMenuItem("&View");
            htmlDetailsMenuItem = CreateShortcutMenuItem("&HTML details view", "F7", delegate { ToggleHtmlDetailsView(); });
            htmlDetailsMenuItem.CheckOnClick = false;
            htmlDetailsMenuItem.Checked = settings.HtmlDetailsView;
            viewMenu.DropDownItems.Add(htmlDetailsMenuItem);
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
            menu.Items.Add(viewMenu);
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

            detailsBrowser = new WebBrowser();
            detailsBrowser.Dock = DockStyle.Fill;
            detailsBrowser.AllowWebBrowserDrop = false;
            detailsBrowser.IsWebBrowserContextMenuEnabled = true;
            detailsBrowser.WebBrowserShortcutsEnabled = true;
            detailsBrowser.ScriptErrorsSuppressed = true;
            detailsBrowser.AccessibleName = "Selected item HTML details";
            detailsBrowser.TabStop = false;
            detailsBrowser.PreviewKeyDown += DetailsBrowser_PreviewKeyDown;
            detailsBrowser.DocumentCompleted += delegate
            {
                if (CanRestoreHtmlDetailsFocus())
                    BeginInvoke((MethodInvoker)FocusHtmlDetailsDocument);
            };
            detailsBrowser.Visible = settings.HtmlDetailsView;
            split.Panel2.Controls.Add(detailsBrowser);
            detailsBrowser.BringToFront();
            detailsBox.Visible = !settings.HtmlDetailsView;
            if (!settings.HtmlDetailsView)
                detailsBox.BringToFront();

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.AutoSize = true;
            buttons.FlowDirection = FlowDirection.LeftToRight;
            buttons.WrapContents = true;
            buttons.TabStop = false;
            main.Controls.Add(buttons, 0, 1);

            addFilesButton = CreateButton("Open files...", delegate { AddFiles(false); }, "Open files", "Ctrl+O", "Choose one or more files to inspect.");
            var openInputFolderButton = CreateButton("Open folder...", delegate { OpenFolder(false); }, "Open folder", "Ctrl+Shift+L", "Choose a folder and recursively inspect its files.");
            enterReportButton = CreateButton("&Enter report", delegate { ToggleHtmlDetailsFocus(); }, "Enter report", "F6", "Enter the HTML details view. Press F6 again to return to the tree.");
            enterReportButton.Visible = settings.HtmlDetailsView;
            copyButton = CreateButton("&Copy report", delegate { CopyReport(); }, "Copy report", "Alt+C", "Copy the current report text.");
            saveButton = CreateButton("Save report...", delegate { SaveReport(); }, "Save report", "Ctrl+S", "Save the current report as text or HTML.");
            viewHtmlButton = CreateButton("&View HTML report", delegate { ViewHtmlReport(); }, "View HTML report", "Alt+V", "Open a temporary HTML version of the current report in the default browser.");
            openFolderButton = CreateButton("Open containing fo&lder", delegate { OpenContainingFolder(); }, "Open containing folder", "Alt+L", "Open the selected file's folder in File Explorer.");
            buttons.Controls.Add(enterReportButton);
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
            singleInstanceService = SingleInstanceService.Start(this, AppendFilesFromAnotherInstance);
            Shown += delegate { ScheduleStartupUpdateCheck(); };
            FormClosing += delegate
            {
                if (settings.AutoSaveLastReport && currentReports.Count > 0)
                    AutoSaveCurrentReport();
            };
            Application.AddMessageFilter(this);
            FormClosed += delegate
            {
                Application.RemoveMessageFilter(this);
                singleInstanceService.Dispose();
                DeleteTemporaryHtmlReports();
            };
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
    }

}

