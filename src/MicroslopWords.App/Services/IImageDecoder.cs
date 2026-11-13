using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Core.Documents;

namespace MicroslopWords.App.Services;

public interface IImageDecoder
{
    OperationResult<ImageMetadata> Validate(byte[] data);
}

public sealed class SkiaImageDecoder : IImageDecoder
{
    public OperationResult<ImageMetadata> Validate(byte[] data) =>
        Rendering.ImageDecoder.Validate(data);
}

public sealed class HeaderOnlyImageDecoder : IImageDecoder
{
    public OperationResult<ImageMetadata> Validate(byte[] data) =>
        ImageValidation.Inspect(data);
}
