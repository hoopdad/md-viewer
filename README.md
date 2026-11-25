# md-viewer

A fast, read-only Markdown viewer for Windows.

Every shell file open creates a separate window, so previously opened documents
remain visible. Windows share the running viewer process and its initialized
WebView2 environment for faster display; the process exits after its last window
closes.

## Install 1.0.1

Download the installer from the [1.0.1 release](https://github.com/hoopdad/md-viewer/releases/tag/v1.0.1):

| Windows system type | Installer |
| --- | --- |
| x64 (Intel or AMD) | [md-viewer-setup-x64.msi](https://github.com/hoopdad/md-viewer/releases/download/v1.0.1/md-viewer-setup-x64.msi) |
| ARM64 (Windows on Arm) | [md-viewer-setup-arm64.msi](https://github.com/hoopdad/md-viewer/releases/download/v1.0.1/md-viewer-setup-arm64.msi) |

Check **Settings > System > About > System type** if unsure. Use Windows 11
(x64 or ARM64), or Windows 10 22H2 x64 where the operating system is still
supported through Microsoft's extended security updates. 32-bit Windows,
Linux, and macOS are not supported.

The [Microsoft Edge WebView2 Evergreen Runtime](https://developer.microsoft.com/microsoft-edge/webview2/#download-section)
is required. It is normally present on Windows 11; install it from Microsoft
if md-viewer reports that WebView2 is missing. The MSI includes the matching
.NET 8 runtime, so no separate .NET installation is needed.

Close md-viewer, then double-click the matching MSI and complete installation.
It installs for the current user into `%LOCALAPPDATA%\Programs\md-viewer`,
without requiring an administrator installation. The release installers are
**not code-signed**; Windows may show an unknown-publisher or SmartScreen
warning. Verify the download against the release's `SHA256SUMS.txt` with
`Get-FileHash .\md-viewer-setup-arm64.msi -Algorithm SHA256` (substitute the
x64 filename as needed) and follow your organization's software policy.

Launch **md-viewer** from Start, use **Open file** or drag in a `.md` or
`.markdown` file. In File Explorer, use **Open with md-viewer** (on Windows 11,
it may be under **Show more options**). To make it the default, use
**Open with > Choose another app** or **Settings > Apps > Default apps**;
the installer does not override a protected user-selected default.

Uninstall through **Settings > Apps > Installed apps** (Windows 11) or
**Apps & features** (Windows 10). Settings and the WebView2 cache remain under
`%LOCALAPPDATA%\md-viewer`. If you previously installed an unreleased development
MSI labeled 1.1.0, 1.2.0, or 1.3.0, uninstall it before installing 1.0.1:
Windows Installer correctly blocks that version downgrade.

## Use

| Action | Shortcut |
| --- | --- |
| Open a Markdown file | Ctrl+O |
| Reload the current file from disk | F5 |
| Open the current file in your editor | Ctrl+E |

**Editor settings** lets you select an editor EXE; by default, Windows chooses
the editor using file associations. The viewer never saves the source file.
Documents are limited to 8 MiB. Remote images, arbitrary HTML, scripts, and
local images outside the document directory tree are blocked. Relative links to
existing local Markdown files open in a new viewer window. Links to web pages or
email open in the associated external application after confirmation.

Opening files from Explorer, `start x.md`, or another app creates a new window
for each launch, including repeated opens of the same file. **Open file** and
drag and drop replace the document only in the window where you use them.

## Goals

- Open `.md` files directly from File Explorer.
- Render CommonMark and popular GitHub-flavored Markdown conventions.
- Never edit or save the source file.
- Open a document in a user-selected editor EXE, with Windows file
  associations as the default.
- Block raw HTML, scripts, remote images, local resources outside the document
  tree, and unsafe URI schemes by default.
- Lazily display relative local PNG, JPEG, GIF, WebP, and SVG images, including
  narrowly sanitized HTML `<img>` elements used by GitHub READMEs.
- Provide an installer with Windows Open With and context-menu integration.

## Build

Prerequisites: Windows, the .NET 10 SDK (the solution uses `.slnx`), and access
to NuGet feeds. The app targets .NET 8. WiX 5 is restored automatically for
installer builds; a separate WiX installation is not required.

```powershell
dotnet restore MdViewer.slnx --locked-mode
dotnet test MdViewer.slnx -c Release --no-restore
dotnet build MdViewer.slnx -c Release --no-restore
dotnet list MdViewer.slnx package --vulnerable --include-transitive
.\scripts\Test-MultiWindow.ps1 -Executable .\src\MdViewer.App\bin\Release\net8.0-windows\md-viewer.exe
```

Close existing viewer windows before the desktop regression test. To test
installed file associations with `start`, run:

```powershell
.\scripts\Test-MultiWindow.ps1 -Executable "$env:LOCALAPPDATA\Programs\md-viewer\md-viewer.exe" -UseFileAssociation
```

Run a representative renderer benchmark:

```powershell
dotnet run -c Release --project tests\MdViewer.Performance -- --scenario images
```

Build native, self-contained MSI installers for both x64 and ARM64 Windows:

```powershell
.\scripts\Build-Installer.ps1
```

The installers are written to
`installer\MdViewer.Installer\bin\x64\Release\md-viewer-setup-x64.msi` and
`installer\MdViewer.Installer\bin\arm64\Release\md-viewer-setup-arm64.msi`.
To build only one architecture, pass `-Architecture x64` or
`-Architecture arm64`.

Product and assembly versions are set in `Directory.Build.props`; the WiX
package consumes that same version. See [the changelog](CHANGELOG.md) for
release notes. Before publishing, validate both MSI versions and architecture
payloads, smoke-test the app, and attach both installers and their SHA-256
checksums to the matching GitHub release. Build outputs are ignored by Git;
GitHub release assets preserve the downloadable binaries.

Each installer contains the matching .NET runtime and native app host, so ARM64
devices run the ARM64 build without x64 emulation. ReadyToRun remains disabled:
profiling showed no useful improvement for the WebView2-bound startup path, and
it substantially increases installer size; the previous x64 ReadyToRun build
also failed under Windows-on-ARM emulation.

The app icon is maintained as an SVG master, a 512 px PNG export, and a Windows
ICO with hand-tuned 16, 20, and 24 px frames. After changing the SVG, export it
to `src\MdViewer.App\Assets\AppIcon.png`, then install Pillow and rebuild the
ICO:

```powershell
python -m pip install Pillow
python .\scripts\Build-AppIcon.py
```

The MSI installs per-user, registers md-viewer for `.md` and `.markdown`,
creates an explicit **Open with md-viewer** Explorer action, and registers the
application with Windows Default Apps. Windows may preserve an existing
user-selected default until the user confirms md-viewer in Default Apps.

See [the architecture and threat model](docs/ARCHITECTURE.md) for design
details.
