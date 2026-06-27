# FileDentify GitHub Release Rules

This file captures the release rules for coding agents preparing FileDentify for GitHub.

## Authentication

Never use a GitHub path that opens an interactive browser, passkey, Git Credential Manager, or "Connect to GitHub" prompt.

Use only non-interactive authentication:

- `gh` with `GH_TOKEN` or `GITHUB_TOKEN` set from the local token file.
- `git` with `GIT_TERMINAL_PROMPT=0`, `GCM_INTERACTIVE=Never`, credential helpers disabled, askpass disabled, and an explicit authorization header derived from the token.
- An already-authenticated GitHub connector.

If token-based authentication fails, stop and report it. Do not trigger an interactive login prompt.

## GitHub Issue Gate

Before publishing any FileDentify release, release-asset refresh, or hotfix, read open GitHub issues and pull requests. Do not publish first and inspect issues afterward.

If an open issue is fixed by the release, mention it in user-facing release notes with wording such as `Closes issue #N`, `Fixes issue #N`, or `Resolves issue #N`. If an open issue is intentionally deferred, say that explicitly in the handoff or release notes.

## Repository And Project URL

Current provisional project URL in source:

```text
https://github.com/OnjLouis/FileDentify
```

If the real GitHub repository uses a different casing or slug, update all of these together:

- `Program.ProjectUrl` in `src\FileDentify.cs`
- README project links
- updater release checks
- release package scripts
- this file

## Source Layout

- Durable source lives in `<local-path>`.
- Source code lives under `src`.
- Embedded third-party `file`/libmagic build inputs live under `third_party\libmagic`.
- `Build.ps1` builds the packaged executable.
- `SMOKE-TEST.md` is the smoke/release checklist.
- `<local-path>` is package output, not the durable source.
- `<local-path>` is the configured installed copy.

Do not commit or publish local runtime files:

- `FileDentify.ini`
- startup error logs
- temp update folders
- token files
- private Codex handover files

## Versioning

Before release, update these together:

- `Program.Version`
- `AssemblyVersion`
- `AssemblyFileVersion`
- `AssemblyInformationalVersion`
- README current version, if present
- release ZIP filename
- release notes

Use `1.0`, `1.1`, etc. for user-facing versions unless Andre requests a different versioning scheme. Use four-part assembly/file versions such as `1.0.0.0`.

## Build And Smoke

Before publishing, run the smoke checklist in:

```text
<local-path>
```

At minimum:

```powershell
powershell -ExecutionPolicy Bypass -File <local-path>
```

Then verify:

- no-argument launch opens a useful empty UI;
- F1 help opens at the top and closes with Escape;
- command-line `--report` works;
- SendTo install/uninstall works;
- executable metadata is correct;
- reports avoid duplicated or useless output.
- reports include `Unix file/libmagic` when the embedded probe succeeds.
- Help > Third-party notices shows the embedded `file`/libmagic and libgnurx notices.

## Release ZIP

The release ZIP should be clean and portable. It should include only files users need, normally:

- `FileDentify.exe`
- `README.md`
- `LICENSE.txt`, if present
- third-party notices, if release packaging keeps them as a separate file in addition to the embedded Help menu

It must not include:

- `FileDentify.ini`
- logs
- source-only scratch files
- private handover files
- token files
- generated update temp folders

The updater searches the release ZIP for `FileDentify.exe`, so the ZIP can either contain files at the root or inside one top-level folder.

## Local Backup Copies

After each public release or release-asset refresh, mirror the convention used by Clipman and Sensor Readout:

```text
<local-path> Builds
<local-path> Snapshots
```

- Copy the user-installable ZIP to `Program Builds\FileDentify-<version>.zip`.
- Create `Source Snapshots\FileDentify-source-<version>.zip` from the released git tree, normally with `git archive`, not from untracked working files.
- Source snapshots must not include `.git`, `release`, `FileDentify.ini`, startup error logs, token files, or other runtime/private artifacts.

For version `1.0`, the expected backup artifacts are:

```text
<local-path> Builds\FileDentify-1.0.zip
<local-path> Snapshots\FileDentify-source-1.0.zip
```

## Embedded Third-party Components

FileDentify embeds a Windows build of Unix `file`/libmagic from `third_party\libmagic`.

- `file.exe`, `libmagic-1.dll`, and `magic.mgc` power the report's `Unix file/libmagic` section.
- `libgnurx-0.dll` is dynamically extracted and loaded with the tool; it is not statically merged into FileDentify code.
- `COPYING.file` and `COPYING.libgnurx` are embedded and shown from Help > Third-party notices.
- Before a public release, confirm the third-party notices are still reachable from the app and that the source package reference in `third_party\libmagic\README.txt` is still accurate.

## Documentation

Update `README.md` for user-facing behavior changes.

Update `SMOKE-TEST.md` whenever release checks, updater behavior, SendTo integration, accessibility behavior, package layout, or command-line behavior changes.

Keep public docs free of private paths except when documenting Andre's private paths for internal smoke-test use. Do not include private tokens, local machine names, or unrelated the repository checkout contents in release notes.

## SendTo Packaging

When FileDentify is included in the SendTo Project package:

- `<local-path>` should exist.
- `<local-path>` should create `File&Dentify.lnk`.
- The shortcut should target `%USERPROFILE%\encoders\FileDentify.exe`.
- The visible SendTo mnemonic should be `D`.
- Old-cased `Filedentify.exe` and `File&dentify.lnk` should not be left behind.

## Publishing

For future release automation, use the MidiCleaner/MoveToMidi `Release.ps1` model:

- build clean portable output;
- create a versioned ZIP;
- inspect open GitHub issues and PRs;
- create or update a GitHub release using non-interactive token auth;
- upload the ZIP asset;
- verify the release page and asset URL.

Do not publish until Andre explicitly asks for a GitHub release.
