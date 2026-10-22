---
status: final
stepsCompleted: [1, 2, 3, 4]
created: 2026-10-22
updated: 2026-10-22
project: MicroslopWords
inputDocuments:
  - ../AGENTS.md
  - ../docs/bmad/README.md
  - ../docs/adr/0001-technology-stack.md
  - ../docs/adr/0002-document-model.md
  - ../docs/adr/0003-spec-driven-development.md
  - specs/spec-microslopwords/SPEC.md
  - specs/spec-microslopwords/.memlog.md
  - prd-microslopwords.md
  - architecture.md
  - ux-design.md
---

# MicroslopWords - Epic Breakdown

## Overview

This document decomposes the MicroslopWords v1 contract into six user-value epics and twenty independently reviewable stories. The execution order is the story order below; every story depends only on earlier stories.

**Source precedence.** `SPEC.md` is canonical. Where the PRD or UX text is narrower or stale, the SPEC and `.memlog.md` decisions govern. The main resolved conflicts are recorded at the end of this document.

**Bootstrap baseline.** The current `src/` and `tests/` code is a scaffold, not a feature claim. It creates a one-paragraph document, appends runs through a simple command, wraps text by an average-character approximation, draws line rectangles, imports and exports paragraph text only, shows a placeholder shell, and supports only `msw to-text`. Stories may complete, replace, or delete scaffold code. No story may describe scaffold behavior as already satisfying a capability.

**Story quality bar.** Each story is sized for roughly 1-3 developer days and one independently reviewable PR. Every story obeys the global definition of done below, uses stable IDs, routes edits through commands, keeps layout pure, preserves one-way dependencies, adds tests at the applicable layer, and does not add a package version directly to a `.csproj`.

### Global Definition of Done

- Acceptance criteria are implemented with edge cases.
- New behavior has unit, integration, layout golden, DOCX corpus, or headless UI coverage as applicable.
- Every edit is an invertible command through `CommandHistory`; new edits clear redo; existing IDs are never reused or renumbered.
- Layout remains a pure function of document state, styles, page settings, fonts, and constraints.
- Project references obey AD-1; any new edge needs an approved ADR.
- Package versions stay central in `Directory.Packages.props`; no floating versions.
- `dotnet build`, `dotnet test`, and `dotnet format --verify-no-changes` pass on Windows, macOS, and Linux CI.
- No secrets, signing keys, or generated build output are committed.
- Public behavior that is not obvious is documented; spec ambiguity is recorded in `deferred-work.md`, not invented in code.

## Requirements Inventory

### Functional Requirements

The FRs are stable and are not renumbered. This inventory is condensed; the full wording is in the PRD and the capability contract is in `SPEC.md`.

| FR | Scope | Primary stories |
|---|---|---|
| FR-1 | Create a blank document or open a `.docx`; safe cancel/failure; one document per window. | 001, 006 |
| FR-2 | Save and Save As with atomic replacement, path update only on success, and dirty clearing only on success. | 002 |
| FR-3 | Visible dirty state, autosave snapshots, recovery prompt, and source-safe restore. | 002, 003, 017 |
| FR-4 | Unicode typing and IME composition committed exactly once at the caret. | 006 |
| FR-5 | Paragraph split/merge, soft line break, and a one-paragraph minimum. | 006 |
| FR-6 | Grapheme-aware deletion, word deletion modifiers, and selection deletion. | 006 |
| FR-7 | Pointer and keyboard caret placement and navigation at every zoom. | 006, 007 |
| FR-8 | Mouse and keyboard selection, including words, paragraphs, and multi-page ranges. | 007 |
| FR-9 | Internal cut, copy, and paste preserving available rich content. | 007, 012, 013 |
| FR-10 | System clipboard interop with plain text and HTML; untrusted payload handling. | 007, 012, 013 |
| FR-11 | Undo/redo for every mutation, typing coalescing, and redo clearing. | 006, 007 |
| FR-12 | Character formatting, mixed state, insertion formatting, and toggles. | 008 |
| FR-13 | Clear direct character or paragraph formatting without deleting content. | 008, 009 |
| FR-14 | Paragraph alignment, spacing, line spacing, and indentation. | 009 |
| FR-15 | Bulleted/numbered lists up to nine levels and explicit page breaks. | 009 |
| FR-16 | Apply and create named paragraph and character styles. | 010 |
| FR-17 | Edit, rename, and delete styles safely without flattening. | 010 |
| FR-18 | Find body text across runs with case and whole-word options. | 011 |
| FR-19 | Replace and Replace All with count, formatting inheritance, and one undo step. | 011 |
| FR-20 | Insert and restructure tables, merge/split cells, and Tab navigation. | 012 |
| FR-21 | Format tables and edit full cell content; rows stay intact across pages. | 012 |
| FR-22 | Insert embedded PNG/JPEG/GIF-first-frame images with size limits and alt text. | 013 |
| FR-23 | Resize, edit, remove, and safely placeholder inline images. | 013 |
| FR-24 | Edit per-section headers and footers, first-page variants, and link/unlink. | 014 |
| FR-25 | Render headers and footers on every applicable page without silent overlap. | 014, 015 |
| FR-26 | Insert live page-number and Page X of Y fields with formats and restarts. | 014 |
| FR-27 | Deterministic pagination using section page settings and supported content. | 004, 005, 015, 019 |
| FR-28 | Zoom 25-400%, fit width/page, pointer-anchored wheel zoom, and view state. | 005, 017, 019 |
| FR-29 | Print through the system dialog with copies and page ranges. | 015 |
| FR-30 | Export all pages or a range to a searchable PDF with embedded/subset fonts. | 015, 018 |
| FR-31 | Import all supported DOCX constructs without flattening or executing content. | 001, 008-014, 016 |
| FR-32 | Export a valid, non-active-content DOCX package. | 002, 008-014, 016 |
| FR-33 | Round-trip 100% of text and at least 95% of supported constructs. | 002, 008-014, 016 |
| FR-34 | Detect, disclose, preserve, or block loss of unsupported content. | 001, 002, 016 |
| FR-35 | Platform-standard keyboard shortcuts, discoverable from menus and Help. | 007, 017 |
| FR-36 | Keyboard-only core journeys, accessibility APIs, high contrast, and scaling. | 017 |
| FR-37 | Persistent light/dark/system theme, default zoom, and autosave settings. | 017 |
| FR-38 | Consistent Windows, macOS, and Linux behavior and native dialogs. | 017, 020 |
| FR-39 | CLI `inspect`, `validate`, and `to-text` with stable output and exit codes. | 018 |
| FR-40 | CLI `export-pdf` with desktop-equivalent pagination and atomic output. | 018 |

### NonFunctional Requirements

| NFR | Scope | Primary stories |
|---|---|---|
| NFR-1 | Deterministic layout and pinned fonts/engines on each platform. | 004, 005, 019 |
| NFR-2 | Reference-hardware performance budgets for open, typing, scroll, zoom, save, PDF, and memory. | 006, 019 |
| NFR-3 | Stability and data integrity under interrupted saves, crashes, and malformed inputs. | 002, 003, 016 |
| NFR-4 | Untrusted DOCX/clipboard content, package limits, no execution, and no network fetch. | 013, 016, 018, 020 |
| NFR-5 | WCAG 2.1 AA where applicable, keyboard access, high contrast, and 100-200% scaling. | 017 |
| NFR-6 | Self-contained, non-admin installs for the specified OS/architecture matrix. | 020 |
| NFR-7 | Automated or documented verification for every FR; headless layout and corpus tests. | All stories |
| NFR-8 | Offline and local-first behavior with no account, cloud, or server dependency. | 001, 002, 015, 016, 018, 020 |
| NFR-9 | Privacy: no telemetry, no document content or paths in logs, local recovery only. | 003, 016, 017, 020 |
| NFR-10 | Small codebase, architecture tests, warnings as errors, central packages, and format gates. | All stories |

### Additional Requirements

All architecture decisions are binding. The table identifies where each AD most directly constrains the backlog.

| AD | Constraint | Stories |
|---|---|---|
| AD-1 | One-way production dependency graph; new edges require an ADR. | All |
| AD-2 | Immutable records; stable section/block/run IDs; IDs never enter OOXML. | All edits |
| AD-3 | Every mutation is an `IEditCommand`; `CommandHistory` alone owns undo/redo. | All edits |
| AD-4 | Selection/caret are logical Core identities, not pixels or document indices. | 006, 007, 011 |
| AD-5 | Layout is pure and deterministic with an explicit `FontCatalog`. | 004, 005, 019 |
| AD-6 | Rendering consumes layout and never reflows or mutates. | 005, 015, 018 |
| AD-7 | OOXML mapping lives only in `Formats.Docx`. | 001, 002, 008-016 |
| AD-8 | Round-trip means semantic preservation, not byte identity. | 002, 008-016 |
| AD-9 | Unsupported content is classified, preserved, or diagnosed through one matrix. | 001, 002, 016 |
| AD-10 | Layout cache invalidation is revision- and content-addressed; stale results are dropped. | 019 |
| AD-11 | MVVM shell with one custom editor control; no document mutation in views. | 001, 005-017 |
| AD-12 | Keyboard, IME, paste, drop, and accessibility insertion commit through commands. | 006, 007 |
| AD-13 | One writer mutates the document; long work runs on immutable snapshots with cancellation. | 002, 003, 015, 017, 019 |
| AD-14 | DOCX is untrusted; bounded package/image I/O; atomic saves; no execution or network. | 002, 013, 016, 018 |
| AD-15 | Boundary failures return `OperationResult` and stable `MSW####` diagnostics. | 001, 002, 015, 016, 018 |
| AD-16 | Diagnostics are local and private; no content, filename, path, or identity logs. | 003, 016, 017, 020 |
| AD-17 | xUnit v3, Shouldly, layout goldens, corpus tests, headless UI tests, and architecture tests. | All |
| AD-18 | Self-contained desktop releases from one source tree; signing material only in CI secrets. | 020 |
| AD-19 | CLI is deterministic and non-interactive; widening it to layout/rendering requires an ADR. | 018 |
| AD-20 | Avalonia, SkiaSharp, and HarfBuzzSharp move as one pinned version set. | 004, 020 |
| AD-21 | Central package versions; no floating or direct project versions. | All |
| AD-22 | Screen, print, and PDF consume the same `DocumentLayout`. | 005, 015, 018 |

### UX Design Requirements

| UX-DR | Actionable requirement | Stories |
|---|---|---|
| UX-DR1 | Implement the paper-first shell, tokens, window geometry, menu/toolbar/status regions, and page-first canvas. | 001, 005, 017, 020 |
| UX-DR2 | Implement the formatting toolbar with selection reflection, mixed state, overflow, tooltips, and keyboard access. | 008, 009, 010 |
| UX-DR3 | Implement the 264 px styles panel with document styles, previews, current style, and keyboard apply. | 010 |
| UX-DR4 | Implement the Navigation/Find panel with heading navigation, search, result count, replace row, and empty states. | 011 |
| UX-DR5 | Implement page canvas, rulers, page labels, status page/word/zoom values, and zoom interaction rules. | 005, 006, 019 |
| UX-DR6 | Implement header/footer canvas mode, mode strip, section label, focus behavior, and resolved field display. | 014 |
| UX-DR7 | Implement native file/print dialogs, app-modal dialogs, warning/error banners, and unsaved/recovery prompts. | 001, 002, 003, 012, 013, 015, 016 |
| UX-DR8 | Implement keyboard focus order, F6 region cycling, focus rings, Esc behavior, and shortcut discoverability. | 006, 007, 017 |
| UX-DR9 | Meet the accessibility floor: names, roles, states, announcements, high contrast, scaling, and no color-only meaning. | 017 |
| UX-DR10 | Implement light/dark/system themes, page legibility in dark mode, settings persistence, and responsive shell behavior. | 017, 020 |

### CAP Coverage Map

Every capability has at least one story. A capability may complete across several stories; the earliest story is shown first.

| CAP | Stories | Epics | FR coverage |
|---|---|---|---|
| CAP-1 | 001, 002, 003, 006 | 1, 3 | FR-1, FR-2, FR-3 |
| CAP-2 | 006, 007 | 3 | FR-4, FR-5, FR-6, FR-11 |
| CAP-3 | 005, 006, 007 | 2, 3 | FR-7, FR-8 |
| CAP-4 | 007, 012, 013 | 3, 4 | FR-9, FR-10, FR-11 |
| CAP-5 | 008, 009, 010 | 3 | FR-12, FR-13, FR-14, FR-15, FR-16, FR-17 |
| CAP-6 | 011 | 3 | FR-18, FR-19 |
| CAP-7 | 012 | 4 | FR-20, FR-21 |
| CAP-8 | 013 | 4 | FR-22, FR-23 |
| CAP-9 | 014 | 4 | FR-24, FR-25, FR-26 |
| CAP-10 | 004, 005, 006, 007, 015, 019 | 2, 3, 5, 6 | FR-27, FR-28 |
| CAP-11 | 015, 018, 020 | 5, 6 | FR-29, FR-30 |
| CAP-12 | 001, 002, 008, 009, 010, 012, 013, 014, 015, 016 | 1, 3, 4, 5 | FR-31, FR-32, FR-33, FR-34 |
| CAP-13 | 017, 020 | 5, 6 | FR-35, FR-36, FR-37, FR-38 |
| CAP-14 | 018 | 6 | FR-39, FR-40 |

### FR Coverage Map

| FR range | Stories |
|---|---|
| FR-1 to FR-3 | 001, 002, 003, 006, 017 |
| FR-4 to FR-6 | 006 |
| FR-7 to FR-8 | 006, 007 |
| FR-9 to FR-11 | 007, 012, 013 |
| FR-12 to FR-13 | 008, 009 |
| FR-14 to FR-15 | 009 |
| FR-16 to FR-17 | 010 |
| FR-18 to FR-19 | 011 |
| FR-20 to FR-21 | 012 |
| FR-22 to FR-23 | 013 |
| FR-24 to FR-26 | 014 |
| FR-27 to FR-28 | 004, 005, 015, 019 |
| FR-29 to FR-30 | 015, 018 |
| FR-31 to FR-34 | 001, 002, 008-016 |
| FR-35 to FR-37 | 007, 017 |
| FR-38 | 017, 020 |
| FR-39 to FR-40 | 018 |

## Epic List

### Epic 1: Open, Save, and Recover Real Documents

Users can bring a real DOCX into a trustworthy read-only page view, save without corrupting the source, and recover unsaved work after an abnormal exit.
**CAPs:** CAP-1; CAP-10 partial; CAP-12 partial.
**FRs:** FR-1, FR-2, FR-3, FR-27 provisional, FR-31 partial, FR-33 partial, FR-34 partial.
**Stories:** STORY-001, STORY-002, STORY-003.

### Epic 2: See the Page as It Will Print

Users see deterministically shaped and paginated pages at every supported zoom before editing; the same layout foundation later feeds print and PDF.
**CAPs:** CAP-10; CAP-3 partial; CAP-5 partial.
**FRs:** FR-27, FR-28.
**Stories:** STORY-004, STORY-005.

### Epic 3: Write, Select, and Format

Users can type and compose text, select and navigate it, exchange it through the clipboard, undo/redo, format characters and paragraphs, apply named styles, and find/replace.
**CAPs:** CAP-2, CAP-3, CAP-4, CAP-5, CAP-6; CAP-10 partial.
**FRs:** FR-4 to FR-19.
**Stories:** STORY-006, STORY-007, STORY-008, STORY-009, STORY-010, STORY-011.

### Epic 4: Build Structured Documents

Users can add editable tables, inline images, headers, footers, and live page numbers that paginate and round-trip as structure.
**CAPs:** CAP-7, CAP-8, CAP-9; CAP-4 partial; CAP-10 partial; CAP-12 partial.
**FRs:** FR-20 to FR-26.
**Stories:** STORY-012, STORY-013, STORY-014.

### Epic 5: Deliver, Preserve, and Include

Users can print and export PDF from the same layout, trust DOCX fidelity and unsupported-content disclosure, and use an accessible, themeable shell.
**CAPs:** CAP-11, CAP-12, CAP-13; CAP-10 partial.
**FRs:** FR-29 to FR-37.
**Stories:** STORY-015, STORY-016, STORY-017.

### Epic 6: Automate, Scale, and Ship

Developers and automation can inspect, validate, and export without the GUI; large documents meet the performance envelope; and clean cross-platform packages ship without secrets.
**CAPs:** CAP-14; CAP-10, CAP-11, CAP-13 partial.
**FRs:** FR-30, FR-38, FR-39, FR-40; NFR-2, NFR-6.
**Stories:** STORY-018, STORY-019, STORY-020.

## Epic 1: Open, Save, and Recover Real Documents

**Epic goal:** A user can open a DOCX from someone else, see a read-only page preview, save it atomically, and recover unsaved work after a crash without ever overwriting the source. This epic is independently valuable: it establishes the file lifecycle and page preview that every later editing story builds on.

<a id="story-001"></a>

### STORY-001: Open a DOCX and render a read-only page preview

**Goal (user story):** As a person who receives DOCX files, I can create a blank document or open a real DOCX and see its supported body content on paginated pages, so that I can inspect the document safely before editing exists.

**CAP mapping:** CAP-1 (partial: create/open foundation), CAP-10 (partial: provisional preview), CAP-12 (partial: supported text import and safe failure). **FRs:** FR-1, FR-27 provisional, FR-31 subset, FR-34 partial.

**Dependencies:** None. May replace the placeholder `MainWindow`, the current `DocxService.Load`, and the approximate preview path. The existing scaffold is not evidence that this story is complete.

**Acceptance criteria:**

- **Given** the app is running **When** the user chooses New **Then** an `Untitled` document with one empty paragraph and no file path is created, the title has no modified marker, and exactly one page is shown. Editing is not claimed until STORY-006.
- **Given** the app has a current document **When** the user chooses Open and selects a valid `.docx` **Then** supported body paragraphs and their text are imported, the document title/path/page count are set, and a read-only page preview is shown with visible page boundaries.
- **Given** loading takes longer than one second **When** the import is still running **Then** a progress indicator is shown; the previous document remains visible until the new one is ready; cancellation discards the pending result and leaves the previous document, undo history, caret, and selection unchanged.
- **Given** a missing, malformed, encrypted, or non-DOCX file **When** Open is attempted **Then** an actionable error names the file and reason, no partial document replaces the current one, and the app remains usable.
- **Given** a DOCX containing constructs the current importer cannot represent **When** it is opened **Then** the import records stable diagnostics for those constructs without executing them, renders only the supported content, and does not claim full fidelity. The full warning and preservation surface is STORY-016.
- **Given** an imported document **When** the user interacts with the page canvas **Then** normal selection, typing, formatting, and other edit commands are disabled and the UI communicates a read-only state; no document mutation occurs.
- **Given** an empty DOCX body or a body with no paragraphs **When** it is opened **Then** the preview shows one empty paragraph on one page rather than crashing or showing zero pages.

**Edge cases:** Unicode text and supplementary-plane characters in imported text; a body with only unsupported blocks; duplicate or missing relationships; a file locked by another process; a zero-byte file; a valid DOCX with an empty body. Deterministic shaping, final pagination, styles, tables, images, headers, and footers are not required here.

**Test hints:** Unit: import supported paragraphs and stable diagnostics; document creation with one paragraph. DOCX corpus: a small text-only fixture plus malformed/missing/empty fixtures. Headless UI: open/cancel/error state, read-only command disabling, page count. Golden: provisional first-page geometry/text for a fixed fixture. Architecture: no OOXML types outside `Formats.Docx`; no edit mutation from the view.

**Out of scope:** Editing, caret, selection, save, recovery, real HarfBuzz shaping, pagination v2, zoom controls, styles/tables/images/headers/footers, full unsupported-content warnings, print/PDF, and any claim that the scaffold's approximate layout is final.

<a id="story-002"></a>

### STORY-002: Save, Save As, dirty state, and atomic writes

**Goal (user story):** As a person editing a DOCX, I can save to the current path or a new path and trust that a failed save never damages the previous file or clears my unsaved state.

**CAP mapping:** CAP-1, CAP-12 (partial: safe export and no silent loss). **FRs:** FR-2, FR-3 dirty state, FR-32 subset, FR-33 subset. **NFRs:** NFR-3, NFR-8.

**Dependencies:** STORY-001.

**Acceptance criteria:**

- **Given** a document with an existing path and changes **When** the user chooses Save **Then** a valid DOCX is written to a temporary file in the destination directory, the complete write is flushed and closed, and the target is atomically replaced only after success; the modified marker clears only after the replacement succeeds.
- **Given** a document with no path **When** the user chooses Save **Then** Save As is invoked and the document path changes only after a successful write. Canceling Save As leaves the path, content, dirty state, caret, selection, and undo history unchanged.
- **Given** Save As is invoked for a document that already has a path **When** the new write succeeds **Then** the new path becomes current; **When** it fails or is canceled **Then** the old path and file remain unchanged.
- **Given** a locked target, read-only directory, full disk, interrupted write, or cancellation **When** saving **Then** the previous target file is byte-unchanged, no partial file is left at the target, the document stays dirty, and the error is actionable and retryable.
- **Given** a successful save **When** it completes **Then** the title and status bar show saved, undo history and selection are preserved, and reopening the saved file in MicroslopWords shows the same supported content and layout for the current construct set.
- **Given** import diagnostics indicate content the current exporter cannot represent **When** the user saves **Then** the save defaults to Cancel and requires an explicit loss acknowledgement before writing; no unsupported content is dropped silently. Full classification, preservation, and the loss-summary UI are completed in STORY-016.
- **Given** a save is in progress **When** the user cancels **Then** the write is canceled without touching the target and the document remains dirty.

**Edge cases:** Destination without `.docx` extension (add after confirmation per UX); overwrite confirmation owned by the platform; source and destination the same file; target deleted between open and save; path containing spaces or Unicode; first save; save after undo; save with no edits. Atomicity tests must inject failure after temp creation and before replacement.

**Test hints:** Unit: temp-write/replace service with fault injection, cancellation, and cleanup. DOCX: simple save/reopen fixture that opens without repair. Headless UI: dirty marker, Save As cancel, failure dialog state, undo history retention. Architecture: file-path policy in the caller, not `Formats.Docx`; no OOXML outside the adapter.

**Out of scope:** Recovery snapshots, autosave settings UI, complete unsupported-content preservation, full corpus fidelity, PDF export, and any format conversion beyond DOCX.

<a id="story-003"></a>

### STORY-003: Crash recovery snapshots and restore prompt

**Goal (user story):** As a person whose app exited abnormally, I can recover the most recent unsaved draft without the app ever overwriting my source file.

**CAP mapping:** CAP-1. **FRs:** FR-3. **NFRs:** NFR-3, NFR-9.

**Dependencies:** STORY-001, STORY-002.

**Acceptance criteria:**

- **Given** a document is dirty **When** the configured autosave interval elapses (default 60 seconds, configurable 30-300) **Then** a recovery snapshot is written atomically to a user-scoped local recovery location, the source file is never opened for writing, and the snapshot records the document identity, source path, and timestamp.
- **Given** a document is clean **When** the interval elapses **Then** no snapshot is written; a successful save deletes or supersedes that document's snapshots.
- **Given** the app starts after an abnormal exit with a recent snapshot **When** the user is offered recovery **Then** the prompt shows the document name, snapshot timestamp, and source path, and offers Open recovered draft and Discard.
- **Given** a recovered draft is opened **When** it loads **Then** it appears as an unsaved document whose content comes from the snapshot and whose source file remains untouched; saving requires an explicit Save/Save As. Until STORY-006 the recovered draft is read-only; this story does not claim the editing half of CAP-1.
- **Given** the user discards a snapshot **When** the action is confirmed **Then** the snapshot is deleted and the source file is byte-unchanged; discard without confirmation does not occur.
- **Given** a corrupt, missing, or unreadable snapshot **When** the prompt is shown **Then** the error is reported without a crash, the source is untouched, and Discard remains available.
- **Given** a snapshot write fails **When** editing continues **Then** the user is warned once, editing remains responsive, and no source or document content is logged.

**Edge cases:** Multiple snapshots for one source; source moved or deleted; two windows with different documents; read-only source; clock changes; snapshot file locked; recovery of a document containing content the current exporter cannot represent (disable recovery for that document with a diagnostic rather than writing a lossy snapshot). Fault-injection tests must simulate abnormal exit and verify the snapshot age bound.

**Test hints:** Unit: snapshot store create/rotate/discard, atomicity, source untouched, manifest privacy. Fault injection: kill before/after snapshot write and during save. Headless UI: launch recovery prompt, restore, discard confirmation, corrupt snapshot. Privacy test: no document text, filename, or path in logs. DOCX: simple recovered draft opens.

**Out of scope:** Cloud sync, telemetry/crash reporting, editing in the recovered draft before STORY-006, full preservation of opaque DOCX parts (STORY-016), and the settings UI for the interval (STORY-017). This story supersedes the architecture's stale deferred note that recovery is not in v1.

## Epic 2: See the Page as It Will Print

**Epic goal:** A user can trust the page they see: text is measured and shaped deterministically, pages are paginated from real metrics, and the canvas zooms without changing the document. This epic is independently valuable and establishes the layout foundation for print and PDF.

<a id="story-004"></a>

### STORY-004: Deterministic font catalog and HarfBuzz text shaping

**Goal (user story):** As a user opening a document, I see text shaped with real font metrics and an explicit fallback policy, so that pagination is reproducible instead of depending on hidden system state.

**CAP mapping:** CAP-10 (deterministic layout), CAP-2 support for Unicode/complex text, CAP-5 support for font family. **FRs:** FR-27, FR-4 shaping support. **NFR:** NFR-1.

**Dependencies:** STORY-001. **Human checkpoint required (`spec_checkpoint: true`).**

**Acceptance criteria:**

- **Given** the story starts **When** a font fallback or bundling policy is chosen **Then** a human checkpoint records the policy and licensing review in an approved decision record before any font is bundled; no font binary, subset, or license-restricted asset is committed without that approval and a third-party notice.
- **Given** a document, styles, page settings, and a `FontCatalog` **When** layout runs **Then** `LayoutEngine` measures and shapes text through HarfBuzz using only the explicit catalog; it does not discover system fonts, consult ambient culture, read the clock, use randomness, or mutate static state.
- **Given** a run of Unicode text **When** it is shaped **Then** glyph clusters preserve grapheme boundaries for Latin, combining marks, supplementary-plane characters, emoji sequences, and at least one complex script; line breaking uses shaped advances rather than an average-character-width approximation.
- **Given** the same document, styles, catalog, constraints, and pinned engine versions **When** layout is repeated **Then** line metrics, glyph positions, and box geometry are identical on the same platform.
- **Given** a document references a font missing from the catalog **When** it is laid out **Then** the approved fallback is used, the document font name is not rewritten, and a stable diagnostic identifies the substitution; no glyph is silently substituted without a diagnostic.
- **Given** a corrupt or unreadable font **When** the catalog is built **Then** the font is rejected with a diagnostic, layout fails safely or uses the approved fallback, and the app remains usable.

**Edge cases:** Missing glyphs; emoji ZWJ and skin-tone sequences; combining accents; CJK or other complex-script fallback; zero-width joiners; tabs and soft breaks; font file with no license metadata. The checkpoint must explicitly cover redistribution, subsetting, and PDF embedding rights.

**Test hints:** Unit: shaping clusters, grapheme boundaries, missing-glyph diagnostics, fallback order, deterministic repeated metrics. Golden: fixed test font with known metrics, no normalized IDs hiding shaping errors. Architecture: `Layout` references only `Core` and HarfBuzzSharp. License: font inventory and notice test. Manual: cross-platform golden comparison using the approved font set.

**Out of scope:** Bundling fonts before the checkpoint; final pagination layout (STORY-005); PDF embedding (STORY-015); the font picker UI (STORY-008); full right-to-left UI; network font fetch.

<a id="story-005"></a>

### STORY-005: Pagination v2 and zoomable page canvas

**Goal (user story):** As a user inspecting a document, I see correctly paginated pages and can zoom or fit them without changing the document or losing my place.

**CAP mapping:** CAP-10, CAP-3 partial (viewport geometry), CAP-13 partial (responsive canvas). **FRs:** FR-27, FR-28. **NFR:** NFR-1.

**Dependencies:** STORY-001, STORY-004.

**Acceptance criteria:**

- **Given** a document and page settings **When** layout runs **Then** pages honor page size, orientation, margins, paragraph spacing, line spacing, and supported content at this stage; paragraphs are not clipped or overlapped, and the status bar shows the physical page count.
- **Given** identical document state, styles, page settings, fonts, constraints, and engine versions **When** layout is repeated **Then** page count and page/line geometry are identical on the same platform.
- **Given** the page canvas **When** the user scrolls **Then** pages remain stacked in order with visible boundaries, the current page is identifiable, and the viewport does not become an infinite white sheet.
- **Given** the user chooses a zoom preset or Ctrl/Cmd+wheel **When** zoom changes **Then** zoom spans 25-400%, menu/keyboard zoom anchors at the viewport center, wheel zoom anchors at the pointer, fit-width and fit-page work, and the view does not jump to an unrelated position.
- **Given** zoom changes **When** the page is drawn **Then** pagination and document content are unchanged, page geometry scales correctly, and line/page hit rectangles remain accurate for later caret and selection overlays.
- **Given** a resized window **When** fit-width or fit-page is active **Then** zoom recomputes without altering the document, and the status bar keeps the zoom value readable.
- **Given** an empty document or a one-page document **When** it is laid out **Then** exactly one page is shown and no zero-page state is exposed.

**Edge cases:** Zoom at 25% and 400%; high-DPI rendering; Letter and A4; very tall content; repeated layout after a page-setting change; window at minimum size; scroll position after zoom. Tables, images, headers, footers, and page breaks are not required here and must not be claimed.

**Test hints:** Layout goldens for page geometry and pagination; unit tests for zoom transforms, pointer anchoring, fit calculations, and deterministic repeated layout. Headless UI: zoom presets, wheel zoom, resize/fit, page count. Rendering smoke: page boundaries and sharp text at high DPI.

**Out of scope:** Editing and caret/selection overlays (STORY-006/007), page breaks/lists (STORY-009), tables/images/headers/footers (STORY-012-014), print/PDF (STORY-015), incremental layout (STORY-019), and multi-column/RTL layout.

## Epic 3: Write, Select, and Format

**Epic goal:** A user can turn a preview into a document: type and compose text, select and navigate, use the clipboard and history, format characters and paragraphs, apply named styles, and find/replace. This epic delivers the core writing value.

<a id="story-006"></a>

### STORY-006: Editable caret, text input, IME, and paragraph editing

**Goal (user story):** As a writer, I can place a caret, type Unicode text and IME composition, split and merge paragraphs, and insert soft breaks, so that I can edit the document predictably with undo.

**CAP mapping:** CAP-2, CAP-3 partial, CAP-4 partial, CAP-10 partial. **FRs:** FR-4, FR-5, FR-6, FR-7 basic, FR-11 partial. **NFR:** NFR-2.

**Dependencies:** STORY-001 through STORY-005. **Human checkpoint required (`spec_checkpoint: true`).**

**Acceptance criteria:**

- **Given** Core selection types are introduced **When** a caret or selection is created **Then** it is an anchor/focus pair of block ID, run ID, and UTF-16 offset with line affinity, and no raw document-order index crosses a revision boundary; Core normalizes the active selection after every command.
- **Given** a rendered page **When** the user clicks in text **Then** the caret lands on the nearest grapheme boundary; clicking in the margin lands on the nearest line boundary; basic arrow/Home/End movement supports editing. Full word/page/triple-click navigation is STORY-007.
- **Given** the caret is active **When** the user types Unicode text **Then** the text inserts exactly once at the caret through a command, replaces any selection in one undo step, advances the caret, and never splits a supplementary-plane grapheme.
- **Given** an IME composition **When** the user composes and confirms **Then** preedit text is visible at the caret with a composition underline, shortcuts do not fire during composition, and the committed text is inserted exactly once without duplication, loss, or reordering.
- **Given** the caret inside a paragraph **When** the user presses Enter **Then** the paragraph splits at the caret with text before and after preserved in order and formatting; **When** Backspace is pressed at paragraph start or Delete at paragraph end **Then** paragraphs merge; each operation is undoable and restores the exact prior structure.
- **Given** the caret inside a paragraph **When** the user presses Shift+Enter **Then** a soft line break is inserted in the same paragraph without changing paragraph style or creating a new paragraph.
- **Given** deletion of a grapheme, word, or selection **When** it executes **Then** Backspace/Delete delete one grapheme, the platform word-delete modifier deletes a word, a selection delete removes exactly the selected content, and undo restores text, structure, and formatting.
- **Given** any edit **When** it completes **Then** the document always retains at least one paragraph, layout repaginates without save, and the caret stays visible and accurate at the current zoom.

**Edge cases:** Emoji with skin tone or ZWJ; combining marks; IME cancel; empty paragraph Enter/Backspace; deleting the only content; delete/merge across a paragraph with different formatting; typing at document start/end; rapid input. Unsupported-content safety rules are completed in STORY-016.

**Test hints:** Unit: command apply/revert, selection mapping, grapheme boundaries, paragraph split/merge invariants, typing coalescing boundaries. Headless UI: click placement, keyboard input, simulated IME adapter, caret visibility, undo/redo. Golden: edited page geometry. Manual IME smoke on Windows, macOS, and Linux. DOCX: text/soft-break fixture.

**Out of scope:** Full selection/navigation, clipboard, formatting, styles, find/replace, tables/images, and complete read-only policy for unsafe documents. Story 006 makes supported imported text editable; it does not make unsupported constructs editable.

<a id="story-007"></a>

### STORY-007: Selection, keyboard navigation, clipboard, and undo/redo

**Goal (user story):** As a writer, I can select text by mouse or keyboard, navigate efficiently, move content through the clipboard, and undo or redo every change, so that editing feels complete rather than fragile.

**CAP mapping:** CAP-3, CAP-4, CAP-2 partial, CAP-10 partial. **FRs:** FR-7, FR-8, FR-9, FR-10, FR-11. **NFR:** NFR-2.

**Dependencies:** STORY-006.

**Acceptance criteria:**

- **Given** text is visible **When** the user drags, Shift+clicks, double-clicks, triple-clicks, or presses Ctrl/Cmd+A **Then** the range, word, paragraph, or whole document is selected; Shift+Arrow extends from the anchor by grapheme or visual line; the selection can span pages and remains visible while scrolling.
- **Given** a selection **When** it is replaced, deleted, cut, or formatted **Then** exactly the selected content is affected and the operation is one undo step.
- **Given** keyboard navigation **When** the user presses arrows, Home/End, Ctrl/Cmd+Left/Right, or Page Up/Page Down **Then** movement follows grapheme, visual line, word, or viewport rules and the view scrolls to keep the caret visible; hit testing stays accurate after zoom and repagination.
- **Given** a non-empty selection **When** the user cuts or copies **Then** Cut removes it and places content on the system clipboard, Copy leaves the document unchanged, and Paste inserts at the caret replacing any selection; each mutation is undoable.
- **Given** content copied from MicroslopWords **When** it is pasted internally **Then** all constructs available at this story (runs, paragraph formatting, soft breaks, lists once STORY-009 exists) round-trip; table and image clipboard mapping is extended in STORY-012 and STORY-013.
- **Given** plain text, HTML, or RTF clipboard content **When** it is pasted **Then** plain text uses insertion-point formatting, HTML maps at least bold, italic, underline, paragraph breaks, and lists, unsupported formatting is discarded without changing text, and scripts, event handlers, external references, and hidden content are not executed or fetched. RTF is optional if HTML is supported.
- **Given** every document mutation **When** it is executed, undone, or redone **Then** history is exact; a contiguous typing burst coalesces until caret movement, formatting, or a one-second pause; a new edit clears redo; undo scrolls to the affected region; saving does not clear history and opening/creating a document does.
- **Given** no selection, empty clipboard, or unavailable history **When** the user invokes cut/copy/paste/undo/redo **Then** the operation is a safe no-op and menu/shortcut enabled state is correct.

**Edge cases:** Multi-page selections; mixed formatting; malicious HTML; clipboard unavailable; undo after save; redo after a new edit; coalescing across a one-second boundary; select-all on an empty document; selection anchored in a deleted block. Image paste is rejected with a message until STORY-013.

**Test hints:** Unit: selection map across edits, history coalescing, redo clearing, command inversion. Headless UI: drag/Shift-click/double/triple, keyboard navigation, shortcuts, enabled states. Clipboard adapter: fake system clipboard, HTML sanitizer, plain-text fallback, no network. Golden: multi-page selection overlay. DOCX: re-save after clipboard edit.

**Out of scope:** Rich table/image clipboard until their stories; drag-and-drop of text (not v1); RTF as a required format; find/replace; accessibility announcements (STORY-017); context menus beyond standard platform behavior.

<a id="story-008"></a>

### STORY-008: Character formatting and formatting toolbar

**Goal (user story):** As a writer, I can apply font, size, bold, italic, underline, strikethrough, color, and highlight to selected text or upcoming typing, so that I can control emphasis without leaving the keyboard or toolbar.

**CAP mapping:** CAP-5, CAP-10 partial, CAP-12 partial. **FRs:** FR-12, FR-13 character. **NFR:** NFR-1.

**Dependencies:** STORY-007.

**Acceptance criteria:**

- **Given** a selection or caret **When** the user sets font family, size 1-400 pt, bold, italic, single underline, strikethrough, font color, or highlight **Then** formatting applies only to selected runs or sets insertion formatting when there is no selection, without changing existing unselected text.
- **Given** a mixed selection **When** the toolbar is shown **Then** mixed values are indeterminate rather than blank or falsely uniform; applying a value makes the whole selection consistent.
- **Given** already formatted text **When** the user toggles bold, italic, underline, or strikethrough **Then** the property is removed; **When** the user chooses Clear Character Formatting **Then** direct character overrides are removed and the applicable style or document default is restored in one undo step.
- **Given** an invalid font size, color, or highlight **When** it is submitted **Then** the value is rejected with a clear message and the document is unchanged.
- **Given** a character-format change **When** it completes **Then** layout repaginates, the toolbar reflects the caret/selection, and the change is undoable.
- **Given** a document with character formatting **When** it is saved and reopened **Then** font family, size, bold, italic, underline, strikethrough, color, and highlight survive as run properties, and a missing font uses the approved catalog fallback with a diagnostic rather than rewriting the document font name.

**Edge cases:** Size exactly 1 and 400; 0, negative, or 401; mixed selection; highlight cleared; invalid color hex; a selection spanning multiple runs; direct formatting over a style; clear with no direct overrides; missing font.

**Test hints:** Unit: formatting commands, mixed-state calculation, clear behavior, undo. DOCX corpus: run-properties fixture round-trip. Golden: bold/italic/size/color layout. Headless UI: toolbar state, shortcut toggles, invalid input, accessible names. Font diagnostic integration with STORY-004.

**Out of scope:** Paragraph formatting and lists (STORY-009), named styles (STORY-010), PDF font embedding (STORY-015), full corpus fidelity (STORY-016), and theme-level UI font changes.

<a id="story-009"></a>

### STORY-009: Paragraph formatting, lists, and page breaks

**Goal (user story):** As a writer, I can align, space, and indent paragraphs, create up to nine levels of bullets or numbering, and insert visible page breaks, so that the document has professional structure.

**CAP mapping:** CAP-5, CAP-10 partial, CAP-12 partial. **FRs:** FR-13 paragraph, FR-14, FR-15.

**Dependencies:** STORY-008.

**Acceptance criteria:**

- **Given** selected paragraphs or a caret **When** the user sets left/center/right/justify alignment, space before/after, line spacing 0.5-3.0, or left/right/first-line/hanging indents **Then** every touched paragraph or the current paragraph changes, mixed values show indeterminate, and the document repaginates.
- **Given** invalid or negative values **When** they are submitted **Then** the value is rejected with a message and the document is unchanged.
- **Given** direct paragraph formatting **When** the user chooses Clear Paragraph Formatting **Then** direct alignment, spacing, and indentation are removed, the paragraph style is restored, list membership is retained, and the operation is one undo step.
- **Given** selected paragraphs **When** the user toggles bulleted or numbered list formatting **Then** items nest up to nine levels, continue or restart numbering, and visible numbering updates when items are inserted or deleted; lists round-trip as list structure, not literal bullet/number characters.
- **Given** the caret **When** the user inserts a page break **Then** following content moves to the next page without unintended empty paragraphs, the break is visible as an editor marker, and it can be selected and deleted.
- **Given** list or page-break changes **When** they complete **Then** they are undoable and persist through save, reopen, print, and PDF export.

**Edge cases:** A selection mixing list and non-list paragraphs; restarting numbering mid-list; deleting the last list item; nested level 9; page break at document start/end; invalid line spacing; mixed indentation; page break in a list. Rows/tables and headers are not involved yet.

**Test hints:** Unit: paragraph/list commands, numbering updates, clear behavior, undo. Layout goldens: indentation, spacing, list levels, page break pagination. DOCX corpus: list and page-break fixtures round-trip as structure. Headless UI: toolbar/menu toggles, mixed state, page-break marker selection.

**Out of scope:** Named styles (STORY-010), table keep-together (STORY-012), headers/footers (STORY-014), multi-column/RTL, and any list style gallery beyond the required bulleted/numbered behavior.

<a id="story-010"></a>

### STORY-010: Named styles and styles panel

**Goal (user story):** As a writer, I can apply built-in or custom named styles and edit their definitions once, so that headings and body text stay consistent without manual formatting.

**CAP mapping:** CAP-5, CAP-10, CAP-12. **FRs:** FR-16, FR-17.

**Dependencies:** STORY-009.

**Acceptance criteria:**

- **Given** a document **When** the style list is shown **Then** built-in paragraph styles include Normal, Title, and Heading 1 through Heading 3, the default character style exists, and user-created styles appear in the same document style set.
- **Given** a selection or caret **When** a paragraph style is applied **Then** every touched paragraph gets that style; **When** a character style is applied **Then** selected text gets it, or subsequent typing uses it when there is no selection; the style control shows current or mixed state.
- **Given** current formatting **When** the user creates a paragraph or character style **Then** a unique, non-empty name is required and duplicate names are rejected case-insensitively.
- **Given** an existing style **When** the user edits its formatting **Then** all users of that style update except where direct formatting overrides the changed property; **When** renamed **Then** all references update; **When** deleted **Then** paragraph content is reassigned to Normal or character content to the default character style; Normal and the default character style cannot be deleted.
- **Given** any style create/edit/rename/delete **When** it completes **Then** it is undoable, repaginates when layout changes, and persists through save and reopen.
- **Given** a saved document **When** its styles are re-imported **Then** style definitions and assignments remain styles and are not flattened into direct formatting.
- **Given** the styles panel **When** it is opened **Then** it is keyboard-focusable, shows the current style and document previews, and applies the focused style with Enter or Space.

**Edge cases:** Delete an in-use style; rename to an existing name; empty or whitespace name; style edit with mixed direct overrides; applying a style to a mixed selection; imported user styles; style deletion with no affected content; panel at narrow width.

**Test hints:** Unit: style command apply/edit/rename/delete, ID stability, case-insensitive uniqueness, reassignment. DOCX corpus: styles fixture round-trip without flattening. Layout goldens: style-driven repagination. Headless UI: panel focus/apply, current/mixed label, delete confirmation.

**Out of scope:** Theme gallery, style sets, inheritance editor, style-based table styles, localization, and changing the built-in set beyond the SPEC/PRD requirement.

<a id="story-011"></a>

### STORY-011: Find and replace

**Goal (user story):** As an editor, I can find text across runs with case and whole-word options and replace one or all matches as a single undoable action, so that I can correct documents quickly.

**CAP mapping:** CAP-6, CAP-4 partial, CAP-10 partial. **FRs:** FR-18, FR-19.

**Dependencies:** STORY-007, STORY-010.

**Acceptance criteria:**

- **Given** the document **When** the user presses Ctrl/Cmd+F **Then** the Find panel opens with focus in the search field and pre-fills the current selection if present; Esc closes it and returns focus to the active match.
- **Given** a query **When** the user selects Find Next or Find Previous **Then** matches move in document order, wrap with a visible notice at the ends, and all matches are highlighted while the active match is distinct and scrolled into view.
- **Given** Match case or Whole words **When** toggled **Then** search honors the option; both are off by default; matches that span multiple runs are found and the total count is reported.
- **Given** no matches **When** the search completes **Then** a no-results message appears and the caret and scroll position do not move.
- **Given** an active match **When** the user replaces it **Then** only that match changes; **When** Replace All is used **Then** every match changes, the replaced count is reported, and the whole operation is one undo step.
- **Given** replacement text **When** it is inserted **Then** it inherits the character formatting at the start of the matched text, an empty replacement deletes the match, and paragraph structure remains intact.
- **Given** replacement text that contains the search term **When** Replace All runs **Then** it terminates without looping and reports the actual count.
- **Given** search or replace **When** it runs **Then** the body only is searched, regular expressions are not supported, and the document is not mutated by search.

**Edge cases:** Match spanning runs with different formatting; replacement longer or shorter than the match; overlapping terms; case variants; whole-word punctuation; empty query; zero matches; replace all then undo; replace all after a selection; focus return on Esc.

**Test hints:** Unit: search across run boundaries, case/whole-word, counts, formatting inheritance, no-loop, one-step replace-all. Headless UI: panel open/focus, Enter/Shift+Enter, no-results state, replace confirmation/count. Undo/redo test. No DOCX change required.

**Out of scope:** Regex, headers/footers/table-only search (body text only), comments/tracked changes, search history, and localization.

## Epic 4: Build Structured Documents

**Epic goal:** A user can build the documents they actually send: tables with editable cells, inline images with alt text, and headers, footers, and page numbers that update with pagination. This epic delivers structured content as document structure, not flattened text.

<a id="story-012"></a>

### STORY-012: Tables

**Goal (user story):** As a report writer, I can insert and restructure a table, edit full cell content, merge and format cells, and have rows paginate and save correctly, so that tabular information stays readable.

**CAP mapping:** CAP-7, CAP-4 partial, CAP-10, CAP-12. **FRs:** FR-20, FR-21; FR-9/FR-10 table clipboard. **NFR:** NFR-4 for untrusted pasted table content.

**Dependencies:** STORY-009, STORY-010.

**Acceptance criteria:**

- **Given** a caret in the body **When** the user inserts a table and chooses rows and columns **Then** a table block is inserted with editable empty cells; the model supports at least 20 rows and 20 columns, the dialog defaults to 3 rows by 3 columns with a header row, and the table is not inserted inside a paragraph.
- **Given** a table **When** the user inserts/deletes rows or columns, merges a rectangular selection of cells, splits a merged cell, or deletes the table **Then** the structure changes correctly, the table remains selectable as a unit, and every operation is undoable.
- **Given** a cell **When** the user presses Tab or Shift+Tab **Then** the caret moves to the next or previous cell; Tab in the last cell of the last row appends a row.
- **Given** a cell **When** the user types or formats **Then** it contains one or more paragraphs with the same text, character, paragraph, and list tools as the body; content wraps within the cell.
- **Given** a table **When** the user sets column widths, table width, cell borders, cell shading, or header-row repetition **Then** the table stays within the page margins, does not overlap other content, and rows do not split across pages; tables break between rows.
- **Given** a table that crosses a page boundary **When** paginated **Then** rows remain intact and a repeated header row appears where enabled; imported tables remain editable.
- **Given** table content on the clipboard **When** cut/copy/paste is used **Then** table structure and formatting are preserved and the operation is undoable.
- **Given** a saved document **When** tables are exported and re-imported **Then** structure, cell content, widths, borders, shading, merges, and header repetition survive semantically and render in print/PDF when those outputs exist.

**Edge cases:** Non-rectangular merge request rejected; split of an unmerged cell; 20x20 table; deleting the last row/column; table at document start/end; table with an empty cell; page break between rows; header row enabled on a one-row table; pasted HTML table sanitized; nested table rejected. The PRD's 20x20 minimum governs over the UX dialog's narrower 12-column assumption.

**Test hints:** Unit: table commands, merges/splits, Tab navigation, undo. Layout goldens: widths, borders, shading, row keep-together, header repetition. DOCX corpus: table fixture round-trip, imported editable table, print/PDF page count later. Headless UI: insert dialog, Tab append row, selection, context actions. Clipboard: table mapping.

**Out of scope:** Nested tables, formulas, floating tables, table styles beyond direct borders/shading, row splitting across pages, and advanced table layout beyond the fixed/auto policy chosen for v1.

<a id="story-013"></a>

### STORY-013: Inline images

**Goal (user story):** As a report writer, I can insert a PNG, JPEG, or GIF image, size it without distortion, add alt text, and remove it safely, so that visual content survives layout and round-trip.

**CAP mapping:** CAP-8, CAP-4 partial, CAP-10, CAP-12. **FRs:** FR-22, FR-23. **NFR:** NFR-4.

**Dependencies:** STORY-008.

**Acceptance criteria:**

- **Given** the caret **When** the user inserts a PNG, JPEG, or GIF from a file **Then** the image is embedded at the caret, GIF uses its first frame, a file up to 50 MB is accepted, natural size is capped to content width with aspect ratio preserved, and the insertion is one undo step without changing surrounding text.
- **Given** an unsupported, corrupt, or oversized image **When** insertion is attempted **Then** the image is rejected with a message naming the reason, the document is unchanged, and SVG is explicitly rejected while naming supported formats.
- **Given** an inserted image **When** the user resizes with handles or sets exact width/height **Then** aspect ratio is locked by default and can be unlocked; resizing, alt-text edit, and delete are undoable.
- **Given** an image **When** it is copied, pasted, or deleted **Then** it follows the same undo behavior as text and stays inline within its paragraph; paragraph alignment controls horizontal placement.
- **Given** a missing or corrupt embedded image **When** the document opens **Then** a labeled placeholder shows the file name or alt text and a warning, the document does not crash, and surrounding layout does not collapse.
- **Given** a saved document **When** images are exported and re-imported **Then** media is embedded, relationships are valid, no duplicate media entries are created, and no external path or network fetch is required.
- **Given** the image layout **When** paginated **Then** the image's inline box is measured and paginated without clipping; print/PDF rendering is verified in STORY-015.

**Edge cases:** Exactly 50 MB; zero-byte; animated GIF; CMYK JPEG; huge pixel dimensions or decompression bomb rejected by a decode limit; empty alt text falls back to file name; multiple images sharing media; image at start/end of paragraph; image in a table cell later.

**Test hints:** Unit: image validation/codec metadata, size cap, alt text, undo. Layout goldens: inline image sizing and pagination. DOCX corpus: media fixture round-trip and relationship integrity. Headless UI: insert, corrupt placeholder, resize handles, alt text. Security: decode limits and no network.

**Out of scope:** Floating/text-wrapped images, SVG/WebP, external/linked images, cropping/editing pixels, image borders/effects, and print/PDF output (STORY-015).

<a id="story-014"></a>

### STORY-014: Headers, footers, and page numbers

**Goal (user story):** As a report writer, I can edit per-section headers and footers and insert live page numbers, so that every page carries the right furniture and page count.

**CAP mapping:** CAP-9, CAP-10, CAP-11 partial, CAP-12. **FRs:** FR-24, FR-25, FR-26.

**Dependencies:** STORY-005, STORY-009, STORY-010, STORY-013.

**Acceptance criteria:**

- **Given** a document **When** the user chooses Insert Header/Footer or double-clicks a header/footer zone **Then** the canvas enters header/footer mode with a section label, the body remains visible but is not editable, and Esc or Close returns focus to the last body caret without changing body text.
- **Given** header/footer mode **When** the user types or formats **Then** the zone supports paragraphs, character formatting, inline images, and page-number fields with the same editing commands, and changes are undoable.
- **Given** a section **When** the user enables a different first-page header or footer **Then** the first page uses the variant and other pages use the default; **When** a header or footer is linked to the previous section **Then** edits update everywhere, and unlinking creates an independent copy.
- **Given** a header or footer **When** the user inserts a page-number field **Then** decimal, upper/lower Roman, and upper/lower alphabetic formats work; start values and per-section restarts work; Page X of Y uses separate current-page and total-pages fields.
- **Given** edits or page-setting changes **When** pagination runs **Then** page-number values update without reopening the document, and headers/footers render on every applicable page within the margins.
- **Given** content exceeding the available header/footer area **When** layout runs **Then** the app shows a layout warning and does not silently overlap body text.
- **Given** a section with no header or footer **When** rendered **Then** the area is empty with no placeholder text or artifacts.
- **Given** a saved document **When** headers, footers, and page fields are exported and re-imported **Then** they survive semantically and match print/PDF output.

**Edge cases:** Multiple sections; link then unlink then edit; first-page-only variant; page-number restart; total pages changing after an edit; image in header; long header overflow; empty header; linked section with no prior section; page-number field in a table cell in a header.

**Test hints:** Unit: section header/footer model, link/unlink, field formatting and start values, undo. Layout goldens: header/footer placement, first-page variants, page-number updates, overflow warning. DOCX corpus: header/footer/page-field fixtures. Headless UI: mode entry/exit, focus return, body read-only in mode, page-number insertion. Print/PDF parity in STORY-015.

**Out of scope:** Odd/even headers, multiple independent header/footer variants beyond first/default, fields other than page/total, body page-number fields, multi-column sections, and RTL.

## Epic 5: Deliver, Preserve, and Include

**Epic goal:** A user can produce output and trust what happens to the document: print and PDF match the screen, unsupported content is disclosed rather than silently lost, and the app is accessible and themeable. This epic is the release-quality bar for user trust.

<a id="story-015"></a>

### STORY-015: Print and PDF export

**Goal (user story):** As a user with a finished document, I can print or export a PDF that matches the editor's pagination, headers, images, and page numbers, so that the recipient sees what I see.

**CAP mapping:** CAP-11, CAP-10, CAP-12 partial. **FRs:** FR-29, FR-30. **NFRs:** NFR-1, NFR-2.

**Dependencies:** STORY-005, STORY-009, STORY-010, STORY-012, STORY-013, STORY-014.

**Acceptance criteria:**

- **Given** a document **When** the user chooses Print **Then** the native print dialog opens with available printers, copies, and page choices (all, current, numeric range), and a summary reports the physical page count and pages that will print.
- **Given** printed output **When** it is produced **Then** supported formatting, tables, inline images, headers, footers, and page numbers match the app's pagination; page ranges refer to physical pages shown in the app.
- **Given** printer cancellation, unavailability, or failure **When** printing ends **Then** the document and its dirty state are unchanged and the error is actionable; PDF export remains available.
- **Given** Export PDF **When** the user chooses all pages or a range **Then** the PDF uses the document page size and orientation, embeds or subsets fonts, includes images/headers/footers/page numbers, and contains selectable searchable text.
- **Given** a successful export **When** the PDF is compared with the app **Then** the page count equals the on-screen count, no supported content is clipped, and the file opens in common viewers without errors.
- **Given** export cancellation or failure **When** it ends **Then** no partial file is left at the target and any existing target file is unchanged; the document and dirty state are unchanged.
- **Given** an invalid or out-of-bounds page range **When** print or export is requested **Then** it is rejected with a message and no output is written.
- **Given** the same `DocumentLayout` **When** screen, print, and PDF are produced **Then** they consume the same layout; no Word, LibreOffice, or external conversion process is launched, and output is deterministic for the same inputs.

**Edge cases:** One-page document; range ending at last page; target path same as input document; read-only target; cancel mid-export; missing font embedding rights; image-only page; header/footer on every page; no images; printer offline. PDF font licensing follows the STORY-004 policy.

**Test hints:** Rendering: PDF page count/geometry, text extraction/searchability, font embedding/subsetting metadata, image/header/footer presence. Fault injection: cancel and failure leave no partial file. Headless UI: print/export dialog, page ranges, cancel, status. Golden: PDF page count versus layout page count. Manual: print on supported platforms.

**Out of scope:** A separate print preview beyond the system dialog, export to formats other than PDF, cloud/print services, CLI export (STORY-018), and printer-driver-specific defects.

<a id="story-016"></a>

### STORY-016: DOCX fidelity, unsupported-content warnings, corpus, and security

**Goal (user story):** As a person editing a real document, I can save it without silent loss, see exactly what is preserved or omitted, and trust that active or malicious content is never executed.

**CAP mapping:** CAP-12; completes CAP-1, CAP-5, CAP-7, CAP-8, CAP-9 round-trip; CAP-10 for layout inputs. **FRs:** FR-2 unsupported save behavior, FR-31, FR-32, FR-33, FR-34. **NFRs:** NFR-3, NFR-4, NFR-7.

**Dependencies:** STORY-001 through STORY-015. **Human checkpoints required (`spec_checkpoint: true`, `done_checkpoint: true`).**

**Acceptance criteria:**

- **Given** any imported package part or construct **When** the importer classifies it **Then** one support matrix marks it supported, opaque-preserved, or unsupported, and import/export consult the same matrix; unknown non-active parts and relationships are preserved by default, opaque parts are stored immutably with path/content type/bytes, and opaque content is never interpreted, measured, or rendered.
- **Given** a document with macros, ActiveX, OLE, scripts, embedded executables, tracked changes, comments, content controls, external relationships, or unsupported fields/objects **When** it is opened **Then** every construct is detected and listed with a location and outcome; active content is never executed or fetched; macro-enabled packages are rejected or opened content-only read-only according to the approved policy, and the original remains untouched.
- **Given** unsupported content **When** the document opens **Then** a warning banner states the count and whether content is preserved, and a Review dialog lists each construct, location, and outcome (preserved, read-only, or omitted); the notice remains available from the warnings report; a supported-only document produces no warning.
- **Given** saving could drop unsupported content **When** the user saves **Then** the confirmation defaults to Cancel and offers Save a Copy and Save Anyway with an explicit loss summary; cancel leaves the source byte-unchanged, and no content is silently dropped.
- **Given** an untrusted package **When** it is imported **Then** hard limits are enforced: 250 MB uncompressed, 10,000 parts, 100:1 compression ratio, and 50 MB per image; absolute and parent-traversal part paths are rejected; zip bombs and decompression bombs are rejected; no network access occurs; document-controlled strings are never used as filesystem paths.
- **Given** diagnostics and logs **When** an error occurs **Then** codes and severity are stable and user-safe; no document text, file contents, filenames, paths, or user identifiers are logged or transmitted by default.
- **Given** the release corpus **When** it is version-controlled **Then** it contains at least 30 real-world DOCX documents spanning assignments, reports, specifications, and mixed-content files; ownership/licensing for every fixture is documented, and fixtures are offline and deterministic.
- **Given** the corpus **When** an open-edit-save round-trip runs **Then** every document opens without a crash, 100% of text and at least 95% of supported constructs retain semantic equivalence, opaque parts are preserved where safe, and the output reopens in Microsoft Word and LibreOffice without a repair prompt.
- **Given** a round-trip failure **When** the CLI validation command runs (after STORY-018) **Then** it reports the affected construct and package part with the stable exit contract.

**Edge cases:** Encrypted or malformed packages; empty body; unsupported-only documents; external images/relationships; missing media; duplicate parts; huge packages; macro-enabled files; tracked changes and comments; content controls; corrupt images; path traversal entries; zip bombs; unknown relationships; a document that cannot be edited safely. Corpus approval/ownership (PRD Q-1) remains a human decision.

**Test hints:** Corpus round-trip suite with per-construct assertions and text extraction. Security fixtures for traversal, zip bombs, oversized images, active content, and external relationships. Headless UI: warning banner, review list, save-loss confirmation default Cancel. Architecture tests: one-way dependencies and OOXML isolation. Privacy test: no content/path logging. Release manual: Word and LibreOffice reopen without repair.

**Out of scope:** Editing macros or unsupported constructs, executing active content, Word parity, adding new document features, and fixing every exotic package construct. If the corpus grows beyond the 1-3 day slice, record the additional fixtures in `deferred-work.md` rather than weakening the 30-fixture gate.

<a id="story-017"></a>

### STORY-017: Accessibility, keyboard map, themes, and settings

**Goal (user story):** As a user with assistive technology or a keyboard-only workflow, I can complete every core journey with visible focus and clear announcements, choose a theme, and keep my settings without touching document content.

**CAP mapping:** CAP-13, CAP-3 accessibility, CAP-10 accessibility, CAP-11 accessibility. **FRs:** FR-35, FR-36, FR-37, FR-38 partial. **NFRs:** NFR-5, NFR-9.

**Dependencies:** STORY-006 through STORY-016.

**Acceptance criteria:**

- **Given** the app **When** the user navigates by keyboard **Then** New, Open, edit, select, format, styles, table, image, header/footer/page number, find/replace, save, print, PDF export, recovery, and warning dismissal are all reachable with a logical focus order, visible 2 px focus ring, F6/Shift+F6 region cycling, and no focus trap; Esc exits the topmost layer or mode and restores focus to the invoker.
- **Given** a dialog or mode **When** it closes **Then** focus returns to the invoking control or the last body caret, not the top of the window.
- **Given** assistive technology **When** the UI is inspected **Then** menus, toolbar buttons, fields, tabs, dialogs, banners, style rows, find results, table position, and image selection expose role, name, and state; the canvas exposes document name, page count, current page, caret context, selected text, and active formatting through the platform accessibility layer, with a read-only Document text fallback.
- **Given** state changes **When** save completes, find results change, page count changes, validation fails, warnings appear, or recovery is available **Then** a polite announcement is made; typing is not announced character by character.
- **Given** high-contrast/forced-colors mode or 100-200% UI scaling **When** the app is used **Then** controls do not clip, text remains readable, focus remains visible, and no meaning depends on color alone; selection, caret, dirty, read-only, warning, and error states use text/icon/shape in addition to color.
- **Given** Settings **When** the user chooses light, dark, or system theme, a default zoom, or an autosave interval from 30-300 seconds **Then** the setting persists, can be reset, invalid values are rejected or clamped with a message, and changes apply without a restart where possible.
- **Given** dark mode **When** a document is shown **Then** only chrome/desk change; the page, document colors, and images stay trustworthy and legible.
- **Given** the keyboard map **When** menus and Help are inspected **Then** Windows/Linux use Ctrl and macOS uses Command, shortcuts display correctly, no shortcut fires during IME composition, unmodified letter keys do not trigger commands, and Help provides a searchable reference.
- **Given** settings storage **When** it is inspected **Then** it contains no document text, paths, or content; telemetry remains off by default.

**Edge cases:** 200% scaling; minimum window; both side panels at narrow width; reduced motion; IME active; settings file corrupt; focus after a warning dialog; screen reader in a table cell; high contrast with selection; missing platform text pattern (Document text fallback).

**Test hints:** Headless UI: keyboard journeys, focus order/return, F6, dialog trap/restore, shortcut suppression. Accessibility tree tests for names/roles/states/announcements. Settings unit tests for persistence/reset/clamping/privacy. Theme contrast and high-contrast smoke. Manual screen-reader smoke with Narrator, VoiceOver, and Orca.

**Out of scope:** Localization/RTL UI, a formal VPAT (PRD Q-7), mobile/web, and a native platform text pattern if the Document text fallback is the approved path.

## Epic 6: Automate, Scale, and Ship

**Epic goal:** Automation can inspect, validate, and export documents without the GUI; large documents meet the performance envelope; and clean, self-contained builds ship on every supported platform without committed secrets. This epic turns a working editor into a releasable product.

<a id="story-018"></a>

### STORY-018: CLI inspect, validate, to-text, and export-pdf

**Goal (user story):** As a developer or automation user, I can inspect, validate, extract text from, and export a DOCX from the terminal with deterministic output and stable exit codes, so that corpus checks and scripts do not need the GUI.

**CAP mapping:** CAP-14, CAP-11 partial, CAP-12 validation. **FRs:** FR-30 CLI, FR-39, FR-40. **NFRs:** NFR-4, NFR-8.

**Dependencies:** STORY-015, STORY-016. **Human checkpoint required (`spec_checkpoint: true`).**

**Acceptance criteria:**

- **Given** the CLI needs desktop-equivalent PDF output **When** implementation starts **Then** an approved ADR widens AD-19 so `Cli` may reference `Layout` and `Rendering` for PDF export while still not referencing `App`, Avalonia, or UI; architecture tests enforce the widened boundary and no other new edge is introduced.
- **Given** `msw inspect <input.docx>` **When** it runs **Then** it prints deterministic JSON containing section, paragraph, table, image, and page counts; page dimensions; and detected unsupported constructs, with stable property order and invariant formatting. It does not print document text or log content.
- **Given** `msw validate <input.docx>` **When** it runs **Then** it validates the package and supported constructs and exits 0 with no issues, 3 for warnings/unsupported constructs, and 2 for missing, unreadable, or malformed input; findings identify the construct and package part.
- **Given** `msw to-text <input.docx>` **When** it runs **Then** document text is written to stdout and diagnostics to stderr, using UTF-8 and `\n` line endings.
- **Given** `msw export-pdf <input.docx> <output.pdf>` **When** it runs **Then** it renders all pages with the same layout and pagination as the desktop, exits 0 on success, 2 for missing/malformed input, 3 for format/I/O/render/write failure, and 4 for cancellation only, and leaves no partial output on failure.
- **Given** a CLI PDF **When** its page count is compared with the desktop for the same input and engine versions **Then** the counts match and output contains supported formatting, images, headers, footers, page numbers, and searchable text.
- **Given** `--help`, `--version`, an unknown command, or a usage error **When** invoked **Then** help/version exit 0, usage errors exit 2, and no command prompts or accesses the network.
- **Given** any diagnostic command **When** it runs **Then** it is read-only except for explicit `export-pdf` output, does not modify the input, uses `CancellationToken`, and writes data to stdout and diagnostics to stderr.

**Edge cases:** Empty document; unsupported constructs; encrypted/malformed package; unwritable output path; canceled export; output path same as input; deterministic JSON across repeated runs; desktop/CLI page-count parity; missing arguments; process exit codes on all three platforms.

**Test hints:** CLI integration tests for every command, help/version, usage, malformed/missing input, and exit matrix. Golden JSON with stable ordering. Rendering: PDF page count/text parity. Atomic-output fault injection. Architecture: Cli references no App/Avalonia/UI and only the ADR-approved Layout/Rendering edge.

**Out of scope:** New CLI commands, interactive prompts, GUI launch, conversion to formats other than PDF, network access, and changing the CLI exit contract away from AD-19/SPEC CAP-14.

<a id="story-019"></a>

### STORY-019: Large-document performance and incremental layout

**Goal (user story):** As a user working on a long document with images, I can open, type, scroll, zoom, save, and export without the app freezing or producing stale pages.

**CAP mapping:** CAP-10, CAP-2 partial, CAP-3 partial, CAP-13 responsiveness. **FRs:** FR-27, FR-28, FR-4 performance. **NFR:** NFR-2.

**Dependencies:** STORY-005, STORY-006, STORY-015, STORY-017.

**Acceptance criteria:**

- **Given** a document revision **When** commands execute, undo, or redo **Then** `CommandHistory` alone increments a monotonic revision and every layout result records an input fingerprint covering revision, style version, page settings, constraints, and font/shaping version.
- **Given** an edit **When** layout invalidation runs **Then** it starts from the earliest affected section/block; a per-block measurement is reused only when block ID, content hash, applicable style, available width, and font stack are unchanged; a page is reused only when the pagination state at its start is identical; cached boxes are immutable.
- **Given** a rapid edit or async layout **When** the document has moved to a newer revision **Then** the older result is discarded and never painted; no stale caret, selection, or page boxes appear after undo/redo.
- **Given** a style change, page-setting change, font/catalog change, or undo/redo **When** layout runs **Then** all affected caches invalidate correctly and geometry matches a full relayout.
- **Given** the reference hardware and a 50-page document with 20 images **When** measured **Then** open is 3 seconds or less, keystroke-to-paint is 50 ms p95 or less, scroll/zoom is 100 ms p95 or less, a 10 MB save is 2 seconds or less, PDF export is 5 seconds or less, and peak memory is 500 MB or less.
- **Given** a large document **When** the user scrolls **Then** only visible pages are rendered or retained as needed, memory stays bounded, and the UI remains responsive.
- **Given** a canceled or failed layout/render **When** it ends **Then** the document is unchanged, no partial result is painted, and the app returns to a usable state.

**Edge cases:** Undo/redo after many edits; style/page-setting changes; font fallback; rapid typing; very long paragraphs; 20-image document; memory pressure; cancellation; benchmark variance. The first implementation may still perform a full relayout, but it must obey fingerprinting and stale-result discard.

**Test hints:** Benchmark harness with documented reference hardware and percentile reporting. Incremental-versus-full layout golden equivalence. Stale-result test with delayed async results. Memory profile for the 50-page/20-image fixture. Headless UI responsiveness smoke. Cancel/failure tests. No benchmark network dependency.

**Out of scope:** Collaboration/CRDT, multi-window, cloud, changing the deterministic layout contract, and feature work beyond performance/caching.

<a id="story-020"></a>

### STORY-020: Cross-platform packaging and release smoke tests

**Goal (user story):** As a user on Windows, macOS, or Linux, I can install or launch a self-contained build without administrator rights or a separate .NET runtime, and the release process does not expose signing secrets.

**CAP mapping:** CAP-13, CAP-11 partial, CAP-14 partial. **FRs:** FR-38. **NFRs:** NFR-6, NFR-7, NFR-9. **Release criteria:** RC-7, RC-9.

**Dependencies:** STORY-001 through STORY-019. **Human checkpoint required (`done_checkpoint: true`).**

**Acceptance criteria:**

- **Given** one source tree **When** release publishes run **Then** self-contained .NET 10 artifacts are produced for `win-x64`, `win-arm64`, `linux-x64`, `osx-x64`, and `osx-arm64`; each launches on a clean supported system without a separate .NET install and without administrator rights.
- **Given** a packaged app **When** it first launches offline **Then** it opens, creates, opens, edits, saves, prints, and exports PDF successfully, uses native file/print dialogs and platform menu/shortcut conventions, and does not require network access or an account.
- **Given** the release workflow **When** a version tag is built **Then** it runs build, unit/integration tests, layout goldens, corpus round-trip tests, architecture tests, and `dotnet format --verify-no-changes` on Windows, macOS, and Linux before producing artifacts.
- **Given** signing and notarization **When** release credentials exist **Then** macOS builds are notarized for direct download and Windows builds are signed when a certificate is available; credentials live only in CI secret storage and never in the repository, logs, artifacts, or story branches.
- **Given** no signing credential or a notarization failure **When** the release runs **Then** the release is blocked or follows the human-approved unsigned-build policy, with documented warnings; it never fabricates or commits a key.
- **Given** release artifacts **When** they are inspected **Then** they are versioned and checksummed, contain no generated build output or secrets, include third-party notices, and preserve file associations only where the platform allows.
- **Given** the Linux release **When** artifacts are chosen **Then** the approved packaging format is used with a self-contained `linux-x64` archive as the minimum; additional formats require a release decision and are not invented by the story.
- **Given** clean-VM smoke tests **When** they run **Then** launch, open/save, edit, table/image/header, print dialog, PDF export, CLI inspect/validate/export, and recovery are exercised, and the CLI PDF page count matches the desktop for the same input and engine versions.

**Edge cases:** Missing certificate; notarization rejection; ARM64 hosts; non-admin install; offline clean VM; Linux desktop without a native print dialog; secret scan finding a committed token; checksum mismatch; artifact missing a runtime asset. Signing keys, certificates, webhook URLs, and tokens must never be committed.

**Test hints:** CI packaging jobs per RID and OS; clean-VM smoke scripts; secret scanning; artifact/secret inspection; checksum verification; CLI/desktop PDF parity; launch without .NET and without admin; offline first launch. Manual release checklist for native dialogs and screen-reader smoke if not automatable.

**Out of scope:** App-store distribution, automatic updates, cloud services, localization, and new runtime identifiers beyond the required matrix (additional RIDs are additive). The exact Linux packaging format and signing policy (PRD Q-2/Q-4) remain human release decisions.

## Source Conflicts and Planning Assumptions

These are the points that need human awareness; none changes the story order above.

- **Zoom range:** UX says 50-400%; SPEC CAP-10 and the memlog say 25-400%. Stories use 25-400%.
- **Tables:** UX assumes basic row/column editing only; SPEC/PRD require merge/split, widths, borders, shading, header repetition, and rows intact across pages. STORY-012 follows SPEC/PRD.
- **Headers/footers:** UX lists per-section linking, first-page variants, and odd/even headers as open; SPEC CAP-9 requires first-page variants and link/unlink. STORY-014 implements them and leaves odd/even out.
- **Recovery:** The architecture deferred note says autosave/recovery is not in v1; SPEC CAP-1 and the memlog make it v1. STORY-003 follows SPEC.
- **CLI exit codes:** PRD FR-39/FR-40 and AD-19 differed; SPEC CAP-14 and the memlog resolve to 0 success, 2 usage/missing/unreadable/malformed input, 3 format/I/O including CLI PDF render/write and validation findings, 4 cancellation only, and 0 for help/version. STORY-018 follows SPEC.
- **Font policy:** SPEC retains the font fallback/bundling open question. STORY-004 has a human checkpoint and requires licensing review before any font is bundled; no bundle is assumed by this backlog.
- **Bootstrap ordering:** STORY-001 delivers a read-only preview, so CAP-1's "editable immediately" and CAP-10's caret/selection overlays complete later, primarily in STORY-006 and STORY-007. No story claims those parts early.
- **Corpus ownership and Linux/signing policy:** PRD Q-1, Q-2, and Q-4 remain human decisions. STORY-016 assumes the corpus can be assembled from owned or licensed fixtures; STORY-020 assumes a self-contained Linux archive minimum and macOS notarization for direct download, with additional formats/signing policy approved at release.
- **One document per window:** Assumed throughout; multiple windows are optional and out of scope for the editor shell.
