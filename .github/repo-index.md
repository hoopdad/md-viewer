# Repository index

Freshness: `03e185f`, plus 1.0.2 release metadata (2026-11-25). No paths added or deleted.

## Functional areas

- Windows viewer shell: WPF startup, single-process launch forwarding, commands, drag/drop, status UI, and WebView2 hosting in `src\MdViewer.App\App.xaml*`, `SingleInstanceCoordinator.cs`, and `MainWindow.xaml*`.
- Markdown loading and rendering: bounded file reads, title detection, Markdig parsing, sanitization, local-image and local-Markdown-link resolution, HTML generation, and metrics in `src\MdViewer.Core\MarkdownFileLoader.cs`, `MarkdownRenderer.cs`, `MarkdownDocument.cs`, and `RenderedMarkdown.cs`.
- Editor integration: editor preferences and process launch in `src\MdViewer.App\EditorLauncher.cs`, `EditorSettingsWindow.xaml*`, and `src\MdViewer.Core\EditorSettingsStore.cs`.
- Packaging and shell registration: native self-contained x64 and ARM64 Windows publishing in `scripts\Build-Installer.ps1`; architecture-parameterized WiX installer sources in `installer\MdViewer.Installer`.
- Product, installation, and release notes: `README.md`, `CHANGELOG.md`, and `docs\ARCHITECTURE.md`; README campaign artwork in `docs\assets\md-viewer-hero.png`.

## Technical layers and dependency flow

1. `App.OnStartup` parses shell arguments with `LaunchRequest`; a secondary process forwards the request through `SingleInstanceCoordinator`, while the primary creates `MainWindow`.
2. Each launch creates an independent `MainWindow`; `OnLoaded` starts file load/render work in parallel with WebView2 initialization. All windows share the lazily initialized environment owned by `App`, not document state. The process exits after the last window closes.
3. `MarkdownFileLoader` reads one UTF-8/Unicode snapshot with an 8 MiB limit.
4. `MarkdownRenderer` uses one static Markdig pipeline, sanitizes links and images, and emits a complete inert HTML document plus image mappings and word count.
5. `MainWindow` UTF-8 encodes the generated HTML, serves it and local images through WebView2 resource interception, navigates to the fixed `https://md-viewer.local/document` origin, and keeps loading state visible until the DOM is ready.

`MdViewer.App` depends on `MdViewer.Core`; core has no app dependency. WebView2 is the formatted-content host and security boundary.

## Common navigation

- Startup/open latency: `src\MdViewer.App\App.xaml.cs`, `SingleInstanceCoordinator.cs`, `MainWindow.xaml.cs`, `MdViewer.App.csproj`, `scripts\Build-Installer.ps1`.
- Parse/render cost: `src\MdViewer.Core\MarkdownFileLoader.cs`, `MarkdownRenderer.cs`.
- First-paint/layout cost: generated HTML/CSS in `MarkdownRenderer.cs` and WebView initialization/navigation in `MainWindow.xaml.cs`.
- Local image behavior: `MarkdownRenderer.ImageResolver` and `MainWindow.OnWebResourceRequested`.
- Local Markdown link behavior: `MarkdownRenderer.MarkdownLinkResolver` and `MainWindow.OnNavigationStarting` / `OnNewWindowRequested`; opaque rendered IDs map to validated local `.md` / `.markdown` paths before launching a new viewer window.
- Security invariants: `docs\ARCHITECTURE.md`, renderer tests, and WebView settings/event handlers in `MainWindow.xaml.cs`.
- Installer/publish behavior: `scripts\Build-Installer.ps1`, `installer\MdViewer.Installer`.
- Release version: `Directory.Build.props` supplies `Version` to app/core assemblies and the WiX `ProductVersion` constant.
- README/download experience: benefits, current release links, install/use/source-build instructions in `README.md`; keep its local hero image under `docs\assets` so it renders offline in the viewer as well as on GitHub. Distinguish unreleased source features from installer capabilities using `CHANGELOG.md`.
- Windows execution/DPI compatibility: tracked `src\MdViewer.App\app.manifest`.

## Conventions and tests

- Nullable reference types, implicit usings, warnings as errors, and locked NuGet restores are configured in `Directory.Build.props`.
- Core behavior is tested with xUnit in `tests\MdViewer.Core.Tests`; rendering/security coverage is concentrated in `MarkdownRendererTests.cs`, and file I/O coverage in `MarkdownFileLoaderTests.cs`.
- The package-free launch coordinator is linked into core tests for concurrent forwarding and acknowledgement coverage. A secondary process exits successfully only after the primary handles its launch request.
- Repeatable renderer timing and allocation scenarios live in the package-free console project `tests\MdViewer.Performance`.
- Windows shell/window lifecycle regression: `scripts\Test-MultiWindow.ps1 -Executable <app.exe>`; pass `-UseFileAssociation` to exercise `cmd /c start` with installed associations. Requires an interactive Windows desktop and no existing viewer windows; checks document titles, duplicates, independent closing, forwarding after the original closes, and last-window process exit.
- Standard validation: `dotnet restore MdViewer.slnx --locked-mode`, `dotnet test MdViewer.slnx -c Release --no-restore`, and `dotnet build MdViewer.slnx -c Release --no-restore`; scan dependencies with `dotnet list MdViewer.slnx package --vulnerable --include-transitive`.
- Preserve the read-only, no-network, deny-by-default rendering model when changing performance-sensitive code.
