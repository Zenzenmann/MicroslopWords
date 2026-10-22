using System.Collections.Immutable;

namespace MicroslopWords.Core.Documents;

public abstract record Block(Guid Id);

public sealed record ParagraphBlock(
    Guid Id,
    ImmutableArray<Run> Runs,
    ParagraphProperties? Properties = null) : Block(Id)
{
    public static ParagraphBlock Create(string text = "")
    {
        var runs = text.Length == 0
            ? ImmutableArray<Run>.Empty
            : ImmutableArray.Create(Run.Create(text));

        return new ParagraphBlock(Guid.NewGuid(), runs);
    }

    public ParagraphProperties EffectiveProperties => Properties ?? ParagraphProperties.Default;

    public string PlainText => string.Concat(Runs.Select(run => run.Text));
}
