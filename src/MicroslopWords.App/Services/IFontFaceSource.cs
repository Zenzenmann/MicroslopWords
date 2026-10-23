using System.Collections.Immutable;
using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Layout;

namespace MicroslopWords.App.Services;

public sealed record FontFaceSourceResult(
    ImmutableArray<FontFace> Faces,
    ImmutableArray<Diagnostic> Diagnostics);

public interface IFontFaceSource
{
    FontFaceSourceResult LoadFaces(ImmutableArray<string> fallbackFamilies);
}
