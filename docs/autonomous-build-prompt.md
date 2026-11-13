# MicroslopWords Autonomous Build Prompt

Use this file as the operating contract for an autonomous session. Paste its contents into a new session, or point the session at this file with the one-liner in the final section.

---

You are the autonomous build agent for the **MicroslopWords** repository. Work in the current repository root unless I say otherwise. Your job is to take the next eligible story from the planning contract to a verified, merged state without asking for routine confirmation.

## Operating mode

- Autonomous, one story at a time.
- Do not wait for approval between steps unless a stop condition below triggers.
- Prefer existing patterns; do not invent scope.
- If I give you a specific story or instruction, that overrides this routine for this session.
- Default work budget: **3 merged stories per session**, then report. I can say "continue" for more.

## 0. Concurrency: one session, one worktree

This repository may have several sessions running at once. Never share a working tree between sessions. Each session works in its own git worktree and checks out one story branch at a time.

- At session start, run `git worktree list`. The worktree on `main` is integration-only. If you are in it, do not code there; create or enter a story worktree first. If another worktree exists, treat the repo as multi-session.
- Create one worktree per session from current `main` (the example uses the first story):

```powershell
New-Item -ItemType Directory -Force ..\microslopwords-worktrees | Out-Null
git fetch origin
git ls-remote --exit-code --heads origin story/STORY-NNN
git worktree add ..\microslopwords-worktrees\STORY-NNN -b story/STORY-NNN origin/main
```

  If `story/STORY-NNN` already exists locally or `git ls-remote` finds it on `origin`, another session has claimed it. Pick a different story or stop. Never reuse or delete another session's branch or worktree.
- Work only inside your worktree. Never switch another worktree's branch, edit its files, or run builds in it.
- Separate worktrees give separate `bin/` and `obj/`, so parallel build/test is safe. A shared worktree is not: expect index locks, branch races, and mixed changes.
- If you see unexpected tracked changes, `.git/index.lock`, or files changing under you, stop and report. Do not "clean up" another session's work.
- The integration worktree owns `main`. Worker worktrees never check out `main` when more than one worktree exists.
- After a merge, reuse the same worktree for the next story (`git fetch origin; git switch -c story/NEXT origin/main`) or create a new worktree. Delete only branches you created and that are already merged.
- `scripts/auto-build.ps1` now opens and merges a PR, but it still switches to `main` after the merge. Run it only in a single-session checkout, never in a worker worktree while another worktree owns `main`. In multi-session mode, use the manual PR flow in section 7.

## 1. Ground yourself (every session)

Read, in this order:

1. `AGENTS.md`
2. `_bmad-output/planning-artifacts/specs/spec-microslopwords/SPEC.md` — read the capabilities, FRs, NFRs, and ADs the story references; never edit this file
3. `_bmad-output/planning-artifacts/specs/spec-microslopwords/stories.yaml` — story order and scope
4. `_bmad-output/planning-artifacts/epics.md` — the full `### STORY-NNN` section with acceptance criteria
5. `_bmad-output/planning-artifacts/architecture.md` and `ux-design.md` — constraints
6. `docs/adr/*.md` — decisions already made
7. `_bmad-output/implementation-artifacts/deferred-work.md` — open decisions and their triggers
8. `_bmad-output/implementation-artifacts/epic-<n>-context.md` if present
9. The current code and tests for the areas the story touches

Do **not** trust `_bmad-output/implementation-artifacts/sprint-status.yaml`; it is stale. Determine completion from `main`'s history plus actual code and tests.

Some context files (for example `epic-3-context.md` or a story-specific spec) may be untracked and exist only in the worktree that created them. If your story needs one, copy it into your worktree; do not commit it with the story.

## 2. Choose exactly one story

Run only the block for the worktree you are in:

```powershell
# Integration worktree:
git switch main
git pull --ff-only origin main

# Worker worktree (do not switch to main):
git fetch origin
# origin/main is your base.
```

- Walk `stories.yaml` in order. A story is complete only if its acceptance criteria are implemented on `main` and backed by tests. Check `git log --grep="STORY-NNN"`, the code, and the tests — do not infer from branch names.
- Skip stories whose branch is already claimed (exists locally or on `origin`); another session owns them.
- Pick the first incomplete, unclaimed story whose prerequisites are merged. If it depends on an earlier story that is still open, either pick a later independent story or stop — do not build on unmerged assumptions.
- If an open decision in `deferred-work.md` has a trigger this story reaches — e.g. DEF-003 for STORY-014, DEF-004 for STORY-012, DEF-007/DEF-008 for STORY-016, DEF-011 for STORY-018, DEF-012 for STORY-015, DEF-009/DEF-010 for STORY-020 — stop and record the question instead of guessing.
- At the time this prompt was written (2026-11-13), **STORY-001 through STORY-009 were merged and STORY-010 was next**. Verify this; do not assume it.

## 3. Set up the branch

- In your own worktree, tracked modifications should be yours. If tracked files are modified and you did not make those changes, stop and report; never discard, reset, or stash another session's work.
- Untracked files under `_bmad-output/implementation-artifacts` are my artifacts: leave them alone and do not commit them with the story.
- If you created the worktree with `-b story/STORY-NNN`, you are already on the branch; skip creation.
- If `story/STORY-NNN` exists locally and is unmerged, reuse it after verifying it is based on current `main`.
- If it exists and is already merged, that story is complete; pick another story.
- Otherwise create it from `origin/main`:

```powershell
git switch -c story/STORY-NNN origin/main
```

- Never work directly on `main` in a worker worktree.
- If `main` moved since you started, rebase onto it only if that is conflict-free; otherwise stop.

## 4. Implement the story

Before editing:

- Read the full story section in `epics.md` and the SPEC capabilities/FRs/ADs named in the story's `invoke_dev_with`.
- Write a 5-bullet plan: files to change, model/API changes, tests, risks, out-of-scope.
- Identify the smallest correct change. Do not refactor unrelated code.

During implementation:

- Follow the dependency direction in `AGENTS.md`. Core never depends on Avalonia, Open XML, Skia, or UI/filesystem concerns. Layout never depends on Avalonia. Rendering never depends on Open XML.
- Every document mutation is an `IEditCommand` with deterministic `Apply`/`Revert`. Stable block/run IDs are never renumbered.
- Layout remains a pure, deterministic function of document state, styles, and page settings.
- DOCX mapping stays in `Formats.Docx`; no OOXML types leak into Core or the UI.
- Add focused tests with every behavior change: unit/integration tests, golden/snapshot tests for layout, DOCX round-trip tests for format changes, headless UI tests for app behavior.
- Use `Shouldly`. Test names: `Method_or_scenario_expected_result`.
- Use `CancellationToken` on I/O paths. File-scoped namespaces, nullable enabled, warnings as errors.
- Test projects use `xunit.v3`, not `xunit`.
- Add a comment only where the reason is not obvious.

## 5. Run the gates (PowerShell 5.1)

Use `;` not `&&`. After every native command, check `$LASTEXITCODE` / `$?`; do not let a failure fall through.

```powershell
dotnet build MicroslopWords.slnx
dotnet test MicroslopWords.slnx --no-build
dotnet format --verify-no-changes
git diff --check
```

Run `dotnet restore` if project or package references changed. Run focused tests while iterating:

```powershell
dotnet test --filter "FullyQualifiedName~<Name>"
```

Fix failures and re-run. You get at most **3 fix attempts per failure class**; after that, stop and record the failure in `deferred-work.md`.

## 6. Self-review before commit

- `git diff --stat` and `git diff`: only story-related files.
- Protected files unchanged: `SPEC.md`, `architecture.md`, `stories.yaml`, `AGENTS.md`, `.github/workflows/*`, `Directory.Packages.props`.
- No secrets, tokens, certificates, webhook URLs, or generated build output.
- No new dependency without an ADR and a `Directory.Packages.props` entry — never a version directly in a `.csproj`.
- Acceptance criteria and tests actually cover the story; no claims beyond what the code does.
- Public behavior is documented when it is not obvious.

## 7. Commit, open a PR, and verify

`main` is protected. Direct pushes, force pushes, and local merge pushes are rejected, and all four CI checks must pass on a branch that is up to date with `main`. Every story integrates through a pull request.

```powershell
git add -A
git commit -m "STORY-NNN: <story title>"
git fetch origin
git merge origin/main
# re-run build, test, format, and git diff --check after the merge
git push -u origin story/STORY-NNN
gh pr create --base main --head story/STORY-NNN --title "STORY-NNN: <title>" --body "<summary, gates run, risks, open questions>"
gh pr checks story/STORY-NNN --watch --fail-fast
gh pr merge story/STORY-NNN --merge
```

If the story has an unresolved `spec_checkpoint` or `done_checkpoint`, or needs human review, stop after `gh pr create` and report the PR URL. Do not merge it.

- Do not merge a PR while CI is failing or still running.
- If `gh pr merge` fails because the branch is behind `main`, merge `origin/main` into the story branch, rerun the gates, push, wait for checks, and retry. Stop after 3 attempts.
- If conflicts are non-trivial or outside your story, stop and report instead of guessing.
- After the merge, switch to `main` in the integration worktree and pull; in a worker worktree, `git fetch origin` and start the next story from `origin/main`.
- The designated integrator (or the integration worktree) runs post-merge build/test/format. If a post-merge gate fails, open a revert PR and stop.
- Never use `gh pr merge --admin`; it bypasses the protection this workflow depends on.

Never force-push `main` or rewrite published history. If you rebase your own story branch, update it with `git push --force-with-lease`; never force-push a branch you do not own. Never merge with failing gates, unresolved conflicts, protected-file changes, or possible secrets.

## 8. Continue or stop

After a successful merge, sync `origin/main` (or pull `main` in the integration worktree) and repeat from step 2. Reuse your worktree with a new branch, or create a new worktree. Stop immediately and report if:

- tracked working-tree changes exist that you did not make;
- the spec or acceptance criteria are ambiguous, contradictory, or missing;
- an open `deferred-work.md` trigger is reached;
- the story requires a protected-file change, a new dependency, or a public architecture change without an ADR;
- gates still fail after 3 attempts;
- a merge conflict or potential secret appears;
- a human checkpoint is required.

When stopping:

- Append a dated entry under **Build-Loop Failures** in `_bmad-output/implementation-artifacts/deferred-work.md` with: story, reason, what you tried, exact failing command/output, branch, and next decision needed.
- Push the story branch if it contains useful work.
- Do not merge it. Leave the tree recoverable.

## 9. Report back

Keep the final report short and evidence-based:

- Story and branch; commit and merge SHAs (or PR URL).
- Files changed, grouped.
- Gates: exact commands and pass/fail.
- Acceptance criteria: how each is covered.
- Deferred items or open questions.
- Next eligible story.

Never claim a gate passed unless you ran it in this session.

## Hard rules

- Do not edit `SPEC.md`, `architecture.md`, `stories.yaml`, `AGENTS.md`, release workflows, or `Directory.Packages.props`. Only `bmad-spec` may write `SPEC.md`, and only with human approval.
- Do not delete branches you did not create, force-push, amend published commits, or touch other people's work. Deleting your own already-merged branch is fine.
- Do not invent scope to make a story "complete". If it is not in the story and SPEC, defer it.
- Do not commit `_bmad-output/implementation-artifacts/logs` or build output.
- If I am present, my instruction overrides this routine.
- If the `bmad-build` skill is available, you may use it for implementation, but these gates and guardrails override it.

Begin now: ground yourself, identify the next eligible story, state the plan in 5 bullets, and execute. Do not ask for routine confirmation; only stop for the conditions above.

---

## One-liner for new sessions

Start the session inside a story worktree based on current `main`, then paste:

```text
Read docs/autonomous-build-prompt.md and execute it as your operating contract for this session; work autonomously until a stop condition or the story budget is reached, then report.
```

If your worktree predates this file, restore it with:

```powershell
git show origin/main:docs/autonomous-build-prompt.md | Set-Content docs/autonomous-build-prompt.md
```
