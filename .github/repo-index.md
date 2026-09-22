# Repository index

Freshness: `d882a6045eb84fe84ac919314093515529a52e84` (2026-08-26), plus the current performance worktree. Added paths considered: `src\MdViewer.App\SingleInstanceCoordinator.cs` and `tests\MdViewer.Performance`.

## Functional areas

- Windows viewer shell: WPF startup, single-process launch forwarding, commands, drag/drop, status UI, and WebView2 hosting in `src\MdViewer.App\App.xaml*`, `SingleInstanceCoordinator.cs`, and `MainWindow.xaml*`.
- Markdown loading and rendering: bounded file reads, title detection, Markdig parsing, sanitization, local-image resolution, HTML generation, and metrics in `src\MdViewer.Core\MarkdownFileLoader.cs`, `MarkdownRenderer.cs`, `MarkdownDocument.cs`, and `RenderedMarkdown.cs`.
- Editor integration: editor preferences and process launch in `src\MdViewer.App\EditorLauncher.cs`, `EditorSettingsWindow.xaml*`, and `src\MdViewer.Core\EditorSettingsStore.cs`.
- Packaging and shell registration: self-contained Windows publishing in `scripts\Build-Installer.ps1`; WiX installer sources in `installer\MdViewer.Installer`.
- Product and security design: `README.md` and `docs\ARCHITECTURE.md`.

## Technical layers and dependency flow

1. `App.OnStartup` parses shell arguments with `LaunchRequest`; a secondary process forwards the request through `SingleInstanceCoordinator`, while the primary creates `MainWindow`.
2. `MainWindow.OnLoaded` starts cold file load/render work in parallel with WebView2 environment initialization. Warm launch requests reuse that window and environment.
3. `MarkdownFileLoader` reads one UTF-8/Unicode snapshot with an 8 MiB limit.
4. `MarkdownRenderer` uses one static Markdig pipeline, sanitizes links and images, and emits a complete inert HTML document plus image mappings and word count.
5. `MainWindow` UTF-8 encodes the generated HTML, serves it and local images through WebView2 resource interception, navigates to the fixed `https://md-viewer.local/document` origin, and keeps loading state visible until the DOM is ready.

`MdViewer.App` depends on `MdViewer.Core`; core has no app dependency. WebView2 is the formatted-content host and security boundary.

## Common navigation

- Startup/open latency: `src\MdViewer.App\App.xaml.cs`, `SingleInstanceCoordinator.cs`, `MainWindow.xaml.cs`, `MdViewer.App.csproj`, `scripts\Build-Installer.ps1`.
- Parse/render cost: `src\MdViewer.Core\MarkdownFileLoader.cs`, `MarkdownRenderer.cs`.
- First-paint/layout cost: generated HTML/CSS in `MarkdownRenderer.cs` and WebView initialization/navigation in `MainWindow.xaml.cs`.
- Local image behavior: `MarkdownRenderer.ImageResolver` and `MainWindow.OnWebResourceRequested`.
- Security invariants: `docs\ARCHITECTURE.md`, renderer tests, and WebView settings/event handlers in `MainWindow.xaml.cs`.
- Installer/publish behavior: `scripts\Build-Installer.ps1`, `installer\MdViewer.Installer`.

## Conventions and tests

- Nullable reference types, implicit usings, warnings as errors, and locked NuGet restores are configured in `Directory.Build.props`.
- Core behavior is tested with xUnit in `tests\MdViewer.Core.Tests`; rendering/security coverage is concentrated in `MarkdownRendererTests.cs`, and file I/O coverage in `MarkdownFileLoaderTests.cs`.
- Repeatable renderer timing and allocation scenarios live in the package-free console project `tests\MdViewer.Performance`.
- Standard validation: `dotnet restore MdViewer.slnx`, `dotnet test MdViewer.slnx --no-restore`, and `dotnet build MdViewer.slnx -c Release --no-restore`.
- Preserve the read-only, no-network, deny-by-default rendering model when changing performance-sensitive code.
