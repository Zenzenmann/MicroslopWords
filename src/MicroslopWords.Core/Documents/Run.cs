namespace MicroslopWords.Core.Documents;

public sealed record Run(Guid Id, string Text, RunProperties? Properties = null)
{
    public static Run Create(string text, RunProperties? properties = null) =>
        new(Guid.NewGuid(), text, properties);

    public RunProperties EffectiveProperties => Properties ?? RunProperties.Default;
}
