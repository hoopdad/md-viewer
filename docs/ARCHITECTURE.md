# md-viewer architecture

## Product constraints

md-viewer is a Windows desktop reader for `.md` and `.markdown` files. It never
edits or saves the source document. Windows shell integration launches the
application with exactly one quoted file path.

## Components

- `MdViewer.Core` opens a bounded, read-only file snapshot and converts Markdown
  to inert HTML with Markdig.
- `MdViewer.App` hosts the generated document in a hardened WebView2 inside a
  native WPF shell.
- `MdViewer.Installer` installs per-user binaries and registers the ProgID,
  Open With capability, and explicit Explorer context-menu verb.

## Rendering and trust boundary

Markdown files are untrusted input.

1. Files are opened once with `FileAccess.Read` and a maximum size of 8 MiB.
2. Raw HTML is disabled before Markdown rendering. Exact HTML `<img>` elements
   receive narrow compatibility handling; only `src`, `alt`, `title`, `width`,
   and `height` are considered, and arbitrary styles and event handlers remain
   disabled.
3. Relative PNG, JPEG, GIF, WebP, and SVG images inside the Markdown document's
   directory tree are mapped to opaque same-origin URLs and streamed on demand.
   Remote, absolute, UNC, unsupported, missing, and directory-escaping images
   become inert placeholders. This prevents tracking pixels and SMB credential
   leakage while supporting repository-local documentation assets.
4. Only `https`, `http`, `mailto`, and in-document fragment links survive
   rendering. All other targets become inert.
5. Generated HTML carries a deny-by-default Content Security Policy.
6. WebView2 JavaScript, host objects, web messages, downloads, permissions,
   context menus, developer tools, and unsolicited navigation are disabled.
7. External links are brokered by the native app and opened only after the
   destination scheme is independently validated.

## Windows integration

The installer registers `md-viewer.Markdown` and the exact command
`"INSTALLDIR\md-viewer.exe" "%1"`. It does not use a command shell or script
trampoline. Modern Windows protects an existing user-selected default via
`UserChoice`; the installer registers md-viewer as a capable handler and may
open Default Apps so the user can confirm the default when Windows requires it.

## Performance

The parser pipeline is created once, input is read sequentially, and rendering
is performed from an in-memory snapshot. On shell launch, file parsing runs in
parallel with WebView2 initialization. Title and word-count scans are
allocation-free, image URLs are resolved while walking the parsed document, and
the WebView receives one static HTML document. Local images use native lazy
loading and asynchronous decoding, remain out of the initial HTML payload, and
are streamed only when WebView2 requests them. Installed binaries are published
with composite ReadyToRun compilation to reduce managed startup JIT work. No
network resources, plugins, syntax-highlighting scripts, or document-selected
extensions are loaded.

WebView2 process startup and HTML layout dominate normal document-open latency.
Rewriting the parser in Rust, C, or assembly would add interop and deployment
cost without addressing that bottleneck. Native code would only be justified by
profiling unusually large documents that spend most of their time inside the
Markdown parser; the current 8 MiB input bound and Markdig pipeline do not make
that tradeoff worthwhile.
