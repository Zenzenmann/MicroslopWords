using System.Collections.Immutable;
using MicroslopWords.Core.Documents;

namespace MicroslopWords.Core.Editing;

public sealed record InsertTextCommand(
    Guid ParagraphId,
    Run RunToInsert) : IEditCommand
{
    public string Description => $"Insert \"{RunToInsert.Text}\"";

    public Document Apply(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var paragraph = document.FindParagraph(ParagraphId);
        if (paragraph is null)
        {
            throw new InvalidOperationException($"Paragraph {ParagraphId} was not found.");
        }

        var updated = paragraph with { Runs = paragraph.Runs.Add(RunToInsert) };
        return document.ReplaceBlock(updated);
    }

    public Document Revert(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var paragraph = document.FindParagraph(ParagraphId);
        if (paragraph is null)
        {
            throw new InvalidOperationException($"Paragraph {ParagraphId} was not found.");
        }

        var runs = paragraph.Runs
            .Where(run => run.Id != RunToInsert.Id)
            .ToImmutableArray();

        return document.ReplaceBlock(paragraph with { Runs = runs });
    }
}
