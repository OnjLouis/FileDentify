# FileDentify Smoke Test

This is the working smoke-test checklist for FileDentify. Use it before replacing a local installed copy, packaging a release ZIP, or publishing to GitHub.

## Scope

FileDentify is a portable, keyboard-first WinForms file identification utility. The source lives in the repository checkout.

The SendTo Project package should contain the GUI executable and console companion:

```text
encoders\FileDentify.exe
encoders\fd.com
```

Set `FILEDENTIFY_PACKAGE_DIR` and `FILEDENTIFY_INSTALL_DIR` when a local machine needs build output copied to specific folders.

## Build

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\Build.ps1
```

Expected:

- `FileDentify.exe` exists in the configured package output folder.
- `fd.com` exists in the configured package output folder.
- The configured local installed copy is updated when `FILEDENTIFY_INSTALL_DIR` exists.
- Old-cased `Filedentify.exe` is not left beside `FileDentify.exe`.
- Build fails hard if `csc.exe` fails.
- If the installed copy is running or locked, the package output is still built and the script prints a warning instead of deleting the installed executable.
- Embedded `file`/libmagic resources are present under `third_party\libmagic\extracted`; the build fails if any required resource is missing.
- Embedded `file.exe --version` reports the intended libmagic version, currently `file-5.48`.
- Embedded Tolk resources are present under `third_party\tolk`; the build fails if `Tolk.dll`, `nvdaControllerClient64.dll`, or `Tolk.LICENSE.txt` is missing.
- Build compiles all `.cs` files under `src`; the source is intentionally split by responsibility rather than kept in one monolithic file.

## Release Gate: GitHub And Community

Before publishing, run the GitHub issue and pull request checks:

```powershell
gh issue list --repo OnjLouis/FileDentify --state open
gh pr list --repo OnjLouis/FileDentify --state open
```

Then run the community search:

```powershell
powershell -ExecutionPolicy Bypass -File .\CommunitySearch.ps1
```

Expected:

- GitHub issue and PR checks complete. If they fail, stop the release until GitHub can be checked.
- Open issues and PRs are reviewed before publishing. Fix, explicitly defer, or ask Andre before release.
- `CommunitySearch.md` is written as a generated checklist and is not committed.
- GitHub issue and PR searches in `CommunitySearch.md` complete or report a clear API/search error.
- The checklist includes exact-name searches for `FileDentify`, `OnjLouis/FileDentify`, accessibility terms such as NVDA/JAWS/screen reader, SendTo, libmagic, and public forum/community sites.
- Review public/community feedback before release, especially accessibility complaints, unsupported common formats, report readability problems, terminal-mode friction, update issues, and safety/privacy concerns.
- This gate is mandatory for FileDentify releases, release-asset refreshes, and hotfixes. Do not ship first and check afterward.

## Version Metadata

Check:

```powershell
$v = [System.Diagnostics.FileVersionInfo]::GetVersionInfo('<path-to-built-FileDentify.exe>')
$v | Format-List ProductName,FileDescription,CompanyName,LegalCopyright,FileVersion,ProductVersion
```

Expected:

- ProductName: `FileDentify`
- FileDescription: `FileDentify`
- CompanyName: `Andre Louis`
- LegalCopyright: `Copyright (c) Andre Louis`
- FileVersion and ProductVersion match the intended release.

## No-Argument Launch

Run:

```powershell
Start-Process <path-to-built-FileDentify.exe>
```

Expected:

- Main window opens as `FileDentify <version>`.
- No startup error dialog appears.
- Empty state is useful, with Open files and Preferences reachable.
- Opening the graphical app with a folder path, including from Send To, shows a finding/generating progress state and recursively scans files in that folder instead of showing a blank window.
- Opening the graphical app with an empty folder path shows a clear no-files message in the tree and status area.
- The first useful focus target is not a decorative label or layout container.
- Main button row includes Close, reachable by Tab, and Escape closes the main window.

## Keyboard And Screen Reader

Manual NVDA checks:

- Tab order reaches only useful controls.
- The tree view announces useful node names.
- F1 and Help > Help open the built-in HTML manual in the default browser.
- The manual includes headings, keyboard shortcut tables, command-line usage, accessibility notes, and a changelog.
- Ctrl+O opens the file picker. If a report is already loaded, it should warn that opening new files will replace the current report and offer Save, Don't Save, and Cancel behavior.
- Ctrl+Shift+L opens a folder picker and recursively inspects the selected folder into one combined report.
- Ctrl+Shift+O opens the file picker to append files to the current report without clearing already loaded files. Duplicate already-loaded files should be skipped rather than duplicated.
- Ctrl+R opens a saved FileDentify `.fdreport` file.
- Ctrl+Shift+T reopens the automatically saved last report when it exists.
- Ctrl+S opens Save report when a report is loaded, defaults to `FileDentify report (*.fdreport)`, and reports that no report is available when empty.
- F5 refreshes original files when a live or reopened report has available original paths. If no original paths exist, or none are available on this machine, FileDentify reports that clearly. If only some files are available, it reports the missing count and refreshes the available files.
- Alt+V / View HTML report opens a temporary combined HTML report in the default browser when a report is loaded, and reports that no report is available when empty.
- Main-window action mnemonics do not collide with top-level menus. Open files uses Ctrl+O/File > Open files, Open folder uses Ctrl+Shift+L/File > Open folder, Append files uses Ctrl+Shift+O/File > Append files to report, Help uses F1/Help > Help, and Open containing folder uses Alt+L.
- Main-window buttons and dialog buttons expose shortcut keys through accessibility keyboard-shortcut metadata, not by adding visible text or verbose accessible descriptions. For example, Close exposes `ESC`, Open files exposes `Ctrl+O`, Save report exposes `Ctrl+S`, Copy report exposes `Alt+C`, and View HTML report exposes `Alt+V`.
- F4 opens the advanced file viewer for the selected file. Alt+F4 still closes the current window normally.
- Advanced file viewer opens maximized, focuses directly on the output field at the top of the loaded text, and supports Text, Hex, Binary, and Octal radio-button modes; Ctrl+F focus/select search; F3 next search; Shift+F3 previous search; Ctrl+L load more; Ctrl+Shift+L load all; Ctrl+A; Ctrl+C; Save loaded output; and Escape to close. Scrolling or pressing Down at the loaded end loads one more chunk, places review at the start of the newly loaded chunk, and does not jump to the end of that chunk or the whole file.
- Advanced file viewer opened with F4 from the main window is owned by FileDentify and should not appear as a separate useful Alt+Tab target. `FileDentify.exe --advanced-view file` may appear as its own target because it has no main window behind it.
- Advanced file viewer remembers mode, loaded amount, search text, and review position for the same loaded file while the main FileDentify session remains open. Escape out, press F4 again, and confirm the previous place is restored.
- Advanced file viewer status is exposed through a native bottom-docked status bar. NVDA's status command should read the current line first, followed by loaded-data and mode details.
- Advanced file viewer search does not crash when the current selection is at or beyond the end of the loaded output. F3 does not silently wrap to the top; if there is no later loaded match, the caret stays where it is and status tells the user to use Shift+F3, or load more and press F3. Shift+F3 similarly reports no previous match instead of silently wrapping to the bottom.
- Command line advanced viewer output works with `--viewer-output`, `--viewer`, `--viewer-mode readable|hex|binary|octal`, and `--viewer-bytes`.
- Short command-line aliases work, including `-r`, `-hr`, `-fr`, `-av`, `-vo`, `-vw`, `-vm`, `-vb`, `-t`, `-c`, `-u`, `-is`, `-us`, `-id`, `-ud`, `-v`, and `-h`.
- `FileDentify.exe --advanced-view file` and `FileDentify.exe -av file` open the graphical advanced viewer directly.
- `fd.com --report output.txt input1 input2` and `fd.com --viewer-output output.txt input1` refuse to write output over an input file. Console companion errors print to stderr and exit instead of opening a GUI error window.
- Ctrl+C from the tree copies the selected node's details, not the full report.
- Ctrl+C from the details box copies selected text when text is selected, otherwise the current details text.
- Ctrl+A selects all text in read-only edit fields such as details, notices, version history, and update notes.
- Ctrl+Shift+Left collapses the report tree, and Ctrl+Shift+Right expands it.
- Ctrl+Up and Ctrl+Down on a report section move that section earlier or later in the saved section order. Summary remains pinned first, Report overview remains pinned above files, and file order remains natural.
- Ctrl+0 through Ctrl+9 jump through the first ten sections in the current file. Ctrl+Shift+0 through Ctrl+Shift+9 jump through sections 10 to 19 in the current file. Top-level file nodes and Report overview are not assigned Ctrl+number shortcuts.
- If focus is already in the details edit field, Ctrl+number shortcuts should refresh the selected report section/details content but keep focus in the details edit field.
- When a file root is selected, Ctrl+0 jumps to that file's Summary. In a multi-file report, selecting file 5 and pressing Ctrl+0 should jump to file 5's Summary. Report overview has no Ctrl+number shortcut; use Home or normal tree navigation to reach it.
- Alt+Up and Alt+Down move through the current review flow. From Report overview, Alt+Down should enter the first file overview and Alt+Up should report Start of report. From the first file overview, Alt+Up should return to Report overview. From a file overview, Alt+Down should enter the first section. From the first section, Alt+Up should return to that file's overview. If focus is already in the details edit field, the selected details should refresh but focus should stay in the details edit field.
- Alt+Home and Alt+End jump to the first or last report section in the current file.
- Alt+Left and Alt+Right jump to the previous or next file in a multi-file report. If a report section such as Readable text is selected, the destination file should keep that same section selected when it exists; otherwise it should fall back to Summary. If focus is in the details edit field, the file changes and details refresh, but focus stays in the details edit field.
- Alt+PageUp and Alt+PageDown jump to the first or last file in a multi-file report. The destination file should keep the same section selected when it exists; otherwise it should fall back to Summary.
- Alt+1 through Alt+0 jump directly to files 1 through 10 in a multi-file report, with Alt+0 meaning file 10. These shortcuts should keep the same report section selected where possible, fall back to Summary when needed, and report a clear no-file message when the requested file number does not exist.
- With NVDA running, arrowing through the tree should let NVDA speak the tree item first, then FileDentify should politely announce only the current shortcut through embedded Tolk, such as `Ctrl+9`, without repeating the item name and without requiring a loose Tolk.dll beside FileDentify.exe.
- File menu exposes report output actions such as Save report and View HTML report. Edit menu exposes copy and tree expand/collapse actions.
- Report-tree context menu exposes copy, expand/collapse, section move, View HTML report, and Save report actions.
- Application key or Shift+F10 on the tree opens the report-tree context menu.
- The Copy report button copies the full report.
- Ctrl+comma opens Preferences.
- Shift+F1 checks for updates.
- Ctrl+F1 opens the GitHub project page.
- Help > Contact opens `https://onj.me/contact`.
- Help > Donate opens `https://onj.me/donate`.
- Help > Other software opens `https://onj.me/software`.
- Help > Third-party notices opens a selector for individual notices. Tab moves from the notice selector into a multiline read-only text field with preserved line breaks; Shift+Tab returns to the selector. It includes the file/libmagic, libsystre, libtre, gettext/libintl, and libiconv notices.
- Preferences has useful Automation and Updates tabs.
- Preferences can be closed with Escape or Cancel.

## Command Line

Run:

```powershell
$report = Join-Path $env:TEMP 'FileDentify-report-smoke.txt'
Remove-Item -LiteralPath $report -Force -ErrorAction SilentlyContinue
Start-Process -FilePath <path-to-built-FileDentify.exe> -ArgumentList @('--report', $report, '<path-to-built-FileDentify.exe>') -Wait
Get-Content -LiteralPath $report -TotalCount 80
```

Expected:

- Report file is created.
- No UI remains open.
- Report contains `Windows executable or DLL`.
- Report contains `Unix file/libmagic`.
- Signature section does not duplicate identical text.
- Report contains `Windows executable` details.
- Report contains `Version information`.
- Report does not contain useless failed `ffprobe` output for `.exe` files.
- Command-line report generation does not leave `FileDentify-startup-error.txt` beside the executable.
- Command-line report generation cleans its per-process `%TEMP%\FileDentify-libmagic-*` folder after exit.
- Command-line report generation exits after completion and does not leave an empty FileDentify window running.
- Saving or writing a report to `.html` creates one combined HTML document with per-file headings and section tables.
- Saving or writing a report to `.fdreport` creates a native FileDentify report that reopens in the app with the same file tree, report overview, sections, and details.
- Opening a `.fdreport` from File > Open FileDentify report, command line, or Windows association loads the saved tree as static report data rather than treating the report file itself as the inspected target.
- Inspecting a `.fdreport` through Open files, Send To, or folder scanning is safe. It should identify the file as a FileDentify saved report, show a FileDentify saved report section, and not crash or recursively reopen itself unexpectedly.
- Automatic last-report recovery writes `FileDentify.fdreport` beside the app when enabled and makes it available from File > Reopen last report, Ctrl+Shift+T, and File > Open recent items.
- Saved `.fdreport` files remember the selected report overview, file, and section where possible when reopened.
- Combined reports with more than one file start with a Report overview covering counts, generation time, total size, likely types, extensions, largest files, common bytes, common readable strings, signature-match counts, and attention files.
- `FileDentify.exe --close` asks other FileDentify windows from the same executable to close gracefully.
- `fd.com -u`, `FileDentify.exe -u`, and `FileDentify.exe --update` check GitHub Releases and report either current/new version status or a clear pre-release/network message.
- `FileDentify.exe --install-report-association` / `-ir` and `--uninstall-report-association` / `-ur` install and remove the per-user `.fdreport` association without elevation.

Folder report mode:

```powershell
$folderReport = Join-Path $env:TEMP 'FileDentify-folder-report-smoke.txt'
Remove-Item -LiteralPath $folderReport -Force -ErrorAction SilentlyContinue
$args = '--folder-report "{0}" "{1}"' -f $folderReport, "$env:PUBLIC\Desktop"
Start-Process -FilePath <path-to-built-FileDentify.exe> -ArgumentList $args -Wait
Get-Content -LiteralPath $folderReport -TotalCount 80
```

Expected:

- Folder report file is created. Using `.html` for the output path creates an HTML folder report.
- Report contains one top-level report per readable file in the folder.
- Inspection errors, if any, are contained as report entries rather than crashing the run.

Terminal mode:

```powershell
<path-to-fd.com> <path-to-built-FileDentify.exe>
```

Expected:

- Interactive terminal mode shows a page of report text with a footer.
- Run terminal mode through `fd.com` in PowerShell or Windows Terminal so the existing terminal gives FileDentify keyboard control.
- `fd.com` and `FileDentify.exe` must be in the same folder.
- Running `FileDentify.exe -t` is not a supported terminal route; it should show command-line help instead of opening a terminal window.
- F4 in interactive terminal mode opens the in-terminal advanced file viewer for the current file.
- Terminal advanced viewer status mentions Text/Hex/Binary/Octal mode keys.
- In terminal advanced viewer, Alt+T or T switches to Text, Alt+X or X switches to Hex, Alt+B or B switches to Binary, and Alt+O or O switches to Octal.
- In terminal advanced viewer, L or Ctrl+L loads more, Shift+L loads all up to the safety limit, and Escape, Q, Backspace, or Ctrl+C returns to the report pager.
- Down/PageDown/Space moves forward one page.
- Up/PageUp moves backward one page.
- Home and End jump to the beginning or end.
- Q and Escape exit terminal mode.
- When terminal mode output is redirected, it writes the report and exits instead of waiting for keyboard input.

## File Identification Cases

Use representative files when available:

- The built `FileDentify.exe`.
- A known Roland SRX sample file, if available.
- A small UTF-8 text file.
- A ZIP file.
- A PNG or JPEG image.
- An MP3, WAV, FLAC, MP4, or MKV file when `ffprobe.exe` is beside FileDentify.

Expected:

- Executables show PE metadata and embedded version info.
- Executables show section table, image base, entry point, subsystem, DLL characteristics, and CLR-header status.
- Roland SRX files show the Roland SRX expansion ROM hint.
- Text files show text hints, encoding marker, and line-ending counts.
- Binary files with embedded messages show a `Readable text` section before `Printable strings`. It should contain plain extracted lines without byte offsets, while `Printable strings` keeps offset-based forensic detail.
- The `Readable text` section should be useful with NVDA: avoid flooding it with obvious binary noise, alphabet tables, or offset prefixes.
- Images show dimensions for PNG, GIF, BMP, or JPEG where supported.
- ZIP files show entry counts, compressed/uncompressed totals, expansion ratio, first entries, and Office/JAR/OPC hints when applicable.
- DMG files show the Apple disk image/UDIF trailer section when a `koly` trailer is present.
- RAR files show the RAR archive generation where supported.
- PDF files show PDF version, linearized hint, encryption hint, and sampled Info/XMP metadata such as title, author, creator, producer, creation date, and modification date when present.
- Files with Explorer property-handler metadata show a Windows property metadata section when Windows exposes useful non-filesystem properties.
- Office Open XML files such as `.docx`, `.xlsx`, and `.pptx` show an Office document metadata section with document kind, core properties, application properties, structure counts, and custom properties when present.
- OpenDocument files such as `.odt`, `.ods`, and `.odp` show an OpenDocument metadata section with document kind, title/creator/generator fields, editing details, and document statistics when present.
- EPUB `.epub` files show an EPUB ebook section with OPF path, title, creator, language, identifier, manifest count, reading-order count, ZIP entry count, and media/resource counts where available.
- Android `.apk` files show an Android APK section with base/split kind, AndroidManifest presence, DEX count, native ABI folders, resource/asset entry counts, and package/split names inferred from backup filenames where available.
- Backup and configuration files such as Audio Hijack `.ah4session`, Outlook Express `.iaf`, Synology `.dss`, and router `.cfg`, `.conf`, `.bin`, `.dat`, or `.xml` files show a Backup/config data section with format identity, structural hints, selected non-password fields where safe, and a clear privacy warning for credential-bearing backups.
- RIFF/WAV files show RIFF form, chunks, and WAV format details when present.
- RIFF RMID files show both RIFF structure and a RIFF MIDI section with MIDI payload offset, format, track count, and timing division when the embedded MIDI header is visible.
- SoundFont `.sf2` and old Sound Blaster / E-mu `.sbk` files show a SoundFont / SBK section with version/bank metadata when present, visible preset names, and visible preset/instrument/sample header counts when the relevant chunks are in the sampled ranges.
- sfArk `.sfArk` files show an sfArk SoundFont archive section with visible sfArk version strings and target `.sf2` names when present.
- DLS `.dls` files show a DLS instrument bank section with collection instrument count, DLS version, and visible instrument/region/wave-sample chunk counts where available.
- Creative/E-mu `.ecw` wavesets show a Creative ECW waveset section with marker, visible name, internal filename, and copyright where available.
- AIFF/AIFC and MIDI files show header-level structure details.
- Sun/NeXT `.au` or `.snd` files show a Sun/NeXT audio section with `.snd` marker, data offset, declared data size, encoding, sample rate, channels, annotation when present, and duration estimate when possible.
- Blu-ray `.m2ts` and MPEG transport streams show an MPEG transport stream section with detected packet size, sync byte offset, sync confidence, file size, and a clear note that ffprobe can provide stream codec/language/duration details when available.
- SQLite and plist files show useful header-level metadata.
- FLAC, Ogg, and ID3 files show header-level audio metadata.
- FLAC files show STREAMINFO duration and Vorbis comments when present.
- MP3 files with ID3v2 tags show common tag fields such as title, artist, album, date, genre, and comment when present, plus a first-frame bitrate/sample-rate duration estimate for constant-bitrate files.
- Old mobile phone tones such as `.imy`, `.mmf`, `.pmd`, `.amr`, `.rtttl`, `.rtx`, and `.ota` show a Mobile phone tone section. iMelody files should show fields such as version, beat, style, volume, and melody preview; SMAF/MMF files should show MMMD/CNTI metadata when present; PMD files should show cmid metadata and embedded RIFF/DLS clues when present.
- Symbian installed-app and resource files such as `.app`, `.aif`, `.rsc`, `.mbm`, `.mif`, and `.mdl` show a Symbian app/resource section with folder role, UID-like fields where present, item name, visible strings, and a no-execute/no-disassemble note. Java ME `.jad` descriptors and MIDlet `.jar` files under MIDlets folders show a Java MIDlet section with descriptor fields or archive role.
- Game and ROM files such as `.nsf`, `.nes`, `.rom`, `.gb`, `.gbc`, `.gba`, `.nds`, `.sfc`, `.smc`, `.gen`, `.md`, `.sms`, `.gg`, `.n64`, `.z64`, `.wad`, `.chd`, `.pak`, `.bnk`, `.wem`, `.assets`, `.rpa`, `.acf`, `.vdf`, `.cue`, `.gdi`, `.sav`, `.srm`, `.ips`, and `.bps` show a Game/ROM data section. NSF files should show title, artist, song count, addresses, timing, video system, and expansion audio where present. iNES files should show mapper, PRG/CHR ROM sizes, mirroring, trainer, and battery flags. Nintendo DS files should show title, game code, maker code, unit code, ROM capacity, ARM offsets/sizes, file-name table info, and header CRC. Sega Master System/Game Gear files should show the `TMR SEGA` marker, checksum, product code, version, region/system, and ROM size code. Doom WAD and CHD files should expose their marker and core header fields. Steam/Unity/Wwise-style files should at least identify the family and expose header fields or manifest keys where present.
- Nintendo Switch-style `Nintendo` content folders with `Contents\registered`, `save`, or `Album` children show a Nintendo Switch content section with registered `.nca.CONCAT` package and segment counts, save-file counts, album media counts, largest sampled package folders, and a structure-only note. Extensionless files under `Nintendo\Contents\registered\*.nca.CONCAT\00` and similar segments show Game/ROM data as Nintendo Switch NCA split content segments. Sixteen-character save files under `Nintendo\save` show Game/ROM data as Nintendo Switch save data. FileDentify must not try to decrypt, validate, or extract Nintendo payloads.
- Clipman files show a Clipman section. `CLIPDB2` encrypted `.clipdb` files should report container version, salt, IV, ciphertext size, HMAC presence, role by filename, and a clear no-decryption note. Settings JSON should identify machine/settings role and redact `ProtectedDatabasePassword` as present rather than printing the stored blob.
- MP4/MOV/M4A/AVIF/HEIC-style files show ISO base media brands and family hints.
- HEIC files show `HEIF/HEIC image` in the summary when the ISO BMFF brand says so.
- MSI and other OLE compound files show the OLE compound document section; `.msi` files are surfaced as Windows Installer packages.
- Native Instruments files such as `.nicnt`, `.nki`, `.nkm`, `.nkr`, `.nkx`, `.nkc`, `.ncw`, `.nksf`, `.nksfx`, `.nksn`, `.ens`, `.kt3`, `.ksd`, `.nfm8`, `.nabs`, `.nmsv`, `.nrkt`, and Maschine extensions show a Native Instruments section with extension identity, visible product strings, known header markers, metadata when available, and sampled instrument/sample references.
- Apple bundle files such as `Contents\Info.plist` inside `.app`, `.framework`, `.bundle`, `.plugin`, `.appex`, `.kext`, and `.prefPane` folders show an Apple bundle section with useful app or bundle metadata. Apple resource files such as `.strings`, `.car`, `.nib`, `.mobileconfig`, and XAR-based `.pkg` files show Apple localization, asset catalog, interface resource, configuration profile, or installer package sections.
- Package folders that Windows exposes as directories, such as `.logicx`, `.logic`, `.band`, `.garageband`, `.sparsebundle`, `.app`, `.framework`, `.bundle`, `.plugin`, `.appex`, `.kext`, `.prefPane`, `.component`, `.vst`, `.vst3`, `.clap`, and `.aaxplugin`, should appear as single report items when opened directly, sent through Send To, or found during folder scanning. Folder scanning should not flatten those package folders into every internal file by default.
- Logic Pro and GarageBand package folders show package-level project details, ProjectInformation.plist and MetaData.plist presence, ProjectData size, visible musical fields where sampled, and bundled audio-file names where available.
- Time Machine `.sparsebundle` folders show an Apple sparse bundle section with Info.plist metadata, band size, declared virtual size, sampled band count, and Time Machine metadata-file presence without mounting or traversing the backup filesystem.
- Apple iPhone/iPad backup folders containing `Manifest.db`, `Manifest.plist`, `Info.plist`, and `Status.plist` show an Apple mobile backup section with manifest presence, hashed shard-folder count, sampled stored-file count, largest sampled payloads, and privacy notes. Folder scanning should treat the backup root as one reportable package rather than flattening every hashed payload file.
- Extensionless 40-character files inside Apple mobile backup shard folders show an Apple mobile backup file section with backup identifier, shard folder, file ID, likely payload type, Manifest.db lookup hint, and privacy note.
- Direct command-line reports with `--report output.txt some.logicx` or another reportable package directory should work, not only `--folder-report`.
- Apple firmware and mobile app archives such as `.ipsw` and `.ipa` show Apple firmware package or iOS application archive sections. IPSW files should report ZIP container structure, BuildManifest.plist and Restore.plist presence, largest DMG entries, and firmware entries. IPA files should report Payload app metadata path, framework entry count, and app extension entry count where present.
- Roland Cloud files such as `.exz`, VEXP expansion packages, KoaBankFile preset banks, `Preset.bin`, and `InstalledBankNames.dat` show a Roland Cloud section with product folder, expansion code/name, visible bank name, and installed bank strings where available.
- XLN Audio files such as `.xpak` and `InstalledBankNames.dat` show an XLN Audio section with product, pack folder, pack code/name, file size, and installed bank names where visible.
- Spectrasonics STEAM/SAGE files such as `.db`, `.mlt_omn`, `.mlt_key`, `.mlt_trl`, `.mlt_rmx`, `.fxp_rmx`, `.fxr_rmx`, `.kit_rmx`, and `.prt_rmx` show a Spectrasonics section with family/product inference, sample-container index entries, indexed file sizes, and visible XML module types where available.
- Korg sample-library files such as `.wmss`, `.adsr`, `.voiceamp`, `.pitch`, `.dynamicarpeggiator`, `.classicvectoreg`, SQLite `.db` files under Korg folders, and extensionless Korg objects show a Korg section with product folder, role, file size, header marker, visible WaveMotion name, object id, and useful visible object markers where available.
- GForce M-Tron `.cpt2` tape-bank files show a GForce M-Tron section with product folder, library folder, bank name, file size, and a clear no-unpack note for proprietary tape-bank payloads.
- Toontrack files such as `.obw`, `soundstats`, and `s3presetconf` show a Toontrack section with product folder, role, file size, RIFF/container clues, and visible microphone, kit, articulation, preset, or statistics entries where available.
- Decent Sampler `.dspreset` files show a Decent Sampler section with library folder, preset name, referenced sample count, sample paths, note ranges, group/control counts, and UI metadata where present.
- Universal Audio LUNA `.lunacomponent` folders and related `.cir`, `.cmr`, `.rev`, `.dat`, `.bin`, and `.json` files show a Universal Audio LUNA section with component name, role, file size/package size, and visible metadata where available.
- AIR Music Technology `.big` and `.patch` files show an AIR Music Technology section with product/library context, archive marker or XML root, visible embedded paths, parts, part types, and sample references where available.
- Maize Sampler `.mse` files show a Maize Sampler section with the MSE marker, library folder, instrument name, file size, and visible instrument/vendor names where available.
- Applied Acoustics Systems banks, packs, presets, `.aasbank`, `.aas-gui`, and `.lbin` files show an Applied Acoustics Systems section with product folder, item name, file size, and visible preset metadata such as author, category, engine, and version where available.
- Audio Modeling/SWAM files under Audio Modeling folders show an Audio Modeling section with product folder, role, file size, and visible metadata where available.
- UJAM `.blob`, `.patch`, `.settings`, `.json`, `.yaml`, `.nksf`, `.nksfx`, and `.meta` files show a UJAM section with product folder, role, file size, leading UUID for blob files, preset name, build metadata, and DSP setting count where available.
- Crow Hill and Rhodes `.blob` files under sample-library folders show a UJAM-style blob section with vendor folder, product folder, role, file size, and proprietary-payload note, without claiming Native Instruments ownership.
- Valhalla DSP `.vpreset` files show a Valhalla DSP section with product folder, preset folder, preset file, plug-in version, preset name, and selected visible parameters where available.
- Modartt/Pianoteq `.ptq`, `.fxp`, `.mfxp`, `.fxp,1`, and `.prefs` files show a Modartt Pianoteq section with product folder, item name, file size, VST chunk marker, plug-in id, preset name, and visible instrument/version text where available.
- Ollama manifests, Ollama content-addressed blobs, and GGUF model files show an AI model / Ollama section with role, file name, model tag, manifest media type, referenced layer count, GGUF version, tensor count, metadata count, and visible metadata keys where available.
- Piper/Sonata voice files show a Speech voice section for `.onnx`, sidecar `.json`, and `MODEL_CARD` files, including model role, voice folder, sample rate, quality, eSpeak voice, language, and speaker counts where visible.
- Legacy `.jlw` and `.vop` files show a Legacy sound bank section with extension clues, size pattern, first big-endian words, readable strings, and a warning when libmagic's Adobe swatch guess is weaker than the full-file evidence.
- Mac audio plug-in bundles such as `.component`, `.vst`, `.vst3`, `.clap`, `.aaxplugin`, and `.aupreset` show a Mac audio plug-in section. Bundle `Contents\Info.plist` files should expose useful display name, identifier, executable, version, minimum macOS, SDK, and Xcode fields when present.
- Spitfire Audio library files such as `.spitfire`, `.zmulti`, `.zpreset`, `.zconfig`, `.lm`, `.db`, and Spitfire NKS `.nksf` files show a Spitfire Audio section with inferred library name, folder role, version folder, SQLite catalogue hints, header marker when present, and visible sample/library strings where available.
- Steinberg/Cubase files such as old `.all` and `.arr` songs, plus `.cpr`, `.npr`, `.fxb`, `.fxp`, `.vstpreset`, and `.drm` where available, show a Steinberg Cubase section. Old `.all` files should not be mislabelled as unrelated libmagic guesses in the Summary.
- Cakewalk `.wrk` and `.cwp` files show a Cakewalk project section with Cakewalk/Sonar markers, useful driver/audio-system strings, version-looking strings, and visible sample or SoundFont references where available. They should not also get a misleading Steinberg Cubase section merely because common ASIO/VST strings are present.
- REAPER `.rpp` and `.rpp-bak` files show a REAPER project section with header/version, timestamp, tempo, sample rate, track/item/take counts, render target, plug-in counts/previews, and sampled media references where present.
- Ableton `.ablbundle`, `.ablpreset`, and `.abl` files show Ableton sections. Bundles expose ZIP entry count, uncompressed total, common entry extensions, and interesting entries. Presets expose JSON schema/kind/top-level keys where available. Song JSON should expose tempo, scale, melodic layout, step resolution, and track counts when present.
- Synth/music preset formats such as Helm `.helm`, Surge `.wt`, Neural Amp Modeler `.nam`, Microtonic `.mtdrum`, chord preset `.chords`, and VST `.fxp`/`.fxb` files show useful preset/model/header details without dumping large raw structures.
- MIDI SysEx `.syx` files show message count, manufacturer name, and Roland device/model/command fields where applicable.
- Roland `.svd`, `.svq`, `.smp`, and SRX/FA expansion `.bin` files show Roland sound-data, sequencer-song, sample-data, or firmware/device-image sections. SVD files should expose visible chunk table entries, SVQ files should expose visible song names when present, SMP files should expose sample rate/channel/payload fields, and expansion images should expose Roland SRX/title hints.
- Symbian `.sis` and `.sisx` packages show a Symbian package section with UID fields, checksum, compressed-stream hints, and visible package strings where present.
- PC BIOS/UEFI images, including MSI-style BIOS filenames such as `E7A32...`, show a Firmware / device image section with header marker, file size, visible firmware strings, and a clear no-flash/no-modify note.
- LHA/LZH archives show the LHA method and first entry name without extracting contents.
- Tracker music modules such as `.mod`, `.xm`, `.s3m`, and `.it` show tracker-module hints. MOD files should show title, signature, sample slots, song length, and restart byte when the header is valid.
- `.mogg` files show MOGG multitrack audio details, including Ogg payload offset and prefix size where visible.
- SFZ and Logic EXS sampler instruments show sampler-specific sections. SFZ should show region/group/control counts and referenced samples; EXS should show visible sample or EXS strings where present.
- FMOD `.bank` files and Wwise `.wem` files show game-audio sections without claiming to decode or decrypt audio.
- Python `.pyc` files show Python bytecode details such as magic number, flags, timestamp/hash invalidation mode, source timestamp, and source size where available.
- WebAssembly `.wasm` files show a WebAssembly section with version and visible section names and sizes.
- Chromium/Electron `.pak` files show a Chromium resource pack section with version, encoding, resource count, and alias count where available.
- `.msg` files using OLE structured storage show MSG structured-storage hints without claiming to decode private message content.
- Main-window section details preserve multiline item values. Long values such as Ableton interesting entries or SFZ referenced samples should read as separate lines rather than one very long pipe-separated line.
- For files with high-value FileDentify-specific sections, those sections should appear immediately after Summary by default. Examples include Safety hints, Korg, GForce M-Tron, Toontrack, Decent Sampler, Universal Audio LUNA, AIR Music Technology, Maize Sampler, Applied Acoustics Systems, Audio Modeling, UJAM, Valhalla DSP, Modartt Pianoteq, AI model / Ollama, Native Instruments, Clipman, saved reports, project/package formats, and other dedicated format sections. Existing user-configured section order should not bury newly detected high-value sections behind generic sections such as Hashes, Header bytes, Printable strings, and Byte statistics unless that same high-value section was explicitly moved by the user.
- `.blob` files show a Binary blob section without claiming Native Instruments ownership; product-specific detectors such as UJAM or UJAM-style blob should provide the vendor section when the path or metadata supports it.
- `.ufs` files show a UFS sample library container section based on either a `UFS2` marker or the `.ufs` extension.
- TrueType, OpenType, WOFF, and WOFF2 fonts show the Font section with useful header/table details.
- XZ, LZ4 frame, Zstandard, and Mozilla `mozLz40` profile files show compression/container hints. Mozilla profile files should surface as Firefox/Thunderbird LZ4-compressed profile data.
- ISO images show an ISO 9660 volume section when the `CD001` descriptor is present.
- Nero `.nrg` disc images show a Nero disc image section with NERO/NER5 footer marker, chunk table offset, and visible trailer chunks where present.
- Microsoft Cabinet `.cab` and setup-compressed files such as `.DL_`, `.EX_`, and `.SY_` show cabinet version, folder/file counts, flags, set/index fields, and visible stored file names where the file table is sampled.
- Windows `.wim` and `.esd` images show a Windows imaging section with header size, raw version, image count, part count, compression chunk size, and decoded compression flags.
- VMDK/VHD/VHDX/VDI/QCOW2 virtual disk files show a Virtual disk section. Text VMDK descriptors should include useful descriptor keys such as `createType` and `CID`.
- Every file should include a `Unix file/libmagic` section when the embedded probe succeeds. Its result is extra evidence and should not blindly override better FileDentify-specific analysis such as DMG trailer detection.
- Files with obvious header and extension mismatches, such as a PDF header saved as `.scr`, show a `Safety hints` section with the detected header family, current extension, expected extension list, and cautious scan/verify guidance. The wording must not claim the file is malware.
- Media files show a concise `Media details` section when `ffprobe.exe` is available and probing succeeds, followed by the raw `ffprobe` output for full detail.
- MOV/QuickTime files with ffprobe-readable tags show a `QuickTime metadata` section with useful camera/device fields such as make, model, software, app, device, copyright, location, and creation date when present. These fields should be deduplicated and easier to read than the raw ffprobe tag dump.
- Companion tools such as `metaflac.exe`, `opusinfo.exe`, and `vgmstream-cli.exe` may add sections when present and relevant.

## SendTo Integration

Install:

```powershell
<path-to-built-FileDentify.exe> --install-sendto
```

Check:

```powershell
$shortcut = Join-Path ([Environment]::GetFolderPath('SendTo')) 'File&Dentify.lnk'
$shell = New-Object -ComObject WScript.Shell
$lnk = $shell.CreateShortcut($shortcut)
[pscustomobject]@{
  Name = Split-Path -Leaf $shortcut
  Target = $lnk.TargetPath
  Arguments = $lnk.Arguments
  WorkingDirectory = $lnk.WorkingDirectory
} | Format-List
```

Expected:

- Shortcut name is `File&Dentify.lnk`, with the menu accelerator on `D`.
- Target is the built `FileDentify.exe`.
- Arguments are empty.
- Working directory is the executable folder.
- Old-cased `File&dentify.lnk` is not present.

Uninstall:

```powershell
<path-to-built-FileDentify.exe> --uninstall-sendto
```

Expected:

- `File&Dentify.lnk` is removed.
- `FileDentify.ini` records `SendToEnabled=False`.

Desktop shortcut:

```powershell
Start-Process -FilePath <path-to-built-FileDentify.exe> -ArgumentList '--install-desktop' -Wait
$desktopShortcut = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'FileDentify.lnk'
Test-Path -LiteralPath $desktopShortcut
Start-Process -FilePath <path-to-built-FileDentify.exe> -ArgumentList '--uninstall-desktop' -Wait
Test-Path -LiteralPath $desktopShortcut
```

Expected:

- The first `Test-Path` returns `True`.
- The desktop shortcut targets the built `FileDentify.exe`.
- The second `Test-Path` returns `False`.
- `FileDentify.ini` records `DesktopShortcutEnabled=False` after uninstall.

## Settings

Check:

```powershell
Get-Content <path-to-FileDentify.ini>
```

Expected:

- Settings are beside the executable, not in AppData or the registry.
- No private paths are written unless a user explicitly chooses them in future features.
- Settings survive executable replacement.

## Updater

Until the GitHub repository and first release exist, update checks may report that releases cannot be found. That is acceptable before first publish.

After the GitHub repository exists:

- Help > Check for Updates should report current when no newer release exists.
- Help > Version History should show latest release notes.
- A newer release ZIP should update without preserving stale temp files.
- A release ZIP used by the updater must contain `FileDentify.exe` and `fd.com` in the same folder; an update ZIP missing `fd.com` should fail with a clear updater error.
- After publishing a GitHub release asset, extract the previous public portable build to a temporary folder, run its updater, and confirm it updates to the new release before considering the release shipped.
- `FileDentify.ini` must survive updates.
- An update from 1.2 or earlier should remove any old installed `README.md`; the embedded manual is the user documentation source of truth.

## Release Package Cleanliness

Run the automated privacy blocker before any release, release-asset refresh, source snapshot, or hotfix:

```powershell
powershell -ExecutionPolicy Bypass -File .\Test-ReleasePrivacy.ps1 -ReleaseZip <path-to-release-zip> -AllHistory
```

Expected:

- The command exits successfully.
- Public docs, source text, generated release files, and reachable Git history do not contain private local paths, private machine/user names, token-file loading snippets, or private Codex handover path wording.
- Any failure is a release blocker. Fix the source, rebuild the ZIP, regenerate source snapshots if needed, and rerun this check before publishing.

A future release ZIP should include only shipped user-facing files, normally:

- `FileDentify.exe`
- `fd.com`
- `THIRD-PARTY-NOTICES.txt`, if the release process chooses to include external notices as a separate file in addition to the embedded Help menu
- `LICENSE.txt`, if present

It should not include `README.md`; that file is repository documentation only.

It must not include:

- `FileDentify.ini`
- logs
- temp files
- token files
- private token paths
- private machine paths
- local user profile paths
- private Codex handover files
- source-only build artifacts

Public URLs such as GitHub, `onj.me`, and `3.onj.me/programs` are fine. Maintainer-only release instructions must not be included in `README.md`.

