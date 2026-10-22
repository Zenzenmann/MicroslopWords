---
title: "MicroslopWords PRD"
status: final
created: 2026-10-22
updated: 2026-10-22
---

# PRD: MicroslopWords

## 0. Document Purpose

This PRD defines the v1 product contract for MicroslopWords, an open-source, cross-platform DOCX word processor. It is written for the product owner, maintainers, UX and architecture contributors, and the spec/story workflows that follow. It builds on `product-brief-microslopwords.md` and the accepted ADRs under `docs/adr/`; it does not replace `SPEC.md`, which remains the canonical capability contract. Requirements describe observable behavior, not code structure. Inline `[ASSUMPTION: ...]` tags mark inferred decisions; they are indexed in §11.

## 1. Vision and Problem

People routinely receive `.docx` files they must open, edit, and return. The common alternatives are either heavy office suites with features most users never touch, web editors that require an account and a network connection, or platform-specific tools that cannot cover Windows, macOS, and Linux with the same behavior. The result is friction at the exact moment a document should be simple: open it, make a change, and trust that the file will still open correctly for the person on the other end.

MicroslopWords is a practical, local-first DOCX editor built around three promises: predictable editing, clean round-tripping of common documents, and a small codebase that a new contributor can understand. It does not attempt to reproduce every Microsoft Word feature. It prioritizes the constructs used in everyday reports, assignments, proposals, and specifications: paragraphs, character and paragraph formatting, named styles, lists, tables, inline images, headers and footers, page numbers, pagination, printing, and PDF export.

The product is MIT-licensed and desktop-native. Documents stay on the user's machine. Layout is deterministic so that pagination, printing, and PDF output agree, and so that layout defects can be reproduced in headless tests. The first release is judged by whether real documents survive an edit-and-save cycle without silent loss, not by the length of a feature checklist.

## 2. Target Users and Personas

### 2.1 Personas

- **Priya Raman — graduate student.** Priya edits assignment templates and advisor feedback on a Windows laptop. She needs to open a DOCX she did not create, change text and headings, and save it without breaking tables or page layout. She is not interested in configuring an office suite and cannot depend on a stable internet connection.
- **Marcus Chen — open-source maintainer and developer.** Marcus reviews specifications and design documents on macOS and Linux. He cares that the editor is inspectable, that the CLI can report what is inside a DOCX, and that formatting behavior is predictable enough to script and test. He is willing to file detailed bugs but not to debug a black box.
- **Elena Ortiz — operations coordinator.** Elena produces weekly reports on Windows with headings, bullet lists, tables, a logo, page numbers, and a PDF attachment for stakeholders. She needs consistent styles, reliable tables and images, and a PDF that matches what she sees on screen.

### 2.2 Jobs To Be Done

- Open a DOCX from someone else, make a focused edit, and save it without corrupting content the editor does not fully understand.
- Produce a clean, consistently formatted short document using styles rather than manual formatting on every paragraph.
- Add and adjust a table or image without the layout collapsing or the image disappearing on reopen.
- Produce a print-ready or PDF version whose pagination, headers, and page numbers match the editor.
- Inspect and validate a DOCX from a terminal for debugging, automation, or corpus testing.
- Read, modify, and extend the editor's source without reverse-engineering a large framework.

### 2.3 Non-Users (v1)

- Teams that need real-time coauthoring, comments, review workflows, or tracked changes.
- Organizations that depend on VBA macros, ActiveX controls, OLE embedding, mail merge, or add-ins.
- Users producing long-form books, academic papers with citations and cross-references, or desktop-published layouts.
- Mobile and web users; v1 is a desktop application only.

## 3. Glossary

- **Document** — A DOCX file and its editable representation, including one or more sections, their content, styles, and page settings.
- **Section** — A contiguous part of a document with its own page size, orientation, margins, headers, footers, and page-number settings.
- **Paragraph** — A block of text ending at a paragraph mark, with paragraph-level formatting and a named style.
- **Run** — A contiguous span of text sharing the same character formatting.
- **Style** — A named, reusable set of paragraph or character formatting properties. A paragraph style may also define character formatting for the whole paragraph.
- **Supported construct** — A DOCX feature that MicroslopWords can import, display, edit, and export with semantic equivalence in v1.
- **Unsupported construct** — A DOCX feature outside the v1 supported set. It must be preserved where feasible or reported before it can be lost.
- **Corpus** — The version-controlled set of real-world DOCX fixtures used to measure import, export, round-trip, and layout behavior.
- **Recovery snapshot** — An automatically written copy of unsaved work used only to restore a document after an abnormal exit.
- **Reference hardware** — The minimum performance-test machine defined in §8, NFR-2.
- **Page count** — The number of physical pages produced by the current layout, including pages created by explicit page breaks.

## 4. Product Goals

- **G1 — Trustworthy round-trip.** Every document in the release corpus opens without a crash; all text survives an open-edit-save cycle; at least 95% of supported constructs retain semantic equivalence.
- **G2 — Predictable performance.** On reference hardware, a typical 50-page document opens in 3 seconds or less, keystroke-to-paint latency stays at or below 50 ms at the 95th percentile, and save completes in 2 seconds or less for a 10 MB file.
- **G3 — Task completion.** At least 4 of 5 target users complete each core journey in §6 without assistance, and at least 4 of 5 complete all four journeys in one session.
- **G4 — Deterministic layout.** Layout golden tests pass on Windows, macOS, and Linux for the same document, fonts, and engine versions, and repeated runs produce identical geometry and pagination on each platform.
- **G5 — Hackability.** A new contributor can build, run, and test the project on a clean machine by following the README in 30 minutes or less, and can add a small command or formatting behavior with tests without changing the architecture.

## 5. Non-Goals (Explicit)

- **No Word parity.** MicroslopWords does not aim to match every ribbon command, field type, layout mode, or rendering nuance of Microsoft Word.
- **No active content.** VBA macros, ActiveX, OLE automation, embedded executables, and external content are never executed.
- **No collaboration or cloud.** There is no real-time coauthoring, account system, cloud storage, sharing service, or add-in marketplace in v1.
- **No document management.** There is no version history, review workflow, comments, tracked changes, or document comparison in v1.
- **No advanced publishing.** No floating shapes, text boxes, multi-column layouts, drop caps, charts, SmartArt, equations, citations, bibliographies, mail merge, or forms.
- **No format conversion suite.** v1 reads and writes `.docx` and exports PDF; it does not import `.doc`, `.docm`, `.rtf`, `.odt`, or PDF.
- **No telemetry-driven optimization.** Success is not measured by downloads, stars, feature count, or time spent in the app.
- **No localization commitment.** The v1 interface and documentation are English-only.

## 6. User Journeys

### UJ-1. Priya opens, edits, and saves a real DOCX

- **Persona + context:** Priya, a graduate student on Windows, receives an advisor's feedback file with headings, a table, and a footer. She has MicroslopWords installed and is offline.
- **Entry state:** The app is open on a blank document; no account or sign-in exists.
- **Path:** Priya chooses **File > Open**, selects the `.docx`, and sees the document paginate. She clicks into a paragraph, fixes a sentence, applies bold to a phrase, and changes one heading's text. She presses **Ctrl+S**. The modified marker disappears.
- **Climax:** She reopens the saved file and sees her edits, the table, the heading style, and the footer intact. The document opens in Word on a lab machine without a repair prompt.
- **Resolution:** The file is ready to return to her advisor. It appears in the recent-files list and the app remains responsive.
- **Edge case:** The file contains tracked changes and an embedded object. Before editing, the app warns that those constructs are unsupported and will be preserved only if possible. Priya chooses to open a copy; the original is untouched.

### UJ-2. Elena formats a weekly report

- **Persona + context:** Elena, an operations coordinator on Windows, starts from a plain outline she pasted into a new document.
- **Entry state:** A new untitled document contains a title line, section headings, body paragraphs, and a short list.
- **Path:** She applies **Title** to the first line and **Heading 1** to section headings. She sets A4 page size with 2.5 cm margins, changes body line spacing to 1.15, adds a bulleted list, and inserts page numbers in the footer. The document repaginates after each change.
- **Climax:** The headings, spacing, and page numbers update consistently. She edits one heading style and every heading of that style updates without touching direct formatting on individual paragraphs.
- **Resolution:** She saves the report as a DOCX and reuses it as next week's starting point.
- **Edge case:** She accidentally applies direct bold to a heading. She selects the text and uses **Clear Character Formatting**; the heading returns to its style-defined appearance.

### UJ-3. Marcus inserts a table and an image

- **Persona + context:** Marcus, a maintainer on Linux, is updating a technical specification that already contains headings and body text.
- **Entry state:** The document is open and the caret is at the end of a section.
- **Path:** He inserts a 3×4 table, types cell content, merges the top row into a header, and adjusts column widths. Below the table he inserts a PNG architecture diagram, resizes it to fit the content width, and adds alt text. He saves the file and runs the CLI `inspect` command.
- **Climax:** The table and image appear in the app, survive save and reopen, and render in LibreOffice. The CLI reports one table, one inline image, three sections, and no unsupported constructs.
- **Resolution:** He commits the updated DOCX and the CLI report to the project repository.
- **Edge case:** He tries to insert an SVG. The app rejects it with a message naming supported formats and leaves the document unchanged.

### UJ-4. Elena prints and exports a PDF

- **Persona + context:** Elena has finished the weekly report and needs a printed copy for a meeting plus a PDF for stakeholders.
- **Entry state:** The report is saved, with headers, footers, page numbers, a table, and a logo.
- **Path:** She chooses **File > Print**, reviews the preview, selects pages 1–3 and two copies, and prints. She then chooses **Export PDF**, selects all pages, and saves `weekly-report.pdf`. She opens the PDF in a viewer.
- **Climax:** The printed pages and the PDF have the same page count, headers, page numbers, table layout, and image placement as the editor. Text in the PDF is selectable and searchable.
- **Resolution:** She attaches the PDF to an email and files the DOCX in the shared drive.
- **Edge case:** The printer is unavailable. The app reports the failure without changing the document, and the PDF export still succeeds.

## 7. Functional Requirements

FR IDs are globally stable and must not be renumbered. Each FR states observable behavior and testable consequences. Realized journeys are referenced where the chain matters.

### 7.1 Document Lifecycle

#### FR-1: Create and open documents

A user can create a blank document or open an existing `.docx` file. Realizes UJ-1.

**Consequences (testable):**
- **New Document** creates a document titled `Untitled` with one empty editable paragraph, no file path, and no modified marker.
- **Open** accepts `.docx` files, displays a progress indicator if loading exceeds one second, and populates text, styles, tables, images, headers, footers, and page settings.
- Canceling the open dialog, or a failed open, leaves the current document, undo history, caret, and selection unchanged.
- A missing, malformed, encrypted, or non-DOCX file produces an actionable error naming the file and reason; the app remains usable and no partial document replaces the current one.
- A successful open sets the document title and page count, and the document is editable immediately after loading.
- The app supports one document per window; multiple windows may be open, but tabs are not required. [ASSUMPTION: one document per window.]

#### FR-2: Save and Save As

A user can save the current document to its existing path or to a new path. Realizes UJ-1.

**Consequences (testable):**
- **Save** writes a valid DOCX to the current path; if the document has no path, it invokes **Save As**.
- **Save As** prompts for a path, writes the file, and changes the document path only after a successful write.
- Saves are atomic: a failed or interrupted save never truncates, partially overwrites, or deletes the previous file, and the document remains marked as modified.
- A successful save clears the modified marker and updates the window title.
- The saved file reopens in MicroslopWords with the same supported content and layout, and opens in Microsoft Word and LibreOffice without a repair prompt for corpus documents.
- Saving a document that contains unsupported constructs follows FR-34 rather than silently dropping content.

#### FR-3: Unsaved changes, autosave, and recovery

A user is protected from accidental data loss and can recover work after an abnormal exit. Realizes UJ-1.

**Consequences (testable):**
- Editing sets a visible modified marker in the title bar; a successful save clears it.
- Closing the window, opening another document, or creating a new document while modified presents **Save**, **Discard**, and **Cancel**; **Cancel** keeps the document open and unchanged.
- While a document is modified, the app writes a recovery snapshot at the configured autosave interval, which defaults to 60 seconds or less. [ASSUMPTION: default is 60 seconds; range 30–300 seconds.]
- After an abnormal exit, the next launch offers to restore the most recent recovery snapshot for that document, with its timestamp and source path shown.
- A recovery snapshot never overwrites the source file. Restoring creates an editable document; the user must explicitly save to replace the original.
- Discarding a recovery snapshot deletes it and leaves the source file untouched.

### 7.2 Text Editing

#### FR-4: Type and compose text

A user can enter Unicode text at the caret, including text composed through an input method editor (IME).

**Consequences (testable):**
- Typed characters insert at the caret, replace the current selection, and advance the caret.
- IME preedit text is visible at the caret while composing and commits exactly once on confirmation.
- Typing does not duplicate, drop, or reorder characters, including supplementary-plane characters and emoji.
- Replacing a selection by typing is a single undo step.
- Typing performance meets NFR-2 for typical documents.

#### FR-5: Paragraphs and line breaks

A user can split, merge, and create paragraphs and soft line breaks without losing content or formatting.

**Consequences (testable):**
- **Enter** splits the paragraph at the caret; text before and after the caret remains in document order with its character formatting.
- **Backspace** at the start of a paragraph merges it with the previous paragraph; **Delete** at the end of a paragraph merges the next paragraph into the current one.
- **Shift+Enter** inserts a line break within the same paragraph without creating a new paragraph or changing paragraph style.
- A document always contains at least one editable paragraph; deleting all content or merging the last two paragraphs leaves one empty paragraph.
- Each split or merge is undoable and restores the original text and paragraph structure.

#### FR-6: Delete text and selections

A user can remove characters, words, and selected content predictably.

**Consequences (testable):**
- **Backspace** and **Delete** remove one character or grapheme before or after the caret.
- **Ctrl+Backspace** / **Ctrl+Delete** on Windows and Linux, and **Option+Delete** on macOS, remove the previous or next word.
- Deleting a selection removes exactly the selected content, including across paragraph boundaries, and is a single undo step.
- Deletion at a paragraph boundary merges paragraphs as described in FR-5; deletion never merges across a table or image block boundary.
- Undo after deletion restores text, paragraph structure, and character formatting.

### 7.3 Selection and Caret

#### FR-7: Caret placement and navigation

A user can place and move the caret with the mouse and keyboard at any supported zoom level.

**Consequences (testable):**
- Clicking in text places the caret at the nearest grapheme boundary; clicking in the left or right margin places it at the nearest line boundary.
- Clicking below the last line places the caret at the end of the document; clicking above the first line places it at the start.
- Arrow keys move by grapheme; **Home** and **End** move to the visual line start and end; **Ctrl/Cmd+Left** and **Ctrl/Cmd+Right** move by word; **Page Up** and **Page Down** move by a viewport.
- Keyboard navigation scrolls the view as needed so the caret remains visible.
- Caret hit-testing remains accurate after zooming, scrolling, and repagination.

#### FR-8: Select content

A user can select text and structured content with the mouse and keyboard.

**Consequences (testable):**
- Dragging selects a range; **Shift+Click** extends the selection from its anchor; **Shift+Arrow** extends by grapheme or visual line.
- Double-click selects a word; triple-click selects a paragraph; **Ctrl/Cmd+A** selects the entire document.
- Selection can span multiple lines and pages and remains visible when the view scrolls.
- The selection highlight is distinguishable from text and remains visible in light, dark, and high-contrast themes.
- Replacing, deleting, or formatting a selection affects exactly the selected content.

### 7.4 Clipboard

#### FR-9: Cut, copy, and paste within a document

A user can move and duplicate content within the editor without losing formatting.

**Consequences (testable):**
- **Cut** removes the selection and places it on the system clipboard; **Copy** leaves the document unchanged.
- **Paste** inserts clipboard content at the caret, replacing any selection.
- Pasting content copied from MicroslopWords preserves character formatting, paragraph formatting, lists, tables, and inline images.
- Cut, copy, and paste do not alter content outside the selection, and each mutation is a single undo step.
- Cut, copy, or paste with an empty clipboard or no selection is a no-op where no document change is possible.

#### FR-10: Interoperate with the system clipboard

A user can exchange content with other applications safely.

**Consequences (testable):**
- Pasting plain text inserts it at the caret with the formatting at the insertion point.
- Pasting HTML or RTF maps at least bold, italic, underline, paragraph breaks, and lists; unsupported formatting is discarded without changing the text.
- Copying from MicroslopWords places plain text and an HTML representation on the clipboard so common external editors preserve basic formatting.
- Pasting an image in a supported format creates an inline image as described in FR-22; unsupported image data is rejected with a message and no document change.
- Clipboard content is treated as untrusted: scripts, event handlers, external references, and hidden content are not executed or fetched.

### 7.5 Undo and Redo

#### FR-11: Undo and redo all document changes

A user can reverse and reapply document edits reliably.

**Consequences (testable):**
- Every document mutation—typing, deletion, formatting, style changes, table and image edits, find/replace, header/footer edits, and page-setting changes—can be undone and redone.
- A continuous typing burst is grouped into one undo step until the caret moves, a formatting command is applied, or the user pauses for one second.
- A new edit after one or more undos clears the redo history.
- Undo and redo restore the exact prior content and formatting, and the view scrolls to the affected region.
- Undo history is cleared when a new document is created or another document is opened; saving does not clear it.
- Undo and redo menu items and shortcuts are disabled when no history is available.

### 7.6 Character Formatting

#### FR-12: Apply character formatting

A user can change the appearance of selected text or the formatting of subsequently typed text.

**Consequences (testable):**
- The user can set font family, font size from 1 to 400 points, bold, italic, single underline, strikethrough, font color, and highlight color. [ASSUMPTION: single underline only in v1.]
- With a selection, formatting applies only to selected runs; with no selection, it sets the insertion formatting for subsequent typing without changing existing text.
- A selection with mixed values shows a mixed or indeterminate state; applying a value makes the whole selection consistent.
- Toggling bold, italic, underline, or strikethrough on already formatted text removes that property.
- Character formatting persists through save and reopen and is visible in print and PDF output.

#### FR-13: Clear direct formatting

A user can remove direct formatting without deleting content.

**Consequences (testable):**
- **Clear Character Formatting** removes direct character overrides from the selection and restores the formatting defined by the applicable style, or the document default when no style defines it.
- **Clear Paragraph Formatting** removes direct alignment, spacing, and indentation overrides from the selected paragraphs and restores the paragraph style; list membership is retained.
- Clearing works on a mixed selection and affects every run or paragraph touched by the selection.
- Each clear operation is a single undo step and does not delete text, images, or tables.

### 7.7 Paragraph Formatting

#### FR-14: Align, space, and indent paragraphs

A user can control paragraph alignment, vertical spacing, and indentation. Realizes UJ-2.

**Consequences (testable):**
- The user can set left, center, right, or justified alignment; space before and after in points; line spacing as a multiple from 0.5 to 3.0; and left, right, first-line, and hanging indents.
- Formatting applies to every paragraph touched by the selection, or only the current paragraph when there is no selection.
- A mixed selection shows indeterminate values; applying a value makes all affected paragraphs consistent.
- Invalid or negative values are rejected with a message and leave the document unchanged.
- Changes repaginate the document and persist through save and reopen.

#### FR-15: Lists and page breaks

A user can create bulleted and numbered lists and insert explicit page breaks. Realizes UJ-2.

**Consequences (testable):**
- The user can toggle bulleted or numbered list formatting on selected paragraphs, nest list items up to nine levels, and continue or restart numbering.
- Inserting or deleting a list item updates the visible numbering of following items in the same list.
- Inserting a page break at the caret places all following content on the next page without inserting unintended empty paragraphs; the break is visible as an editor marker and can be selected and deleted.
- Lists and page breaks are undoable and persist through save, reopen, print, and PDF export.
- Lists round-trip as list structures in DOCX, not as literal bullet or number characters.

### 7.8 Named Styles

#### FR-16: Apply and create styles

A user can apply named paragraph and character styles and create new styles from formatted content. Realizes UJ-2.

**Consequences (testable):**
- Built-in paragraph styles include Normal, Title, and Heading 1 through Heading 3. [ASSUMPTION: this is the v1 built-in set.]
- Applying a paragraph style assigns that style to every paragraph touched by the selection; applying a character style assigns it to the selected text, or sets it for subsequent typing when there is no selection.
- The style control shows the current paragraph style or the uniform character style of the selection, and a mixed state when applicable.
- A user can create a paragraph or character style from the current formatting with a unique, non-empty name; duplicate names are rejected case-insensitively.
- Applying a style does not remove direct character formatting unless the user explicitly clears it, and all style assignments persist through save and reopen.

#### FR-17: Manage style definitions

A user can change, rename, and delete styles without breaking the document. Realizes UJ-2.

**Consequences (testable):**
- Editing a style's formatting updates every paragraph or character using that style, except where direct formatting overrides the changed property.
- Renaming a style updates all references to it; deleting a style reassigns affected content to Normal for paragraph styles or the default character style for character styles.
- Normal and the default character style cannot be deleted.
- Style edits, renames, and deletions are undoable, repaginate the document when layout changes, and persist through save and reopen.
- Style definitions and assignments are exported to DOCX and re-imported without being flattened into direct formatting.

### 7.9 Find and Replace

#### FR-18: Find text

A user can locate text in the document without altering it.

**Consequences (testable):**
- **Ctrl/Cmd+F** opens the find bar with focus in the search field; **Esc** closes it and returns focus to the editor at the current match.
- **Find Next** and **Find Previous** locate matches in document order and wrap around with a visible notice at the ends.
- The user can enable case-sensitive and whole-word matching; both are off by default.
- Matches are highlighted and the view scrolls to the active match; a query with no matches shows a no-results message without moving the caret.
- Find matches text that spans multiple runs and reports the total match count.

#### FR-19: Replace text

A user can replace one match or all matches with confirmation of the result. Realizes UJ-1, UJ-2.

**Consequences (testable):**
- **Replace** replaces the active match; **Replace All** replaces every match and reports the number replaced.
- Replacement honors the case-sensitive and whole-word options from FR-18.
- Replacement text inherits the character formatting at the start of the matched text; if a match spans runs, the first run's formatting applies.
- Replacing with an empty string deletes the match.
- **Replace All** is a single undo step, does not enter an infinite loop when the replacement contains the search term, and leaves paragraph structure intact.

### 7.10 Tables

#### FR-20: Insert and edit table structure

A user can create and restructure tables. Realizes UJ-3.

**Consequences (testable):**
- The user can insert a table by choosing a row and column count; a newly inserted table supports at least 20 rows and 20 columns and contains editable empty cells.
- The user can insert and delete rows and columns, merge a rectangular selection of cells, split a merged cell, and delete the entire table.
- **Tab** moves to the next cell and **Shift+Tab** to the previous cell, adding a row when tabbing past the last cell of the last row.
- A table can be selected as a unit, and all structural changes are undoable.
- A table is a block-level element; it cannot be inserted inside a paragraph, and deleting a table does not merge surrounding paragraphs unexpectedly.

#### FR-21: Format tables and edit cell content

A user can format tables and edit cell content with the same text tools used in the body. Realizes UJ-3.

**Consequences (testable):**
- Each cell contains one or more paragraphs with full character and paragraph formatting.
- The user can set column widths, table width, cell borders, cell shading, and header-row repetition.
- Cell content wraps within the cell; table rows do not split across pages and tables break between rows. [ASSUMPTION: rows stay intact across page boundaries in v1.]
- A table defaults to the content width and can be resized without overlapping page margins or other content.
- Table structure and formatting persist through save, reopen, print, and PDF export, and imported tables remain editable.

### 7.11 Inline Images

#### FR-22: Insert inline images

A user can place an image at the caret. Realizes UJ-3.

**Consequences (testable):**
- The user can insert PNG, JPEG, and GIF images from a file; GIF uses its first frame. [ASSUMPTION: these are the v1 formats.]
- Unsupported, corrupt, or oversized image files are rejected with a message naming the reason, and the document is unchanged. [ASSUMPTION: the per-image limit is 50 MB.]
- Inserting an image is a single undo step and does not alter surrounding text.
- The image is embedded in the document; no external image path or network fetch is required to display it after save and reopen.
- The user can set alt text when inserting or afterward; alt text is optional but preserved.

#### FR-23: Layout and edit images

A user can size, position, and remove images without breaking pagination. Realizes UJ-3.

**Consequences (testable):**
- Images are inline within a paragraph; paragraph alignment controls horizontal placement. Floating and text-wrapped images are out of scope for v1.
- A newly inserted image displays at its natural size, capped to the content width, preserving aspect ratio.
- The user can resize with handles or set an exact width and height; aspect ratio is locked by default and can be unlocked.
- Images can be copied, pasted, and deleted with the same undo behavior as text.
- A missing or corrupt embedded image shows a placeholder with its file name or alt text and a warning; the document opens without a crash.

### 7.12 Headers and Footers

#### FR-24: Edit headers and footers

A user can edit header and footer content separately from the body. Realizes UJ-4.

**Consequences (testable):**
- The user can enter header or footer editing from the menu or by double-clicking the header or footer area, and can return to the body without losing changes.
- Header and footer content supports paragraphs, character formatting, inline images, and page-number fields, using the same editing commands as the body.
- The user can enable a different first-page header or footer and can link a section's header or footer to the previous section or unlink it.
- Editing a header or footer never changes body text, and body text is not editable while the header or footer is active.
- Header and footer changes are undoable and persist through save, reopen, print, and PDF export.

#### FR-25: Render headers and footers correctly

A user sees headers and footers on every applicable page and in output. Realizes UJ-4.

**Consequences (testable):**
- Headers and footers render within the page margins above and below the body on every page of a section according to its first-page and odd/even settings.
- Linked headers and footers display the same content across sections and update everywhere when edited once; unlinking creates an independent copy.
- Header and footer content never overlaps body text silently; if content exceeds the available margin area, the app shows a layout warning.
- Headers and footers appear identically in print and PDF output and survive save and reopen.
- A section with no header or footer displays an empty area without placeholder text or artifacts.

### 7.13 Page Numbers

#### FR-26: Insert and maintain page numbers

A user can add page numbers that track pagination. Realizes UJ-2, UJ-4.

**Consequences (testable):**
- The user can insert a page-number field in a header or footer, choose decimal, upper- or lower-case Roman, or upper- or lower-case alphabetic format, and set the starting number.
- The user can insert **Page X of Y** using separate current-page and total-pages fields.
- Page numbers update after edits, page-setting changes, and pagination without reopening the document.
- Page numbering can restart per section while following the section's configured start value.
- Page-number fields are preserved through save and reopen and render correctly in print and PDF output.

### 7.14 Pagination and Zoom

#### FR-27: Paginate deterministically

A user sees an accurate page layout at all times. Realizes UJ-2, UJ-4.

**Consequences (testable):**
- The document paginates automatically using the section's page size, orientation, and margins, and the status bar shows the current physical page count.
- No line, table row, image, header, or footer is clipped or overlaps another element in the normal supported cases.
- Page breaks, table breaks, image sizes, headers, and footers affect pagination as specified in their FRs.
- Editing content or changing page settings updates affected pages and the page count without requiring save or reopen.
- For identical document state, styles, page settings, fonts, and engine versions, repeated layouts produce identical page count and geometry on the same platform.

#### FR-28: Zoom and view controls

A user can change the view scale without changing the document.

**Consequences (testable):**
- The user can set zoom from 25% to 400%, choose 100%, fit width, or fit page.
- Zoom is anchored at the pointer for wheel zoom and at the viewport center for menu or keyboard zoom; the view does not jump to an unrelated position.
- Caret placement, selection, and text hit-testing remain accurate at every supported zoom level.
- Zoom does not alter document content, page settings, or pagination.
- The default zoom is configurable in settings, and the last zoom level persists for the session.

### 7.15 Print and PDF

#### FR-29: Print documents

A user can print the current document through the operating system's print workflow. Realizes UJ-4.

**Consequences (testable):**
- **Print** opens the system print dialog with available printers, copies, and page-range choices: all pages, current page, or a numeric range.
- A preview or summary reports the physical page count and the pages that will print. [ASSUMPTION: v1 provides no separate print preview beyond the system print dialog.]
- Printed output includes supported formatting, tables, images, headers, footers, and page numbers and matches the app's pagination.
- Canceling or failing to print does not modify the document, and a failure shows an actionable message.
- Page ranges refer to physical pages as shown in the app.

#### FR-30: Export PDF

A user can export the document to a PDF that matches the editor. Realizes UJ-4.

**Consequences (testable):**
- The user can export all pages or a selected page range to a PDF file.
- The PDF uses the document's page size and orientation, embeds or subsets fonts, includes images and headers/footers, and contains selectable, searchable text. [ASSUMPTION: PDF is the only v1 export format and uses the editor's layout.]
- PDF page count equals the app's page count for the same document state, with no clipped content.
- On success, the PDF opens in common viewers without errors; on failure, no partial file is left at the target path and any existing file is unchanged.
- Exporting does not modify the document or its modified state.

### 7.16 DOCX Import, Export, and Fidelity

#### FR-31: Import supported DOCX constructs

A user can open real-world DOCX files and edit their supported content. Realizes UJ-1, UJ-3.

**Consequences (testable):**
- Import maps paragraphs, runs, character formatting, paragraph formatting, named styles, bulleted and numbered lists, tables, inline images, page breaks, sections, page settings, headers, footers, and page-number fields.
- Text content and document order are preserved exactly, including whitespace, soft line breaks, and Unicode characters.
- Supported constructs remain editable after import; they are not flattened into plain text or images.
- Unsupported constructs are handled per FR-34 and never executed. [ASSUMPTION: the target corpus excludes complex field codes, macros, and heavy floating layouts.]
- Import failures report the file and reason without crashing or replacing the current document.

#### FR-32: Export valid DOCX

A user can save a DOCX that other editors open cleanly. Realizes UJ-1, UJ-3.

**Consequences (testable):**
- Export produces a valid OOXML package with the required document, styles, relationships, media, and content-type parts.
- Supported constructs are exported with their formatting, structure, and media intact.
- The exported file contains no macros, ActiveX, OLE automation, scripts, or external relationships that could execute or fetch content.
- For corpus documents, the exported file opens in Microsoft Word and LibreOffice without a repair prompt and displays the supported content.
- Reopening an exported file in MicroslopWords yields the same supported content and layout.

#### FR-33: Round-trip content without loss

A user can open, edit, and save a document without silent degradation. Realizes UJ-1, UJ-3.

**Consequences (testable):**
- An open-edit-save cycle preserves 100% of text and at least 95% of supported constructs in the release corpus with semantic equivalence.
- Saving without edits preserves page count and layout and does not alter supported content.
- Unmodified unsupported package parts that can be preserved without interpretation are retained; when preservation is not possible, the user is warned before saving.
- Round-trip does not duplicate images, break relationships, corrupt styles, or produce a file that fails to reopen.
- The CLI validation command reports round-trip failures with the affected construct and package part.

### 7.17 Unsupported-Feature Warnings

#### FR-34: Warn before unsupported content can be lost

A user is told when a document contains content the editor does not fully support. Realizes UJ-1.

**Consequences (testable):**
- On open, the app detects and lists unsupported constructs, including macros, ActiveX, OLE objects, tracked changes, comments, content controls, external relationships, and unsupported fields or objects.
- The warning names each construct and, where available, its section or page location; it is dismissible and remains available from the document's warnings report.
- Files that require macros or active content are not opened for editing; the user is told that the original is untouched and that macros are unsupported.
- If saving would drop unsupported content, a confirmation defaults to **Cancel** and offers **Save a Copy** and **Save Anyway** with an explicit loss summary.
- Documents containing only supported constructs produce no unsupported-content warning.

### 7.18 Keyboard Shortcuts

#### FR-35: Standard keyboard operation

A user can perform common tasks without leaving the keyboard.

**Consequences (testable):**
- The app supports New, Open, Save, Save As, Print, Export PDF, Undo, Redo, Cut, Copy, Paste, Select All, Bold, Italic, Underline, Find, Replace, zoom in, zoom out, and reset zoom.
- Windows and Linux use **Ctrl** as the primary modifier; macOS uses **Command**. Menus display the correct modifier for the platform.
- Shortcuts do not fire while an IME composition is active, and unmodified letter keys never trigger commands while the editor has focus.
- Every shortcut has a discoverable menu item except zoom controls, which are exposed in the View menu and help.
- A searchable shortcut reference is available from **Help**.

### 7.19 Accessibility

#### FR-36: Accessible editing

A user with assistive technology can complete the core journeys.

**Consequences (testable):**
- All editing, formatting, file, find, table, image, header/footer, print, and PDF functions are operable by keyboard alone with a logical focus order.
- The editor exposes its role, document text, caret position, and selection state to platform accessibility APIs, and announces save results, find results, warnings, and errors.
- Dialogs and menus have accessible names, descriptions, and focus return behavior; focus is never trapped.
- The app supports the operating system's high-contrast mode and UI scaling from 100% to 200% without clipped controls or unreadable text.
- Information is not conveyed by color alone; text and UI contrast meets WCAG 2.1 AA where applicable, and image alt text is editable.
- Automated accessibility checks and manual smoke tests with Narrator, VoiceOver, and Orca report no critical issues.

### 7.20 Settings and Theme

#### FR-37: Configure appearance and behavior

A user can adjust app preferences without affecting document content.

**Consequences (testable):**
- The user can choose a light, dark, or system theme, a default zoom level, and an autosave interval from 30 to 300 seconds.
- Settings persist across sessions and can be reset to defaults; invalid values are rejected or clamped with a message.
- Theme and zoom changes apply without restarting the app where technically possible.
- Settings never store document text, document paths, or document content.
- Both themes meet the contrast requirements in FR-36, and the page canvas remains legible in dark mode.

### 7.21 Cross-Platform Behavior

#### FR-38: Consistent behavior on Windows, macOS, and Linux

A user gets the same core product on every supported desktop platform. Realizes UJ-1 through UJ-4.

**Consequences (testable):**
- All four user journeys complete on Windows, macOS, and Linux with the same document outcomes.
- File dialogs, print dialogs, menu conventions, keyboard modifiers, and default paths follow each platform's conventions.
- The CLI and all headless tests run on all three platforms.
- No core editing, save, print, or PDF feature depends on a platform-specific component that is unavailable on another supported platform.
- Packaged builds launch on clean supported systems without requiring a separately installed .NET runtime or admin rights. [ASSUMPTION: supported matrix is Windows 10/11 x64 and arm64, macOS 13+ Intel and Apple Silicon, and Linux x64 on Ubuntu 22.04+ or Fedora 40+.]

### 7.22 CLI Diagnostics

#### FR-39: Inspect and validate DOCX files headlessly

A developer or automation user can diagnose a DOCX without opening the GUI. Realizes UJ-3.

**Consequences (testable):**
- `msw inspect <input.docx>` prints document statistics as JSON, including section, paragraph, table, image, and page counts; page dimensions; and detected unsupported constructs.
- `msw to-text <input.docx>` continues to print document text to standard output.
- `msw validate <input.docx>` validates the package and supported constructs and exits with code 0 when no issues are found, 3 when warnings or unsupported constructs are found, and 2 for missing, unreadable, or malformed input.
- `msw --help` and `msw --version` print usage and version information and exit 0.
- Diagnostic commands are read-only, require no GUI, write diagnostics to standard output, and write errors to standard error.

#### FR-40: Export PDF from the CLI

A developer or automation user can render a DOCX to PDF without the GUI. Realizes UJ-4.

**Consequences (testable):**
- `msw export-pdf <input.docx> <output.pdf>` renders all pages using the same layout and pagination as the desktop app.
- The resulting PDF contains the supported formatting, images, headers, footers, page numbers, and searchable text specified in FR-30.
- The command exits 0 on success, 2 for missing or malformed input, 3 for rendering or write failure, and 4 for cancellation.
- On failure, the command leaves no partial output at the target path and does not modify the input.
- For the same input and engine versions, CLI PDF page count matches the desktop app's page count.

## 8. Non-Functional Requirements

### NFR-1: Deterministic layout

Layout results must be reproducible. For the same document state, styles, page settings, fonts, and engine versions, repeated layouts on the same platform must produce identical page count and geometry. No ambient state such as wall-clock time, random values, locale formatting, or machine-specific UI settings may affect layout. Cross-platform differences caused by font availability must be documented and covered by golden tests; the same bundled or fallback font set must produce equivalent pagination on each platform.

### NFR-2: Performance budget

On reference hardware—a 2020-or-newer 4-core CPU, 8 GB RAM, SSD, and integrated GPU, running a supported OS with no other heavy applications [ASSUMPTION]—the app must meet these budgets for a typical 50-page document with 20 images:

- Cold start to an interactive window: 3 seconds or less.
- Open a 10 MB DOCX: 3 seconds or less.
- Keystroke-to-paint latency: 50 ms or less at the 95th percentile.
- Scroll or zoom interaction: 100 ms or less at the 95th percentile.
- Save a 10 MB DOCX: 2 seconds or less.
- Export a 50-page PDF: 5 seconds or less.
- Peak memory while editing: 500 MB or less.

### NFR-3: Stability and data integrity

The app must not lose committed user work. A failed save must leave the previous file intact. An unhandled exception must not terminate silently: the app must preserve a recovery snapshot, show an error, and offer recovery on the next launch. Fault-injection tests must confirm behavior during interrupted saves, malformed packages, out-of-memory image decoding, and unexpected renderer failure. At release, there must be zero known data-loss defects and zero open P0 or P1 defects.

### NFR-4: Security

The app must treat every document and clipboard payload as untrusted. It must never execute macros, ActiveX controls, OLE automation, embedded executables, scripts, or external content. It must not fetch remote images, templates, fonts, or linked data during open, edit, save, print, or PDF export. Macro-enabled and active-content files must be rejected or handled per FR-34. Package and image decoding must enforce size, count, and path limits to resist zip bombs, decompression bombs, and path traversal. Document content and clipboard data must not be logged in plaintext or sent over the network.

### NFR-5: Accessibility

The app must meet WCAG 2.1 AA where the criteria apply to desktop software, support complete keyboard operation, expose the editor and controls to platform accessibility APIs, support high-contrast mode and 100–200% UI scaling, and provide text contrast of at least 4.5:1 for normal text. Accessibility is a release gate, not a post-release enhancement.

### NFR-6: Cross-platform packaging

Release artifacts must be self-contained and installable without administrator rights: Windows x64 and arm64, macOS Intel and Apple Silicon, and Linux x64. Each artifact must launch on a clean supported system without a separate .NET installation. Packaging must preserve file associations where the platform allows and must not require a network connection at first launch. macOS builds must be notarized if distribution outside the App Store is offered; Windows signing is required if a certificate is available. [ASSUMPTION: v1 distribution is direct download, not app stores.]

### NFR-7: Testability

Every FR must have at least one automated test or a documented manual verification procedure. Layout must be testable headlessly with golden or snapshot tests. DOCX behavior must be tested against the version-controlled corpus. CI must run build, unit tests, layout golden tests, round-trip tests, and format checks on Windows, macOS, and Linux. Test results must be deterministic and must not depend on network access.

### NFR-8: Offline and local-first

All core functions—create, open, edit, save, find/replace, print, PDF export, CLI inspection, and recovery—must work with the network disabled. There is no account, subscription, cloud storage, or server dependency. Any future update check must be opt-in and must not block startup or document operations.

### NFR-9: Privacy

Documents, file paths, clipboard content, and recovery snapshots must remain on the user's device. The app must not collect telemetry by default. Crash reporting, if offered, must be opt-in, must exclude document content, and must disclose exactly what is sent. Recovery snapshots must be stored in a user-scoped location and deleted when discarded or after successful save according to a documented policy.

### NFR-10: Maintainability and architecture boundaries

The codebase must remain small and understandable. The document model and editing commands must not depend on UI, rendering, Open XML, or filesystem UI concerns; layout must not depend on the UI toolkit; rendering must not depend on Open XML. Dependency boundaries must be enforced by build or architecture tests. New dependencies and public architecture changes require an ADR. The build must treat warnings as errors, pass `dotnet format --verify-no-changes`, and keep package versions centralized. Public behavior must be documented when it is not obvious.

## 9. Release Criteria

A v1 release is ready only when all of the following are true:

- **RC-1:** FR-1 through FR-40 are implemented and verified; no user-facing command is a placeholder or "planned" stub.
- **RC-2:** The release corpus contains at least 30 real-world DOCX documents spanning assignments, reports, specifications, and mixed-content documents. Every corpus document opens without a crash, 100% of text survives an open-edit-save cycle, and at least 95% of supported constructs retain semantic equivalence.
- **RC-3:** No known data-loss defects and no open P0 or P1 defects remain.
- **RC-4:** NFR-2 performance budgets pass on reference hardware on Windows, macOS, and Linux.
- **RC-5:** `dotnet build`, `dotnet test`, layout golden tests, round-trip tests, and `dotnet format --verify-no-changes` pass on all three platforms.
- **RC-6:** Keyboard-only completion of UJ-1 through UJ-4 passes, and screen-reader smoke tests on Narrator, VoiceOver, and Orca report no critical issues.
- **RC-7:** Packaged artifacts install or launch on clean supported systems without administrator rights or a separate .NET runtime, and open, save, print, and export PDF successfully.
- **RC-8:** User documentation covers opening, editing, saving, formatting, tables, images, headers/footers, page numbers, printing, PDF export, recovery, unsupported features, and CLI commands. Security and privacy behavior is documented.
- **RC-9:** The repository is MIT-licensed, third-party notices are present, no secrets or signing keys are committed, and no generated build output is tracked.
- **RC-10:** A usability check with at least five target users shows at least four completing each core journey without assistance, and at least four completing all four journeys in one session.

## 10. Success Metrics

### Primary

- **SM-1 — Corpus round-trip fidelity.** On the release corpus, 100% of text and at least 95% of supported constructs retain semantic equivalence after open-edit-save, with zero crashes. Validates FR-31, FR-32, FR-33.
- **SM-2 — Core journey completion.** At least 4 of 5 target users complete each of UJ-1 through UJ-4 without assistance, and at least 4 of 5 complete all four in one session. Median time for a two-page report from open to PDF is 10 minutes or less. Validates UJ-1 through UJ-4.
- **SM-3 — Performance budget compliance.** All NFR-2 budgets pass at the 95th percentile on reference hardware on all three platforms. Validates FR-4, FR-27, FR-29, FR-30, FR-40.

### Secondary

- **SM-4 — First-save reliability.** In the release validation run, 100% of successful saves reopen without repair prompts in MicroslopWords, Microsoft Word, and LibreOffice for corpus documents. Validates FR-2, FR-32.
- **SM-5 — Recovery effectiveness.** Fault-injection tests recover 100% of simulated crash cases with a snapshot no older than the configured interval, and no recovery test overwrites a source file. Validates FR-3, NFR-3.
- **SM-6 — Contributor onboarding.** At least 3 of 4 new contributors can build, run, and pass tests within 30 minutes using only the README, and at least 2 can add a small tested formatting or command change within one working day. Validates G5 and NFR-10.
- **SM-7 — Accessibility gate.** All keyboard-only journey checks pass, and automated plus manual accessibility checks report zero critical issues at release. Validates FR-36, NFR-5.

### Counter-Metrics (do not optimize)

- **SM-C1 — Do not maximize construct coverage at the expense of data integrity.** Adding a new DOCX construct is not a success if it increases silent loss or round-trip defects. Counterbalances SM-1 and SM-4.
- **SM-C2 — Do not optimize feature count or "Word compatibility percentage."** A larger feature list is not progress when it makes the codebase harder to understand or the editor less predictable. Counterbalances the product goals and SM-2.
- **SM-C3 — Do not optimize downloads, stars, or time-in-app.** These are attention metrics, not evidence that users can complete document work. Counterbalances SM-2.

## 11. Assumptions and Open Questions

### Assumptions

- **A-1:** The release corpus consists of common paragraph/character styles, basic tables, inline images, headers/footers, and page numbers, without complex field codes, macros, or heavy floating layouts. It will be defined and version-controlled before RC-2.
- **A-2:** Reference hardware is a 2020-or-newer 4-core CPU with 8 GB RAM, SSD, and integrated GPU. Performance targets scale from this baseline.
- **A-3:** v1 supports PNG, JPEG, and GIF (first frame) images up to 50 MB per image and rejects other formats with a warning.
- **A-4:** The app opens one document per window; multiple windows are allowed, but tabs are not required.
- **A-5:** Autosave defaults to 60 seconds and is configurable from 30 to 300 seconds; recovery snapshots are kept until restored, discarded, or superseded by a successful save.
- **A-6:** The supported OS matrix is Windows 10/11 x64 and arm64, macOS 13+ Intel and Apple Silicon, and Linux x64 on Ubuntu 22.04+ or Fedora 40+.
- **A-7:** The v1 interface and documentation are English-only; complex-script shaping is handled by the text engine, but full right-to-left editing is deferred.
- **A-8:** PDF export uses the same layout and pagination as the editor; PDF is the only export format.
- **A-9:** v1 collects no telemetry by default and has no account or cloud dependency.
- **A-10:** Built-in styles are Normal, Title, and Heading 1–3; lists support nine nesting levels; images are inline only.
- **A-11:** There is no print preview beyond the system print dialog's preview; the app's on-screen pagination is the source of truth.
- **A-12:** Tables may break between rows but individual rows do not split across pages in v1.
- **A-13:** v1 ships as direct-download builds; app-store distribution and automatic updates are deferred.
- **A-14:** v1 supports single underline and font sizes from 1 to 400 points.

### Open Questions

- **Q-1:** Who owns the release corpus, how many documents per category are required beyond the minimum of 30, and what is the approval process for adding a fixture?
- **Q-2:** Which Linux packaging formats ship in v1 (AppImage, `.deb`, Flatpak, or a combination), and which is the primary support target?
- **Q-3:** What is the font fallback and bundling policy, and what cross-platform layout tolerance is acceptable when a document references a font that is not installed?
- **Q-4:** Are Windows code signing and macOS notarization required for the first public release, or can direct-download builds ship unsigned with documented warnings?
- **Q-5:** What are the maximum supported document size, page count, table size, and image count for v1, and how should the app behave beyond them?
- **Q-6:** Should macro-enabled `.docm` files be rejected outright or opened as a content-only copy with macros discarded? FR-34 currently assumes rejection for editing.
- **Q-7:** Is a formal accessibility conformance report (for example, a VPAT) required, or is the internal WCAG 2.1 AA checklist sufficient?
- **Q-8:** Is localization expected in v1 or immediately after, and does the UI need externalized strings from the start?
- **Q-9:** Should the app offer an opt-in update check, and if so, what network endpoint and privacy disclosure are acceptable?
- **Q-10:** Does v1 need hyperlinks, footnotes/endnotes, or comments for the target corpus, or are those correctly deferred?

## 12. Scope Boundaries and Deferred Items

### In Scope for v1

- Create, open, edit, and save `.docx` documents on Windows, macOS, and Linux.
- Text editing, selection, caret navigation, clipboard, undo/redo, find/replace.
- Character formatting, paragraph formatting, named styles, bulleted and numbered lists.
- Tables, inline images, headers and footers, page numbers.
- Pagination, zoom, printing, and PDF export.
- DOCX import/export for the supported construct set, unsupported-feature warnings, and round-trip protection.
- Keyboard shortcuts, accessibility, settings and theme, cross-platform packaging.
- CLI inspection, validation, text extraction, and PDF export.
- Recovery snapshots and data-integrity protections.

### Explicitly Out of Scope for v1

- VBA macros, ActiveX, OLE automation, embedded executables, and add-ins.
- Real-time coauthoring, comments, tracked changes, review workflows, and document comparison.
- Cloud sync, accounts, sharing services, and add-in marketplaces.
- 100% Microsoft Word fidelity or pixel-identical rendering.
- Floating shapes, text boxes, multi-column layouts, drop caps, charts, SmartArt, equations, citations, bibliographies, mail merge, forms, content controls, and digital signatures.
- Import of `.doc`, `.docm`, `.rtf`, `.odt`, or PDF; export to formats other than PDF.
- Mobile, web, and server editions.
- Version history, document templates gallery, OCR, AI writing assistance, and plugin APIs.
- Localization and right-to-left editing in the first release.

### Deferred with Revisit Conditions

- **Hyperlinks** — revisit if corpus analysis shows them in more than 10% of target documents or if user testing blocks a core journey without them.
- **Footnotes and endnotes** — revisit after v1 round-trip metrics are stable; they require layout and field work beyond the initial scope.
- **Comments and tracked changes** — revisit when collaboration or review workflows become a product priority; v1 must preserve or warn about them, not edit them.
- **Floating images and text wrapping** — revisit after inline image layout and pagination are proven stable.
- **Multi-column sections and right-to-left text** — revisit when the layout engine has explicit tests for those modes and a target user requests them.
- **`.docm` content-only import** — revisit after the security review of active-content handling and the open question Q-6 is resolved.
- **Localization** — revisit after v1 strings are externalized and the English release is stable.
- **App-store distribution and automatic updates** — revisit after direct-download packaging and signing/notarization are settled.

## 13. Risks and Dependencies

### Risks

| Risk | Impact | Mitigation |
|---|---|---|
| DOCX is broader than the v1 supported set; real files contain unexpected constructs. | Silent data loss or corrupted output. | Define the corpus early, preserve unknown parts where feasible, warn before loss, and gate release on round-trip metrics. |
| Font availability and shaping differ across platforms. | Pagination and PDF output diverge; golden tests fail. | Lock engine and font versions, document fallback policy, test with bundled fonts, and compare golden snapshots on all three OSes. |
| Avalonia, SkiaSharp, and HarfBuzzSharp are tightly coupled. | Dependency upgrades break rendering or input. | Treat versions as a locked set, upgrade only with an ADR and full layout/input regression run. |
| Incremental layout is harder than full relayout. | Typing latency degrades on large documents. | Start with a correct full layout for small/medium documents, measure against NFR-2, and add invalidation only with tests. |
| IME, clipboard, and accessibility behavior vary by OS. | Core editing or accessibility fails on one platform. | Validate early on all three platforms; include IME and screen-reader smoke tests in CI or release checks. |
| Unsupported-feature warnings are ignored or too noisy. | Users save and lose content. | Make warnings specific, default destructive saves to Cancel, and test the warning flow with real corpus files. |
| Scope creep toward Word parity. | Schedule slips and codebase complexity grow. | Enforce the non-goals, require an ADR for scope changes, and use SM-C2 as a counter-metric. |
| Small contributor base and process overhead. | Maintenance burden and slow reviews. | Keep the architecture small, automate build/test/format gates, and document decisions in ADRs. |
| Packaging and signing requirements differ by OS. | Users cannot install or trust builds. | Test clean-VM installs early; decide signing/notarization policy before release; document unsigned-build warnings if needed. |
| Malicious or malformed DOCX packages. | Code execution, denial of service, or data exposure. | Never execute active content, enforce package/image limits, sanitize clipboard input, and fuzz the importer. |
| PDF font embedding and licensing. | Export fails or output is not portable. | Use embeddable fonts, subset where appropriate, and test PDFs in common viewers on all platforms. |

### Dependencies

- .NET 10 LTS runtime and SDK.
- Avalonia 12.1.4 for the desktop shell.
- SkiaSharp 3.119.4 for rendering.
- HarfBuzzSharp 8.3.1.3 for text shaping.
- Open XML SDK 3.5.1 for DOCX import/export.
- xUnit v3, Shouldly, and Avalonia.Headless for automated tests.
- Platform print subsystems and PDF viewing tools for verification.
- A font set available on all supported platforms, or a documented fallback strategy.
- CI runners for Windows, macOS, and Linux.
- A version-controlled DOCX corpus and a process for adding fixtures.
