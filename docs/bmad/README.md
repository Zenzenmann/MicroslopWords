# BMAD Operating Guide

MicroslopWords uses the BMAD method for spec-driven development. This guide defines the agent sequence, the artifacts each step produces, and how the build loop is automated.

## Artifact Ownership

| Artifact | Owner | Notes |
|---|---|---|
| `product-brief-microslopwords.md` | Analyst / PM | Vision, users, differentiation |
| `prd-microslopwords.md` | PM | Capabilities, constraints, success signals |
| `ux-design.md` | UX Designer | Editor shell, canvas, dialogs, keyboard model |
| `architecture.md` | Architect | Layers, model, layout pipeline, ADRs |
| `specs/spec-microslopwords/SPEC.md` | `bmad-spec` only | Canonical contract; never hand-edited |
| `specs/spec-microslopwords/.memlog.md` | `bmad-spec` only | Append-only decision log |
| `specs/spec-microslopwords/stories.yaml` | `bmad-create-epics-and-stories` | Approved backlog |
| `deferred-work.md` | Build loop / humans | Triaged follow-ups |
| `sprint-status.yaml` | `bmad-sprint-planning` | Sprint state |

## Agent Sequence

Run these in order. Each step must be approved before the next begins.

1. **Project context** — `bmad-project-context`
   - Produces the managed block in `AGENTS.md`.
2. **Discovery** — `bmad-brainstorming` (optional), then `bmad-product-brief`
   - Analyst/PM define target users, jobs, and scope boundaries.
3. **Requirements** — `bmad-prd`
   - PM writes capabilities, constraints, non-goals, and success signals.
4. **Experience** — `bmad-ux`
   - UX defines the editor shell, ribbon, canvas interactions, and keyboard model.
5. **Architecture** — `bmad-architecture`
   - Architect defines layers, document model, layout pipeline, DOCX strategy, and ADRs.
6. **Spec kernel** — `bmad-spec`
   - Condenses the PRD and architecture into `SPEC.md` with stable `CAP-N` IDs.
7. **Backlog** — `bmad-create-epics-and-stories`
   - Produces `stories.yaml` with acceptance criteria and test hints.
8. **Sprint plan** — `bmad-sprint-planning`
   - Produces the sprint plan and status file.
9. **Test strategy** — `bmad-testarch-framework`, `bmad-testarch-ci`, `bmad-testarch-atdd`
   - Establishes the test harness, CI gates, and acceptance-test conventions.
10. **Build** — `bmad-build` per story
    - Implement, test, review, and open a PR.
11. **Review** — `bmad-code-review`, `bmad-qa-generate-e2e-tests`
    - Review the diff and extend end-to-end coverage.
12. **Triage** — `bmad-loop-sweep`
    - Verify deferred work and partition it for future stories.
13. **Retro** — `bmad-retrospective`
    - Capture process improvements after each sprint.

## Automation

The unattended loop uses opencode:

```powershell
./scripts/auto-build.ps1 -Story STORY-001
```

The harness creates `story/<id>`, invokes `opencode run` with the story prompt, runs the local gates, retries up to three times, commits, pushes, and opens a PR. It never merges.

### Guardrails

- `main` is protected and requires CI plus human review.
- Agents work only on `story/<id>` branches.
- Agents may not edit `SPEC.md`, architecture decisions, release workflows, or package versions without human review.
- The loop stops after three failed attempts and records the blocker in `deferred-work.md`.
- Token and wall-clock budgets are enforced per story.

## Human Checkpoints

- Approve the PRD, architecture, and `SPEC.md` before story generation.
- Approve Sprint 1 stories before automation begins.
- Review every PR. The loop opens PRs; it does not merge them.
- Approve any change to capability IDs or non-goals.

## References

- BMAD method: https://github.com/bmad-code-org/BMAD-METHOD
- BMAD Loop: https://github.com/bmad-code-org/bmad-loop
