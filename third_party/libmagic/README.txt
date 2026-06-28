Embedded libmagic package
=========================

FileDentify embeds a Windows build of the Unix file/libmagic tool for broad
file identification.

Source packages:
https://packages.msys2.org/package/mingw-w64-x86_64-file
https://packages.msys2.org/package/mingw-w64-x86_64-libsystre
https://packages.msys2.org/package/mingw-w64-x86_64-libtre
https://packages.msys2.org/package/mingw-w64-x86_64-gettext-runtime
https://packages.msys2.org/package/mingw-w64-x86_64-libiconv

Embedded files:
- file.exe
- libmagic-1.dll
- libsystre-0.dll
- libtre-5.dll
- libintl-8.dll
- libiconv-2.dll
- magic.mgc
- COPYING.file
- COPYING.libsystre
- COPYING.libtre
- COPYING.gettext-runtime
- COPYING.libintl
- COPYING.libiconv-gpl
- COPYING.libiconv-lgpl

Licenses:
- file/libmagic uses the BSD-style notice in COPYING.file.
- libsystre and libtre use BSD-style notices.
- libintl is covered by the gettext runtime notices included here.
- libiconv notices are included for the runtime DLL and its library terms.

Runtime behavior:
FileDentify embeds these files into FileDentify.exe as resources. At runtime it
extracts them to a per-process temporary folder, runs file.exe against the
selected file, adds the result to the report, and removes the temporary folder
when the process exits.
