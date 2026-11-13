using System.Collections.Immutable;

namespace MicroslopWords.Core.Documents;

public enum ImageFormat
{
    Png,
    Jpeg,
    Gif,
}

/// <summary>
/// Immutable image data embedded in a run. The run text is the object
/// replacement character so selection, deletion, and layout treat the image as
/// one inline object.
/// </summary>
public sealed record InlineImage(
    ImageFormat Format,
    string FileName,
    ImmutableArray<byte> Data,
    int PixelWidth,
    int PixelHeight,
    double WidthPoints,
    double HeightPoints,
    string AltText = "",
    bool LockAspectRatio = true,
    bool IsAvailable = true)
{
    public const char PlaceholderCharacter = '\uFFFC';

    public const double PixelsPerInch = 96;

    public bool IsPlaceholder => !IsAvailable || Data.IsDefaultOrEmpty;

    public string AccessibleName =>
        string.IsNullOrWhiteSpace(AltText) ? FileName : AltText;

    public double AspectRatio =>
        HeightPoints > 0 && WidthPoints > 0 ? WidthPoints / HeightPoints : 1;

    public string ContentType => Format switch
    {
        ImageFormat.Png => "image/png",
        ImageFormat.Jpeg => "image/jpeg",
        ImageFormat.Gif => "image/gif",
        _ => "application/octet-stream",
    };

    public static double PixelsToPoints(int pixels) =>
        pixels * 72.0 / PixelsPerInch;

    public InlineImage WithSize(double widthPoints, double heightPoints) =>
        this with
        {
            WidthPoints = Math.Max(1, widthPoints),
            HeightPoints = Math.Max(1, heightPoints),
        };
}
