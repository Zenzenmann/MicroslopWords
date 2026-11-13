# Deferred Work

Questions and decisions intentionally deferred from planning. Resolve the item at its trigger before implementation proceeds past that point. The build harness appends failed-story details here and stops instead of inventing scope.

## Open Decisions

| ID | Question | Impact | Owner | Trigger |
|---|---|---|---|---|
| DEF-006 | Is one document per window final for v1? | Shell layout, session restore, file locking | Product + UX | Before `STORY-001` review |
| DEF-007 | Who owns the 30+ DOCX corpus fixtures, and under what license may they be committed? | Fidelity tests, CI size, redistribution | Maintainer | Before `STORY-016` starts |
| DEF-008 | What exact warning and override flow applies when a save would drop unsupported content? | Data-loss prevention, UX, CLI exit behavior | Product + UX + architecture | During `STORY-016` |
| DEF-009 | Which Linux package format is approved beyond the self-contained `linux-x64` archive? | Release automation, docs, smoke tests | Maintainer | Before `STORY-020` starts |
| DEF-010 | Are code signing and macOS notarization required for the first public release, or checksums only? | Release security, CI secrets, user trust | Maintainer | Before `STORY-020` starts |
| DEF-011 | Approve the ADR that lets `MicroslopWords.Cli` reference Layout and Rendering for PDF export only. | Architecture boundary, CLI story scope | Architecture review | Before `STORY-018` starts |

## Resolved Decisions

- `DEF-001` and `DEF-012` (resolved during `STORY-015`): no font binary is bundled or vendored. PDF export embeds the fonts used by the layout from the active `FontCatalog` through the Skia PDF backend, which keeps text selectable/searchable and preserves on-screen metrics. The pinned SkiaSharp 3.119.4 stack does not expose HarfBuzz subsetting, so the current output embeds the full face; subsetting is a follow-up, not a licensing blocker. Users are responsible for the embedding rights of fonts installed on their machine; the exporter does not add a per-font `fsType` gate. See the addendum in ADR 0004.
- `DEF-003` (resolved during `STORY-014`): v1 ships one default header and footer plus a first-page variant per section, with link/unlink to the previous section. Odd/even variants remain out of scope per `STORY-014` and `SPEC.md` CAP-9. Page fields support decimal, upper/lower Roman, and upper/lower alphabetic formats with per-section start values and restarts.
- `DEF-004` (resolved during `STORY-012`): the minimum supported table is `20×20` per `SPEC.md`; a new table defaults to `3×3` with a header row. Merging uses a rectangular cell selection: the top-left cell retains its content and formatting, other cell content is appended as paragraphs, widths are summed, the outer border becomes the merged border, and covered cells are marked as continuations. Splitting distributes the merged width evenly across the restored columns, keeps content in the top-left cell, and gives covered cells one empty paragraph. Nested tables are rejected. Rows stay intact across page breaks and the header row repeats where enabled.
- `DEF-005` (resolved during `STORY-003`): v1 keeps at most 20 recovery snapshots, one per document identity; a newer snapshot supersedes the prior snapshot for the same document, and a successful save removes that document's snapshot. Startup offers each snapshot with Open recovered draft, Discard (confirmed), or Later. Recovered drafts open unsaved with no source path, and recovery is disabled with a diagnostic for documents containing unsupported content.
- `DEF-002` (resolved during `STORY-005`): the supported canvas zoom range is `25–400%`, following `SPEC.md` and superseding the UX `50–400%` assumption. The page canvas clamps all zoom inputs to that range.
- `STORY-006` checkpoint (resolved in ADR 0005): selection positions are paragraph/run/UTF-16 identities with line affinity, soft breaks are `\n` inside runs, IME composition is transient, and imports with unsupported-content warnings stay read-only.

## Planning Notes

- `sprint_plan.py` could not parse the project’s `### STORY-NNN` headings; `sprint-status.yaml` was built against the template and validated successfully.
- `STORY-001` is read-only by design; editable CAP-1 behavior is completed in `STORY-006`.
- Manual IME smoke on Windows, macOS, and Linux remains for release validation; `STORY-006` covers the simulated IME client and headless composition flow.
- `STORY-007` clipboard supports plain text, sanitized HTML, and an in-memory internal fragment. RTF is intentionally not implemented because HTML satisfies the optional requirement. HTML list markup pastes as prefixed paragraphs until `STORY-009` adds structured lists.
- `STORY-008` writes run highlight as `w:shd` fill and imports both `w:highlight` and `w:shd`; style-based inheritance is completed in `STORY-010` through per-property direct-override flags.
- `STORY-009` models lists with a list identity, kind, and level; layout computes bullet/decimal labels, and DOCX numbering definitions are regenerated on save. Page breaks are stored as `\f` in run text so they remain selectable/deletable and map to `w:br w:type="page"`.
- `STORY-010` stores the document `StyleSheet`, resolves paragraph and character styles with per-property override flags, and round-trips `styles.xml`, `w:pStyle`, and `w:rStyle`. The styles panel supports apply, create, rename, delete, and update-from-selection; a full inheritance editor and style sets remain out of scope.
- `STORY-012` adds `TableBlock`, `TableRow`, and `TableCell` with stable IDs, 20×20 support, rectangular merge/split with continuation cells, row keep-together, repeated header rows, cell shading/borders/widths, internal table clipboard capture, and DOCX `w:tbl` round-trip. Nested tables are rejected with a diagnostic.
- `STORY-014` adds per-section default and first-page headers/footers with link/unlink, header/footer editing mode, page-number and total-pages fields, per-section start/format settings, and DOCX header/footer parts with multi-section `sectPr` round-trip. Header/footer overflow emits `MSW4005` instead of silently overlapping body content; odd/even variants remain out of scope.
- Human checkpoints are required after `STORY-016` and `STORY-020`, and SPEC checkpoints are required at `STORY-004`, `STORY-006`, `STORY-016`, `STORY-018`, and `STORY-020`.

## Build-Loop Failures

None yet.
