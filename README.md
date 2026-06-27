# FileDentify

FileDentify is a portable, accessible Windows file-identification utility.

It is designed to work well from Explorer's Send To menu, from the keyboard, and with screen readers such as NVDA.

Build with:

```powershell
powershell -ExecutionPolicy Bypass -File .\Build.ps1
```

The build script creates the portable executable and, when the configured package output folder exists, updates that installed copy as well.

Before release-style work, maintainers should read:

- `SMOKE-TEST.md`
- `GITHUB-RELEASE-RULES.md`

## Runtime features

- Screen-reader-first WinForms UI with a tree view, selected-item details, F1 help, and predictable keyboard shortcuts.
- Standard shortcuts include Ctrl+O for Open files, Ctrl+S for Save report, Ctrl+comma for Preferences, Shift+F1 for Check for Updates, and Ctrl+F1 for the project page.
- `Options > Preferences` has an Automation tab for adding or removing FileDentify from the Windows Send To menu.
- `Options > Preferences` has an Updates tab for GitHub Releases update checks.
- `Help > Check for Updates`, `Help > Version History`, `Help > Project page`, `Help > Contact`, and `Help > Donate` follow the same model as Andre's other utilities.
- `Help > Third-party notices` shows the embedded Unix `file`/libmagic and libgnurx notices.
- Reports include built-in FileDentify analysis plus an embedded Unix `file`/libmagic section for broader signature coverage.
- Reports include a reader-friendly `Readable text` section for discovered strings, plus offset-based `Printable strings` for forensic detail.
- FileDentify recognizes common containers, archives, media headers, fonts, disk images, virtual disks, installer formats, sample-library containers, and ZIP-based application bundles such as Ableton Move/Live bundles.

## Embedded libmagic

The build embeds a Windows `file`/libmagic package from `third_party\libmagic` into `FileDentify.exe`. At runtime FileDentify extracts it to a per-process temporary folder, runs `file.exe`, adds the result to the report, and removes the temporary folder on exit.

## Command line

```powershell
FileDentify.exe [files...]
FileDentify.exe --report report.txt [files...]
FileDentify.exe --install-sendto
FileDentify.exe --uninstall-sendto
FileDentify.exe --version
FileDentify.exe --help
```

The `--report` mode writes a text report without opening the UI. The SendTo install switches are non-interactive and update `FileDentify.ini` beside the executable.
