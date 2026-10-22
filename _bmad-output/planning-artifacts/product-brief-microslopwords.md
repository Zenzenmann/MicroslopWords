---
title: "MicroslopWords Product Brief"
status: "final"
created: "2026-10-22"
updated: "2026-10-22"
---
# Problem/Opportunity

Microsoft Word is powerful but heavy and over-engineered for many everyday tasks. Many users just need to open, edit, and save simple DOCX files reliably across platforms without dealing with bloated installers or complex feature sets. There's an opportunity for a small, open-source DOCX editor that focuses on correctness and hackability over feature parity.

# Target Users

- **Students and researchers** who need to edit simple assignments and papers in DOCX format.
- **Open-source enthusiasts** who want a lightweight, inspectable word processor they can extend.
- **Developers and writers** working in mixed OS environments (Windows, macOS, Linux) who need deterministic, predictable behavior.

# Value Proposition

MicroslopWords provides a fast, predictable, cross-platform DOCX editing experience with a small, understandable codebase. It emphasizes clean DOCX round-tripping, deterministic layout, and undo/redo through a command-based architecture.

# What Makes It Different

- **Simplicity over fidelity**: Practical DOCX editor, not a 100% Word clone.
- **Deterministic layout**: Pure layout function of document state and styles, testable headlessly.
- **Clean architecture**: Strict dependency boundaries (Core has no UI/format deps), enabling easy inspection and modification.
- **Spec-driven development**: Built through BMAD with a single source of truth (SPEC.md), automated story-to-PR harness.
- **Open and hackable**: MIT licensed, small codebase designed for learning and contribution.

# v1 Scope

- Create, open, edit, and save `.docx` documents
- Paragraph and character formatting, styles, undo/redo, find/replace
- Tables, images, headers and footers, page numbers
- Pagination and print/PDF export
- Windows, macOS, and Linux desktop builds

# Out of Scope

- VBA/macros
- Real-time coauthoring
- Cloud sync
- Add-in marketplace
- 100% Microsoft Word fidelity

# Success Signals

- **Usable milestone**: A usable editor for simple real-world DOCX files (the first public milestone), not feature parity.
- **Round-trip quality**: Clean round-tripping of common DOCX constructs through the test corpus.
- **Build health**: Consistent `dotnet build`, `dotnet test`, and `dotnet format` passing across platforms.
- **Hackability**: Contributors can understand and modify the codebase without digging through sprawling abstractions.

# Risks and Assumptions

- **Assumption**: Users prioritize reliability and simplicity over advanced Word features.
- **Assumption**: A small codebase with strict boundaries is maintainable by a small contributor base.
- **Risk**: DOCX is complex; achieving clean round-tripping for "simple real-world files" may require careful scoping.
- **Risk**: Layout determinism across platforms requires discipline with Skia/HarfBuzz versions (locked to Avalonia 12.1.4 with SkiaSharp 3.119.4, HarfBuzzSharp 8.3.1.3).
- **Risk**: Spec-driven automation is powerful but adds process overhead.

# Open Questions

- **Corpus definition**: What constitutes "simple real-world DOCX files" for the first milestone? (Assumed: common paragraph/character styles, basic tables/images/headers/footers without complex field codes.)
- **Performance targets**: Are there any specific performance expectations for large documents? (Assumed: reasonable for typical small-to-medium documents; not optimized for 1000+ page novels initially.)
- **Testing coverage**: How comprehensive should the golden/snapshot tests be for layout? (Assumed: focus on critical layout paths that affect pagination and common formatting.)
