# FileDentify Smoke Test

This is the working smoke-test checklist for FileDentify. Use it before replacing the configured installed copy, packaging a release ZIP, or publishing to GitHub.

## Scope

FileDentify is a portable, keyboard-first WinForms file identification utility. The source lives in the repository checkout:

```text
<local-path>
```

The SendTo package should contain only the built executable:

```text
<local-path>
```

the configured installed copy is:

```text
<local-path>
```

## Build

Run:

```powershell
powershell -ExecutionPolicy Bypass -File <local-path>
```

Expected:

- `<local-path>` exists.
- `<local-path>` is updated when the installed encoders folder exists.
- Old-cased `Filedentify.exe` is not left beside `FileDentify.exe`.
- Build fails hard if `csc.exe` fails.
- If the installed copy is running or locked, the package output is still built and the script prints a warning instead of deleting the installed executable.
- Embedded `file`/libmagic resources are present under `third_party\libmagic\extracted`; the build fails if any required resource is missing.

## Version Metadata

Check:

```powershell
$v = [System.Diagnostics.FileVersionInfo]::GetVersionInfo('<local-path>')
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
Start-Process <local-path>
```

Expected:

- Main window opens as `FileDentify <version>`.
- No startup error dialog appears.
- Empty state is useful, with Open files and Preferences reachable.
- The first useful focus target is not a decorative label or layout container.
- Escape closes the main window.

## Keyboard And Screen Reader

Manual NVDA checks:

- Tab order reaches only useful controls.
- The tree view announces useful node names.
- F1 and Help > Help open help at the top of the text.
- Escape closes Help.
- Ctrl+O opens the file picker.
- Ctrl+S opens Save report when a report is loaded, and reports that no report is available when empty.
- F4 moves to selected-item details.
- Ctrl+C from the tree copies the selected node's details, not the full report.
- Ctrl+C from the details box copies selected text when text is selected, otherwise the current details text.
- The Copy report button copies the full report.
- Ctrl+comma opens Preferences.
- Shift+F1 checks for updates.
- Ctrl+F1 opens the GitHub project page.
- Help > Contact opens `https://onj.me/contact`.
- Help > Donate opens `https://onj.me/donate`.
- Help > Third-party notices opens at the top and includes the file/libmagic and libgnurx notices.
- Preferences has useful Automation and Updates tabs.
- Preferences can be closed with Escape or Cancel.

## Command Line

Run:

```powershell
$report = Join-Path $env:TEMP 'FileDentify-report-smoke.txt'
Remove-Item -LiteralPath $report -Force -ErrorAction SilentlyContinue
Start-Process -FilePath <local-path> -ArgumentList @('--report', $report, '<local-path>') -Wait
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

## File Identification Cases

Use representative files when available:

- `<local-path>`
- `<local-path>`, if still present, for Roland SRX detection.
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
- PDF files show PDF version, linearized hint, and encryption hint.
- RIFF/WAV files show RIFF form, chunks, and WAV format details when present.
- AIFF/AIFC and MIDI files show header-level structure details.
- SQLite and plist files show useful header-level metadata.
- FLAC, Ogg, and ID3 files show header-level audio metadata.
- MP4/MOV/M4A/AVIF/HEIC-style files show ISO base media brands and family hints.
- HEIC files show `HEIF/HEIC image` in the summary when the ISO BMFF brand says so.
- MSI and other OLE compound files show the OLE compound document section; `.msi` files are surfaced as Windows Installer packages.
- TrueType, OpenType, WOFF, and WOFF2 fonts show the Font section with useful header/table details.
- XZ, LZ4 frame, Zstandard, and Mozilla `mozLz40` profile files show compression/container hints. Mozilla profile files should surface as Firefox/Thunderbird LZ4-compressed profile data.
- ISO images show an ISO 9660 volume section when the `CD001` descriptor is present.
- VMDK/VHD/VHDX/VDI/QCOW2 virtual disk files show a Virtual disk section. Text VMDK descriptors should include useful descriptor keys such as `createType` and `CID`.
- Every file should include a `Unix file/libmagic` section when the embedded probe succeeds. Its result is extra evidence and should not blindly override better FileDentify-specific analysis such as DMG trailer detection.
- Media files show useful `ffprobe` output only when probing succeeds.
- Companion tools such as `metaflac.exe`, `opusinfo.exe`, and `vgmstream-cli.exe` may add sections when present and relevant.

## SendTo Integration

Install:

```powershell
<local-path> --install-sendto
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
- Target is `<local-path>`.
- Arguments are empty.
- Working directory is `<local-path>`.
- Old-cased `File&dentify.lnk` is not present.

Uninstall:

```powershell
<local-path> --uninstall-sendto
```

Expected:

- `File&Dentify.lnk` is removed.
- `FileDentify.ini` records `SendToEnabled=False`.

## Settings

Check:

```powershell
Get-Content <local-path>
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
- `FileDentify.ini` must survive updates.

## Release Package Cleanliness

A future release ZIP should include only shipped user-facing files, normally:

- `FileDentify.exe`
- `README.md`
- `THIRD-PARTY-NOTICES.txt`, if the release process chooses to include external notices as a separate file in addition to the embedded Help menu
- `LICENSE.txt`, if present

It must not include:

- `FileDentify.ini`
- logs
- temp files
- token files
- private Codex handover files
- source-only build artifacts
