---
id: SPEC-microslopwords
companions:
  - ../../architecture.md
  - ../../ux-design.md
sources:
  - ../../prd-microslopwords.md
  - ../../product-brief-microslopwords.md
---

> **Canonical contract.** This SPEC and the files in `companions:` are the complete, preservation-validated contract for what to build, test, and validate. Source documents listed in frontmatter are for traceability — consult them only if you need narrative rationale or prose color this contract intentionally omits.

# MicroslopWords

## Why

People routinely receive `.docx` files they must edit and return, but their options are heavy office suites, account-bound web editors, or single-platform tools. MicroslopWords is a local-first, open-source alternative for students, maintainers, and operations staff who need predictable editing, clean round-tripping of common documents, deterministic layout, and a small, inspectable codebase that favors reliability over Word feature parity.

## Capabilities

- **CAP-1**
  - **intent:** Users can create, open, save, and save-as DOCX documents, see unsaved state, and recover work after an abnormal exit.
  - **success:** New creates one editable paragraph; a canceled or failed open leaves the current document, undo history, caret, and selection unchanged; Save and Save As replace the destination atomically only after a complete write; dirty state clears only after success; crash recovery never overwrites the source.

- **CAP-2**
  - **intent:** Users can type Unicode text, compose with an IME, create and merge paragraphs, insert soft breaks, and delete text predictably.
  - **success:** Typing and IME commit exactly once at the caret, preserve supplementary-plane graphemes, and replace a selection in one undo step; Enter splits, Backspace and Delete merge at boundaries, Shift+Enter inserts a soft break, word deletion follows platform modifiers, a document always retains one paragraph, and undo restores text and structure.

- **CAP-3**
  - **intent:** Users can place, move, and select text and structured content by pointer or keyboard at every supported zoom.
  - **success:** Clicks and arrow keys land on grapheme boundaries; Shift extends from the anchor; double- and triple-click select a word and paragraph; selections may span pages and stay visible; hit testing and caret geometry remain accurate after zoom, scroll, and repagination.

- **CAP-4**
  - **intent:** Users can cut, copy, paste, undo, and redo document changes without losing formatting or breaking history.
  - **success:** Internal cut, copy, and paste round-trip runs, paragraphs, lists, tables, and inline images; plain-text and HTML interop preserve at least text and basic formatting; every mutation is undoable and redoable; a new edit clears redo; contiguous typing coalesces until caret movement, formatting, or a one-second pause.

- **CAP-5**
  - **intent:** Users can apply character and paragraph formatting, bulleted and numbered lists, and named styles, and can modify style definitions without flattening them.
  - **success:** Font family, 1–400 pt size, bold, italic, single underline, strikethrough, color, highlight, alignment, spacing, indentation, and nine-level lists persist; mixed selections report a mixed state and become consistent when changed; clearing direct formatting restores the style; built-in and user styles can be applied, edited, renamed, and deleted safely; list and style assignments round-trip as structure, not literal characters.

- **CAP-6**
  - **intent:** Users can find and replace body text with case-sensitive and whole-word options, review matches, and reverse a replace-all in one step.
  - **success:** Find highlights and scrolls to matches across runs, reports counts, wraps with a notice, and leaves the caret unchanged on no result; Replace and Replace All honor both options and inherit formatting from the match start; Replace All reports its count, is one undo step, and cannot loop on replacement text containing the search term.

- **CAP-7**
  - **intent:** Users can create, edit, format, and paginate tables whose cells contain full text content.
  - **success:** Insert, row and column edits, rectangular merge and split, Tab navigation with row append, widths, borders, shading, and header-row repetition work and are undoable; rows stay intact across page breaks; table structure and formatting survive save, reopen, print, and PDF; imported tables remain editable.

- **CAP-8**
  - **intent:** Users can insert, size, describe, and remove inline images without breaking text flow or pagination.
  - **success:** PNG, JPEG, and GIF first-frame images up to 50 MB insert embedded at the caret, capped to content width with aspect ratio preserved; resize, alt text, and delete are undoable; corrupt or unsupported images are rejected or shown as a labeled placeholder without changing surrounding content; images survive round-trip and render in print and PDF.

- **CAP-9**
  - **intent:** Users can edit per-section headers and footers and insert live page-number fields that update with pagination.
  - **success:** Header and footer editing supports paragraphs, formatting, inline images, page fields, first-page variants, and link or unlink across sections; page numbers support decimal, Roman, and alphabetic formats plus start and restart values; values update after edits and match print and PDF; body text is unchanged and headers and footers round-trip.

- **CAP-10**
  - **intent:** Users can view a deterministic, paginated, zoomable rendering of the document that reflects edits immediately.
  - **success:** Page count and geometry are identical for identical document state, styles, page settings, fonts, and engine versions across repeated runs on a platform; supported content is not clipped or overlapped; edits repaginate without save; zoom spans 25–400% with fit-width, fit-page, pointer-anchored wheel zoom, and accurate caret and selection overlays.

- **CAP-11**
  - **intent:** Users can print or export to PDF using the same layout and pagination they see on screen.
  - **success:** Print supports printer, copies, and page ranges; PDF supports all pages or a range, embeds or subsets fonts, and contains selectable searchable text, images, headers, footers, and page numbers; PDF page count equals the on-screen count; cancel or failure leaves the document and any existing target unchanged and no partial output behind.

- **CAP-12**
  - **intent:** Users can open and save supported DOCX constructs with semantic preservation while unsupported content is classified, preserved where safe, or disclosed before loss.
  - **success:** Import and export cover the declared corpus constructs without flattening; 100% of text and at least 95% of supported constructs survive open-edit-save; opaque parts are preserved; unsupported and active content is listed and saving defaults to Cancel with a loss summary; package limits, path traversal, zip bombs, external fetches, macros, scripts, and embedded executables are rejected or never executed; corpus files reopen in Word and LibreOffice without repair.

- **CAP-13**
  - **intent:** Users can operate a native Windows, macOS, and Linux desktop shell with keyboard-only access, platform conventions, accessible announcements, and persistent appearance settings.
  - **success:** All core journeys complete keyboard-only with visible focus and screen-reader name, role, and state; high contrast and 100–200% UI scaling work; light, dark, and system themes plus zoom and autosave settings persist without touching document content; self-contained builds launch on clean supported systems without admin rights or a separate .NET install.

- **CAP-14**
  - **intent:** Developers and automation can inspect, validate, extract text from, and export PDF for DOCX files without the GUI.
  - **success:** `msw inspect`, `validate`, `to-text`, `export-pdf`, `--help`, and `--version` are read-only except for explicit output; JSON is deterministic; data goes to stdout and diagnostics to stderr; exit codes are 0 success, 2 usage error or missing, unreadable, or malformed input, 3 format/I/O failure including CLI PDF render or write failure and validation findings (warnings or unsupported constructs), and 4 cancellation only; `--help` and `--version` exit 0; CLI PDF page count matches the desktop for the same input and engine versions.

## Constraints

- **Stack lock:** .NET 10 (`net10.0`), Avalonia 12.1.4, SkiaSharp 3.119.4, HarfBuzzSharp 8.3.1.3, and Open XML SDK 3.5.1. Avalonia, Skia, and HarfBuzz upgrade only together after cross-platform layout and rendering gates; package versions are centralized with no floating versions.
- **One-way dependencies:** Core has no UI, OOXML, Skia, filesystem, or network concern; Layout is a pure deterministic function of document, styles, page settings, fonts, and constraints; Rendering consumes layout and never reflows or mutates; OOXML lives only in Formats.Docx; new dependency edges require an ADR.
- **Model and edit integrity:** Immutable records; section, block, and run IDs are stable for one document lifetime and never reused; every edit is an invertible command through one history; a new edit clears redo; one writer mutates state; stale async results are discarded.
- **Untrusted and local-first:** No macros, ActiveX, OLE, scripts, or embedded executables; no network fetch during open, edit, save, print, or PDF; enforce package and image limits plus traversal and zip-bomb rejection; use atomic temp-write replacement; no telemetry or document content, paths, or secrets in logs.
- **One layout for all output:** Screen, print, and PDF consume the same layout; with the same inputs, fonts, and engine versions, repeated runs produce identical geometry and pagination on a platform.
- **Cross-platform release:** Self-contained Windows x64/arm64, macOS Intel and Apple Silicon, and Linux x64 builds with no admin rights or separate .NET; native file and print dialogs; platform shortcuts and modifiers.
- **Quality gates:** Small codebase, warnings-as-errors, xUnit v3 with Shouldly, layout goldens, a release DOCX corpus of at least 30 documents, architecture tests, and `dotnet build`, `dotnet test`, and `dotnet format --verify-no-changes` passing on Windows, macOS, and Linux.
- **Performance envelope:** On 2020-or-newer 4-core, 8 GB RAM, SSD reference hardware, a 50-page document with 20 images opens in 3 seconds or less, keystroke-to-paint is 50 ms p95 or less, scroll and zoom are 100 ms p95 or less, a 10 MB save is 2 seconds or less, PDF export is 5 seconds or less, and peak memory is 500 MB or less.
- **English and offline:** The v1 UI is English-only and has no account, cloud, collaboration, plugin marketplace, or telemetry dependency; all core features work with the network disabled.

## Non-goals

- 100% Microsoft Word parity or pixel-identical rendering.
- VBA, macros, ActiveX, OLE, embedded executables, add-ins, forms and content controls, mail merge, and digital signatures.
- Real-time coauthoring, cloud sync, accounts, sharing, comments, tracked changes, document comparison, and version history.
- Add-in marketplace, plugin API, automation, OCR, AI writing assistance, and template gallery.
- Floating or text-wrapped shapes, text boxes, multi-column layouts, drop caps, charts, SmartArt, equations, citations, and bibliographies.
- Import of `.doc`, `.docm`, `.rtf`, `.odt`, or PDF, and export to any format other than PDF.
- Mobile, web, and server editions.
- Localization and right-to-left UI in v1.

## Success signal

A target user can open a real document from a 30-or-more file corpus, make representative text, formatting, table, image, header, footer, and page-number edits, save it, and print or export without silent loss; 100% of text and at least 95% of supported constructs survive round-trip, files reopen in Word and LibreOffice without repair, and build, test, format, layout, round-trip, and accessibility gates pass on Windows, macOS, and Linux.

## Assumptions

- One document per window; tabs are not required and multiple windows are optional.
- A new ADR will widen AD-19 so CLI `export-pdf` may use layout and rendering without adding App, Avalonia, or UI dependencies.
- The release corpus can be assembled from assignments, reports, specifications, and mixed-content documents before release; fixture membership is not fixed here.

## Open Questions

- What font fallback and bundling policy applies, and what cross-platform layout tolerance is acceptable when a document references a missing font? Deterministic pagination depends on the answer.
