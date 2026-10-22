# MicroslopWords

An open-source, cross-platform word processor built with C#, Avalonia, and a native Skia rendering pipeline.

MicroslopWords aims to be a practical DOCX editor, not a clone of every Microsoft Word feature. The first release focuses on fast, predictable editing, clean DOCX round-tripping, and a small, hackable codebase.

## Status

Early bootstrap. The repository currently contains the architecture skeleton, CI, BMAD planning artifacts, and an automated story-to-main harness. The editor is under active development.

## Planned v1 Scope

- Create, open, edit, and save `.docx` documents
- Paragraph and character formatting, styles, undo/redo, find/replace
- Tables, images, headers and footers, page numbers
- Pagination and print/PDF export
- Windows, macOS, and Linux desktop builds

Out of scope for v1: VBA/macros, real-time coauthoring, cloud sync, add-in marketplace, and 100% Word fidelity.

## Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 LTS |
| UI | Avalonia 12 |
| Rendering | SkiaSharp |
| Text shaping | HarfBuzzSharp |
| DOCX | Open XML SDK |
| Tests | xUnit v3, Shouldly, Avalonia.Headless |

## Repository Layout

```text
src/
  MicroslopWords.Core/          Document model, commands, undo/redo, styles
  MicroslopWords.Layout/        Text shaping, line breaking, pagination
  MicroslopWords.Rendering/     Skia drawing, caret, selection
  MicroslopWords.Formats.Docx/  OOXML import/export
  MicroslopWords.App/           Avalonia desktop application
  MicroslopWords.Cli/           Headless conversion and diagnostics
tests/
  MicroslopWords.Core.Tests/
  MicroslopWords.Layout.Tests/
  MicroslopWords.Docx.Tests/
  MicroslopWords.Ui.Tests/
docs/
  adr/                          Architecture decision records
  bmad/                         BMAD operating guide
_bmad-output/                   BMAD planning and implementation artifacts
scripts/                        Automation and developer tooling
```

## Build

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/MicroslopWords.App
```

CI runs on Windows, Linux, and macOS. Release builds are self-contained and produced from version tags.

## Spec-Driven Development

This project uses the BMAD method. Product requirements, architecture, capability IDs, and stories live under `_bmad-output/planning-artifacts/`. The canonical contract is `SPEC.md`; only the `bmad-spec` workflow writes it.

Stories are implemented one at a time through the automated harness:

```powershell
./scripts/auto-build.ps1 -Story STORY-001
```

See [AGENTS.md](AGENTS.md) for the agent contract and [docs/bmad/README.md](docs/bmad/README.md) for the workflow.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Please read the [Code of Conduct](CODE_OF_CONDUCT.md) and report vulnerabilities per [SECURITY.md](SECURITY.md).

## License

MIT. See [LICENSE](LICENSE).
