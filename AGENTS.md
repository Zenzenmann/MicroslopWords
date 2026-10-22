# AGENTS.md

Instructions for AI agents working in the MicroslopWords repository.

## Mission

Build a practical, open-source DOCX editor for Windows, macOS, and Linux. Optimize for a small, understandable codebase, deterministic layout, and clean DOCX round-tripping. Do not chase 100% Microsoft Word fidelity.

## Commands

```powershell
dotnet restore
dotnet build
dotnet test
dotnet format --verify-no-changes
dotnet run --project src/MicroslopWords.App
```

Use `dotnet test --filter "FullyQualifiedName~<Name>"` while iterating. On Windows, the shell is PowerShell 5.1: use `cmd1; if ($?) { cmd2 }` instead of `&&`.

## Architecture Map

| Project | Responsibility | May reference |
|---|---|---|
| `MicroslopWords.Core` | Document model, commands, undo/redo, styles, selections | Nothing UI or format-specific |
| `MicroslopWords.Layout` | Text shaping, line breaking, pagination, layout boxes | Core, HarfBuzzSharp |
| `MicroslopWords.Rendering` | Skia drawing of layout boxes, caret, selection | Core, Layout, SkiaSharp |
| `MicroslopWords.Formats.Docx` | OOXML import/export | Core, Open XML SDK |
| `MicroslopWords.App` | Avalonia shell, MVVM, editor control, dialogs | All of the above |
| `MicroslopWords.Cli` | Headless conversion, corpus checks, diagnostics | Core, Formats.Docx |

Dependency direction is one-way: Core must never depend on Avalonia, Open XML, Skia, or file-system UI concerns. Layout must not depend on Avalonia. Rendering must not depend on Open XML.

## Invariants

- Document edits go through commands so undo/redo and collaboration can be added without rewrites.
- Blocks and runs have stable IDs; never renumber existing IDs.
- Layout is a pure function of document state, styles, and page settings. Keep it deterministic and testable headlessly.
- `SPEC.md` is the single source of truth for capabilities. Only the `bmad-spec` workflow writes it. Never hand-edit it.
- Do not change public architecture or add a dependency without an ADR in `docs/adr/`.
- Do not add a NuGet package version directly to a `.csproj`; add it to `Directory.Packages.props`.
- Never commit secrets, webhook URLs, tokens, certificates, or signing keys.
- Do not push to `main`. Work on `story/<id>` branches and open a PR.

## Spec-Driven Workflow

Planning artifacts live in `_bmad-output/planning-artifacts/`. The execution contract is:

1. `product-brief-microslopwords.md`
2. `prd-microslopwords.md`
3. `ux-design.md`
4. `architecture.md`
5. `specs/spec-microslopwords/SPEC.md`
6. `specs/spec-microslopwords/stories.yaml`

Implement one story at a time. Read the story's acceptance criteria and the referenced capabilities in `SPEC.md` before writing code. If the spec is ambiguous, stop and record the question in `deferred-work.md` rather than inventing scope.

## Definition of Done

A story is done only when:

- Acceptance criteria are implemented.
- New behavior has unit or integration tests.
- Layout changes have a golden/snapshot test.
- DOCX changes round-trip through the corpus.
- `dotnet build` and `dotnet test` pass.
- `dotnet format --verify-no-changes` passes.
- Public behavior is documented when it is not obvious.
- No secrets or generated build output are committed.

## Coding Conventions

- File-scoped namespaces, nullable enabled, warnings as errors.
- Prefer records and immutable value types for model data.
- Keep methods small; put behavior on the model or in focused services.
- Use `Shouldly` for assertions in tests.
- Test names: `Method_or_scenario_expected_result`.
- Add comments only where the reason is not obvious from the code.
- Use `CancellationToken` on I/O paths.

## Automation Guardrails

`scripts/auto-build.ps1` is the only supported unattended coding entry point. It must:

- Refuse to run on a dirty tree or on `main`.
- Create and work on `story/<id>`.
- Run build, tests, and format gates.
- Retry at most 3 times, then write to `deferred-work.md` and stop.
- Open a PR; never merge it.
- Never edit `SPEC.md`, `architecture.md`, release workflows, or `Directory.Packages.props` without human review.

## Known Pitfalls

- Avalonia 12 uses SkiaSharp `3.119.4` and HarfBuzzSharp `8.3.1.3`; do not upgrade them independently of Avalonia.
- `Avalonia.Headless.XUnit` requires xUnit v3. Test projects use `xunit.v3`, not `xunit`.
- .NET 9 is out of support. Target `net10.0`.
- The solution file is `MicroslopWords.slnx`, the .NET 10 solution format. Use `dotnet sln MicroslopWords.slnx ...`.
- Do not install packages with `dotnet add package` unless the version is also added to `Directory.Packages.props`.
