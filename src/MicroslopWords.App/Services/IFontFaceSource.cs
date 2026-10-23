using System.Collections.Immutable;
using MicroslopWords.Layout;

namespace MicroslopWords.App.Services;

public interface IFontFaceSource
{
    ImmutableArray<FontFace> LoadFaces(ImmutableArray<string> fallbackFamilies);
}
