using System.Collections.Immutable;

namespace MicroslopWords.Core.Documents;

public sealed record Section(
    Guid Id,
    ImmutableArray<Block> Blocks,
    PageSettings? Page = null)
{
    public static Section CreateEmpty(PageSettings? page = null) =>
        new(Guid.NewGuid(), ImmutableArray.Create<Block>(ParagraphBlock.Create()), page);

    public PageSettings EffectivePage => Page ?? PageSettings.Letter;
}
