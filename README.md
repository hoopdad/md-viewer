# md-viewer

**Your Markdown. Ready to read.**

A fast, focused Markdown reader for Windows. Open a README, browse your notes,
or keep project docs side by side, without launching a full editor.

![md-viewer: Read at full speed. A cinematic cartoon crew celebrates fast opening, rich Markdown, and focused reading.](docs/assets/md-viewer-hero.png)

**[Download for x64](https://github.com/hoopdad/md-viewer/releases/download/v1.0.1/md-viewer-setup-x64.msi)**
| **[Download for ARM64](https://github.com/hoopdad/md-viewer/releases/download/v1.0.1/md-viewer-setup-arm64.msi)**
| [Installation](#install-101) | [Build from source](#build-from-source)

## Less friction. More reading.

- **Fast by design.** File parsing overlaps WebView2 startup, and later shell
  opens reuse the running process and shared browser environment.
- **Markdown that reads beautifully.** CommonMark and popular GitHub-flavored
  conventions bring headings, tables, task lists, code blocks, quotes, and
  footnotes into a clean reading view.
- **Your documents, side by side.** Each Explorer or command-line open gets its
  own window, even when you open the same file twice.
- **Open it your way.** Use File Explorer, drag and drop, or **Ctrl+O**. Install
  natively on Intel/AMD x64 or Windows on Arm.
- **Read without rewriting.** The viewer never edits or saves your source.
  Need to make a change? **Ctrl+E** hands it to your preferred editor.
- **Local docs stay local.** Relative images load on demand; remote images,
  scripts, and resources outside the document directory tree are blocked.

## Install 1.0.1

**No build tools required.** Download the installer for your Windows system
from the [1.0.1 release](https://github.com/hoopdad/md-viewer/releases/tag/v1.0.1):

| Windows system type | Installer |
| --- | --- |
| x64 (Intel or AMD) | [md-viewer-setup-x64.msi](https://github.com/hoopdad/md-viewer/releases/download/v1.0.1/md-viewer-setup-x64.msi) |
| ARM64 (Windows on Arm) | [md-viewer-setup-arm64.msi](https://github.com/hoopdad/md-viewer/releases/download/v1.0.1/md-viewer-setup-arm64.msi) |

1. **Check your system.** Find your architecture in
   **Settings > System > About > System type**.
   Use Windows 11 x64 or ARM64, or Windows 10 22H2 x64 where
   supported through Microsoft's extended security updates. 32-bit Windows,
   Linux, and macOS are not supported.
2. **Check WebView2.** The [Microsoft Edge WebView2 Evergreen Runtime](https://developer.microsoft.com/microsoft-edge/webview2/#download-section)
   is required and is normally present on Windows 11. Install it from Microsoft
   if the app reports it missing. The MSI bundles the matching .NET 8 runtime;
   no separate .NET installation is needed.
3. **Run the installer.** Close existing md-viewer windows, double-click the
   matching MSI, and complete setup. Installation is per-user, without an
   administrator installation, at `%LOCALAPPDATA%\Programs\md-viewer`.
4. **Open a document.** Launch **md-viewer** from Start and choose **Open file**,
   or drag in a `.md` or `.markdown` file.

**Download verification:** Release installers are **not code-signed**; Windows
may show an unknown-publisher or SmartScreen warning. Compare the downloaded
file's hash with the release's [SHA256SUMS.txt](https://github.com/hoopdad/md-viewer/releases/download/v1.0.1/SHA256SUMS.txt)
and follow your organization's software policy:

```powershell
Get-FileHash .\md-viewer-setup-x64.msi -Algorithm SHA256
```

Substitute `md-viewer-setup-arm64.msi` for Windows on Arm.

**Explorer integration:** Right-click a file and select **Open with md-viewer**
(on Windows 11, it may be under **Show more options**). To make it the default,
use **Open with > Choose another app** or **Settings > Apps > Default apps**.
The installer does not override a protected user-selected default.

**Upgrading development builds:** If you installed an unreleased MSI labeled
1.1.0, 1.2.0, or 1.3.0, uninstall it before installing 1.0.1; Windows Installer
blocks that version downgrade.

**Uninstalling:** Use **Settings > Apps > Installed apps** (Windows 11) or
**Apps & features** (Windows 10). Preferences and the WebView2 cache remain in
`%LOCALAPPDATA%\md-viewer`.

## Use

| Action | Shortcut |
| --- | --- |
| Open a Markdown file | Ctrl+O |
| Reload the current file from disk | F5 |
| Open the current file in your editor | Ctrl+E |

**Editor settings** lets you select an editor EXE; by default, Windows chooses
the editor using file associations.

Opening files from Explorer, `start x.md`, or another app creates a new window
for each launch, including repeated opens of the same file. **Open file** and
drag and drop replace the document only in the window where you use them.

### Markdown support and boundaries

Read `.md` and `.markdown` files up to **8 MiB**, with relative local PNG, JPEG,
GIF, WebP, and SVG images, including narrowly sanitized HTML `<img>` elements
used by GitHub READMEs. Arbitrary HTML, scripts, remote images, unsafe URI
schemes, and local images outside the document directory tree are blocked.
Links to web pages or email open in the associated external application after
confirmation. There are no plugins or script-based diagrams.

**In the source tree, not yet in 1.0.1:** Relative links to existing local
Markdown files open the linked document in a new viewer window. See the
[changelog](CHANGELOG.md) for unreleased changes.

## Build from source

### Prerequisites

- Windows and [Git](https://git-scm.com/downloads/win).
- The [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
  (the solution uses `.slnx`) and access to your configured NuGet feeds.
- [WebView2 Evergreen Runtime](https://developer.microsoft.com/microsoft-edge/webview2/#download-section)
  to run the app.
- The [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
  to run the framework-dependent build below. The self-contained MSI bundles it.

### Download, build, and run

In PowerShell:

```powershell
git clone https://github.com/hoopdad/md-viewer.git
Set-Location md-viewer
dotnet restore MdViewer.slnx --locked-mode
dotnet build MdViewer.slnx -c Release --no-restore
dotnet run --project src\MdViewer.App -c Release --no-build
```

The app executable is
`src\MdViewer.App\bin\Release\net8.0-windows\md-viewer.exe`.
Try opening [the sample document](docs/SAMPLE.md) or this README.

### Tests and performance

After restoring and building:

```powershell
dotnet test MdViewer.slnx -c Release --no-build --no-restore
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

This benchmark measures renderer time and allocations, not end-to-end app
startup. Warm shell opens share the initialized process and WebView2 environment;
the process exits after its last document window closes.

### Build Windows installers

Build native, self-contained MSI installers for both x64 and ARM64 Windows:

```powershell
.\scripts\Build-Installer.ps1
```

The installers are written to
`installer\MdViewer.Installer\bin\x64\Release\md-viewer-setup-x64.msi` and
`installer\MdViewer.Installer\bin\arm64\Release\md-viewer-setup-arm64.msi`.
To build only one architecture, pass `-Architecture x64` or
`-Architecture arm64`. WiX 5 is restored automatically; a separate WiX
installation is not required.

Each installer includes the matching .NET runtime and native app host, so ARM64
devices run natively without x64 emulation. ReadyToRun remains disabled:
profiling found no useful improvement for the WebView2-bound startup path and
substantially larger installers; a previous x64 build also failed under
Windows-on-Arm emulation.

## Development and releases

Product and assembly versions are set in `Directory.Build.props`; the WiX
package consumes that same version. Before publishing, validate both MSI
versions and architecture payloads, smoke-test the app, and attach both installers
and their SHA-256 checksums to the matching GitHub release. Build outputs are ignored by Git;
GitHub release assets preserve the downloadable binaries.

### App icon

The app icon is maintained as an SVG master, a 512 px PNG export, and a Windows
ICO with hand-tuned 16, 20, and 24 px frames. After changing the SVG, export it
to `src\MdViewer.App\Assets\AppIcon.png`, then install Pillow and rebuild the
ICO:

```powershell
python -m pip install Pillow
python .\scripts\Build-AppIcon.py
```

## Project reference

- [Release notes](CHANGELOG.md)
- [Architecture and threat model](docs/ARCHITECTURE.md)
- [Sample Markdown](docs/SAMPLE.md)
- [License](LICENSE)
