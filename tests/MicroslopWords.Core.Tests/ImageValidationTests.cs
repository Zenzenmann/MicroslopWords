using System.Buffers.Binary;
using MicroslopWords.Core.Documents;
using Shouldly;

namespace MicroslopWords.Core.Tests;

public sealed class ImageValidationTests
{
    [Fact]
    public void Inspect_detects_png_jpeg_and_gif_dimensions()
    {
        var png = ImageValidation.Inspect(ImageTestData.Png()).Value;
        var jpeg = ImageValidation.Inspect(ImageTestData.Jpeg()).Value;
        var gif = ImageValidation.Inspect(ImageTestData.Gif()).Value;

        png.ShouldNotBeNull();
        png.Format.ShouldBe(ImageFormat.Png);
        png.PixelWidth.ShouldBe(100);
        png.PixelHeight.ShouldBe(50);

        jpeg.ShouldNotBeNull();
        jpeg.Format.ShouldBe(ImageFormat.Jpeg);
        jpeg.PixelWidth.ShouldBe(80);
        jpeg.PixelHeight.ShouldBe(40);

        gif.ShouldNotBeNull();
        gif.Format.ShouldBe(ImageFormat.Gif);
        gif.PixelWidth.ShouldBe(32);
        gif.PixelHeight.ShouldBe(16);
    }

    [Fact]
    public void Inspect_rejects_unsupported_empty_and_corrupt_data()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>"u8;

        ImageValidation.Inspect(svg).Diagnostics.Single().Code
            .ShouldBe(ImageDiagnosticCodes.UnsupportedFormat);
        ImageValidation.Inspect([]).Diagnostics.Single().Code
            .ShouldBe(ImageDiagnosticCodes.MissingData);

        var truncated = ImageTestData.Png()[..20];
        ImageValidation.Inspect(truncated).Diagnostics.Single().Code
            .ShouldBe(ImageDiagnosticCodes.CorruptImage);
    }

    [Fact]
    public void Inspect_rejects_files_over_fifty_megabytes()
    {
        var data = new byte[ImageValidation.MaxImageBytes + 1];

        var result = ImageValidation.Inspect(data);

        result.IsSuccess.ShouldBeFalse();
        result.Diagnostics.Single().Code.ShouldBe(ImageDiagnosticCodes.ImageTooLarge);
    }

    [Fact]
    public void Inspect_rejects_images_over_the_decoded_pixel_limit()
    {
        var data = new byte[24];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }
            .CopyTo(data, 0);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(16), 20_000);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(20), 20_000);

        var result = ImageValidation.Inspect(data);

        result.IsSuccess.ShouldBeFalse();
        result.Diagnostics.Single().Code
            .ShouldBe(ImageDiagnosticCodes.DecodeLimitExceeded);
    }

    [Fact]
    public void Create_uses_natural_size_and_caps_to_content()
    {
        var natural = ImageValidation.Create(
            ImageTestData.Png(),
            "red.png",
            altText: "A red rectangle");

        natural.IsSuccess.ShouldBeTrue();
        natural.Value.ShouldNotBeNull();
        natural.Value.WidthPoints.ShouldBe(75, 0.0001);
        natural.Value.HeightPoints.ShouldBe(37.5, 0.0001);
        natural.Value.AltText.ShouldBe("A red rectangle");
        natural.Value.AspectRatio.ShouldBe(2, 0.0001);

        var capped = ImageValidation.Create(
            ImageTestData.Png(),
            "red.png",
            maxWidthPoints: 60,
            maxHeightPoints: 100);

        capped.Value.ShouldNotBeNull();
        capped.Value.WidthPoints.ShouldBe(60, 0.0001);
        capped.Value.HeightPoints.ShouldBe(30, 0.0001);
    }

    [Fact]
    public void Create_rejects_invalid_dimensions()
    {
        var result = ImageValidation.Create(
            ImageTestData.Png(),
            "red.png",
            widthPoints: 0,
            heightPoints: -1);

        result.IsSuccess.ShouldBeFalse();
        result.Diagnostics.Single().Code.ShouldBe(ImageDiagnosticCodes.InvalidSize);
    }
}
