# ADR 0003: Spec-Driven Development with BMAD

- Status: Accepted
- Date: 2026-10-22
- Deciders: MicroslopWords maintainers

## Context

AI agents can generate code quickly, but without a stable contract they drift, duplicate behavior, and silently expand scope. The project needs a single source of truth, small stories, and deterministic verification.

## Decision

- Adopt the **BMAD method** for planning and implementation.
- Planning artifacts live under `_bmad-output/planning-artifacts/`.
- `SPEC.md` is the canonical contract and carries the five-field kernel: Why, Capabilities, Constraints, Non-goals, Success signal.
- Capability IDs (`CAP-N`) are stable and never reused.
- `bmad-spec` is the only writer of `SPEC.md` and its memory log.
- Stories are generated from the spec, implement one capability slice, and include acceptance criteria plus test hints.
- Every story is implemented on `story/<id>` and merged through a PR with green CI.
- The automated harness runs build, tests, format, and DOCX round-trip gates; it stops after three failures.
- Agents may not edit the spec, architecture decisions, release workflows, or package versions without human review.

## Consequences

Positive:

- Scope changes require an explicit spec update.
- Stories are small enough to review and verify.
- CI and tests are the objective completion signal.
- Planning artifacts remain useful to humans and agents.

Negative:

- More ceremony than ad-hoc prompting.
- Spec maintenance is a real cost.
- The loop requires careful token and time budgets.

## Guardrails

- `main` is protected.
- PRs are never auto-merged.
- Deferred work is recorded in `deferred-work.md` with evidence, not lost in chat.
- Secrets and webhooks stay outside the repository.
