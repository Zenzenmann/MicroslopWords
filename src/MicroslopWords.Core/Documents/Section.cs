using System.Collections.Immutable;

namespace MicroslopWords.Core.Documents;

public sealed record Section(
    Guid Id,
    ImmutableArray<Block> Blocks,
    PageSettings? Page = null,
    HeaderFooter? Header = null,
    HeaderFooter? Footer = null,
    HeaderFooter? FirstPageHeader = null,
    HeaderFooter? FirstPageFooter = null,
    bool DifferentFirstPage = false,
    bool LinkHeaderToPrevious = false,
    bool LinkFooterToPrevious = false,
    int? PageNumberStart = null,
    PageNumberFormat? PageNumberFormat = null)
{
    public static Section CreateEmpty(PageSettings? page = null) =>
        new(Guid.NewGuid(), ImmutableArray.Create<Block>(ParagraphBlock.Create()), page);

    public PageSettings EffectivePage => Page ?? PageSettings.Letter;

    public bool LinksHeaderFooter(HeaderFooterKind kind) =>
        kind == HeaderFooterKind.Header
            ? LinkHeaderToPrevious
            : LinkFooterToPrevious;

    public HeaderFooter? GetHeaderFooter(
        HeaderFooterKind kind,
        HeaderFooterVariant variant) =>
        (kind, variant) switch
        {
            (HeaderFooterKind.Header, HeaderFooterVariant.Default) => Header,
            (HeaderFooterKind.Header, HeaderFooterVariant.FirstPage) => FirstPageHeader,
            (HeaderFooterKind.Footer, HeaderFooterVariant.Default) => Footer,
            (HeaderFooterKind.Footer, HeaderFooterVariant.FirstPage) => FirstPageFooter,
            _ => null,
        };

    public Section WithHeaderFooter(
        HeaderFooterKind kind,
        HeaderFooterVariant variant,
        HeaderFooter? value) =>
        (kind, variant) switch
        {
            (HeaderFooterKind.Header, HeaderFooterVariant.Default) =>
                this with { Header = value },
            (HeaderFooterKind.Header, HeaderFooterVariant.FirstPage) =>
                this with { FirstPageHeader = value },
            (HeaderFooterKind.Footer, HeaderFooterVariant.Default) =>
                this with { Footer = value },
            (HeaderFooterKind.Footer, HeaderFooterVariant.FirstPage) =>
                this with { FirstPageFooter = value },
            _ => this,
        };

    public Section WithLinkToPrevious(HeaderFooterKind kind, bool linked) =>
        kind == HeaderFooterKind.Header
            ? this with { LinkHeaderToPrevious = linked }
            : this with { LinkFooterToPrevious = linked };
}
