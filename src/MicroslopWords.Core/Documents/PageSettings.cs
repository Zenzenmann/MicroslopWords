namespace MicroslopWords.Core.Documents;

public enum PageOrientation
{
    Portrait,
    Landscape,
}

public sealed record PageSettings
{
    public double WidthPoints { get; init; } = 612;

    public double HeightPoints { get; init; } = 792;

    public PageOrientation Orientation { get; init; } = PageOrientation.Portrait;

    public double MarginLeftPoints { get; init; } = 72;

    public double MarginTopPoints { get; init; } = 72;

    public double MarginRightPoints { get; init; } = 72;

    public double MarginBottomPoints { get; init; } = 72;

    public static PageSettings Letter { get; } = new();

    public static PageSettings A4 { get; } = new()
    {
        WidthPoints = 595.28,
        HeightPoints = 841.89,
    };

    public double EffectiveWidthPoints =>
        Orientation == PageOrientation.Landscape ? HeightPoints : WidthPoints;

    public double EffectiveHeightPoints =>
        Orientation == PageOrientation.Landscape ? WidthPoints : HeightPoints;

    public double ContentWidthPoints =>
        EffectiveWidthPoints - MarginLeftPoints - MarginRightPoints;

    public double ContentHeightPoints =>
        EffectiveHeightPoints - MarginTopPoints - MarginBottomPoints;
}
