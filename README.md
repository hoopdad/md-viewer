# md-viewer

A fast, read-only Markdown viewer for Windows.

## Goals

- Open `.md` files directly from File Explorer.
- Render CommonMark and popular GitHub-flavored Markdown conventions.
- Never edit or save the source file.
- Open a document in a user-selected editor EXE, with Windows file
  associations as the default.
- Block raw HTML, scripts, remote images, local-resource reads, and unsafe URI
  schemes by default.
- Provide an installer with Windows Open With and context-menu integration.

## Build

```powershell
dotnet restore MdViewer.slnx
dotnet test MdViewer.slnx --no-restore
dotnet build MdViewer.slnx -c Release --no-restore
```

Build the self-contained x64 Windows installer:

```powershell
.\scripts\Build-Installer.ps1
```

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
