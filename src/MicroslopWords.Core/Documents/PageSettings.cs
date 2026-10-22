namespace MicroslopWords.Core.Documents;

public sealed record PageSettings
{
    public double WidthPoints { get; init; } = 612;

    public double HeightPoints { get; init; } = 792;

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

    public double ContentWidthPoints => WidthPoints - MarginLeftPoints - MarginRightPoints;

    public double ContentHeightPoints => HeightPoints - MarginTopPoints - MarginBottomPoints;
}
