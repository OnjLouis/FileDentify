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
- Ctrl+O opens the file picker.
- Ctrl+S opens Save report when a report is loaded, and reports that no report is available when empty.
- Alt+V / View HTML report opens a temporary combined HTML report in the default browser when a report is loaded, and reports that no report is available when empty.
- Main-window action mnemonics do not collide with top-level menus. Open files uses Ctrl+O/File > Open files, Help uses F1/Help > Help, and Open containing folder uses Alt+L.
- F4 opens the advanced file viewer for the selected file. Alt+F4 still closes the current window normally.
- Advanced file viewer opens focus directly on the output field at the top of the loaded text and supports Text, Hex, Binary, and Octal radio-button modes; Ctrl+F focus/select search; F3 next search; Shift+F3 previous search; Ctrl+L load more; Ctrl+Shift+L load all; Ctrl+A; Ctrl+C; Save loaded output; and Escape to close. Scrolling to the bottom loads one more chunk, not the whole file.
- Advanced file viewer status is exposed through a native bottom-docked status bar. NVDA's status command should read the current line first, followed by loaded-data and mode details.
- Advanced file viewer search does not crash when the current selection is at or beyond the end of the loaded output; searching wraps safely and returns focus to the output field after Enter, F3, or Shift+F3.
- Command line advanced viewer output works with `--viewer-output`, `--viewer`, `--viewer-mode readable|hex|binary|octal`, and `--viewer-bytes`.
- Short command-line aliases work, including `-r`, `-hr`, `-fr`, `-av`, `-vo`, `-vw`, `-vm`, `-vb`, `-t`, `-c`, `-u`, `-is`, `-us`, `-id`, `-ud`, `-v`, and `-h`.
- `FileDentify.exe --advanced-view file` and `FileDentify.exe -av file` open the graphical advanced viewer directly.
- Ctrl+C from the tree copies the selected node's details, not the full report.
- Ctrl+C from the details box copies selected text when text is selected, otherwise the current details text.
- Ctrl+A selects all text in read-only edit fields such as details, notices, version history, and update notes.
- Ctrl+Shift+Left collapses the report tree, and Ctrl+Shift+Right expands it.
- File menu exposes report output actions such as Save report and View HTML report. Edit menu exposes copy and tree expand/collapse actions.
- Report-tree context menu exposes copy, expand/collapse, View HTML report, and Save report actions.
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
- Combined reports with more than one file start with a Report overview covering counts, generation time, total size, likely types, extensions, largest files, common bytes, common readable strings, signature-match counts, and attention files.
- `FileDentify.exe --close` asks other FileDentify windows from the same executable to close gracefully.
- `fd.com -u`, `FileDentify.exe -u`, and `FileDentify.exe --update` check GitHub Releases and report either current/new version status or a clear pre-release/network message.

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
- RIFF/WAV files show RIFF form, chunks, and WAV format details when present.
- AIFF/AIFC and MIDI files show header-level structure details.
- SQLite and plist files show useful header-level metadata.
- FLAC, Ogg, and ID3 files show header-level audio metadata.
- FLAC files show STREAMINFO duration and Vorbis comments when present.
- MP3 files with ID3v2 tags show common tag fields such as title, artist, album, date, genre, and comment when present, plus a first-frame bitrate/sample-rate duration estimate for constant-bitrate files.
- Old mobile phone tones such as `.imy`, `.mmf`, `.pmd`, `.amr`, `.rtttl`, `.rtx`, and `.ota` show a Mobile phone tone section. iMelody files should show fields such as version, beat, style, volume, and melody preview; SMAF/MMF files should show MMMD/CNTI metadata when present; PMD files should show cmid metadata and embedded RIFF/DLS clues when present.
- Game and ROM files such as `.nsf`, `.nes`, `.rom`, `.gb`, `.gbc`, `.gba`, `.sfc`, `.smc`, `.gen`, `.md`, `.n64`, `.z64`, `.wad`, `.chd`, `.pak`, `.bnk`, `.wem`, `.assets`, `.rpa`, `.acf`, `.vdf`, `.cue`, `.gdi`, `.sav`, `.srm`, `.ips`, and `.bps` show a Game/ROM data section. NSF files should show title, artist, song count, addresses, timing, video system, and expansion audio where present. iNES files should show mapper, PRG/CHR ROM sizes, mirroring, trainer, and battery flags. Doom WAD and CHD files should expose their marker and core header fields. Steam/Unity/Wwise-style files should at least identify the family and expose header fields or manifest keys where present.
- Clipman files show a Clipman section. `CLIPDB2` encrypted `.clipdb` files should report container version, salt, IV, ciphertext size, HMAC presence, role by filename, and a clear no-decryption note. Settings JSON should identify machine/settings role and redact `ProtectedDatabasePassword` as present rather than printing the stored blob.
- MP4/MOV/M4A/AVIF/HEIC-style files show ISO base media brands and family hints.
- HEIC files show `HEIF/HEIC image` in the summary when the ISO BMFF brand says so.
- MSI and other OLE compound files show the OLE compound document section; `.msi` files are surfaced as Windows Installer packages.
- Native Instruments files such as `.nicnt`, `.nki`, `.nkm`, `.nkr`, `.nkx`, `.nkc`, `.ncw`, `.nksf`, `.nksfx`, `.nksn`, `.ens`, `.kt3`, `.ksd`, `.nfm8`, `.nabs`, `.nmsv`, `.nrkt`, and Maschine extensions show a Native Instruments section with extension identity, visible product strings, known header markers, metadata when available, and sampled instrument/sample references.
- Steinberg/Cubase files such as old `.all` and `.arr` songs, plus `.cpr`, `.npr`, `.fxb`, `.fxp`, `.vstpreset`, and `.drm` where available, show a Steinberg Cubase section. Old `.all` files should not be mislabelled as unrelated libmagic guesses in the Summary.
- `.blob` files show a Binary blob section and, when relevant, Native Instruments hints based on extension and visible strings.
- `.ufs` files show a UFS sample library container section based on either a `UFS2` marker or the `.ufs` extension.
- TrueType, OpenType, WOFF, and WOFF2 fonts show the Font section with useful header/table details.
- XZ, LZ4 frame, Zstandard, and Mozilla `mozLz40` profile files show compression/container hints. Mozilla profile files should surface as Firefox/Thunderbird LZ4-compressed profile data.
- ISO images show an ISO 9660 volume section when the `CD001` descriptor is present.
- VMDK/VHD/VHDX/VDI/QCOW2 virtual disk files show a Virtual disk section. Text VMDK descriptors should include useful descriptor keys such as `createType` and `CID`.
- Every file should include a `Unix file/libmagic` section when the embedded probe succeeds. Its result is extra evidence and should not blindly override better FileDentify-specific analysis such as DMG trailer detection.
- Media files show a concise `Media details` section when `ffprobe.exe` is available and probing succeeds, followed by the raw `ffprobe` output for full detail.
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
- `FileDentify.ini` must survive updates.

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
- `README.md`
- `THIRD-PARTY-NOTICES.txt`, if the release process chooses to include external notices as a separate file in addition to the embedded Help menu
- `LICENSE.txt`, if present

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

