# md-viewer

A fast, read-only Markdown viewer for Windows.

Repeated file opens reuse the running viewer process and its initialized
WebView2 environment for faster display.

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

```powershell
dotnet restore MdViewer.slnx
dotnet test MdViewer.slnx --no-restore
dotnet build MdViewer.slnx -c Release --no-restore
```

Run a representative renderer benchmark:

```powershell
dotnet run -c Release --runtime win-x64 --project tests\MdViewer.Performance -- --scenario images
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
