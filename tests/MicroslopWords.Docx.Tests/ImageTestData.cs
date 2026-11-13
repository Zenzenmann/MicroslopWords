namespace MicroslopWords.Docx.Tests;

internal static class ImageTestData
{
    public const string PngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAGQAAAAyCAIAAAAlV+npAAAAhUlEQVR4nO3SMREAIBADQUAI/qUgCwt//W6d6ib73buYOcMdYjWeFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYgViBWIFYi15j5uMQFoB1/W+QAAAABJRU5ErkJggg==";

    public const string GifBase64 =
        "R0lGODdhIAAQAIAAAAAAAAAAACwAAAAAIAAQAEAIJwABCBxIsKDBgwgTKlzIsKHDhxAjSpxIsaLFixgzatzIsaPHjxADAgA7";

    public static byte[] Png() => Convert.FromBase64String(PngBase64);

    public static byte[] Gif() => Convert.FromBase64String(GifBase64);
}

