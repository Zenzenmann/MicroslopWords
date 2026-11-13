namespace MicroslopWords.App.Services;

public sealed record ImageDialogRequest(
    string FileName,
    byte[] Data,
    int PixelWidth,
    int PixelHeight,
    double WidthPoints,
    double HeightPoints,
    bool LockAspectRatio,
    string AltText,
    double MaxWidthPoints,
    double MaxHeightPoints);

public sealed record ImageDialogResult(
    double WidthPoints,
    double HeightPoints,
    bool LockAspectRatio,
    string AltText);

public interface IImageInsertDialog
{
    Task<ImageDialogResult?> ShowAsync(
        ImageDialogRequest request,
        CancellationToken cancellationToken = default);
}
