Embedded libmagic package
=========================

FileDentify embeds a Windows build of the Unix file/libmagic tool for broad
file identification.

Source package:
https://github.com/nscaife/file-windows/releases/tag/20170108

Embedded files:
- file.exe
- libmagic-1.dll
- libgnurx-0.dll
- magic.mgc
- COPYING.file
- COPYING.libgnurx

Licenses:
- file/libmagic uses the BSD-style notice in COPYING.file.
- libgnurx-0.dll uses LGPL 2.1, included in COPYING.libgnurx.

Runtime behavior:
FileDentify embeds these files into FileDentify.exe as resources. At runtime it
extracts them to a per-process temporary folder, runs file.exe against the
selected file, adds the result to the report, and removes the temporary folder
when the process exits.
