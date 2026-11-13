using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;
using SkiaSharp;

namespace MicroslopWords.Rendering;

public static class ImageDecoder
{
    public static OperationResult<ImageMetadata> Validate(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var inspected = ImageValidation.Inspect(data);

        if (!inspected.IsSuccess || inspected.Value is null)
        {
            return inspected;
        }

        try
        {
            using var bitmap = SKBitmap.Decode(data);

            if (bitmap is null)
            {
                return OperationResult.Failure<ImageMetadata>(
                    ImageDiagnosticCodes.CorruptImage,
                    "The image file is damaged or incomplete.");
            }

            return inspected;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            return OperationResult.Failure<ImageMetadata>(
                ImageDiagnosticCodes.CorruptImage,
                "The image file is damaged or incomplete.");
        }
    }
}
