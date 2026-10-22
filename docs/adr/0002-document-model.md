# ADR 0002: Document Model and Layout Boundary

- Status: Accepted
- Date: 2026-10-22
- Deciders: MicroslopWords maintainers

## Context

A word processor needs a model that supports editing, undo/redo, layout, serialization, and future collaboration. Coupling the model to Avalonia or OOXML would make testing and evolution difficult.

## Decision

- The document is a tree of **sections, blocks, and runs**.
- Every block and run has a stable `Guid` (or monotonic ID) that is never reused.
- All mutations are **commands** with an `Apply` and `Revert` operation.
- A command bus owns undo/redo history.
- Layout is a **pure function** of `(document, styles, page settings, layout constraints)` and returns immutable layout boxes.
- Rendering consumes layout boxes and never mutates the document.
- DOCX import/export maps between the model and OOXML in `MicroslopWords.Formats.Docx`.
- Core has no dependency on Avalonia, Skia, Open XML, or filesystem UI.

## Consequences

Positive:

- Layout can be tested headlessly and deterministically.
- Undo/redo is centralized and auditable.
- A future collaboration layer can replicate commands.
- Format importers are replaceable.

Negative:

- More upfront structure than a mutable text buffer.
- Large documents require careful change tracking to avoid full re-layout.
- Commands must be designed carefully to remain invertible.

## Follow-ups

- Define the initial block types: paragraph, heading, list item, table, image, page break.
- Define run properties: font family, size, weight, style, color, underline, highlight.
- Define section properties: page size, margins, orientation, headers, footers.
- Add an incremental layout invalidation strategy after the first vertical slice.
