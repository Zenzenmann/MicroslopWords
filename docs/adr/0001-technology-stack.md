# ADR 0001: Technology Stack

- Status: Accepted
- Date: 2026-10-22
- Deciders: MicroslopWords maintainers

## Context

MicroslopWords needs a native desktop word processor that runs on Windows, macOS, and Linux, renders rich text deterministically, and round-trips DOCX. The team is small and the codebase must remain approachable for open-source contributors.

## Decision

- **.NET 10 LTS** for the runtime and SDK.
- **Avalonia 12** for the desktop UI and windowing.
- **SkiaSharp** for rasterization and custom editor drawing.
- **HarfBuzzSharp** for text shaping.
- **Open XML SDK** for DOCX import/export.
- **xUnit v3 + Shouldly + Avalonia.Headless** for tests.
- **MIT** license.

## Consequences

Positive:

- One C# codebase across all three desktop platforms.
- Avalonia and Skia are mature, permissively licensed, and support custom controls.
- HarfBuzz provides correct complex-script shaping.
- Open XML SDK is the reference OOXML implementation.

Negative:

- Avalonia's text input and IME behavior must be validated early.
- The editor surface cannot rely on a browser engine or HTML contenteditable.
- SkiaSharp and HarfBuzzSharp versions are coupled to Avalonia and must move together.

## Alternatives Considered

- **WPF** — excellent Windows support, but Windows-only.
- **WinUI 3** — modern Windows UI, but no macOS/Linux path and heavier packaging.
- **Electron/WebView editor** — fast to prototype, but poor resource use and layout control.
- **AvaloniaEdit** — useful for code editing, not a paginated rich-text document surface.
