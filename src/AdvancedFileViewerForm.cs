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
        private readonly Label statusLabel;
        private long offset;
        private long fileLength;
        private AdvancedViewMode mode;

        public AdvancedFileViewerForm(string path)
        {
            this.path = path;
            fileLength = new FileInfo(path).Length;
            Text = "Advanced viewer - " + Path.GetFileName(path);
            StartPosition = FormStartPosition.CenterParent;
            Width = 960;
            Height = 720;
            MinimumSize = new Size(760, 500);
            KeyPreview = true;

            var menu = new MenuStrip();
            var fileMenu = new ToolStripMenuItem("&File");
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("&Save loaded output...", null, delegate { SaveLoadedOutput(); }) { ShortcutKeys = Keys.Control | Keys.S });
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("&Load more", null, delegate { LoadMore(); }) { ShortcutKeys = Keys.Control | Keys.L });
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Load &all", null, delegate { LoadAll(); }) { ShortcutKeys = Keys.Control | Keys.Shift | Keys.L });
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("E&xit viewer", null, delegate { Close(); }) { ShortcutKeyDisplayString = "Esc" });
            var editMenu = new ToolStripMenuItem("&Edit");
            editMenu.DropDownItems.Add(new ToolStripMenuItem("&Copy", null, delegate { viewerBox.Copy(); }) { ShortcutKeys = Keys.Control | Keys.C });
            editMenu.DropDownItems.Add(new ToolStripMenuItem("Select &all", null, delegate { viewerBox.SelectAll(); }) { ShortcutKeys = Keys.Control | Keys.A });
            var viewMenu = new ToolStripMenuItem("&View");
            viewMenu.DropDownItems.Add(new ToolStripMenuItem("&Text", null, delegate { readableRadio.Checked = true; }));
            viewMenu.DropDownItems.Add(new ToolStripMenuItem("He&x", null, delegate { hexRadio.Checked = true; }));
            viewMenu.DropDownItems.Add(new ToolStripMenuItem("&Binary", null, delegate { binaryRadio.Checked = true; }));
            viewMenu.DropDownItems.Add(new ToolStripMenuItem("&Octal", null, delegate { octalRadio.Checked = true; }));
            var searchMenu = new ToolStripMenuItem("&Search");
            searchMenu.DropDownItems.Add(new ToolStripMenuItem("Find next", null, delegate { FindNext(); }) { ShortcutKeys = Keys.F3 });
            menu.Items.Add(fileMenu);
            menu.Items.Add(editMenu);
            menu.Items.Add(viewMenu);
            menu.Items.Add(searchMenu);
            MainMenuStrip = menu;
            Controls.Add(menu);

            var layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 3;
            layout.Padding = new Padding(10);
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(layout);
            layout.BringToFront();

            var controls = new FlowLayoutPanel();
            controls.Dock = DockStyle.Fill;
            controls.AutoSize = true;
            controls.WrapContents = true;
            controls.TabStop = false;
            layout.Controls.Add(controls, 0, 0);

            controls.Controls.Add(new Label { Text = "Mode:", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
            readableRadio = CreateModeRadio("&Text", "Text mode, Alt+T", AdvancedViewMode.ReadableText, true);
            hexRadio = CreateModeRadio("He&x", "Hex mode, Alt+X", AdvancedViewMode.Hex, false);
            binaryRadio = CreateModeRadio("&Binary", "Binary mode, Alt+B", AdvancedViewMode.Binary, false);
            octalRadio = CreateModeRadio("&Octal", "Octal mode, Alt+O", AdvancedViewMode.Octal, false);
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

            var findButton = new Button { Text = "Find next", AutoSize = true, AccessibleName = "Find next, F3", AccessibleDescription = "Searches loaded viewer output. Shortcut F3." };
            findButton.Click += delegate { FindNext(); };
            controls.Controls.Add(findButton);

            loadMoreButton = new Button { Text = "&Load more", AutoSize = true, AccessibleName = "Load more" };
            loadMoreButton.Click += delegate { LoadMore(); };
            controls.Controls.Add(loadMoreButton);

            loadAllButton = new Button { Text = "Load &all", AutoSize = true, AccessibleName = "Load all" };
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
            viewerBox.KeyDown += TextBoxSelectAll_KeyDown;
            viewerBox.KeyUp += ViewerBox_KeyUp;
            viewerBox.MouseWheel += delegate { MaybeAutoLoadMore(); };
            layout.Controls.Add(viewerBox, 0, 1);

            statusLabel = new Label();
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.AutoSize = true;
            statusLabel.AccessibleName = "Status";
            layout.Controls.Add(statusLabel, 0, 2);

            KeyDown += AdvancedFileViewerForm_KeyDown;
            Shown += delegate
            {
                LoadMore();
                BeginInvoke(new MethodInvoker(ResetViewerToTop));
            };
        }

        private RadioButton CreateModeRadio(string text, string accessibleName, AdvancedViewMode radioMode, bool isChecked)
        {
            var radio = new RadioButton();
            radio.Text = text;
            radio.AccessibleName = accessibleName;
            radio.AutoSize = true;
            radio.Margin = new Padding(4, 4, 4, 0);
            radio.Checked = isChecked;
            radio.CheckedChanged += delegate
            {
                if (radio.Checked)
                    ChangeMode(radioMode);
            };
            return radio;
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
                FindNext();
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
                readableRadio.Checked = true;
                e.Handled = true;
            }
            else if (e.Alt && e.KeyCode == Keys.X)
            {
                hexRadio.Checked = true;
                e.Handled = true;
            }
            else if (e.Alt && e.KeyCode == Keys.B)
            {
                binaryRadio.Checked = true;
                e.Handled = true;
            }
            else if (e.Alt && e.KeyCode == Keys.O)
            {
                octalRadio.Checked = true;
                e.Handled = true;
            }
        }

        private void ViewerBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.PageDown || e.KeyCode == Keys.End)
                MaybeAutoLoadMore();
        }

        private void MaybeAutoLoadMore()
        {
            if (offset >= fileLength || viewerBox.TextLength == 0)
                return;
            if (viewerBox.SelectionStart >= Math.Max(0, viewerBox.TextLength - 2000))
                LoadMore();
        }

        private void ChangeMode(AdvancedViewMode newMode)
        {
            mode = newMode;
            offset = 0;
            viewerBox.Clear();
            LoadMore();
            BeginInvoke(new MethodInvoker(ResetViewerToTop));
        }

        private void LoadMore()
        {
            if (offset >= fileLength)
            {
                UpdateStatus("End of file reached.");
                return;
            }

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
            if (viewerBox.TextLength > 0 && text.Length > 0)
                viewerBox.AppendText(Environment.NewLine);
            viewerBox.AppendText(text);
            UpdateStatus("Loaded " + FormatBytes(offset) + " of " + FormatBytes(fileLength) + ". Lines: " + viewerBox.Lines.Length.ToString(CultureInfo.InvariantCulture) + ".");
            loadMoreButton.Enabled = offset < fileLength;
            loadAllButton.Enabled = offset < fileLength;
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
            viewerBox.SelectionStart = 0;
            viewerBox.SelectionLength = 0;
            viewerBox.ScrollToCaret();
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
                statusLabel.Text = "Enter text to search for.";
                searchBox.Focus();
                return;
            }
            var start = viewerBox.SelectionStart + Math.Max(viewerBox.SelectionLength, 1);
            var index = viewerBox.Text.IndexOf(needle, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0 && start > 0)
                index = viewerBox.Text.IndexOf(needle, 0, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                statusLabel.Text = "Search text not found in loaded content. Load more and press F3 to continue.";
                viewerBox.Focus();
                return;
            }
            viewerBox.Focus();
            viewerBox.SelectionStart = index;
            viewerBox.SelectionLength = needle.Length;
            viewerBox.ScrollToCaret();
            UpdateStatus("Found search text.");
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
                    statusLabel.Text = "Loaded output saved.";
                }
            }
        }

        private void UpdateStatus(string prefix)
        {
            var more = offset < fileLength ? " More data is available; use Ctrl+L, Load all, or keep scrolling near the bottom." : string.Empty;
            statusLabel.Text = prefix + " Current line " + CurrentLineNumber().ToString(CultureInfo.InvariantCulture) + " of " + viewerBox.Lines.Length.ToString(CultureInfo.InvariantCulture) + "." + more;
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
}
