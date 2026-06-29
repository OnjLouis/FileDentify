using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace FileDentify
{
    internal sealed class AdvancedFileViewerForm : Form
    {
        private const int ChunkSize = 4 * 1024 * 1024;

        private readonly string path;
        private readonly RadioButton readableRadio;
        private readonly RadioButton hexRadio;
        private readonly RadioButton binaryRadio;
        private readonly RadioButton octalRadio;
        private readonly TextBox searchBox;
        private readonly TextBox viewerBox;
        private readonly Button loadMoreButton;
        private readonly Button loadAllButton;
        private readonly StatusBar statusBar;
        private readonly AdvancedFileViewerState state;
        private readonly ToolTip toolTip;
        private long offset;
        private long fileLength;
        private AdvancedViewMode mode;

        public AdvancedFileViewerForm(string path)
            : this(path, null)
        {
        }

        public AdvancedFileViewerForm(string path, AdvancedFileViewerState state)
        {
            this.path = path;
            this.state = state ?? new AdvancedFileViewerState();
            fileLength = new FileInfo(path).Length;
            mode = this.state.Mode;
            Text = "Advanced viewer - " + Path.GetFileName(path);
            StartPosition = FormStartPosition.CenterParent;
            Width = 960;
            Height = 720;
            MinimumSize = new Size(760, 500);
            WindowState = FormWindowState.Maximized;
            ShowInTaskbar = false;
            KeyPreview = true;
            toolTip = new ToolTip();

            var menu = new MenuStrip();
            var fileMenu = new ToolStripMenuItem("&File");
            fileMenu.DropDownItems.Add(CreateShortcutMenuItem("&Save loaded output...", "Ctrl+S", delegate { SaveLoadedOutput(); }));
            fileMenu.DropDownItems.Add(CreateShortcutMenuItem("&Load more", "Ctrl+L", delegate { LoadMore(); }));
            fileMenu.DropDownItems.Add(CreateShortcutMenuItem("Load &all", "Ctrl+Shift+L", delegate { LoadAll(); }));
            fileMenu.DropDownItems.Add(CreateShortcutMenuItem("E&xit viewer", "Esc", delegate { Close(); }));
            var editMenu = new ToolStripMenuItem("&Edit");
            editMenu.DropDownItems.Add(CreateShortcutMenuItem("&Copy", "Ctrl+C", delegate { viewerBox.Copy(); }));
            editMenu.DropDownItems.Add(CreateShortcutMenuItem("Select &all", "Ctrl+A", delegate { viewerBox.SelectAll(); }));
            var viewMenu = new ToolStripMenuItem("&View");
            viewMenu.DropDownItems.Add(CreateShortcutMenuItem("&Text", "Alt+T", delegate { SelectMode(AdvancedViewMode.ReadableText, true); }));
            viewMenu.DropDownItems.Add(CreateShortcutMenuItem("He&x", "Alt+X", delegate { SelectMode(AdvancedViewMode.Hex, true); }));
            viewMenu.DropDownItems.Add(CreateShortcutMenuItem("&Binary", "Alt+B", delegate { SelectMode(AdvancedViewMode.Binary, true); }));
            viewMenu.DropDownItems.Add(CreateShortcutMenuItem("&Octal", "Alt+O", delegate { SelectMode(AdvancedViewMode.Octal, true); }));
            var searchMenu = new ToolStripMenuItem("&Search");
            searchMenu.DropDownItems.Add(CreateShortcutMenuItem("&Find...", "Ctrl+F", delegate { FocusSearchBox(); }));
            searchMenu.DropDownItems.Add(CreateShortcutMenuItem("Find &next", "F3", delegate { FindNext(); }));
            searchMenu.DropDownItems.Add(CreateShortcutMenuItem("Find &previous", "Shift+F3", delegate { FindPrevious(); }));
            menu.Items.Add(fileMenu);
            menu.Items.Add(editMenu);
            menu.Items.Add(viewMenu);
            menu.Items.Add(searchMenu);
            MainMenuStrip = menu;
            Controls.Add(menu);

            var layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 2;
            layout.Padding = new Padding(10);
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(layout);
            layout.BringToFront();

            var controls = new FlowLayoutPanel();
            controls.Dock = DockStyle.Fill;
            controls.AutoSize = true;
            controls.WrapContents = true;
            controls.TabStop = false;
            layout.Controls.Add(controls, 0, 0);

            controls.Controls.Add(new Label { Text = "Mode:", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
            readableRadio = CreateModeRadio("&Text", "Text mode", "Alt+T", AdvancedViewMode.ReadableText, mode == AdvancedViewMode.ReadableText);
            hexRadio = CreateModeRadio("He&x", "Hex mode", "Alt+X", AdvancedViewMode.Hex, mode == AdvancedViewMode.Hex);
            binaryRadio = CreateModeRadio("&Binary", "Binary mode", "Alt+B", AdvancedViewMode.Binary, mode == AdvancedViewMode.Binary);
            octalRadio = CreateModeRadio("&Octal", "Octal mode", "Alt+O", AdvancedViewMode.Octal, mode == AdvancedViewMode.Octal);
            controls.Controls.Add(readableRadio);
            controls.Controls.Add(hexRadio);
            controls.Controls.Add(binaryRadio);
            controls.Controls.Add(octalRadio);

            controls.Controls.Add(new Label { Text = "Search:", AutoSize = true, Margin = new Padding(12, 6, 4, 0) });
            searchBox = new TextBox { Width = 220, AccessibleName = "Search text" };
            searchBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    FindNext();
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                }
            };
            controls.Controls.Add(searchBox);

            var findButton = CreateButton("Find next", "Find next", "F3", "Search loaded viewer output.");
            findButton.Click += delegate { FindNext(); };
            controls.Controls.Add(findButton);

            loadMoreButton = CreateButton("&Load more", "Load more", "Ctrl+L", "Load more content from the file.");
            loadMoreButton.Click += delegate { LoadMore(); };
            controls.Controls.Add(loadMoreButton);

            loadAllButton = CreateButton("Load &all", "Load all", "Ctrl+Shift+L", "Load all remaining content from the file.");
            loadAllButton.Click += delegate { LoadAll(); };
            controls.Controls.Add(loadAllButton);

            viewerBox = new TextBox();
            viewerBox.Dock = DockStyle.Fill;
            viewerBox.Multiline = true;
            viewerBox.ReadOnly = true;
            viewerBox.WordWrap = false;
            viewerBox.ScrollBars = ScrollBars.Both;
            viewerBox.Font = new Font("Consolas", 10);
            viewerBox.AccessibleName = "Advanced file view";
            viewerBox.KeyDown += ViewerBox_KeyDown;
            viewerBox.KeyUp += delegate { UpdateStatus("Ready."); };
            viewerBox.MouseUp += delegate { UpdateStatus("Ready."); };
            viewerBox.MouseWheel += delegate { MaybeAutoLoadMore(); };
            layout.Controls.Add(viewerBox, 0, 1);

            statusBar = new StatusBar();
            statusBar.Dock = DockStyle.Fill;
            statusBar.SizingGrip = false;
            statusBar.ShowPanels = false;
            statusBar.AccessibleRole = AccessibleRole.StatusBar;
            statusBar.Height = 22;
            Controls.Add(statusBar);
            statusBar.BringToFront();

            KeyDown += AdvancedFileViewerForm_KeyDown;
            Shown += delegate
            {
                RestoreInitialView();
            };
            FormClosing += delegate { SaveViewerState(); };
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Alt | Keys.T))
            {
                SelectMode(AdvancedViewMode.ReadableText, true);
                return true;
            }
            if (keyData == (Keys.Alt | Keys.X))
            {
                SelectMode(AdvancedViewMode.Hex, true);
                return true;
            }
            if (keyData == (Keys.Alt | Keys.B))
            {
                SelectMode(AdvancedViewMode.Binary, true);
                return true;
            }
            if (keyData == (Keys.Alt | Keys.O))
            {
                SelectMode(AdvancedViewMode.Octal, true);
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private RadioButton CreateModeRadio(string text, string accessibleName, string shortcutText, AdvancedViewMode radioMode, bool isChecked)
        {
            var radio = new RadioButton();
            radio.Text = text;
            radio.AccessibleName = accessibleName;
            radio.AccessibleDescription = shortcutText;
            radio.AutoSize = true;
            radio.Margin = new Padding(4, 4, 4, 0);
            radio.Checked = isChecked;
            toolTip.SetToolTip(radio, shortcutText);
            radio.CheckedChanged += delegate
            {
                if (radio.Checked)
                    ChangeMode(radioMode);
            };
            return radio;
        }

        private Button CreateButton(string text, string accessibleName, string shortcutText, string accessibleDescription)
        {
            var button = new ShortcutButton();
            button.Text = text;
            button.AutoSize = true;
            button.AccessibleName = accessibleName;
            button.AccessibleDescription = accessibleDescription;
            button.ShortcutText = shortcutText;
            if (!string.IsNullOrWhiteSpace(shortcutText))
                toolTip.SetToolTip(button, shortcutText);
            return button;
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

        private void AdvancedFileViewerForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F3)
            {
                if (e.Shift)
                    FindPrevious();
                else
                    FindNext();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.F)
            {
                FocusSearchBox();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.L)
            {
                if (e.Shift)
                    LoadAll();
                else
                    LoadMore();
                e.Handled = true;
            }
            else if (e.Alt && e.KeyCode == Keys.T)
            {
                SelectMode(AdvancedViewMode.ReadableText, true);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.Alt && e.KeyCode == Keys.X)
            {
                SelectMode(AdvancedViewMode.Hex, true);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.Alt && e.KeyCode == Keys.B)
            {
                SelectMode(AdvancedViewMode.Binary, true);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.Alt && e.KeyCode == Keys.O)
            {
                SelectMode(AdvancedViewMode.Octal, true);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void ViewerBox_KeyDown(object sender, KeyEventArgs e)
        {
            TextBoxSelectAll_KeyDown(sender, e);
            if (e.Handled)
                return;

            if (!e.Control && !e.Alt && !e.Shift &&
                (e.KeyCode == Keys.Down || e.KeyCode == Keys.PageDown) &&
                IsViewerAtLoadedEnd())
            {
                var appendStart = LoadMore(true, null);
                if (appendStart >= 0)
                    SetViewerSelection(appendStart);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void MaybeAutoLoadMore()
        {
            if (offset >= fileLength || viewerBox.TextLength == 0)
                return;
            if (IsViewerAtLoadedEnd())
                LoadMore(true, null);
        }

        private bool IsViewerAtLoadedEnd()
        {
            if (viewerBox.TextLength == 0)
                return false;
            return viewerBox.SelectionStart >= viewerBox.TextLength - 1 ||
                viewerBox.GetLineFromCharIndex(viewerBox.SelectionStart) >= viewerBox.Lines.Length - 1;
        }

        private void ChangeMode(AdvancedViewMode newMode)
        {
            mode = newMode;
            offset = 0;
            viewerBox.Clear();
            LoadMore(false, 0);
            BeginInvoke(new MethodInvoker(ResetViewerToTop));
        }

        private void SelectMode(AdvancedViewMode newMode, bool focusViewer)
        {
            if (mode == newMode)
            {
                if (focusViewer)
                    viewerBox.Focus();
                UpdateStatus("Ready.");
                return;
            }

            switch (newMode)
            {
                case AdvancedViewMode.ReadableText:
                    readableRadio.Checked = true;
                    break;
                case AdvancedViewMode.Hex:
                    hexRadio.Checked = true;
                    break;
                case AdvancedViewMode.Binary:
                    binaryRadio.Checked = true;
                    break;
                case AdvancedViewMode.Octal:
                    octalRadio.Checked = true;
                    break;
            }

            if (focusViewer)
                BeginInvoke(new MethodInvoker(delegate { viewerBox.Focus(); }));
        }

        private void LoadMore()
        {
            LoadMore(true, null);
        }

        private int LoadMore(bool preserveCaret, int? finalSelectionStart)
        {
            if (offset >= fileLength)
            {
                UpdateStatus("End of file reached.");
                return -1;
            }

            var oldSelectionStart = viewerBox.SelectionStart;
            var oldSelectionLength = viewerBox.SelectionLength;
            byte[] data;
            var start = offset;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                fs.Position = offset;
                var count = (int)Math.Min(ChunkSize, fs.Length - offset);
                data = new byte[count];
                var read = fs.Read(data, 0, count);
                if (read != count)
                    Array.Resize(ref data, read);
            }
            offset += data.Length;

            var text = RenderChunk(data, start);
            var appendStart = viewerBox.TextLength;
            if (viewerBox.TextLength > 0 && text.Length > 0)
            {
                viewerBox.AppendText(Environment.NewLine);
                appendStart = viewerBox.TextLength;
            }
            viewerBox.AppendText(text);
            if (finalSelectionStart.HasValue)
                SetViewerSelection(finalSelectionStart.Value);
            else if (preserveCaret)
            {
                viewerBox.SelectionStart = Math.Min(oldSelectionStart, viewerBox.TextLength);
                viewerBox.SelectionLength = Math.Min(oldSelectionLength, Math.Max(0, viewerBox.TextLength - viewerBox.SelectionStart));
            }
            UpdateStatus("Loaded " + FormatBytes(offset) + " of " + FormatBytes(fileLength) + ". Lines: " + viewerBox.Lines.Length.ToString(CultureInfo.InvariantCulture) + ".");
            loadMoreButton.Enabled = offset < fileLength;
            loadAllButton.Enabled = offset < fileLength;
            return appendStart;
        }

        private void LoadAll()
        {
            if (offset >= fileLength)
            {
                UpdateStatus("End of file reached.");
                return;
            }

            var remaining = fileLength - offset;
            if (remaining > 128L * 1024L * 1024L)
            {
                var result = MessageBox.Show(
                    this,
                    "This will load " + FormatBytes(remaining) + " more source data into the viewer and may create a very large text box. Continue?",
                    "Load all",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                if (result != DialogResult.Yes)
                    return;
            }

            UseWaitCursor = true;
            loadMoreButton.Enabled = false;
            loadAllButton.Enabled = false;
            try
            {
                while (offset < fileLength)
                {
                    LoadMore();
                    Application.DoEvents();
                    if (IsDisposed)
                        break;
                }
            }
            finally
            {
                UseWaitCursor = false;
                if (!IsDisposed)
                {
                    loadMoreButton.Enabled = offset < fileLength;
                    loadAllButton.Enabled = offset < fileLength;
                }
            }
        }

        private void ResetViewerToTop()
        {
            viewerBox.Focus();
            SetViewerSelection(0);
        }

        private void SetViewerSelection(int selectionStart)
        {
            viewerBox.Focus();
            viewerBox.SelectionStart = Math.Max(0, Math.Min(selectionStart, viewerBox.TextLength));
            viewerBox.SelectionLength = 0;
            viewerBox.ScrollToCaret();
        }

        private void RestoreInitialView()
        {
            var targetOffset = Math.Min(Math.Max(state.Offset, ChunkSize), fileLength);
            if (fileLength == 0)
                targetOffset = 0;
            while (offset < targetOffset)
            {
                LoadMore(true, null);
                if (offset >= fileLength || IsDisposed)
                    break;
            }

            BeginInvoke(new MethodInvoker(delegate
            {
                if (state.SelectionStart > 0)
                    SetViewerSelection(state.SelectionStart);
                else
                    ResetViewerToTop();
                if (!string.IsNullOrEmpty(state.SearchText))
                    searchBox.Text = state.SearchText;
                UpdateStatus("Ready.");
            }));
        }

        private void SaveViewerState()
        {
            state.Mode = mode;
            state.Offset = offset;
            state.SelectionStart = viewerBox.SelectionStart;
            state.SearchText = searchBox.Text;
        }

        private string RenderChunk(byte[] data, long start)
        {
            return AdvancedFileViewRenderer.RenderChunk(data, start, mode);
        }

        private void FindNext()
        {
            var needle = searchBox.Text;
            if (string.IsNullOrEmpty(needle))
            {
                SetStatusText("Enter text to search for.");
                searchBox.Focus();
                return;
            }
            var text = viewerBox.Text;
            if (string.IsNullOrEmpty(text))
            {
                SetStatusText("No loaded content to search.");
                viewerBox.Focus();
                return;
            }

            var start = viewerBox.SelectionStart + Math.Max(viewerBox.SelectionLength, 1);
            if (start < 0)
                start = 0;
            if (start >= text.Length)
                start = text.Length;
            var index = text.IndexOf(needle, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                SetStatusText("No further match in loaded content. Press Shift+F3 to search backward, or load more and press F3.");
                viewerBox.Focus();
                return;
            }
            viewerBox.Focus();
            viewerBox.SelectionStart = index;
            viewerBox.SelectionLength = needle.Length;
            viewerBox.ScrollToCaret();
            UpdateStatus("Found search text.");
        }

        private void FindPrevious()
        {
            var needle = searchBox.Text;
            if (string.IsNullOrEmpty(needle))
            {
                SetStatusText("Enter text to search for.");
                searchBox.Focus();
                return;
            }

            var text = viewerBox.Text;
            if (string.IsNullOrEmpty(text))
            {
                SetStatusText("No loaded content to search.");
                viewerBox.Focus();
                return;
            }

            var start = viewerBox.SelectionStart - 1;
            if (start >= text.Length)
                start = text.Length - 1;
            if (start < 0)
            {
                SetStatusText("No previous match in loaded content. Press F3 to search forward.");
                viewerBox.Focus();
                return;
            }
            var index = text.LastIndexOf(needle, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                SetStatusText("No previous match in loaded content. Press F3 to search forward.");
                viewerBox.Focus();
                return;
            }
            viewerBox.Focus();
            viewerBox.SelectionStart = index;
            viewerBox.SelectionLength = needle.Length;
            viewerBox.ScrollToCaret();
            UpdateStatus("Found previous search text.");
        }

        private void FocusSearchBox()
        {
            searchBox.Focus();
            searchBox.SelectAll();
        }

        private void SaveLoadedOutput()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Save loaded viewer output";
                dialog.Filter = "Text file (*.txt)|*.txt|All files (*.*)|*.*";
                dialog.FileName = Path.GetFileName(path) + "-" + AdvancedFileViewRenderer.ModeName(mode).Replace(" ", "-") + ".txt";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    File.WriteAllText(dialog.FileName, viewerBox.Text, Encoding.UTF8);
                    SetStatusText("Loaded output saved.");
                }
            }
        }

        private void UpdateStatus(string prefix)
        {
            var more = offset < fileLength ? " More data is available; use Ctrl+L, Load all, or keep scrolling near the bottom." : string.Empty;
            SetStatusText("Line " + CurrentLineNumber().ToString(CultureInfo.InvariantCulture) + " of " + viewerBox.Lines.Length.ToString(CultureInfo.InvariantCulture) + ". " + prefix + " Mode " + AdvancedFileViewRenderer.ModeName(mode) + ". Keys: Alt+T text, Alt+X hex, Alt+B binary, Alt+O octal." + more);
        }

        private void SetStatusText(string text)
        {
            statusBar.Text = text;
            statusBar.AccessibleName = text;
            statusBar.AccessibleDescription = text;
        }

        private int CurrentLineNumber()
        {
            return viewerBox.GetLineFromCharIndex(viewerBox.SelectionStart) + 1;
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

        private static string FormatBytes(long bytes)
        {
            string[] units = { "bytes", "KiB", "MiB", "GiB", "TiB" };
            double value = bytes;
            var unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }
            return unit == 0 ? bytes.ToString(CultureInfo.InvariantCulture) + " bytes" : value.ToString("0.##", CultureInfo.InvariantCulture) + " " + units[unit];
        }

    }

    internal sealed class AdvancedFileViewerState
    {
        public AdvancedViewMode Mode { get; set; }
        public long Offset { get; set; }
        public int SelectionStart { get; set; }
        public string SearchText { get; set; }
    }
}
