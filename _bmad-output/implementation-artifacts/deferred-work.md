# Deferred Work

Questions and decisions intentionally deferred from planning. Resolve the item at its trigger before implementation proceeds past that point. The build harness appends failed-story details here and stops instead of inventing scope.

## Open Decisions

| ID | Question | Impact | Owner | Trigger |
|---|---|---|---|---|
| DEF-001 | Which fonts may be bundled, and what is the fallback/substitution policy when a DOCX requests an unavailable font? (STORY-004 shipped an interim catalog-plus-fallback policy in ADR 0004 with **no bundled fonts**; the human licensing/bundling decision is still required.) | Deterministic shaping, PDF embedding, licensing, package size | Product + legal review | Before any font is bundled and before `STORY-015` PDF embedding |
| DEF-002 | Is the supported zoom range `25–400%` (SPEC) or `50–400%` (UX)? | Canvas behavior, tests, accessibility | Product + UX | Before `STORY-005` completes |
| DEF-003 | Which header/footer variants ship in v1: first-page only, or also odd/even? | DOCX round-trip, pagination, UI | Product + architecture | During `STORY-014` |
| DEF-004 | What is the minimum table size and the exact merge/split formatting behavior? | Table editing, DOCX fidelity, tests | Product + UX | During `STORY-012` |
| DEF-006 | Is one document per window final for v1? | Shell layout, session restore, file locking | Product + UX | Before `STORY-001` review |
| DEF-007 | Who owns the 30+ DOCX corpus fixtures, and under what license may they be committed? | Fidelity tests, CI size, redistribution | Maintainer | Before `STORY-016` starts |
| DEF-008 | What exact warning and override flow applies when a save would drop unsupported content? | Data-loss prevention, UX, CLI exit behavior | Product + UX + architecture | During `STORY-016` |
| DEF-009 | Which Linux package format is approved beyond the self-contained `linux-x64` archive? | Release automation, docs, smoke tests | Maintainer | Before `STORY-020` starts |
| DEF-010 | Are code signing and macOS notarization required for the first public release, or checksums only? | Release security, CI secrets, user trust | Maintainer | Before `STORY-020` starts |
| DEF-011 | Approve the ADR that lets `MicroslopWords.Cli` reference Layout and Rendering for PDF export only. | Architecture boundary, CLI story scope | Architecture review | Before `STORY-018` starts |
| DEF-012 | Should PDF fonts be embedded always, or only when licensing permits, and should they be subset? | PDF fidelity, file size, licensing | Product + legal review | Before `STORY-015` completes |

## Resolved Decisions

- `DEF-005` (resolved during `STORY-003`): v1 keeps at most 20 recovery snapshots, one per document identity; a newer snapshot supersedes the prior snapshot for the same document, and a successful save removes that document's snapshot. Startup offers each snapshot with Open recovered draft, Discard (confirmed), or Later. Recovered drafts open unsaved with no source path, and recovery is disabled with a diagnostic for documents containing unsupported content.

## Planning Notes

- `sprint_plan.py` could not parse the project’s `### STORY-NNN` headings; `sprint-status.yaml` was built against the template and validated successfully.
- `STORY-001` is read-only by design; editable CAP-1 behavior is completed in `STORY-006`.
- Human checkpoints are required after `STORY-016` and `STORY-020`, and SPEC checkpoints are required at `STORY-004`, `STORY-006`, `STORY-016`, `STORY-018`, and `STORY-020`.

## Build-Loop Failures

None yet.
