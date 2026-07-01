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
            SupportedFileTypes(html);
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
            Link(html, "supported-file-types", "Supported file types");
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
            html.AppendLine("<h3>1.6</h3>");
            html.AppendLine("<ul>");
            html.AppendLine("<li>Expanded the FileDentify built-in file-type database from a fresh local software-library scan, with extra attention on extensionless files and older music/game/support formats. See <a href=\"#supported-file-types\">Supported file types</a> for the maintained coverage table.</li>");
            html.AppendLine("<li>Added Roland Fantom Librarian reporting for <code>.fxl</code> and <code>.fsl</code> files, including Fantom-S/Fantom-X markers, target family, file role, and visible patch or library names where present.</li>");
            html.AppendLine("<li>Added Yamaha S-YXG software-synthesizer table reporting for older Yamaha softsynth <code>.tbl</code> files, with separate handling so unrelated <code>.tbl</code> files are not mislabeled.</li>");
            html.AppendLine("<li>Added OpenAL spatial-audio reporting for <code>.mhr</code> HRTF data and <code>.ambdec</code> ambisonic decoder presets, including header/configuration clues and filename sample-rate hints where available.</li>");
            html.AppendLine("<li>Added BBC Micro / BeebEm media reporting for <code>.ssd</code>, <code>.uef</code>, and PHROM-style extensionless emulator files.</li>");
            html.AppendLine("<li>Added header-based handling for same-extension <code>.pat</code> collisions, distinguishing Gravis UltraSound GF1 instrument patches from Synology DSM/SRM update packages.</li>");
            html.AppendLine("<li>Added production-audio resource reporting from a refreshed production-library scan, covering Reason NN-XT, classic Logic, GarageBand chord data, Native Instruments sample-add files, MAGIX/SEK'D sidecars, Akai sampler programs/multis, ReBirth mods, and GN Audio MIDI containers.</li>");
            html.AppendLine("<li>Added Chromium hyphenation dictionaries, LibreOffice/OpenOffice support resources, and Winamp 5/Wasabi plug-in modules to the app-resource coverage.</li>");
            html.AppendLine("<li>Added another candidate-list pass for common but easy-to-overlook support formats, including dictionaries, RTF, Jet/Access databases, COM type libraries, Windows Media/ASF containers, and Type 1 font support files. See <a href=\"#supported-file-types\">Supported file types</a> for the maintained coverage table.</li>");
            html.AppendLine("<li>Added real-world TV/music archive candidates including Sound Designer II audio, Cool Edit / Adobe Audition sessions, Windows Media Player playlists, raw MPEG audio streams, extensionless ID3-tagged MP3s, and Barrcode AA audio-resource markers.</li>");
            html.AppendLine("<li>Added a FileDentify database section near the top of reports when FileDentify's own built-in rules identify the file, making it clearer when the answer comes from FileDentify rather than Unix file/libmagic or generic signatures.</li>");
            html.AppendLine("<li>Expanded Native Instruments reporting from a full Komplete library audit, including clearer product and role hints for Kontakt, Battery, Reaktor, Maschine, Guitar Rig, Komplete Kontrol, and related preset, snapshot, ensemble, resource, and extensionless files.</li>");
            html.AppendLine("<li>Audited format notes across the newer FileDentify-specific sections so obscure formats explain their likely product, platform, or purpose without implying that FileDentify decodes proprietary payloads.</li>");
            html.AppendLine("</ul>");
            html.AppendLine("<h3>1.5</h3>");
            html.AppendLine("<ul>");
            html.AppendLine("<li>Expanded real-world audio-library and macOS-resource coverage from local sample-library, plug-in, application-support, and system-library scans. See <a href=\"#supported-file-types\">Supported file types</a> for the maintained coverage table.</li>");
            html.AppendLine("<li>Added richer reporting for VST3 module metadata, including module name, vendor, class/category, subcategory, class ID, and version fields where <code>moduleinfo.json</code> is present.</li>");
            html.AppendLine("<li>Added focused audio-resource reporting for Arturia, ROLI, Initial Audio, Guitar Rig impulse-response, Apple Core Audio/Space Designer, Scala tuning, and wavetable files, with context and visible metadata where available.</li>");
            html.AppendLine("<li>Send To now appends plain file and folder inputs to an already running FileDentify window instead of replacing the current report. Use File &gt; New report or <code>Ctrl+N</code> when you explicitly want to start over.</li>");
            html.AppendLine("</ul>");
            html.AppendLine("<h3>1.4.1</h3>");
            html.AppendLine("<ul>");
            html.AppendLine("<li>Media reports now surface high-value sections such as Windows property metadata, QuickTime metadata, ISO base media, Media details, and ffprobe output closer to the top of each file report.</li>");
            html.AppendLine("<li>Improved old memory-card reports with clearer ScanSoft RealSpeak Mobile speech headers, Symbian thumbnail/mask sidecars, Roland FA sample/song section priority, and Nintendo flashcart save clues.</li>");
            html.AppendLine("<li>Expanded macOS application-bundle coverage for XPC services, compiled storyboard and Core Data resource folders, RTFD documents, Metal shader libraries, scripting definitions, entitlements, and privacy manifests.</li>");
            html.AppendLine("<li>Embedded runtime extraction now uses one FileDentify temp root per running process and removes stale FileDentify temp folders on launch and exit.</li>");
            html.AppendLine("</ul>");
            html.AppendLine("<h3>1.4</h3>");
            html.AppendLine("<ul>");
            html.AppendLine("<li>Fixed last-report recovery so closing FileDentify with <code>Escape</code> saves the currently selected file, section, and report item before exit.</li>");
            html.AppendLine("<li>Fixed locked or unreadable files, such as a live Windows <code>NTUSER.DAT</code>, so they appear as inspection-error entries instead of stopping the GUI report run.</li>");
            html.AppendLine("<li>Added cautious identification for protected <code>.bpak</code> game packages, including fruit-machine or digital slot-machine package filename clues.</li>");
            html.AppendLine("<li>Added more classic game detail for Quake-family files, including BSP maps, demo recordings, QuakeC <code>progs.dat</code>, models, and sprites, plus Duke Nukem 3D saved-game context.</li>");
            html.AppendLine("<li>Added Quick Windows Sequencer (QWS) support-file reporting for instrument maps, note transforms, language prompts, and settings files while avoiding printing recent-file paths from QWS settings.</li>");
            html.AppendLine("<li>Added NVDA add-on package reporting for <code>.nvda-addon</code> files, including manifest metadata and package structure counts.</li>");
            html.AppendLine("<li>Expanded reporting for firmware, hardware-ID databases, VMware metadata, Electron ASAR archives, NuGet packages, AI model files, speech-engine data, and other large or confusing system/support files. See <a href=\"#supported-file-types\">Supported file types</a> for the maintained coverage table.</li>");
            html.AppendLine("<li>Added personal/export reporting for Nokia/Symbian messages, vCard contacts, iCalendar files, OPML subscription lists, EML messages, and Windows Media Encoder session files.</li>");
            html.AppendLine("<li>Added Windows/system reporting for event logs, driver setup caches, printer descriptions, licensing XML, compatibility databases, catalog databases, AppX/MSIX packages, translation catalogs, network config files, and application manifests.</li>");
            html.AppendLine("</ul>");
            html.AppendLine("<h3>1.3</h3>");
            html.AppendLine("<ul>");
            html.AppendLine("<li>Added configurable report-section ordering from the main tree. Use <code>Ctrl+Up</code> and <code>Ctrl+Down</code> on a report section to move it earlier or later. <code>Summary</code> stays pinned first, and file order stays natural.</li>");
            html.AppendLine("<li>Added Sensor Readout-style section navigation. <code>Ctrl+0</code> through <code>Ctrl+9</code> jump through the current file's first ten sections, <code>Ctrl+Shift+0</code> through <code>Ctrl+Shift+9</code> jump through sections 10 to 19, <code>Alt+Up</code> / <code>Alt+Down</code> move to the previous or next section, and <code>Alt+Home</code> / <code>Alt+End</code> jump to the first or last section without leaving the details field.</li>");
            html.AppendLine("<li>Added file navigation shortcuts for multi-file reports. <code>Alt+Left</code> and <code>Alt+Right</code> jump to the previous or next file, <code>Alt+PageUp</code> and <code>Alt+PageDown</code> jump to the first or last file, <code>Alt+Backspace</code> jumps to Report overview, and <code>Alt+1</code> through <code>Alt+0</code> jump directly to files 1 through 10. File navigation keeps the same report section selected when that section exists in the destination file, otherwise it falls back to Summary.</li>");
            html.AppendLine("<li>Added <code>Ctrl+Shift+O</code> to append files to the current report. <code>Ctrl+O</code> now clearly starts a new report and offers to save the current report first.</li>");
            html.AppendLine("<li>Added File &gt; Open folder with <code>Ctrl+Shift+L</code> to recursively inspect an entire folder from inside FileDentify.</li>");
            html.AppendLine("<li>Added native <code>.fdreport</code> saved reports that reopen inside FileDentify with the tree intact. These reports can be sent to another user for analysis, and FileDentify can optionally register the extension with Windows.</li>");
            html.AppendLine("<li>Added package-folder reporting for macOS-style bundles that Windows exposes as folders, including Logic Pro projects, GarageBand projects, Time Machine sparse bundles, app bundles, framework/loadable bundles, and audio plug-in bundles.</li>");
            html.AppendLine("<li>Added automatic last-report recovery. When enabled, FileDentify keeps a private <code>FileDentify.fdreport</code> recovery file beside the app. Use <code>Ctrl+Shift+T</code> or File &gt; Reopen last report to bring it back, including the selected file, section, and report item where possible.</li>");
            html.AppendLine("<li>Added <code>F5</code> to refresh original files from a live or reopened report. If the original files are not available on the current machine, FileDentify explains why the report cannot be fully refreshed.</li>");
            html.AppendLine("<li>Changed the default section order so high-value FileDentify-specific sections appear immediately after Summary. Safety hints and format-specific sections such as Korg, Toontrack, Decent Sampler, Native Instruments, Clipman, project files, package folders, and media/document details are surfaced before generic evidence such as hashes and raw header bytes. If you explicitly move one of those sections, your saved position for that section is respected.</li>");
            html.AppendLine("<li>Added embedded Tolk screen-reader output, including the NVDA controller client companion DLL, for concise shortcut and section-move announcements when a supported screen reader is running.</li>");
            html.AppendLine("<li>Expanded format-specific identification across music production files, sample libraries, Mac package folders, game/audio assets, archives, firmware, AI model files, documents, and media containers. See <a href=\"#supported-file-types\">Supported file types</a> for the maintained coverage table.</li>");
            html.AppendLine("<li>Added more local-sample-backed coverage from the format-discovery comparison, including macOS Finder <code>.DS_Store</code>, Windows icon <code>.ico</code> resources, Windows bitmap font <code>.fon</code> files, and richer Opus-in-Ogg metadata.</li>");
            html.AppendLine("<li>Improved main-window section details so multiline values stay on separate lines instead of being flattened into very long pipe-separated lines.</li>");
            html.AppendLine("<li>Added safety hints for obvious header and extension mismatches, such as a file with a document, archive, image, shortcut, or executable header but an unexpected extension. FileDentify does not declare malware; it recommends caution and a trusted security scan when the mismatch is suspicious.</li>");
            html.AppendLine("<li>Added command-line output safety checks so report and advanced-viewer output refuse to overwrite one of the input files. Console companion errors now print to stderr instead of opening a GUI error window.</li>");
            html.AppendLine("<li>Added an optional embedded HTML details view in the main window. Press <code>F7</code> or use View &gt; HTML details view to switch between the traditional read-only text details and heading/table-based HTML details. The choice is remembered.</li>");
            html.AppendLine("</ul>");
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
            html.AppendLine("<li>Added Contact, Donate, and Other software entries to the Help menu, matching the other Andre Louis utilities.</li>");
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
            html.AppendLine("<p>Run FileDentify with no arguments to open the main window. Use <code>Ctrl+O</code> or File &gt; Open files to inspect one or more files. Use <code>Ctrl+Shift+L</code> or File &gt; Open folder to recursively inspect a folder. If a report is already loaded, <code>Ctrl+O</code> and Open folder start a new report and offer to save the current report first. Use <code>Ctrl+Shift+O</code> or File &gt; Append files to report to add more files without clearing the current report.</p>");
            html.AppendLine("<p>Use <code>Ctrl+R</code>, File &gt; Open FileDentify report, or open a <code>.fdreport</code> file from Explorer to reload a saved report with the tree intact. Use <code>Ctrl+Shift+T</code> or File &gt; Reopen last report to reopen FileDentify's automatic recovery report after an accidental close. Saved reports remember the selected file, section, and report item where possible. A saved report is static evidence; press <code>F5</code> to re-inspect the original files if those paths still exist on the current machine.</p>");
            html.AppendLine("<p>If FileDentify is opened from Send To with a folder, the graphical app recursively scans the files in that folder and shows each file as a top-level tree item. Package folders such as <code>.logicx</code>, <code>.sparsebundle</code>, <code>.app</code>, and Mac plug-in bundles are inspected as single report items instead of being flattened into all of their internal files.</p>");
            html.AppendLine("<p>The left side is a tree of files and report sections. The right side shows details for the selected tree item. By default this is a read-only text field. Press <code>F7</code> or use View &gt; HTML details view to switch to an embedded HTML view with headings and tables. Use <code>F4</code> on a file or one of its sections to open the advanced file viewer.</p>");
            html.AppendLine("<p>The advanced file viewer has readable text, hex, binary, and octal modes. Use standard navigation keys, Ctrl+A, Ctrl+C, Ctrl+F to focus search, F3 and Shift+F3 to search next or previous, Ctrl+L to load more, Ctrl+Shift+L to load all, and Escape to close the viewer and return to FileDentify. Alt+F4 remains the normal Windows close command.</p>");
            html.AppendLine("<p>When opened from the main FileDentify window, the advanced viewer is an owned part of the same app rather than a separate Alt+Tab target. If you close it with Escape and press F4 again while the same file is still loaded, FileDentify restores the previously loaded amount, mode, search text, and review position.</p>");
            html.AppendLine("<p>The report tree also has a context menu with copy, expand/collapse, save, and HTML report actions. Press the Application key or Shift+F10 while focused on the tree, or right-click with a mouse.</p>");
            html.AppendLine("<p>Save report writes a native <code>.fdreport</code> by default. It can also write plain text or HTML depending on the chosen file extension. View HTML report opens a temporary HTML report in the default browser; use Save report if you want to keep it.</p>");
            html.AppendLine("<p>When a report contains more than one file, FileDentify adds a Report overview before the individual file reports. This is useful for spotting unknown files, inspection errors, large files, common strings, common byte patterns, and unusual type or extension clusters.</p>");
            html.AppendLine("<p>Report section order is configurable from the tree. Select a section such as Readable text, Hashes, or Printable strings and press <code>Ctrl+Up</code> or <code>Ctrl+Down</code> to move it. The order is saved in <code>FileDentify.ini</code> and applies to all loaded files. Summary stays pinned first, then FileDentify surfaces newly detected high-value safety and format-specific sections before generic evidence unless you have explicitly moved that same section. Report overview stays pinned above files.</p>");
            html.AppendLine("<p>FileDentify can also be installed into the Windows Send To menu from Options &gt; Preferences. The Send To shortcut name is <code>File&amp;Dentify</code>, so the menu mnemonic is D. If FileDentify is already open, sending more plain files or folders appends them to the current report. If FileDentify is closed, Send To starts a new report.</p>");
            html.AppendLine("<p>Options &gt; Preferences can also create a normal desktop shortcut for opening FileDentify directly.</p>");
        }

        private static void KeyboardShortcuts(StringBuilder html)
        {
            html.AppendLine("<h2 id=\"keyboard-shortcuts\">Keyboard shortcuts</h2>");
            html.AppendLine("<table><thead><tr><th>Shortcut</th><th>Action</th></tr></thead><tbody>");
            Row(html, "Ctrl+N", "Start a new empty report after offering to save the current report.");
            Row(html, "Ctrl+O", "Open files.");
            Row(html, "Ctrl+Shift+L", "Open a folder and recursively inspect its files.");
            Row(html, "Ctrl+Shift+O", "Append files to the current report without clearing already loaded files.");
            Row(html, "Ctrl+R", "Open a saved FileDentify .fdreport file.");
            Row(html, "Ctrl+Shift+T", "Reopen the automatically saved last report.");
            Row(html, "Ctrl+S", "Save the current report.");
            Row(html, "F5", "Refresh the report by re-inspecting original files that are available on this machine.");
            Row(html, "Ctrl+C in the tree", "Copy details for the selected tree item.");
            Row(html, "Ctrl+C in details", "Copy selected text, or the current details text when no text is selected.");
            Row(html, "Ctrl+A in read-only edit fields", "Select all text.");
            Row(html, "F6 in HTML details view", "Move focus between the report tree and the embedded HTML details document. When HTML details view is active, Enter report is the first button in the main button row and performs the same action. Tab stays inside the HTML details document; use F6 to return to the tree.");
            Row(html, "F7", "Toggle the main-window details pane between text view and embedded HTML view. The preference is remembered.");
            Row(html, "Ctrl+Shift+Left", "Collapse all items in the report tree.");
            Row(html, "Ctrl+Shift+Right", "Expand all items in the report tree.");
            Row(html, "Ctrl+Up / Ctrl+Down on a report section", "Move that report section earlier or later in the saved section order. Summary stays pinned first.");
            Row(html, "Ctrl+0 to Ctrl+9", "Jump through the first ten sections in the current file. When a file root is selected, Ctrl+0 jumps to that file's Summary.");
            Row(html, "Ctrl+Shift+0 to Ctrl+Shift+9", "Jump through sections 10 to 19 in the current file.");
            Row(html, "Alt+Up / Alt+Down", "Move through the current review flow. From Report overview, Alt+Down enters the first file overview. From a file overview, Alt+Down enters the first section. From the first section, Alt+Up returns to the file overview. If focus is in details, focus stays there and the details refresh.");
            Row(html, "Alt+Home / Alt+End", "Jump to the first or last report section in the current file.");
            Row(html, "Alt+Left / Alt+Right", "Jump to the previous or next file in a multi-file report, keeping the same report section selected where possible.");
            Row(html, "Alt+Backspace", "Jump to Report overview, or to the first report item if the current report does not have a combined overview.");
            Row(html, "Alt+PageUp / Alt+PageDown", "Jump to the first or last file in a multi-file report, keeping the same report section selected where possible.");
            Row(html, "Alt+1 to Alt+0", "Jump directly to files 1 through 10 in a multi-file report. Alt+0 means file 10. The current report section is kept where possible, otherwise FileDentify falls back to Summary.");
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
            Row(html, "FileDentify.exe [files...]", "Open the graphical inspector with the supplied files loaded. If FileDentify is already running, plain file and folder arguments are handed to that window and appended to its current report.");
            Row(html, "FileDentify.exe [report.fdreport]", "Open a saved FileDentify report in the main window.");
            Row(html, "FileDentify.exe --report (-r) report.fdreport [files...]", "Write a native reopenable FileDentify report and exit. Use a .txt output path for plain text or a .html/.htm output path for HTML. The file list can contain multiple non-contiguous files from different folders or drives.");
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
            Row(html, "FileDentify.exe --install-report-association (-ir)", "Register .fdreport files with Windows for the current user.");
            Row(html, "FileDentify.exe --uninstall-report-association (-ur)", "Remove FileDentify's .fdreport file association for the current user.");
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
            Row(html, "Alt+T or T in terminal advanced viewer", "Switch to Text mode.");
            Row(html, "Alt+X or X in terminal advanced viewer", "Switch to Hex mode.");
            Row(html, "Alt+B or B in terminal advanced viewer", "Switch to Binary mode.");
            Row(html, "Alt+O or O in terminal advanced viewer", "Switch to Octal mode.");
            Row(html, "L or Ctrl+L in terminal advanced viewer", "Load more source data.");
            Row(html, "Shift+L in terminal advanced viewer", "Load all source data up to the terminal safety limit.");
            Row(html, "Escape, Q, Backspace, or Ctrl+C in terminal advanced viewer", "Return to the terminal report pager.");
            Row(html, "Q or Escape in report pager", "Exit terminal mode.");
            html.AppendLine("</tbody></table>");
            html.AppendLine("<p class=\"note\">When terminal mode output is redirected, FileDentify writes the report and exits instead of waiting for paging keys.</p>");
        }

        private static void AdvancedViewer(StringBuilder html)
        {
            html.AppendLine("<h2 id=\"advanced-viewer\">Advanced file viewer</h2>");
            html.AppendLine("<p>Press <code>F4</code> on a file or one of its report sections to open the advanced file viewer. The viewer opens maximized, directly on the read-only output field, so review can begin immediately. Press <code>Escape</code> to close it and return to FileDentify.</p>");
            html.AppendLine("<p>The viewer status bar is readable with NVDA's status command. It reports the current line first, then loaded-data and mode information, so repeated checks start with the changing information.</p>");
            html.AppendLine("<p>Search does not silently wrap. If F3 finds no later match, the review position stays where it is and the status explains that there is no further match in loaded content. Use Shift+F3 to search backward, or load more content and press F3 again.</p>");
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
            html.AppendLine("<p>The viewer loads the file in chunks and opens at the top of the loaded output. It also loads one more chunk when navigation reaches the end of the loaded content, placing the review cursor at the start of the newly loaded content rather than jumping to the end of that chunk. Use <code>Ctrl+L</code> for a deliberate next chunk, or <code>Ctrl+Shift+L</code> to load all remaining content. Command-line equivalents are available with <code>--viewer-output</code>, <code>--viewer</code>, <code>--viewer-mode</code>, and <code>--viewer-bytes</code>.</p>");
        }

        private static void SupportedFileTypes(StringBuilder html)
        {
            html.AppendLine("<h2 id=\"supported-file-types\">Supported file types</h2>");
            html.AppendLine("<p>This table is the maintained list for FileDentify-specific coverage beyond broad file/libmagic identification. New format families are added here instead of making the changelog noisy.</p>");
            html.AppendLine("<table><thead><tr><th>Family</th><th>Examples</th><th>What FileDentify tries to show</th></tr></thead><tbody>");
            SupportedRow(html, "Windows and web shortcuts", ".lnk, .url", "Target, flags, timestamps, web URL fields, and shortcut metadata.");
            SupportedRow(html, "Windows system data", ".evtx, .pnf, .inf_loc, .gpd, .gdl, .xrm-ms, .sdb, catdb, .cat, .appx, .msix, .msixbundle, .manifest, .man, .fon, .mo, .qm, .tlb, boot.sdi/.sdi, hosts, services, protocol, networks", "Event-log headers, driver setup cache and localization clues, printer description directives, licensing XML counts, app compatibility database markers, catalog database markers, Windows app package manifest/signature presence, instrumentation manifest counts, bitmap font NE-wrapper fields, translation-catalog counts, COM type-library markers, boot SDI image hints, and network-config entries.");
            SupportedRow(html, "Documents and ebooks", ".pdf, .docx, .xlsx, .pptx, .odt, .ods, .odp, .rtf, .epub, .lit, .mobi, .prc, .pdb, .lrf, .brf, .cbr, .cbz, .chm, .hlp", "Document metadata, ZIP/package structure, creator/date fields, counts, embedded media/resource clues, RTF header fields and safe text preview, legacy ebook headers, Palm/MOBI database fields, braille text previews, comic archive hints, and compiled help headers.");
            SupportedRow(html, "Audio and media containers", ".flac, .mp3, extensionless ID3-tagged MP3, .mp2, .mpga, .ogg/.opus, .wav, .aiff, .mid, .au, .wma, .wmv, .asf, .m2ts, .ts, .mts, .mov, .mp4", "Header metadata, OpusHead/OpusTags fields, stream/container clues, raw MPEG frame estimates, ASF/Windows Media object markers, transport-stream packet details, and ffprobe summaries when available.");
            SupportedRow(html, "Sample and instrument banks", ".sf2, .sbk, .sfArk, .dls, .ecw, .sfz, .exs, .ufs, .blob, .jlw, .vop, .scl, Gravis UltraSound GF1 .pat files by header, Ensoniq .efe/.eda/.edt", "Bank metadata, visible preset/instrument/sample names, microtuning scale text, container hints, legacy sound-bank clues, GUS patch header fields, Ensoniq EPS/ASR/TS header/catalog strings, and proprietary payload notes.");
            SupportedRow(html, "Legacy audio/session support", ".sd2, .bcw, .ses", "Sound Designer II audio/resource-fork notes, Barrcode AA broadcast-playout audio-resource markers, Cool Edit / Adobe Audition session identity, bounded visible strings, and privacy notes for session files that may reference local media paths.");
            SupportedRow(html, "Native Instruments", ".nki, .nkm, .nkb, .nkp, .nka, .nkr, .nkx, .nkc, .ncw, .nicnt, .nkl, .nksn, .nksf, .nksfx, .nksr, .ens, .ism, .rkplr, .kt3, .nbkt, .ksd, .nfm8, .nabs, .nmsv, .nrkt, .mxprj, .mxgrp, .mxsnd, .mxfx, .mxinst, .mprj, .mgrp, .msnd, .ngrr, .ndx", "Kontakt/NKS/Reaktor/Reaktor Player/Battery/FM8/Absynth/Massive/Maschine/Guitar Rig identity, primary product or host, extension role, visible product strings, metadata, and sample references.");
            SupportedRow(html, "Music projects and presets", ".als, .ablbundle, .abl, .ablpreset, .rpp, .rpp-bak, .ptx, WaveCache.wfm, .spn, .cpr, .npr, .all, .arr, .lso, .sxt, .chtr, .nac, .nov, .h0, .hdp, .ovm, .akp, .akm, .rbm, .mdd, .fxp, .fxb, .vstpreset, .wrk, .cwp, .logikcs, .loopparam, .progindex, .jgl, .fxl, .fsl, Yamaha S-YXG .tbl files, .mod, .xm, .s3m, .it, .mogg, .syx, QWS .ini/.lng support files", "Project/preset identity, visible names, tracker details, SysEx clues, DAW-specific markers, Pro Tools session/cache sidecars, Logic key-command exports, Reason NN-XT, GarageBand chord data, Native Instruments sample-add sidecars, MAGIX/SEK'D metadata, Akai sampler programs/multis, ReBirth mods, GN Audio embedded-MIDI containers, iKaossilator project/index sidecars, Roland Juno-G and Fantom Librarian data, Yamaha S-YXG table roles, and QWS instrument-map/transform/settings summaries.");
            SupportedRow(html, "Music software libraries", "Spitfire, XLN Audio, Spectrasonics, Korg, GForce M-Tron, Toontrack, Decent Sampler, Universal Audio LUNA, AIR Music Technology, Maize Sampler, Applied Acoustics Systems, Audio Modeling, UJAM, UJAM-style vendor blobs such as Crow Hill and Rhodes, Valhalla DSP, Modartt Pianoteq", "Product folder, library role, preset/bank names, visible metadata, package sizes, and clear notes when proprietary payloads are not decoded.");
            SupportedRow(html, "Mac and Apple packages", ".logicx, .logic, .band, .garageband, .sparsebundle, .DS_Store, iPhone/iPad backup folders, extensionless iOS backup payloads, .app, .framework, .bundle, .plugin, .appex, .xpc, .kext, .prefPane, .storyboardc, .momd, .rtfd, .component, .vst, .vst3, .clap, .aaxplugin, .ipa, .ipsw, .pkg, .car, .strings, .nib, .metallib, .sdef, .entitlements, .xcprivacy, .mobileconfig", "Package-folder identity, Info.plist fields, Finder folder metadata, project metadata, sparse-bundle details, mobile-backup manifests and hashed stored-file layout, app/archive structure, compiled UI/resource packages, scripting/privacy/entitlement files, and Apple resource hints.");
            SupportedRow(html, "Game, ROM, and game audio", ".nsf, .nes, .fds, .gb, .gbc, .gba, .nds, .sfc, .smc, .gen, .md, .sms, .gg, .n64, .z64, .vgm, .vgz, .sid, .s98, .rsn, .mini2sf, .wad, .chd, .pak, .vpk, .pk3, .pk4, .bsp, .mdl, .spr, .dem, .qwd, progs.dat/qwprogs.dat, .bpak, .bnk, .wem, .assets, .rpa, .acf, .vdf, .cue, .gdi, .sav, .srm, .ips, .bps, BBC Micro/BeebEm .ssd/.uef/PHROM files, MoonShell/R4 .msp/.mse/.b15/.u8m/.glf/.l2u files, Nintendo Switch content folders, .nca.CONCAT segments", "ROM headers, mapper/platform clues, game package markers, VGM/SID/S98/RSN/2SF music fields, Quake map/demo/model/sprite/program clues, BBC Micro emulator-media clues, Wwise/Unity/Ren'Py/Steam fields, MoonShell/R4 plugin/theme/support roles, Nintendo content-folder structure, and save/patch identity.");
            SupportedRow(html, "Spatial audio support files", ".mhr, .ambdec", "OpenAL Soft HRTF headers, AmbDec configuration descriptions, versions, channel masks, filename sample-rate hints, and clear notes that FileDentify does not render or validate spatial-audio filters.");
            SupportedRow(html, "Archives, installers, and disk images", ".zip-compatible containers, .rar, .7z, .gz, .bz2, .xz, .lz4, .zst, .lha, .lzh, .cab, .msi, .wim, .esd, .iso, .dmg, .nrg, .vhd, .vhdx, .vmdk, .vdi, .qcow2, VMware .vmx/.vmsd/.vmxf/.nvram/.scoreboard", "Container signatures, selected header fields, filesystem/image hints, VMware configuration/state summaries, and safety notes for suspicious mismatches.");
            SupportedRow(html, "Firmware, mobile, and device files", ".sis, .sisx, Symbian .app/.aif/.rsc/.mbm/.mif/.mdl, Java ME .jad/.jar MIDlets, .imy, .mmf, .pmd, .qcp, .mld, .mxmf, .xmf, .rmf, .amr, .ota, .rtttl, .rtx, BIOS/UEFI images, Android boot images, U-Boot uImage, UF2, TRX, DTB, Intel HEX, Motorola S-record, Synology DSM/SRM .pat files by wrapper, usb.ids/pci.ids, Roland SRX/SVD/SVQ/RFWV", "Installer/device headers, Symbian UID/resource clues, MIDlet manifest fields, ringtone and old phone-audio fields, XMF/RMF music-container hints, firmware markers, boot-image fields, router-firmware headers, Synology update-package wrapper clues, text firmware record summaries, hardware-ID database summaries, Roland identifiers, and visible package strings.");
            SupportedRow(html, "Personal data exports", ".vmg, .vcf, .ics, .opml, .eml, .wme, .wpl", "Nokia/Symbian message structure, vCard/contact counts, calendar/reminder counts, OPML subscription counts, email headers/MIME hints, Windows Media Encoder session fields, Windows Media Player playlist entries, and privacy notes.");
            SupportedRow(html, "Programming and app resources", ".apk, .nvda-addon, .asar, .nupkg, .pyc, .wasm, .pak, .msg, .plist, .json, .sqlite, .mdb, .ico, .pfb, .pfm, fonts, .dic, .aff, Chromium .hyb dictionaries, LibreOffice/OpenOffice .soc/.sod/.soe/.sog/.soh/.sor resources, liblouis .ctb/.utb/.uti/.cti/.dis/.tbl tables, Rockbox .rock plug-ins, MilkDrop .milk presets, Winamp .maki scripts and .w5s modules, Microsoft Chat .avb avatars, Piper/Sonata/SuperTonic .onnx voice models, Microsoft voice .msix/.bin/.dat/.ini/.xml/.txt files, AT&amp;T/IVONA/Loquendo voice packages, Dolphin Orpheus, application-map, default keyboard/language, and Nuance Vocalizer Expressive speech data, Eloquence/IBM TTS .syn modules, RHVoice voice.data/.fst/model files, FlexVoice .bin/.dat files, eSpeak NG dictionaries/phoneme data, Acapela .qvcu/.nuul216 voice data", "Bytecode/module/resource headers, Android and NVDA add-on package structure, Electron ASAR indexes, NuGet nuspec metadata, SQLite/plist/Jet database details, Windows icon embedded-image entries, Type 1 font support strings, dictionary and affix-rule summaries, browser and office support-resource roles, accessibility table metadata, legacy application resource roles, speech-voice metadata, and font/container signatures.");
            SupportedRow(html, "Installer support files", "InstallShield .inx/.hdr and old Windows setup-compressed SZDD files such as .ex_", "Installer support-file identity, header markers, visible setup strings, and a clear note that FileDentify does not run installers or expand payloads.");
            SupportedRow(html, "Backup and configuration files", "Audio Hijack .ah4session, Outlook Express .iaf, Synology .dss, router .cfg/.conf/.bin/.dat/.xml backups", "Session/config identity, binary-plist or compression markers, selected non-password fields, visible key names, and privacy notes for credential-bearing backups.");
            SupportedRow(html, "AI model files", "Ollama manifests, Ollama blobs, GGUF, PyTorch .ckpt/.pt/.pth checkpoints, SafeTensors .safetensors", "Model tag, manifest media type, referenced layers, GGUF version, tensor count, metadata count, PyTorch archive or pickle container hints, SafeTensors header size, and visible metadata keys.");
            SupportedRow(html, "Clipman and FileDentify", ".clipdb, Clipman settings, .fdreport", "Clipman container/settings metadata with protected password blobs redacted, and reopenable FileDentify report structure.");
            html.AppendLine("</tbody></table>");
        }

        private static void ReportSections(StringBuilder html)
        {
            html.AppendLine("<h2 id=\"report-sections\">Report sections</h2>");
            html.AppendLine("<table><thead><tr><th>Section</th><th>What it contains</th></tr></thead><tbody>");
            Row(html, "Summary", "Likely type, Unix file/libmagic result, path, size, extension, and timestamps.");
            Row(html, "Readable text", "Plain extracted strings without byte offsets, intended to be comfortable with screen readers. This appears near the top by default, after higher-priority safety or format-specific sections when those are present.");
            Row(html, "Signature matches", "Common first-byte or fixed-offset signature matches from the beginning of the file. If FileDentify identifies a file through its own database using extension, path, structure, or deeper sampled evidence, the FileDentify database section carries that result instead of showing a misleading no-match row here.");
            Row(html, "FileDentify database", "A source marker shown when FileDentify's own built-in file-type database identifies the file. This helps distinguish FileDentify-specific knowledge from Unix file/libmagic and generic signature evidence, and calls out extensionless files when header, path, filename, or structure clues were used.");
            Row(html, "Unix file/libmagic", "The embedded file/libmagic description, MIME result, and engine version.");
            Row(html, "Safety hints", "Warnings for obvious header and extension mismatches, with a cautious recommendation to scan and verify unexpected files before opening them directly.");
            Row(html, "Hashes", "SHA-256 and bounded hash information.");
            Row(html, "Filesystem", "Local file attributes and timestamps.");
            Row(html, "Clipman", "Clipman .clipdb and settings details. Encrypted CLIPDB2 databases are identified by container metadata only; FileDentify does not ask for passwords, decrypt history, or reveal protected remembered-password blobs.");
            Row(html, "Backup/config data", "Application, network-device, NAS, router, and mail-client backup/configuration files, including Audio Hijack sessions, Outlook Express IAF exports, Synology DSS backups, and router configs. Password-looking values are not shown in this section, and privacy warnings are surfaced when the file type commonly contains credentials.");
            Row(html, "Legacy sound bank", "Legacy .jlw and .vop sound-bank or voice-data files, including extension clues, size patterns, first big-endian words, readable strings, and notes when generic libmagic evidence appears misleading.");
            Row(html, "Ensoniq sampler", "Ensoniq EPS/ASR/TS files such as .efe, .eda, and .edt, including file role, header title, visible disk or instrument catalog strings, size, and a clear note that FileDentify does not mount or decode proprietary sample payloads.");
            Row(html, "Speech voice", "Piper/Sonata, SuperTonic, Microsoft natural voice/SAPI, AT&amp;T Natural Voices, IVONA, Loquendo, Dolphin Orpheus, Dolphin application/default data, Nuance Vocalizer Expressive, Eloquence/IBM TTS, RHVoice, FlexVoice, eSpeak NG, STSpeech, BestSpeech, tgSpeechBox, and Acapela voice files, including ONNX model role, voice folder, JSON voice configuration fields such as sample rate, quality, eSpeak voice, language, speaker counts where visible, Microsoft package/index/model roles and payload lists, voice archive payload sizes where ZIP indexes are readable, speech engine/add-on manifest details, Dolphin voice preference tables and sidecars, RHVoice HTS fields, eSpeak dictionary/phoneme roles, and Acapela voice-data role and size.");
            Row(html, "PDF", "PDF version, linearization/encryption hints, and sampled Info/XMP metadata.");
            Row(html, "Windows property metadata", "Useful Explorer property-handler metadata where Windows exposes it, such as title, authors, tags, comments, media fields, image fields, and document fields.");
            Row(html, "Rich Text Format", "RTF header details such as version, code page, default language, font/color table counts, and a safe plain-text preview. FileDentify does not render the document or execute embedded objects.");
            Row(html, "Dictionary / wordlist", "Hunspell/MySpell .aff data, plain .dic word lists, NVDA speech dictionaries, and Windows input-method dictionaries, including safe line counts, selected affix fields, preview lines, and a note that dictionary extensions are shared by many applications.");
            Row(html, "Database", "Microsoft Access/Jet database headers, version markers, and visible object/table names where present. FileDentify does not open tables, run queries, or extract records.");
            Row(html, "COM type library", "COM/OLE type-library markers from .tlb files, including wrapper and marker offsets. FileDentify does not register or load the library.");
            Row(html, "Office document metadata", "Word, Excel, and PowerPoint Open XML properties such as title, creator, dates, application, document statistics, embedded/media counts, and custom properties.");
            Row(html, "OpenDocument metadata", "OpenDocument metadata and statistics for formats such as .odt, .ods, and .odp.");
            Row(html, "EPUB ebook", "EPUB package metadata such as OPF path, title, creator, language, identifier, manifest count, reading-order count, and media/resource counts.");
            Row(html, "Ebook / help file", "Legacy ebook and help formats such as Microsoft Reader .lit, Mobipocket/Kindle .mobi, Palm/eReader .pdb/.prc, Sony .lrf, Braille Ready Format .brf, Comic Book .cbr/.cbz archives, and compiled HTML Help .chm files, including header markers, database names, creator/type fields, record counts, and safe text previews where available.");
            Row(html, "Android APK", "APK container details such as base/split kind, AndroidManifest presence, DEX count, native ABI folders, resource/asset entry counts, and package/split names inferred from backup filenames.");
            Row(html, "NVDA add-on", "NVDA .nvda-addon package details from manifest.ini, including add-on name, summary, description, author, version, URL, minimum NVDA version, last tested NVDA version, documentation file, update channel, package entry count, Python module count, and top-level package entries.");
            Row(html, "Developer/app resources", "Application support resources such as Electron ASAR archives, NuGet packages, Chromium hyphenation .hyb dictionaries, LibreOffice/OpenOffice .soc/.sod/.soe/.sog/.soh/.sor resources, and Winamp .w5s modules, including role clues and bounded metadata without executing code.");
            Row(html, "Accessibility data", "liblouis braille translation, display, contraction, and include tables such as .ctb, .utb, .uti, .cti, .dis, and context-proven .tbl files, including table title, included tables, opcode-line counts, and a note that FileDentify does not compile or validate the table.");
            Row(html, "FLAC, ID3, Ogg/Opus, RIFF, RIFF MIDI, AIFF, MIDI, Sun/NeXT AU, MPEG transport stream, ISO base media", "Built-in audio/media structure and metadata where available, including OpusHead/OpusTags fields, AU sample rate/channel/encoding fields, RMID-wrapped MIDI header details, and Blu-ray .m2ts packet-size/sync details.");
            Row(html, "Windows Media", "ASF, WMA, and WMV container headers, common object markers, and a note that ffprobe can provide codec, duration, bitrate, and tag details when available.");
            Row(html, "MPEG audio", "Raw MPEG audio streams such as .mp2, .mpga, or extensionless ID3-tagged MP3 files, including first-frame offset, bitrate, sample rate, and estimated duration where possible.");
            Row(html, "Legacy audio resource", "Older or proprietary audio-resource files such as Sound Designer II .sd2 and Barrcode AA .bcw files. Barrcode AA is reported as a likely broadcast playout audio resource from Barrcode's UK radio/TV automation ecosystem. FileDentify reports extension/header markers and safe byte evidence, with clear notes when metadata may live outside the Windows-visible file or the payload is proprietary.");
            Row(html, "Audio session", "Cool Edit / Adobe Audition .ses session files, including format identity, size, and bounded visible strings where present. Local media paths may appear in sessions, so review reports before sharing.");
            Row(html, "SoundFont / SBK", "SoundFont and old Sound Blaster / E-mu SBK bank metadata from RIFF INFO chunks, visible preset names, and visible preset/instrument/sample header counts where available.");
            Row(html, "sfArk SoundFont archive", "Legacy compressed SoundFont archives, including visible sfArk version strings and target .sf2 names where present.");
            Row(html, "DLS instrument bank", "Downloadable Sounds RIFF bank metadata including collection instrument count, DLS version, and visible instrument/region/wave-sample chunk counts.");
            Row(html, "Creative ECW waveset", "Creative/E-mu ECW wavetable bank marker, visible name, internal filename, and copyright where present.");
            Row(html, "Media details", "A concise ffprobe summary when ffprobe.exe is beside FileDentify and probing succeeds.");
            Row(html, "QuickTime metadata", "MOV/QuickTime tags from ffprobe when available, surfaced with friendly labels such as camera make, camera/device model, software, app, device, copyright, location, and creation date.");
            Row(html, "Mobile phone tone", "Old mobile ringtone and phone-audio clues for iMelody .imy, Yamaha SMAF/MMF .mmf, Qualcomm CMX/PMD .pmd, AMR, RTTTL, RTX, and Nokia OTA-style files where the bytes expose useful fields.");
            Row(html, "Game/ROM data", "Header-level details for game and emulator files such as NSF, iNES/NES, Game Boy, GBA, Nintendo DS, SNES, Genesis/Mega Drive, Sega Master System/Game Gear, Nintendo 64, Doom WAD, CHD, Quake PACK/BSP/demo/model/sprite/progs.dat, BBC Micro/BeebEm .ssd/.uef/PHROM media, protected .bpak packages, Wwise BNK/WEM, Unity assets, Ren'Py archives, Steam ACF/VDF, generic .rom files, game saves, disc layout files, and common patch/package extensions.");
            Row(html, "Gravis Ultrasound patch", "GF1PATCH110 Gravis UltraSound .pat instrument/sample files, including manufacturer ID, description, instrument count, voice/channel fields, waveform count, and master volume where present.");
            Row(html, "Legacy music/game audio", "Older game, mobile, MIDI, and audio-production formats such as VGM/VGZ, SID, S98, RSN, mini2SF, QCP, XMF/MXMF, MFi/MLD, Beatnik RMF, QSEQ DOS MIDI sequencer files, Recomposer RCP/HED/G36, WRD/LYC/MAG/ZEL/GMC sidecars, Cubase/Sound Forge waveform overview files, microtuning .tun maps, SFI/SFIP impulse data, and raw .sam samples.");
            Row(html, "Nintendo Switch content", "Nintendo content folders, including registered .nca.CONCAT package and segment counts, save-file counts, album media counts, largest sampled package folders, and a clear note that FileDentify reports structure and sizes only.");
            Row(html, "MoonShell/R4", "Nintendo DS MoonShell/R4 support files such as .msp plugins, .mse support files, .b15 theme bitmaps, .u8m UI sounds, .glf bitmap fonts, and .l2u language/support files, including role, header marker, visible plugin strings, dimensions where obvious, and a note that DS code and custom assets are not executed or rendered.");
            Row(html, "Native Instruments", "Kontakt, NKS, Reaktor/Reaktor Player, Battery, FM8, Absynth, Massive, Maschine, Guitar Rig, Kontour, and related NI extension hints, including primary product or host, extension role, user-context notes, visible product strings, header markers, .nicnt metadata, and sampled instrument or sample references.");
            Row(html, "Apple bundle", "macOS application, framework, plug-in, app extension, XPC service, audio or hardware driver, kernel extension, and preference-pane Info.plist metadata such as display name, identifier, executable, version, package type, minimum macOS, SDK, and principal class.");
            Row(html, "Apple Finder metadata", "macOS .DS_Store files, including Finder metadata marker, folder path, and safe header fields.");
            Row(html, "Apple localization / asset resources", "Apple .strings localization files, compiled .car asset catalogs, Interface Builder .nib resources, configuration profiles, and XAR-based .pkg installer package headers.");
            Row(html, "Apple firmware package / iOS application archive", "IPSW and IPA ZIP-family packages, including IPSW BuildManifest.plist/Restore.plist presence, disk images, firmware entries, IPA payload Info.plist path, framework counts, and app extension counts.");
            Row(html, "Logic Pro / GarageBand project package", "Logic and GarageBand package folders such as .logicx, .logic, .band, and .garageband, including package structure, ProjectInformation.plist and MetaData.plist presence, ProjectData size, visible musical fields, and bundled audio-file names where available.");
            Row(html, "Logic Pro", "Logic Pro key-command exports such as .logikcs, including property-list structure and visible key counts where available.");
            Row(html, "Pro Tools", "Pro Tools .ptx sessions, WaveCache.wfm files, and .spn waveform overview sidecars, including visible session/cache strings, project folder clues, file size, and a note that proprietary session and waveform payloads are not decoded.");
            Row(html, "Apple sparse bundle", "Time Machine and other .sparsebundle package folders, including Info.plist sparse-bundle fields, virtual size, band size, sampled band counts, and Time Machine metadata-file presence.");
            Row(html, "Apple mobile backup", "iPhone and iPad backup folders, including Manifest.db, Manifest.plist, Info.plist, Status.plist, hashed shard-folder counts, sampled stored-file counts, largest sampled payloads, and privacy notes.");
            Row(html, "Apple mobile backup file", "Extensionless 40-character stored files inside Apple mobile backups, including backup identifier, shard folder, file ID, Manifest.db lookup hint, likely payload type, and privacy note.");
            Row(html, "Mac audio plug-in", "Audio Unit, VST, VST3, CLAP, AAX, and AU preset bundle hints, including Info.plist display name, bundle identifier, executable, version, minimum macOS, SDK, and Xcode fields where present; VST3 moduleinfo.json name, vendor, class, category, compatibility, and SDK metadata where present.");
            Row(html, "Audio sample resource", "Arturia .arta/.astr/.eiiwav, ROLI .roliaudio, Initial Audio .ignitex, Guitar Rig .grir, Apple .caf/.sdir, Scala .scl, and wavetable .wt files, including product/vendor context, library folder, role, CAF chunk markers, scale text, visible strings, and proprietary-payload notes.");
            Row(html, "OpenAL spatial audio", "OpenAL Soft .mhr HRTF files and .ambdec ambisonic decoder presets, including header/configuration clues, descriptions, versions, channel masks, filename sample-rate hints, and a note that FileDentify does not render HRTF filters or validate decoder matrices.");
            Row(html, "Production audio resource", "Older DAW, sampler, and sample-CD support files such as Reason NN-XT .sxt, classic Logic .lso, GarageBand .chtr, Native Instruments .nac/.nov, MAGIX/SEK'D .h0/.hdp/.ovm, Akai .akp/.akm, Propellerhead ReBirth .rbm, and GN Audio .mdd files, including container markers, file roles, visible names, sample references, and embedded MIDI clues where present.");
            Row(html, "Roland Cloud", "Roland Cloud expansion and preset files such as .exz, VEXP headers, KoaBankFile preset banks, Preset.bin, and InstalledBankNames.dat, including product folder, expansion code/name, visible bank name, and installed bank strings where available.");
            Row(html, "XLN Audio", "XLN Audio .xpak sample packs and InstalledBankNames.dat files, including product, pack folder, pack code/name, file size, and installed bank names where visible.");
            Row(html, "Spectrasonics", "Spectrasonics STEAM/SAGE files, including .db sample containers with readable FileSystem indexes, Omnisphere/Keyscape/Trilian/Stylus RMX multi and preset extensions, product/family inference, indexed file names and sizes, and visible XML module types where available.");
            Row(html, "Korg", "Korg sample-library and wavestate data, including WaveMotion .wmss files, extensionless Korg objects, ADSR, voice amp, pitch, arpeggiator, vector-envelope, database, product folder, role, and visible object markers where available.");
            Row(html, "iKaossilator", "Korg iKaossilator project and index sidecars such as .loopparam and .progindex, including role, file size, and visible referenced loop names where present.");
            Row(html, "GForce M-Tron", "GForce M-Tron .cpt2 tape banks, including product folder, library folder, bank name, size, and a clear note that the sample payload is not unpacked.");
            Row(html, "Toontrack", "Toontrack .obw sound-library banks and related text metadata, including product folder, role, RIFF/container clues, visible kit, microphone, articulation, preset, or sound-stat entries where available.");
            Row(html, "Decent Sampler", "Decent Sampler .dspreset files, including library folder, preset name, sample-reference count, sample paths, note ranges, groups, controls, and UI image or size metadata where present.");
            Row(html, "Universal Audio LUNA", "Universal Audio LUNA .lunacomponent folders and related .cir, .cmr, .rev, .dat, .bin, and .json files, including component name, role, bounded package/file sizes, largest sampled internal files, and visible metadata where present.");
            Row(html, "AIR Music Technology", "AIR Music Technology Structure and Transfuser files, including .big content archives with visible embedded paths and .patch XML files with visible parts, part types, sample references, and root element.");
            Row(html, "Maize Sampler", "Maize Sampler .mse exported instruments used by several sample-library developers, including header marker, library folder, instrument name, file size, and visible instrument/vendor text where available.");
            Row(html, "Applied Acoustics Systems", "Applied Acoustics Systems banks, packs, presets, GUI files, and Lua/resource bundles, including product folder, item name, visible preset metadata, version/engine fields, name-field counts, and resource notes.");
            Row(html, "Audio Modeling", "Audio Modeling and SWAM NKS presets, preview audio, metadata, and artwork, including product folder, role, file size, and visible metadata where available.");
            Row(html, "UJAM", "UJAM content blobs, preset patches, NKS files, settings, and metadata, including product folder, role, file size, leading UUID for blob payloads, preset names, build metadata, and DSP setting counts where visible.");
            Row(html, "UJAM-style blob", "UJAM-style .blob payloads used by other sample-library vendors such as Crow Hill and Rhodes, including vendor folder, product folder, file size, leading UUID where visible, and a proprietary-payload note.");
            Row(html, "Valhalla DSP", "Valhalla DSP .vpreset files, including product folder, preset folder, preset name, plug-in version, and selected visible parameters.");
            Row(html, "Modartt Pianoteq", "Modartt/Pianoteq add-on packages, presets, and preferences, including VST chunk marker, plug-in id, preset name, visible instrument/version text, and package notes.");
            Row(html, "AI model / Ollama", "Ollama manifests and content-addressed blobs plus GGUF, PyTorch checkpoint, and SafeTensors model files, including model tag, manifest media type, referenced layers, model family/type, GGUF version, tensor count, metadata key count, PyTorch archive/pickle hints, SafeTensors header size, and visible metadata keys.");
            Row(html, "Spitfire Audio", "Spitfire Audio library files such as .spitfire, .zmulti, .zpreset, .zconfig, .lm, .db, and NKS presets, with inferred library name, folder role, version folder, SQLite catalogue hints, header markers, and visible sample/library strings where available.");
            Row(html, "Steinberg Cubase", "Cubase, Nuendo, and VST-related extension hints plus readable project clues such as ASIO/VST markers, MIDI edit commands, drum-map names, effects, grooves, fonts, internal markers, and VST FXP/FXB header fields where present.");
            Row(html, "REAPER project", "REAPER .rpp and .rpp-bak text project files, including header/version, timestamp, tempo, sample rate, track/item/take counts, render target, plug-in markers, and sampled media references where present.");
            Row(html, "Cakewalk project", "Cakewalk WRK and Cakewalk/Sonar CWP project hints, including visible Cakewalk/Sonar markers, version strings, audio-driver clues, and sampled sample/SoundFont references.");
            Row(html, "Ableton", "Ableton bundle, song, and preset details, including ZIP entry counts, JSON schema/kind fields, song tempo, scale, melodic layout, and track counts where available.");
            Row(html, "Synth preset", "Helm synthesizer presets with patch name, category, author, synth version, setting count, and selected musical settings.");
            Row(html, "Surge wavetable", "Surge wavetable files identified by the vawt marker, with header bytes shown for review without pretending to render oscillator frames.");
            Row(html, "Neural Amp Modeler", "NAM model JSON details such as model format version, architecture, sample rate when present, and layer count where available.");
            Row(html, "Drum preset", "Microtonic-style drum preset text fields such as oscillator wave/frequency/decay, noise filter mode, level, and parameter count.");
            Row(html, "Chord preset", "Text chord preset name, chord count, and first chord rows.");
            Row(html, "MIDI System Exclusive", "SysEx dump summaries including visible message count, manufacturer, Roland device/model/command fields where applicable, and visible text.");
            Row(html, "QWS sequencer", "Quick Windows Sequencer support files, including instrument definition names and patch counts, note-transform counts, language prompt counts, and selected settings such as MIDI ports. Recent file paths in qws.ini are counted but not printed.");
            Row(html, "Roland sound data", "Roland SVD sound/backup data and Roland Juno-G Librarian .jgl patch/library files, including SVD markers, visible chunk table entries, Juno-G header marker, visible patch or library names, and a note that FileDentify does not send SysEx or write to hardware.");
            Row(html, "Roland Fantom Librarian", "Roland Fantom-S and Fantom-X Librarian .fsl/.fxl files, including header markers, target family, file role, visible patch or library names, and a note that FileDentify does not decode synth parameters, send SysEx, or write to hardware.");
            Row(html, "Yamaha softsynth", "Yamaha S-YXG software-synthesizer .tbl table files, including table role, clear markers such as UTG VPRM, Yamaha/S-YXG path evidence where needed, visible strings, and a note that FileDentify does not decode or load synthesizer payloads.");
            Row(html, "Roland sequencer song", "Roland SVQ sequencer song marker, visible song name, and header size/offset fields.");
            Row(html, "Roland sample data", "Roland FA sample waveform marker, payload size field, sample rate, channel count, and format-like fields.");
            Row(html, "LHA archive", "LHA/LZH archive method and first entry name, useful for old Amiga, DOS, and module-collection archives.");
            Row(html, "Tracker module", "MOD, XM, S3M, and IT tracker module hints such as title, signature, song length, and tracker-specific header fields where available.");
            Row(html, "MOGG multitrack audio", "MOGG multitrack Ogg hints, including Ogg payload offset and prefix size.");
            Row(html, "Sampler instrument", "SFZ and Logic EXS sampler instrument hints, including SFZ region counts and visible referenced sample names.");
            Row(html, "FMOD bank", "FMOD bank hints for RIFF/FEV-style game-audio banks.");
            Row(html, "Wwise media", "Wwise WEM media hints, including RIFF/WAVE framing, channels, sample rate, and format code where visible.");
            Row(html, "Python bytecode", "PYC magic number, flags, timestamp/hash invalidation mode, source timestamp, and source size where available.");
            Row(html, "WebAssembly", "WASM version and visible section names and sizes.");
            Row(html, "Chromium resource pack", "Chromium/Electron .pak version, text encoding, resource count, and alias count where available.");
            Row(html, "Developer/app resources", "Electron ASAR archives and NuGet packages, including ASAR header JSON size, top-level entries, file/directory counts, package.json presence, NuGet nuspec id/version/authors/description, dependency count, and lib/content entry counts where available.");
            Row(html, "Legacy app/plugin resource", "Rockbox .rock plug-ins, MilkDrop .milk visualisation presets, Winamp Modern skin .maki scripts, Microsoft Chat Comic Art .avb avatars, and HAL speech mapping data, including visible markers, roles, sample settings or strings, and a note that FileDentify does not execute legacy plug-ins, scripts, avatars, or mappings.");
            Row(html, "MSG structured storage", "Outlook or installer/resource message container hints when .msg files use OLE structured storage.");
            Row(html, "Microsoft Cabinet archive", "CAB and Windows setup-compressed files such as .DL_, .EX_, and .SY_, including cabinet version, file/folder counts, flags, set/index fields, and visible stored file names.");
            Row(html, "Windows imaging", "WIM and ESD deployment images, including header size, raw version, image count, part count, compression chunk size, decoded compression flags, and common use.");
            Row(html, "Nero disc image", "Nero Burning ROM .nrg disc images, including footer marker, chunk table offset, and visible trailer chunks where present.");
            Row(html, "Symbian package", "Symbian SIS/SISX installer headers, including package UID fields, UID checksum, selected header fields, zlib stream hint, and visible package strings.");
            Row(html, "Symbian app/resource", "Installed Symbian OS application and resource files such as .app, .aif, .rsc, .mbm, .mif, and .mdl, including folder role, UID-like header fields, item name, and visible strings where useful.");
            Row(html, "Java MIDlet", "Java ME MIDlet .jad descriptors and installed MIDlet JARs, including name, version, vendor, JAR path/size, Java ME profile/configuration, and listed MIDlet entries where present.");
            Row(html, "Firmware / device image", "PC BIOS/UEFI firmware, Android boot images, U-Boot legacy uImage files, UF2 microcontroller firmware, Broadcom/OpenWrt TRX router firmware, flattened device tree blobs, Intel HEX, Motorola S-records, Synology DSM/SRM PAT update packages, Roland expansion data, file size, visible firmware strings, header fields, record summaries, and a clear no-flash/no-modify note.");
            Row(html, "Hardware ID database", "usb.ids and pci.ids style hardware identifier databases, including version/date comments, sampled vendor/device/interface/class counts, and example entries.");
            Row(html, "Message/contact data", "Personal/export formats such as Nokia/Symbian VMG messages, vCard contacts, iCalendar calendars/reminders, OPML subscription lists, EML email messages, Windows Media Encoder sessions, and Windows Media Player playlists. These sections summarize structure and key metadata and include privacy notes because the files often contain personal addresses, phone numbers, locations, message bodies, URLs, local paths, or device paths.");
            Row(html, "Windows/system data", "Windows system and application-support files such as EVTX logs, PNF/INF localization data, GPD/GDL printer descriptions, XRM-MS licensing XML, SDB compatibility databases, catalog databases, AppX/MSIX packages, legacy .fon bitmap font libraries, gettext/Qt translation catalogs, boot SDI ramdisk images, network config files, instrumentation manifests, and application manifests. These are reported from headers, filenames, and safe text/XML/package structure only.");
            Row(html, "Installer data", "InstallShield and old Windows setup support files such as setup.inx, .hdr cabinet/header files, and SZDD-compressed underscore files. The section reports magic markers, role, visible setup strings, and header clues without executing installers or expanding payloads.");
            Row(html, "Virtual machine metadata", "VMware configuration and state files such as .vmx, .vmsd, .vmxf, .nvram, and .scoreboard, including selected non-identity fields such as display name, guest OS hint, virtual hardware version, memory, CPU count, controller/disk/network counts, and role notes. UUIDs, MAC addresses, and full host paths are not shown in this section.");
            Row(html, "UFS sample library container", "UFS marker and extension details for UVI/Falcon-style sample-library containers.");
            Row(html, "Binary blob", "Generic .blob extension details without assigning vendor ownership; product-specific sections such as UJAM or UJAM-style blob provide vendor context when the path or metadata supports it.");
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
            html.AppendLine("<li>Tree section order is stored in <code>FileDentify.ini</code> after using <code>Ctrl+Up</code> or <code>Ctrl+Down</code> on report sections.</li>");
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
            Credit(html, "Tolk screen-reader library", "https://github.com/dkager/tolk", "and NVDA controller client", "https://github.com/nvaccess/nvda", "for optional screen-reader announcements");
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
            html.AppendLine("<p>The main FileDentify application is released under the MIT License. Bundled third-party components have their own notices under Help &gt; Third-party notices.</p>");
        }

        private static void Link(StringBuilder html, string id, string text)
        {
            html.AppendLine("<li><a href=\"#" + Html(id) + "\">" + Html(text) + "</a></li>");
        }

        private static void Row(StringBuilder html, string left, string right)
        {
            html.AppendLine("<tr><td><code>" + Html(left) + "</code></td><td>" + Html(right) + "</td></tr>");
        }

        private static void SupportedRow(StringBuilder html, string family, string examples, string details)
        {
            html.AppendLine("<tr><td>" + Html(family) + "</td><td><code>" + Html(examples) + "</code></td><td>" + Html(details) + "</td></tr>");
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

