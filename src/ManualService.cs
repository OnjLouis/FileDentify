using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace FileDentify
{
    internal static class ManualService
    {
        public static void OpenManual(IWin32Window owner)
        {
            try
            {
                var dir = Path.Combine(Path.GetTempPath(), "FileDentify-manual");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "FileDentify-Manual.html");
                File.WriteAllText(path, BuildHtml(), Encoding.UTF8);
                Process.Start(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, ex.Message, "FileDentify manual", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string BuildHtml()
        {
            var generated = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            var html = new StringBuilder();
            html.AppendLine("<!doctype html>");
            html.AppendLine("<html lang=\"en\">");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset=\"utf-8\">");
            html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            html.AppendLine("<title>FileDentify Manual</title>");
            html.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;line-height:1.55;max-width:900px;margin:2rem auto;padding:0 1rem;color:#111;background:#fff}h1,h2{line-height:1.25}code{font-family:Consolas,monospace;background:#f2f2f2;padding:.1rem .25rem}table{border-collapse:collapse;width:100%;margin:1em 0 1.5em}th,td{border:1px solid #999;padding:.55em .7em;text-align:left;vertical-align:top}th{background:#f2f2f2}pre{background:#f7f7f7;padding:1rem;overflow:auto}a{color:#0645ad}li{margin:.25rem 0}.note{border-left:.3rem solid #555;padding-left:1rem}</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("<h1>FileDentify</h1>");
            html.AppendLine("<p>Current version: " + Html(Program.Version) + ". Manual generated " + Html(generated) + ".</p>");
            html.AppendLine("<p>FileDentify is a portable Windows file identification tool built for keyboard and screen-reader users. It combines built-in file analysis, embedded Unix file/libmagic, readable text extraction, hashes, advanced viewing, reports, and optional companion tools.</p>");
            html.AppendLine("<p>FileDentify is on GitHub at <a href=\"" + Html(Program.ProjectUrl) + "\">" + Html(Program.ProjectUrl) + "</a>.</p>");
            html.AppendLine("<p>Contact:<br><a href=\"https://onj.me/contact\">onj.me/contact</a><br>Donate:<br><a href=\"https://onj.me/donate\">onj.me/donate</a><br>Other Andre Louis software:<br><a href=\"https://onj.me/software\">onj.me/software</a></p>");

            Contents(html);
            Changelog(html);
            WhenToUse(html);
            GettingStarted(html);
            KeyboardShortcuts(html);
            AdvancedViewer(html);
            CommandLineOptions(html);
            ReportSections(html);
            Preferences(html);
            AccessibilityNotes(html);
            Troubleshooting(html);
            Credits(html);
            License(html);

            html.AppendLine("</body>");
            html.AppendLine("</html>");
            return html.ToString();
        }

        private static void Contents(StringBuilder html)
        {
            html.AppendLine("<h2 id=\"contents\">Contents</h2>");
            html.AppendLine("<ul>");
            Link(html, "changelog", "Changelog");
            Link(html, "when-to-use", "When to use FileDentify");
            Link(html, "getting-started", "Getting started");
            Link(html, "keyboard-shortcuts", "Keyboard shortcuts");
            Link(html, "advanced-viewer", "Advanced file viewer");
            Link(html, "command-line-options", "Command line options");
            Link(html, "report-sections", "Report sections");
            Link(html, "preferences", "Preferences");
            Link(html, "accessibility-notes", "Accessibility notes");
            Link(html, "troubleshooting", "Troubleshooting");
            Link(html, "credits", "Credits");
            Link(html, "license", "License");
            html.AppendLine("</ul>");
        }

        private static void WhenToUse(StringBuilder html)
        {
            html.AppendLine("<h2 id=\"when-to-use\">When to use FileDentify</h2>");
            html.AppendLine("<p>FileDentify is not meant to make existing tools obsolete. If Windows already tells you what you need, or if a one-line <code>file</code> result from WSL, Cygwin, MSYS2, or another Unix-like environment is enough, use that. Those tools are fast, familiar, and excellent for quick identification.</p>");
            html.AppendLine("<p>Use FileDentify when you want more context on Windows: a screen-reader-friendly tree, selected-item details, hashes, readable strings, byte offsets, an advanced text/hex/binary/octal viewer, HTML or plain-text reports, folder and multi-file overviews, Send To integration, and Windows-specific metadata.</p>");
            html.AppendLine("<p>FileDentify includes embedded Unix file/libmagic so reports start with a broad, familiar identification engine. FileDentify then adds its own Windows and format-specific sections, such as shortcuts, installers, media metadata, virtual disks, sample-library formats, Native Instruments files, Cubase projects, game files, old phone tones, Clipman files, and other structures where useful information can be surfaced.</p>");
            html.AppendLine("<p>The practical rule is simple: use the tool already on your system when it answers the question. Use FileDentify when you want to inspect, compare, save, share, or review the evidence more comfortably.</p>");
        }

        private static void Changelog(StringBuilder html)
        {
            html.AppendLine("<h2 id=\"changelog\">Changelog</h2>");
            html.AppendLine("<h3>1.2</h3>");
            html.AppendLine("<ul>");
            html.AppendLine("<li>Added Windows property metadata reporting for formats supported by Explorer property handlers, giving FileDentify another way to surface useful title, author, tag, media, document, and image metadata without bundling a large new parser. Closes <a href=\"https://github.com/OnjLouis/FileDentify/issues/3\">issue 3</a>.</li>");
            html.AppendLine("<li>Added Office Open XML metadata reporting for Word, Excel, and PowerPoint files, including document kind, title, subject, creator, modifier, created/modified dates, keywords, application properties, document counts, embedded/media counts, titles of parts, and custom properties when present.</li>");
            html.AppendLine("<li>Added OpenDocument metadata reporting for files such as <code>.odt</code>, <code>.ods</code>, and <code>.odp</code>, including title, creator, generator, editing details, and document statistics when present.</li>");
            html.AppendLine("<li>Improved likely-type summaries for ZIP-based document formats so common Office and OpenDocument files are identified near the top of the report, not only as generic ZIP-compatible containers.</li>");
            html.AppendLine("<li>Added short command-line switches for every command-line option, including <code>--advanced-view</code> / <code>-av</code> to open the graphical advanced viewer directly on a file.</li>");
            html.AppendLine("<li>Improved the advanced viewer status bar so NVDA's status command can read it. The status now starts with the current line number before the static mode and shortcut text.</li>");
            html.AppendLine("<li>Improved graceful closing so <code>--close</code> / <code>-c</code> can close owned advanced-viewer windows as well as the main FileDentify window.</li>");
            html.AppendLine("<li>Filtered noisy Explorer storage fields from Windows property metadata, including large internal status blobs that are not useful report data.</li>");
            html.AppendLine("</ul>");
            html.AppendLine("<h3>1.1.1</h3>");
            html.AppendLine("<ul>");
            html.AppendLine("<li>Fixed an advanced file viewer crash when pressing Enter after searching from a selection at the end of the loaded output.</li>");
            html.AppendLine("<li>Added <code>Ctrl+F</code> in the advanced file viewer to focus the search field and select the previous search text so typing replaces it.</li>");
            html.AppendLine("<li>Added <code>Shift+F3</code> in the advanced file viewer to search backward through loaded output.</li>");
            html.AppendLine("<li>Improved advanced viewer scrolling so loading more data only happens when the review cursor is actually at the loaded end, avoiding confusing line jumps near the end of a loaded block.</li>");
            html.AppendLine("<li>Advanced viewer status now refreshes the current line number while moving through loaded output.</li>");
            html.AppendLine("</ul>");
            html.AppendLine("<h3>1.1</h3>");
            html.AppendLine("<ul>");
            html.AppendLine("<li>Updated embedded Unix file/libmagic to MSYS2 MinGW-w64 file/libmagic 5.48.</li>");
            html.AppendLine("<li>Added PDF Info and XMP metadata reporting, including title, author, creator, producer, creation date, and modification date when present. Closes <a href=\"https://github.com/OnjLouis/FileDentify/issues/1\">issue 1</a>.</li>");
            html.AppendLine("<li>Added multimedia metadata reporting for common audio files, including FLAC duration and Vorbis comments, MP3 ID3v2 tags, MP3 bitrate/sample-rate duration estimates, and concise ffprobe summaries when ffprobe is available. Closes <a href=\"https://github.com/OnjLouis/FileDentify/issues/2\">issue 2</a>.</li>");
            html.AppendLine("<li>Added broad format-specific reporting for DMG, RAR, UFS sample libraries, Clipman .clipdb/settings files, Native Instruments/Kontakt/NKS/Reaktor/Battery/FM8/Absynth/Massive/Maschine/Kontour-related files, Steinberg Cubase/Nuendo/VST files, old mobile phone tones such as iMelody/SMAF/MMF/CMX/PMD, game and ROM files such as NSF/NES/Game Boy/GBA/SNES/Sega/N64/Doom WAD/CHD/Unity/Ren'Py/Wwise/Steam manifests, .blob assets, virtual disks, ISO images, RIFF/IFF, MIDI, SQLite, property lists, fonts, compressed streams, ZIP-compatible containers, and other common structures.</li>");
            html.AppendLine("<li>Added built-in Windows shortcut analysis for <code>.lnk</code> files, including Shell Link flags, target attributes, timestamps, target size, icon index, show command, and hotkey.</li>");
            html.AppendLine("<li>Added built-in Internet Shortcut and saved web favorite analysis for <code>.url</code> files, including modern <code>[InternetShortcut]</code> files and older favorites with <code>BASEURL</code> or <code>ORIGURL</code>.</li>");
            html.AppendLine("<li>Summary now prioritizes FileDentify-specific identifications and keeps long libmagic descriptions in the Unix file/libmagic section.</li>");
            html.AppendLine("<li>Added screen-reader-friendly details behavior: details start at the top, include a trailing blank line, expose shortcut names where Windows supports them, and keep long findings in reviewable text instead of forcing users through dense one-line summaries.</li>");
            html.AppendLine("<li>Added Ctrl+A support in read-only edit fields, scoped Ctrl+C behavior for tree selections and details fields, and a Readable text section for plain strings without byte offsets.</li>");
            html.AppendLine("<li>Added Ctrl+Shift+Left and Ctrl+Shift+Right to collapse or expand the report tree, matching Sensor Readout.</li>");
            html.AppendLine("<li>Added consistent keyboard access: Open files uses Ctrl+O, Save report uses Ctrl+S, Preferences uses Ctrl+comma, View HTML report uses Alt+V, Open containing folder uses Alt+L, and Help uses F1.</li>");
            html.AppendLine("<li>Added an Edit menu, a report-tree context menu, and a main-screen Help button so copy, expand/collapse, save, HTML report, and help actions are discoverable to mouse and keyboard users.</li>");
            html.AppendLine("<li>Added Contact, Donate, and Other software entries to the Help menu, matching Andre's other apps.</li>");
            html.AppendLine("<li>Added combined HTML report output with headings and section tables. Save report can now write either plain text or HTML, and command-line reports automatically use HTML when the output file ends in <code>.html</code> or <code>.htm</code>.</li>");
            html.AppendLine("<li>Added a Report overview at the top of combined reports, with file counts, generation time, total reported size, likely-type distribution, extension distribution, largest files, common sampled byte values, common readable strings, signature-match counts, and files that need attention.</li>");
            html.AppendLine("<li>Added View HTML report on the main screen. It opens a temporary combined HTML report in the default browser and cleans up the temporary file when FileDentify exits.</li>");
            html.AppendLine("<li>Added <code>--html-report</code> for explicit command-line HTML output.</li>");
            html.AppendLine("<li>Added <code>--folder-report</code> to recursively scan folders into one combined report, either plain text or HTML depending on the output extension.</li>");
            html.AppendLine("<li>Added folder input support in the graphical app and Send To workflow. Folders are recursively expanded into files, progress is shown while files are found and inspected, and empty inputs show a clear no-files message.</li>");
            html.AppendLine("<li>Added a Preferences option to create or remove a FileDentify desktop shortcut.</li>");
            html.AppendLine("<li>Added <code>--close</code> to ask other FileDentify windows from the same executable to close gracefully.</li>");
            html.AppendLine("<li>Added command-line switches for desktop shortcut install/uninstall and update checks.</li>");
            html.AppendLine("<li>Added non-interactive command-line report generation that exits after writing the requested report.</li>");
            html.AppendLine("<li>Added terminal mode through <code>fd.com</code>. Running <code>fd.com [files...]</code> opens terminal paging mode directly. <code>fd.com</code> and <code>FileDentify.exe</code> must live in the same folder.</li>");
            html.AppendLine("<li>Update checks default to startup on new installs, and the updater requires <code>fd.com</code> to be present beside <code>FileDentify.exe</code> in release ZIP files.</li>");
            html.AppendLine("<li>Added this built-in HTML manual on F1.</li>");
            html.AppendLine("<li>Added an advanced file viewer on F4 with readable text, hex, binary, and octal modes, incremental loading, load-all, F3 search, save loaded output, and standard read-only copy/select-all behavior.</li>");
            html.AppendLine("<li>Added command-line advanced viewer output with <code>--viewer-output</code>, <code>--viewer</code>, <code>--viewer-mode</code>, and <code>--viewer-bytes</code>. Terminal mode can also open an in-terminal advanced viewer with F4.</li>");
            html.AppendLine("<li>Updated third-party notices for the embedded MSYS2 file/libmagic runtime dependencies, including libsystre, libtre, gettext/libintl, and libiconv.</li>");
            html.AppendLine("</ul>");
            html.AppendLine("<h3>1.0</h3>");
            html.AppendLine("<ul><li>Initial public release.</li></ul>");
        }

        private static void GettingStarted(StringBuilder html)
        {
            html.AppendLine("<h2 id=\"getting-started\">Getting started</h2>");
            html.AppendLine("<p>Run FileDentify with no arguments to open the main window. Use <code>Ctrl+O</code> or File &gt; Open files to inspect one or more files.</p>");
            html.AppendLine("<p>If FileDentify is opened from Send To with a folder, the graphical app recursively scans the files in that folder and shows each file as a top-level tree item.</p>");
            html.AppendLine("<p>The left side is a tree of files and report sections. The right side is a read-only details edit field for the selected tree item. Use <code>F4</code> on a file or one of its sections to open the advanced file viewer.</p>");
            html.AppendLine("<p>The advanced file viewer has readable text, hex, binary, and octal modes. Use standard navigation keys, Ctrl+A, Ctrl+C, Ctrl+F to focus search, F3 and Shift+F3 to search next or previous, Ctrl+L to load more, Ctrl+Shift+L to load all, and Escape to close the viewer and return to FileDentify. Alt+F4 remains the normal Windows close command.</p>");
            html.AppendLine("<p>The report tree also has a context menu with copy, expand/collapse, save, and HTML report actions. Press the Application key or Shift+F10 while focused on the tree, or right-click with a mouse.</p>");
            html.AppendLine("<p>Save report can write either plain text or HTML depending on the chosen file extension. View HTML report opens a temporary HTML report in the default browser; use Save report if you want to keep it.</p>");
            html.AppendLine("<p>When a report contains more than one file, FileDentify adds a Report overview before the individual file reports. This is useful for spotting unknown files, inspection errors, large files, common strings, common byte patterns, and unusual type or extension clusters.</p>");
            html.AppendLine("<p>FileDentify can also be installed into the Windows Send To menu from Options &gt; Preferences. The Send To shortcut name is <code>File&amp;Dentify</code>, so the menu mnemonic is D.</p>");
            html.AppendLine("<p>Options &gt; Preferences can also create a normal desktop shortcut for opening FileDentify directly.</p>");
        }

        private static void KeyboardShortcuts(StringBuilder html)
        {
            html.AppendLine("<h2 id=\"keyboard-shortcuts\">Keyboard shortcuts</h2>");
            html.AppendLine("<table><thead><tr><th>Shortcut</th><th>Action</th></tr></thead><tbody>");
            Row(html, "Ctrl+O", "Open files.");
            Row(html, "Ctrl+S", "Save the current report.");
            Row(html, "Ctrl+C in the tree", "Copy details for the selected tree item.");
            Row(html, "Ctrl+C in details", "Copy selected text, or the current details text when no text is selected.");
            Row(html, "Ctrl+A in read-only edit fields", "Select all text.");
            Row(html, "Ctrl+Shift+Left", "Collapse all items in the report tree.");
            Row(html, "Ctrl+Shift+Right", "Expand all items in the report tree.");
            Row(html, "F1", "Open this manual.");
            Row(html, "F4", "Open the advanced file viewer for the selected file.");
            Row(html, "Ctrl+comma", "Open Preferences.");
            Row(html, "Shift+F1", "Check for updates.");
            Row(html, "Ctrl+F1", "Open the GitHub project page.");
            Row(html, "Alt+C", "Copy the full report from the main window button.");
            Row(html, "Alt+V", "Open a temporary HTML report in the default browser.");
            Row(html, "Alt+L", "Open the selected file's containing folder.");
            Row(html, "Escape", "Close the current FileDentify window or dialog.");
            html.AppendLine("</tbody></table>");
            html.AppendLine("<p>Advanced file viewer shortcuts are listed in the Advanced file viewer section.</p>");
        }

        private static void CommandLineOptions(StringBuilder html)
        {
            html.AppendLine("<h2 id=\"command-line-options\">Command line options</h2>");
            html.AppendLine("<table><thead><tr><th>Command</th><th>Meaning</th></tr></thead><tbody>");
            Row(html, "FileDentify.exe [files...]", "Open the graphical inspector with the supplied files loaded.");
            Row(html, "FileDentify.exe --report (-r) report.txt [files...]", "Write a plain text report and exit. Use a .html or .htm output path for HTML. The file list can contain multiple non-contiguous files from different folders or drives.");
            Row(html, "FileDentify.exe --html-report (-hr) report.html [files...]", "Explicitly write an HTML report and exit.");
            Row(html, "FileDentify.exe --folder-report (-fr) report.txt [folders-or-files...]", "Recursively scan folders and write one complete report. Use a .html or .htm output path for HTML.");
            Row(html, "FileDentify.exe --advanced-view (-av) file", "Open the graphical advanced file viewer directly on the first supplied readable file.");
            Row(html, "FileDentify.exe --viewer-output (-vo) output.txt --viewer-mode (-vm) hex file", "Write advanced viewer output without opening the graphical interface.");
            Row(html, "FileDentify.exe --viewer (-vw) --viewer-mode (-vm) readable file", "Write advanced viewer output to standard output.");
            Row(html, "FileDentify.exe --viewer-bytes (-vb) 4194304", "Limit command-line advanced viewer output to this many bytes per file.");
            Row(html, "fd.com [files...]", "Open terminal paging mode for one or more files. The file list can contain multiple non-contiguous files from different folders or drives.");
            Row(html, "fd.com --terminal (-t)", "Explicitly open terminal paging mode. This is usually implied by running fd.com with files.");
            Row(html, "FileDentify.exe --close (-c)", "Ask other FileDentify windows from the same executable to close gracefully.");
            Row(html, "FileDentify.exe --update (-u)", "Check GitHub Releases for updates.");
            Row(html, "FileDentify.exe --install-sendto (-is)", "Add FileDentify to the Windows Send To menu.");
            Row(html, "FileDentify.exe --uninstall-sendto (-us)", "Remove FileDentify from the Windows Send To menu.");
            Row(html, "FileDentify.exe --install-desktop (-id)", "Create a FileDentify shortcut on the desktop.");
            Row(html, "FileDentify.exe --uninstall-desktop (-ud)", "Remove the FileDentify desktop shortcut.");
            Row(html, "FileDentify.exe --version (-v)", "Show the current FileDentify version.");
            Row(html, "FileDentify.exe --help (-h)", "Show command-line help.");
            html.AppendLine("</tbody></table>");
            html.AppendLine("<h3>Terminal mode keys</h3>");
            html.AppendLine("<p>Use <code>fd.com</code> for terminal workflows in PowerShell or Windows Terminal. <code>fd.com</code> and <code>FileDentify.exe</code> must live in the same folder. <code>FileDentify.exe</code> is the graphical Windows executable used by Explorer and Send To.</p>");
            html.AppendLine("<table><thead><tr><th>Key</th><th>Action</th></tr></thead><tbody>");
            Row(html, "Down, PageDown, or Space", "Move forward one page.");
            Row(html, "Up or PageUp", "Move backward one page.");
            Row(html, "Home", "Jump to the beginning.");
            Row(html, "End", "Jump to the end.");
            Row(html, "F4", "Open the in-terminal advanced file viewer for the file currently shown at the top of the terminal page.");
            Row(html, "Q or Escape", "Exit terminal mode.");
            html.AppendLine("</tbody></table>");
            html.AppendLine("<p class=\"note\">The terminal advanced viewer stays inside the terminal. Its status line lists the available keys: Alt+T or T for Text, Alt+X or X for Hex, Alt+B or B for Binary, Alt+O or O for Octal, L or Ctrl+L to load more, Shift+L to load all up to the safety limit, and Escape, Q, Backspace, or Ctrl+C to return to the report pager.</p>");
            html.AppendLine("<p class=\"note\">When terminal mode output is redirected, FileDentify writes the report and exits instead of waiting for paging keys.</p>");
        }

        private static void AdvancedViewer(StringBuilder html)
        {
            html.AppendLine("<h2 id=\"advanced-viewer\">Advanced file viewer</h2>");
            html.AppendLine("<p>Press <code>F4</code> on a file or one of its report sections to open the advanced file viewer. The viewer opens directly on the read-only output field so review can begin immediately. Press <code>Escape</code> to close it and return to FileDentify.</p>");
            html.AppendLine("<p>The viewer status bar is readable with NVDA's status command. It reports the current line first, then loaded-data and mode information, so repeated checks start with the changing information.</p>");
            html.AppendLine("<table><thead><tr><th>Control or command</th><th>Meaning</th></tr></thead><tbody>");
            Row(html, "Alt+T", "Switch to Text mode.");
            Row(html, "Alt+X", "Switch to Hex mode.");
            Row(html, "Alt+B", "Switch to Binary mode.");
            Row(html, "Alt+O", "Switch to Octal mode.");
            Row(html, "Ctrl+F", "Move to the search field and select the previous search text so typing replaces it.");
            Row(html, "F3", "Find next match in the loaded output.");
            Row(html, "Shift+F3", "Find previous match in the loaded output.");
            Row(html, "Ctrl+L", "Load more content from the file.");
            Row(html, "Ctrl+Shift+L", "Load all remaining content from the file.");
            Row(html, "Ctrl+S", "Save the currently loaded viewer output.");
            Row(html, "Ctrl+A", "Select all loaded output.");
            Row(html, "Ctrl+C", "Copy selected output.");
            Row(html, "Escape", "Close the advanced viewer.");
            html.AppendLine("</tbody></table>");
            html.AppendLine("<p>The viewer loads the file in chunks and opens at the top of the loaded output. It also loads one more chunk when navigation reaches the end of the loaded content. Use <code>Ctrl+L</code> for a deliberate next chunk, or <code>Ctrl+Shift+L</code> to load all remaining content. Command-line equivalents are available with <code>--viewer-output</code>, <code>--viewer</code>, <code>--viewer-mode</code>, and <code>--viewer-bytes</code>.</p>");
        }

        private static void ReportSections(StringBuilder html)
        {
            html.AppendLine("<h2 id=\"report-sections\">Report sections</h2>");
            html.AppendLine("<table><thead><tr><th>Section</th><th>What it contains</th></tr></thead><tbody>");
            Row(html, "Summary", "Likely type, Unix file/libmagic result, path, size, extension, and timestamps.");
            Row(html, "Signature matches", "Built-in signature matches from the beginning of the file.");
            Row(html, "Unix file/libmagic", "The embedded file/libmagic description, MIME result, and engine version.");
            Row(html, "Hashes", "SHA-256 and bounded hash information.");
            Row(html, "Filesystem", "Local file attributes and timestamps.");
            Row(html, "Clipman", "Clipman .clipdb and settings details. Encrypted CLIPDB2 databases are identified by container metadata only; FileDentify does not ask for passwords, decrypt history, or reveal protected remembered-password blobs.");
            Row(html, "PDF", "PDF version, linearization/encryption hints, and sampled Info/XMP metadata.");
            Row(html, "Windows property metadata", "Useful Explorer property-handler metadata where Windows exposes it, such as title, authors, tags, comments, media fields, image fields, and document fields.");
            Row(html, "Office document metadata", "Word, Excel, and PowerPoint Open XML properties such as title, creator, dates, application, document statistics, embedded/media counts, and custom properties.");
            Row(html, "OpenDocument metadata", "OpenDocument metadata and statistics for formats such as .odt, .ods, and .odp.");
            Row(html, "FLAC, ID3, Ogg, RIFF, AIFF, MIDI, ISO base media", "Built-in audio/media structure and metadata where available.");
            Row(html, "Media details", "A concise ffprobe summary when ffprobe.exe is beside FileDentify and probing succeeds.");
            Row(html, "Mobile phone tone", "Old mobile ringtone and phone-audio clues for iMelody .imy, Yamaha SMAF/MMF .mmf, Qualcomm CMX/PMD .pmd, AMR, RTTTL, RTX, and Nokia OTA-style files where the bytes expose useful fields.");
            Row(html, "Game/ROM data", "Header-level details for game and emulator files such as NSF, iNES/NES, Game Boy, GBA, SNES, Genesis/Mega Drive, Nintendo 64, Doom WAD, CHD, Quake PACK, Wwise BNK/WEM, Unity assets, Ren'Py archives, Steam ACF/VDF, generic .rom files, game saves, disc layout files, and common patch/package extensions.");
            Row(html, "Native Instruments", "Kontakt, NKS, Reaktor, Battery, FM8, Absynth, Massive, Maschine, Kontour, and related NI extension hints, visible product strings, header markers, .nicnt metadata, and sampled instrument or sample references.");
            Row(html, "Steinberg Cubase", "Cubase, Nuendo, and VST-related extension hints plus readable project clues such as ASIO/VST markers, MIDI edit commands, drum-map names, effects, grooves, fonts, and internal markers.");
            Row(html, "UFS sample library container", "UFS marker and extension details for UVI/Falcon-style sample-library containers.");
            Row(html, "Binary blob", "Generic .blob extension details plus visible family hints when the header contains recognizable product strings.");
            Row(html, "Readable text", "Plain extracted strings without byte offsets, intended to be comfortable with screen readers.");
            Row(html, "Printable strings", "Offset-based string findings for forensic detail.");
            Row(html, "Companion tools", "Optional output from tools such as metaflac.exe, opusinfo.exe, or vgmstream-cli.exe when present.");
            html.AppendLine("</tbody></table>");
        }

        private static void Preferences(StringBuilder html)
        {
            html.AppendLine("<h2 id=\"preferences\">Preferences</h2>");
            html.AppendLine("<p>Preferences are stored in <code>FileDentify.ini</code> beside the executable. They are not stored in AppData or the registry.</p>");
            html.AppendLine("<ul>");
            html.AppendLine("<li>Automation controls whether FileDentify is installed in the Windows Send To menu and whether a FileDentify shortcut is present on the desktop.</li>");
            html.AppendLine("<li>Updates controls GitHub release checks and quiet update installation. New installs check at startup by default; this can be changed to hourly, daily, weekly, or never.</li>");
            html.AppendLine("</ul>");
        }

        private static void AccessibilityNotes(StringBuilder html)
        {
            html.AppendLine("<h2 id=\"accessibility-notes\">Accessibility notes</h2>");
            html.AppendLine("<ul>");
            html.AppendLine("<li>The main report tree uses useful node names rather than decorative labels.</li>");
            html.AppendLine("<li>Details fields are read-only edit controls, so screen readers can review text with normal caret commands.</li>");
            html.AppendLine("<li>Read-only edit fields include a trailing blank line so arrowing past the end is clearer.</li>");
            html.AppendLine("<li><code>Ctrl+C</code> copies the current scope instead of unexpectedly copying the whole report.</li>");
            html.AppendLine("<li><code>Ctrl+A</code> works in read-only edit fields.</li>");
            html.AppendLine("</ul>");
        }

        private static void Troubleshooting(StringBuilder html)
        {
            html.AppendLine("<h2 id=\"troubleshooting\">Troubleshooting</h2>");
            html.AppendLine("<ul>");
            html.AppendLine("<li>If a file type is unknown in the summary, check the Unix file/libmagic section and format-specific sections further down.</li>");
            html.AppendLine("<li>If media details are sparse, place <code>ffprobe.exe</code> beside FileDentify for broader audio/video metadata.</li>");
            html.AppendLine("<li>If startup fails, FileDentify writes <code>FileDentify-startup-error.txt</code> beside the executable when possible.</li>");
            html.AppendLine("</ul>");
        }

        private static void Credits(StringBuilder html)
        {
            html.AppendLine("<h2 id=\"credits\">Credits</h2>");
            html.AppendLine("<p>FileDentify is an Andre Louis utility.</p>");
            html.AppendLine("<p>A huge thanks to everyone who has submitted a GitHub issue or suggestion. You have helped make FileDentify more useful.</p>");
            html.AppendLine("<p>FileDentify is free software. Contact, Donate, and Other software links are available from the Help menu and near the top of this manual.</p>");
            html.AppendLine("<p>FileDentify uses or can use components from these projects:</p>");
            html.AppendLine("<ul>");
            Credit(html, "file/libmagic", "https://www.darwinsys.com/file/", "and file on GitHub", "https://github.com/file/file", "for broad file signature identification");
            Credit(html, "MSYS2", "https://www.msys2.org/", "and MSYS2 Packages", "https://packages.msys2.org/", "for the embedded Windows build of file/libmagic");
            Credit(html, "mingw-w64-x86_64-file", "https://packages.msys2.org/package/mingw-w64-x86_64-file", "", "", "the embedded file/libmagic package");
            Credit(html, "libsystre", "https://packages.msys2.org/package/mingw-w64-x86_64-libsystre", "libtre, gettext runtime/libintl, and libiconv", "https://packages.msys2.org/", "bundled as runtime dependencies for the embedded file/libmagic build");
            Credit(html, "FFmpeg ffprobe", "https://ffmpeg.org/ffprobe.html", "", "", "used when ffprobe.exe is beside FileDentify for richer media metadata");
            Credit(html, "FLAC/metaflac", "https://xiph.org/flac/", "", "", "used when metaflac.exe is beside FileDentify for FLAC stream information");
            Credit(html, "Opus tools/opusinfo", "https://opus-codec.org/", "", "", "used when opusinfo.exe is beside FileDentify for Opus/Ogg information");
            Credit(html, "vgmstream", "https://github.com/vgmstream/vgmstream", "", "", "used when vgmstream-cli.exe is beside FileDentify for supported game-audio metadata");
            html.AppendLine("<li>Microsoft .NET Framework and support libraries.</li>");
            html.AppendLine("</ul>");
        }

        private static void License(StringBuilder html)
        {
            html.AppendLine("<h2 id=\"license\">License</h2>");
            html.AppendLine("<p>The main FileDentify application is intended to be released under the MIT License. Bundled third-party components have their own notices under Help &gt; Third-party notices.</p>");
        }

        private static void Link(StringBuilder html, string id, string text)
        {
            html.AppendLine("<li><a href=\"#" + Html(id) + "\">" + Html(text) + "</a></li>");
        }

        private static void Row(StringBuilder html, string left, string right)
        {
            html.AppendLine("<tr><td><code>" + Html(left) + "</code></td><td>" + Html(right) + "</td></tr>");
        }

        private static void Credit(StringBuilder html, string name, string url, string secondName, string secondUrl, string purpose)
        {
            html.Append("<li><a href=\"" + Html(url) + "\">" + Html(name) + "</a>");
            if (!string.IsNullOrWhiteSpace(secondName))
            {
                html.Append(" ");
                html.Append(string.IsNullOrWhiteSpace(secondUrl)
                    ? Html(secondName)
                    : "<a href=\"" + Html(secondUrl) + "\">" + Html(secondName) + "</a>");
            }
            html.AppendLine(", " + Html(purpose) + ".</li>");
        }

        private static string Html(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }
    }
}

