# Changelog

## Unreleased

## 1.0.2 - 2026-11-25

- Fixed valid hyperlinks appearing struck through by explicitly underlining
  rendered links.
- Relative links to existing local `.md` and `.markdown` files now open the
  linked document in a new viewer window.
- The no-document welcome screen displays the application version.
- Native self-contained x64 and ARM64 MSI installers, version 1.0.2.

## 1.0.1 - 2026-10-10

- Fixed shell launches replacing the document in an existing window. Each
  Explorer or command-line file open now creates a separate window, including
  repeated opens of the same file.
- Preserved warm startup by sharing the running process and WebView2
  environment across independent document windows.
- The app continues accepting files after its original window closes and
  exits only after the last window closes.
- Shell launch forwarding waits for the new window to be created before the
  launching process exits; failed forwarding reports an explicit error.
- Added a Windows regression test covering concurrent file opens, duplicate
  opens, document titles, independent closing, and process lifetime.
- Native self-contained x64 and ARM64 MSI installers, version 1.0.1.

## 1.0.0 - 2026-10-10

First public release of md-viewer, a read-only Markdown viewer for Windows.

- CommonMark and GitHub-flavored Markdown rendering in a native WPF shell.
- Open files from Explorer, the app, or drag and drop; reload with F5.
- Warm file opens reuse the running process and WebView2 environment.
- Relative local images, including narrowly sanitized HTML image elements.
- Remote content, scripts, unsafe links, and out-of-tree local images blocked.
- Configurable external editor with Ctrl+E.
- Per-user x64 and ARM64 MSI installers with Start menu, Open With,
  context-menu, and Default Apps registration.
- Self-contained .NET 8 payloads; Microsoft Edge WebView2 Runtime is required
  separately.

### Release notes

Install the MSI matching your Windows architecture. See
[installation instructions](README.md#install-102) for prerequisites,
file associations, uninstalling, and SHA-256 verification.

The installers are unsigned. Files larger than 8 MiB are not supported.
The app does not edit files, fetch remote images, render arbitrary HTML,
or provide plugins or script-based diagrams.

Unreleased development installers used higher version numbers through 1.3.0.
Uninstall those builds before installing this first public release.
