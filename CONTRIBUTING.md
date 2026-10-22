# Contributing to MicroslopWords

Thanks for helping build an open-source word processor.

## Prerequisites

- .NET 10 SDK
- Git
- Optional: Visual Studio 2026, Rider, or VS Code with the C# Dev Kit

## Setup

```powershell
git clone https://github.com/Zenzenmann/MicroslopWords.git
cd MicroslopWords
dotnet restore
dotnet build
dotnet test
```

Run the app with:

```powershell
dotnet run --project src/MicroslopWords.App
```

## Workflow

1. Pick an approved story from `_bmad-output/planning-artifacts/specs/spec-microslopwords/stories.yaml` or open an issue.
2. Create a branch: `git switch -c story/<id>` or `feature/<short-name>`.
3. Implement the smallest change that satisfies the acceptance criteria.
4. Add or update tests.
5. Run the full local gate:
   ```powershell
   dotnet build
   dotnet test
   dotnet format --verify-no-changes
   ```
6. Open a pull request, or merge the branch to `main` after the gates pass. Describe the story, the behavior change, and the tests.

## Commit Style

Use Conventional Commits:

```text
feat(core): add paragraph style inheritance
fix(layout): handle zero-width spaces at line start
test(docx): add nested table round-trip fixture
docs(adr): record the rendering boundary
chore(ci): cache NuGet packages
```

Keep commits focused. Do not mix refactors with behavior changes.

## Merge and Pull Request Requirements

- One story or one coherent change per branch or PR.
- CI must be green on Windows, Linux, and macOS.
- New packages require a note in the change and an entry in `Directory.Packages.props`.
- Architecture changes require an ADR in `docs/adr/`.
- No secrets, generated binaries, or local tool output.

## Code Style

`dotnet format` and `.editorconfig` define the style. Warnings are errors. Use nullable annotations and file-scoped namespaces. Prefer clear names over abbreviations.

## Tests

- Unit tests live next to the subsystem they cover.
- DOCX fixtures go in `tests/MicroslopWords.Docx.Tests/Fixtures/`.
- Layout golden files go in `tests/MicroslopWords.Layout.Tests/Golden/`.
- UI smoke tests use Avalonia.Headless.

## Reporting Bugs

Include the OS, .NET version, steps to reproduce, expected behavior, actual behavior, and a minimal DOCX if the bug is format-specific. Remove confidential content first.

## License

By contributing, you agree that your contributions are licensed under the MIT License.
