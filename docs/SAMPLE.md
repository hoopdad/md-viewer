# md-viewer sample

This page exercises **CommonMark**, GitHub-flavored Markdown, and a few
widely-used extensions. It is safe to open because md-viewer renders documents
as read-only content.

## Text and links

Use *emphasis*, **strong emphasis**, ~~strikethrough~~, `inline code`, and an
[external HTTPS link](https://example.com).

> Markdown should be pleasant to scan, with comfortable line length and strong
> visual hierarchy.

## Task list

- [x] Open Markdown from Explorer
- [x] Keep the source read-only
- [ ] Enjoy the document

## Table

| Capability | Behavior | Status |
| --- | --- | ---: |
| Raw HTML | Rendered as text | Safe |
| Remote images | Replaced with a placeholder | Blocked |
| Code fences | Preserved without scripts | Ready |

## Code

```csharp
var document = MarkdownFileLoader.Load(path);
var rendered = new MarkdownRenderer().Render(document.Markdown, document.DisplayName);
```

## Other conventions

Definition
: A term followed by its description.

The formula extension remains inert: $a^2 + b^2 = c^2$.

---

![This remote image should be blocked](https://example.com/tracker.png)
