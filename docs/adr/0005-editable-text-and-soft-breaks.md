# ADR 0005: Editable Text Positions and Soft Breaks

- Status: Accepted (STORY-006 checkpoint)
- Date: 2026-11-05
- Deciders: MicroslopWords maintainers

## Context

STORY-006 is the editable transition for supported imported text. It introduces
caret and selection identities, keyboard and IME input, paragraph split/merge,
grapheme deletion, and soft line breaks. AD-4 and AD-12 are recorded assumptions,
so the story checkpoint must fix the concrete representation before commands and
layout depend on it.

## Decision

- Selection positions are immutable `TextPosition` values made of paragraph ID,
  run ID, a UTF-16 offset inside that run, and a line affinity. A `Selection` is an
  anchor/focus pair; a caret is collapsed. UUIDs identify positions across edits,
  and no document-order integer crosses a command boundary.
- `SelectionMap` is the immutable before/after selection pair produced by a
  selection-aware command. `CommandHistory` stores the pair and normalizes the
  active selection after every apply, undo, and redo.
- `CommandHistory` owns the active selection. Commands that change text implement
  `ISelectionEditCommand` and return the resulting selection; other commands keep
  the current selection after normalization.
- A soft line break is stored as `\n` inside `Run.Text`. This keeps one paragraph
  and its formatting intact, maps directly to OOXML `w:br`, and lets layout force
  a line break without a second block type.
- Documents that imported with unsupported-content warnings remain read-only in
  this story. New documents and clean imports become editable; the full
  unsupported-content policy remains STORY-016.
- IME composition is transient control state. Only the committed text is inserted,
  through the same command path as keyboard input, exactly once.

## Consequences

Positive:

- Caret, selection, typing, paragraph structure, and soft breaks share one
  command-based edit path, so undo/redo and future collaboration stay coherent.
- UUID-based positions survive unrelated edits and layout repagination.
- Soft breaks round-trip as structure instead of literal characters.

Negative:

- Every text command must provide selection mapping; commands that do not are
  normalized conservatively.
- `\n` is reserved inside run text and must be escaped by future format adapters
  that have a different literal-newline meaning.
