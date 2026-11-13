using System.Buffers.Binary;
using System.Collections.Immutable;
using MicroslopWords.Core.Diagnostics;

namespace MicroslopWords.Core.Documents;

public static class ImageDiagnosticCodes
{
    public const string UnsupportedFormat = "MSW5001";
    public const string CorruptImage = "MSW5002";
    public const string ImageTooLarge = "MSW5003";
    public const string DecodeLimitExceeded = "MSW5004";
    public const string InvalidSize = "MSW5005";
    public const string MissingData = "MSW5006";
}

public sealed record ImageMetadata(ImageFormat Format, int PixelWidth, int PixelHeight);

/// <summary>
/// Header-level image validation used before an image enters the document
/// model. Decoding is intentionally not performed here; the app and renderer
/// verify that the encoded data is decodable.
/// </summary>
public static class ImageValidation
{
    public const long MaxImageBytes = 50L * 1024 * 1024;

    public const long MaxDecodedPixels = 40_000_000;

    public const double MinimumSizePoints = 1;

    public const double MaximumSizePoints = 100_000;

    private static readonly byte[] PngSignature =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static OperationResult<ImageMetadata> Inspect(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
        {
            return OperationResult.Failure<ImageMetadata>(
                ImageDiagnosticCodes.MissingData,
                "The image file is empty.");
        }

        if (data.Length > MaxImageBytes)
        {
            return OperationResult.Failure<ImageMetadata>(
                ImageDiagnosticCodes.ImageTooLarge,
                $"Images must be 50 MB or smaller; this one is {data.Length / (1024 * 1024)} MB.");
        }

        ImageMetadata? metadata = null;

        if (data.Length >= 8 && data[..8].SequenceEqual(PngSignature))
        {
            metadata = data.Length >= 24 ? ReadPng(data) : null;
        }
        else if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xD8)
        {
            metadata = ReadJpeg(data);
        }
        else if (data.Length >= 6
            && (data[..6].SequenceEqual("GIF87a"u8)
                || data[..6].SequenceEqual("GIF89a"u8)))
        {
            metadata = data.Length >= 10 ? ReadGif(data) : null;
        }
        else
        {
            return OperationResult.Failure<ImageMetadata>(
                ImageDiagnosticCodes.UnsupportedFormat,
                "Only PNG, JPEG, and GIF images are supported.");
        }

        if (metadata is null)
        {
            return OperationResult.Failure<ImageMetadata>(
                ImageDiagnosticCodes.CorruptImage,
                "The image file is damaged or incomplete.");
        }

        if (metadata.PixelWidth <= 0 || metadata.PixelHeight <= 0)
        {
            return OperationResult.Failure<ImageMetadata>(
                ImageDiagnosticCodes.CorruptImage,
                "The image has no readable dimensions.");
        }

        var pixels = (long)metadata.PixelWidth * metadata.PixelHeight;

        if (pixels > MaxDecodedPixels)
        {
            return OperationResult.Failure<ImageMetadata>(
                ImageDiagnosticCodes.DecodeLimitExceeded,
                $"The image is too large to decode safely ({pixels:N0} pixels).");
        }

        return OperationResult.Success(metadata);
    }

    public static OperationResult<InlineImage> Create(
        ReadOnlySpan<byte> data,
        string fileName,
        string? altText = null,
        double? widthPoints = null,
        double? heightPoints = null,
        bool lockAspectRatio = true,
        double maxWidthPoints = double.PositiveInfinity,
        double maxHeightPoints = double.PositiveInfinity)
    {
        var inspected = Inspect(data);

        if (!inspected.IsSuccess || inspected.Value is null)
        {
            return OperationResult.Failure<InlineImage>(
                inspected.Diagnostics.Select(diagnostic => diagnostic).ToArray());
        }

        var metadata = inspected.Value;
        var natural = NaturalSize(metadata, maxWidthPoints, maxHeightPoints);
        var width = widthPoints ?? natural.Width;
        var height = heightPoints ?? natural.Height;

        if (!IsValidSize(width) || !IsValidSize(height))
        {
            return OperationResult.Failure<InlineImage>(
                ImageDiagnosticCodes.InvalidSize,
                "Image width and height must be positive numbers.");
        }

        (width, height) = FitSize(width, height, maxWidthPoints, maxHeightPoints);

        var name = string.IsNullOrWhiteSpace(fileName) ? "Image" : fileName;
        var image = new InlineImage(
            metadata.Format,
            name,
            data.ToArray().ToImmutableArray(),
            metadata.PixelWidth,
            metadata.PixelHeight,
            width,
            height,
            altText?.Trim() ?? string.Empty,
            lockAspectRatio);

        return OperationResult.Success(image);
    }

    public static (double Width, double Height) NaturalSize(
        ImageMetadata metadata,
        double maxWidthPoints = double.PositiveInfinity,
        double maxHeightPoints = double.PositiveInfinity) =>
        FitSize(
            InlineImage.PixelsToPoints(metadata.PixelWidth),
            InlineImage.PixelsToPoints(metadata.PixelHeight),
            maxWidthPoints,
            maxHeightPoints);

    public static (double Width, double Height) FitSize(
        double widthPoints,
        double heightPoints,
        double maxWidthPoints,
        double maxHeightPoints)
    {
        if (!IsValidSize(widthPoints) || !IsValidSize(heightPoints))
        {
            return (1, 1);
        }

        var scale = 1d;

        if (IsFiniteLimit(maxWidthPoints) && widthPoints > maxWidthPoints)
        {
            scale = Math.Min(scale, maxWidthPoints / widthPoints);
        }

        if (IsFiniteLimit(maxHeightPoints) && heightPoints > maxHeightPoints)
        {
            scale = Math.Min(scale, maxHeightPoints / heightPoints);
        }

        return (Math.Max(MinimumSizePoints, widthPoints * scale),
            Math.Max(MinimumSizePoints, heightPoints * scale));
    }

    private static bool IsValidSize(double value) =>
        double.IsFinite(value) && value >= MinimumSizePoints && value <= MaximumSizePoints;

    private static bool IsFiniteLimit(double value) =>
        double.IsFinite(value) && value > 0;

    private static ImageMetadata? ReadPng(ReadOnlySpan<byte> data)
    {
        const int widthOffset = 16;
        const int heightOffset = 20;

        if (data.Length < heightOffset + 4)
        {
            return null;
        }

        var width = BinaryPrimitives.ReadUInt32BigEndian(data[widthOffset..]);
        var height = BinaryPrimitives.ReadUInt32BigEndian(data[heightOffset..]);

        return width is > 0 and <= int.MaxValue && height is > 0 and <= int.MaxValue
            ? new ImageMetadata(ImageFormat.Png, (int)width, (int)height)
            : null;
    }

    private static ImageMetadata? ReadJpeg(ReadOnlySpan<byte> data)
    {
        var index = 2;

        while (index + 3 < data.Length)
        {
            if (data[index] != 0xFF)
            {
                index++;
                continue;
            }

            var marker = data[index + 1];
            index += 2;

            if (marker is 0xD8 or 0xD9 || (marker >= 0xD0 && marker <= 0xD7))
            {
                continue;
            }

            if (index + 1 >= data.Length)
            {
                return null;
            }

            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(data[index..]);

            if (segmentLength < 2 || index + segmentLength > data.Length)
            {
                return null;
            }

            if (IsStartOfFrame(marker))
            {
                if (segmentLength < 7)
                {
                    return null;
                }

                var height = BinaryPrimitives.ReadUInt16BigEndian(data[(index + 3)..]);
                var width = BinaryPrimitives.ReadUInt16BigEndian(data[(index + 5)..]);

                return width > 0 && height > 0
                    ? new ImageMetadata(ImageFormat.Jpeg, width, height)
                    : null;
            }

            index += segmentLength;
        }

        return null;
    }

    private static bool IsStartOfFrame(byte marker) =>
        marker is 0xC0 or 0xC1 or 0xC2 or 0xC3
            or 0xC5 or 0xC6 or 0xC7
            or 0xC9 or 0xCA or 0xCB
            or 0xCD or 0xCE or 0xCF;

    private static ImageMetadata? ReadGif(ReadOnlySpan<byte> data)
    {
        var width = BinaryPrimitives.ReadUInt16LittleEndian(data[6..]);
        var height = BinaryPrimitives.ReadUInt16LittleEndian(data[8..]);

        return width > 0 && height > 0
            ? new ImageMetadata(ImageFormat.Gif, width, height)
            : null;
    }
}
