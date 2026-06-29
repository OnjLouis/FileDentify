# FileDentify

FileDentify is a portable, accessible Windows file-identification utility.

It is designed to work well from Explorer's Send To menu, from the keyboard, and with screen readers such as NVDA.

FileDentify is not a replacement for every existing tool. If Windows, WSL `file`, Cygwin `file`, or MSYS2 `file` already gives enough information, use that. FileDentify is for the moments when you want more context on Windows: embedded file/libmagic results, FileDentify-specific sections, readable strings, hashes, advanced text/hex/binary/octal viewing, combined reports, folder overviews, Send To integration, and a screen-reader-friendly interface.

Build with:

```powershell
powershell -ExecutionPolicy Bypass -File .\Build.ps1
```

By default, the build script writes portable output under `bin\Release`. Set `FILEDENTIFY_PACKAGE_DIR` before building if you want the executable and console companion copied somewhere else.

FileDentify can also be used from the SendTo Project, available from <https://3.onj.me/programs>.

## Runtime features

- Screen-reader-first WinForms UI with a tree view, selected-item details, an HTML manual on F1, and predictable keyboard shortcuts.
- Standard shortcuts include Ctrl+O for Open files, Ctrl+S for Save report, Ctrl+comma for Preferences, Shift+F1 for Check for Updates, and Ctrl+F1 for the project page.
- `Options > Preferences` has an Automation tab for adding or removing FileDentify from the Windows Send To menu.
- `Options > Preferences` has an Updates tab for GitHub Releases update checks.
- `Help > Check for Updates`, `Help > Version History`, `Help > Project page`, `Help > Contact`, `Help > Donate`, and `Help > Other software` are available from the menu bar.
- `Help > Third-party notices` shows the embedded Unix `file`/libmagic and required runtime notices.
- Reports include built-in FileDentify analysis plus an embedded Unix `file`/libmagic section for broader signature coverage.
- Reports include a reader-friendly `Readable text` section for discovered strings, plus offset-based `Printable strings` for forensic detail.
- FileDentify recognizes common containers, archives, media headers, fonts, disk images, virtual disks, installer formats, sample-library containers, and ZIP-based application bundles such as Ableton Move/Live bundles.
- Terminal mode with `fd.com [files...]` shows reports in a keyboard-controlled pager when run from PowerShell or Windows Terminal, or prints the report when output is redirected. `fd.com` and `FileDentify.exe` must live in the same folder.

## Source layout

Source files live under `src`. UI, settings, updater, manual, terminal mode, libmagic integration, signatures, models, and file-inspection domains are kept in separate files instead of one monolithic source file.

## Embedded libmagic

The build embeds a Windows `file`/libmagic package from `third_party\libmagic` into `FileDentify.exe`. At runtime FileDentify extracts it to a per-process temporary folder, runs `file.exe`, adds the result to the report, and removes the temporary folder on exit.

## Command line

```powershell
FileDentify.exe [files...]
FileDentify.exe --report report.txt [files...]
FileDentify.exe --report report.html [files...]
FileDentify.exe --html-report report.html [files...]
FileDentify.exe --folder-report report.txt [folders-or-files...]
FileDentify.exe --folder-report report.html [folders-or-files...]
fd.com [files...]
fd.com -u
FileDentify.exe --close
FileDentify.exe -u
FileDentify.exe --update
FileDentify.exe --install-sendto
FileDentify.exe --uninstall-sendto
FileDentify.exe --install-desktop
FileDentify.exe --uninstall-desktop
FileDentify.exe --install-report-association
FileDentify.exe --uninstall-report-association
FileDentify.exe --version
FileDentify.exe --help
```

The `--report` / `-r` mode writes one combined report without opening the UI. Use a `.fdreport` output path for a reopenable native report, a `.html` or `.htm` output path, or `--html-report` / `-hr`, to write an HTML report with per-file headings and section tables. `--folder-report` / `-fr` recursively scans folders into one combined report. `--advanced-view` / `-av` opens the graphical advanced viewer directly on a file. Terminal mode uses `fd.com`, which pages through the report with Up, Down, PageUp, PageDown, Home, End, Q, and Escape. `fd.com` and `FileDentify.exe` must live in the same folder. `-u` and `--update` check GitHub Releases. `--close` / `-c` asks other FileDentify windows from the same executable to close gracefully. The SendTo, desktop shortcut, and `.fdreport` association install switches are non-interactive, have short forms, and update `FileDentify.ini` beside the executable where applicable.

## Credits

FileDentify is an Andre Louis utility.

A huge thanks to everyone who has submitted a GitHub issue or suggestion. You have helped make FileDentify more useful.

Questions and feedback can be sent through `Help` > `Contact` in the app or <https://onj.me/contact>.

FileDentify is free software. If you want to support Andre Louis software, use `Help` > `Donate` in the app or visit <https://onj.me/donate>. Other Andre Louis software is listed at <https://onj.me/software>.

FileDentify uses or can use components from these projects:

- [file/libmagic](https://www.darwinsys.com/file/) and [file on GitHub](https://github.com/file/file), for broad file signature identification.
- [MSYS2](https://www.msys2.org/) and [MSYS2 Packages](https://packages.msys2.org/), for the embedded Windows build of file/libmagic.
- [mingw-w64-x86_64-file](https://packages.msys2.org/package/mingw-w64-x86_64-file), the embedded file/libmagic package.
- [libsystre](https://packages.msys2.org/package/mingw-w64-x86_64-libsystre), [libtre](https://packages.msys2.org/package/mingw-w64-x86_64-libtre), [gettext runtime/libintl](https://packages.msys2.org/package/mingw-w64-x86_64-gettext-runtime), and [libiconv](https://packages.msys2.org/package/mingw-w64-x86_64-libiconv), bundled as runtime dependencies for the embedded file/libmagic build.
- [Tolk screen-reader library](https://github.com/dkager/tolk) and [NVDA controller client](https://github.com/nvaccess/nvda), for optional screen-reader announcements.
- [FFmpeg ffprobe](https://ffmpeg.org/ffprobe.html), used when `ffprobe.exe` is beside FileDentify for richer media metadata.
- [FLAC/metaflac](https://xiph.org/flac/), used when `metaflac.exe` is beside FileDentify for FLAC stream information.
- [Opus tools/opusinfo](https://opus-codec.org/), used when `opusinfo.exe` is beside FileDentify for Opus/Ogg information.
- [vgmstream](https://github.com/vgmstream/vgmstream), used when `vgmstream-cli.exe` is beside FileDentify for supported game-audio metadata.
- Microsoft .NET Framework and support libraries.

