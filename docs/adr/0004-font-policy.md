# ADR 0004: Font Policy for Deterministic Layout

- Status: Accepted (interim)
- Date: 2026-10-22
- Deciders: MicroslopWords maintainers

## Context

Deterministic layout (AD-5, CAP-10) depends on an explicit font set and a fixed
fallback order: `LayoutEngine` must never discover system fonts, consult ambient
culture, read the clock, or use randomness, and an unresolved glyph must produce a
diagnostic rather than a silent substitution. STORY-004 was gated on a human
font fallback/bundling and licensing checkpoint (open question DEF-001 and the
architecture's font-policy deferral), and STORY-005 depends on STORY-004.

The open checkpoint question is what fonts the v1 product may ship, how a document
font that is absent from that set is handled, and under what license bundled font
binaries may be redistributed. That decision needs cross-platform golden evidence
and licensing review and is not yet made.

## Decision

For this iteration, STORY-004 is narrowed to the shaping and catalog plumbing only:

- Implement `FontCatalog` as an explicit input to layout that holds opaque
  `FontFace` values keyed by (family name, weight, style). The catalog is built by
  the caller and shipped as a dependency into `LayoutEngine`; layout resolves faces
  only from the catalog it receives.
- Resolve document font requests from the catalog in a fixed, documented order and
  emit a stable diagnostic on fallback; never rewrite the requested font name or
  silently substitute a glyph.
- Shape and measure text through HarfBuzzSharp against the resolved face, producing
  glyph clusters, advances, and line metrics as immutable values, exposed to App via
  a font-source port (`IFontFaceSource`) whose production adapter loads a face from a
  caller-provided path or byte buffer.
- **Do not commit any font binary, subset, or license-restricted asset.** No font is
  bundled or vendored in this iteration. Tests use a single font present on the
  build/CI hosts (a platform system font accessed by explicit path) or a
  metadata-only synthetic face; no font file enters the repository.
- Record the human font fallback/bundling and licensing decision as an explicit,
  human-owned follow-up in `deferred-work.md` (DEF-001) with the trigger "before any
  font is bundled and before STORY-015 PDF embedding". This ADR is an interim policy;
  the follow-up pending the maintainer's licensing decision must remain visible and
  must not be silently closed.

This ADR resolves the STORY-004 `spec_checkpoint` for the purposes of this
iteration only by deferring the bundling decision, and it does not by itself widen
any project boundary: `Layout` references only `Core` and HarfBuzzSharp (AD-1), and
the file-loading port lives in `App`.

## Consequences

Positive:

- STORY-004 and the dependent STORY-005 can proceed without an unresolved
  licensing decision.
- Layout determinism, fallback diagnostics, and shaped-metric plumbing are
  established now and remain valid when a font set is later approved.
- No third-party font license obligation is introduced.

Negative:

- Layout is not yet reproducible across machines that lack the chosen system font;
  cross-platform golden stability is deferred until a bundled set is approved.
- A later change is required to add the approved catalog and any bundled assets,
  which will re-run the cross-platform golden gates.

## Alternatives Considered

- **Bundle a permissively licensed font set now** — rejected: no recorded licensing
  review, and committing font binaries before the checkpoint violates the story.
- **Discover system fonts at layout time** — rejected: violates AD-5 determinism.
- **Block all of Epic 2 until the font policy is decided** — rejected: defers the
  shaping/pagination foundation the rest of v1 depends on.
