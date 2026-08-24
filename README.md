# md-viewer

A fast, read-only Markdown viewer for Windows.

## Goals

- Open `.md` files directly from File Explorer.
- Render CommonMark and popular GitHub-flavored Markdown conventions.
- Never edit or save the source file.
- Block raw HTML, scripts, remote images, local-resource reads, and unsafe URI
  schemes by default.
- Provide an installer with Windows Open With and context-menu integration.

## Build

```powershell
dotnet restore MdViewer.slnx
dotnet test MdViewer.slnx --no-restore
dotnet build MdViewer.slnx -c Release --no-restore
```

See [the architecture and threat model](docs/ARCHITECTURE.md) for design
details.
