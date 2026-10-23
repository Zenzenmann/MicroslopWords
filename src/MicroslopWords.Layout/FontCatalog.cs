using System.Collections.Immutable;
using MicroslopWords.Core.Diagnostics;

namespace MicroslopWords.Layout;

public sealed record FontCatalog
{
    private readonly ImmutableDictionary<FontKey, FontFace> _faces;

    private FontCatalog(
        ImmutableDictionary<FontKey, FontFace> faces,
        ImmutableArray<string> fallbackFamilies,
        ImmutableArray<Diagnostic> diagnostics)
    {
        _faces = faces;
        FallbackFamilies = fallbackFamilies;
        Diagnostics = diagnostics;
    }

    public ImmutableArray<string> FallbackFamilies { get; }

    public ImmutableArray<Diagnostic> Diagnostics { get; }

    public ImmutableArray<FontFace> Faces => _faces.Values.ToImmutableArray();

    public static FontCatalog Empty { get; } = new(
        ImmutableDictionary<FontKey, FontFace>.Empty,
        ImmutableArray<string>.Empty,
        ImmutableArray<Diagnostic>.Empty);

    public static FontCatalog Create(
        IEnumerable<FontFace> faces,
        IEnumerable<string>? fallbackFamilies = null,
        IEnumerable<Diagnostic>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(faces);

        var builder = ImmutableDictionary.CreateBuilder<FontKey, FontFace>();
        var dropped = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var face in faces)
        {
            if (!builder.TryAdd(face.Key, face))
            {
                dropped.Add(new Diagnostic(
                    LayoutDiagnosticCodes.FontUnreadable,
                    DiagnosticSeverity.Warning,
                    $"Duplicate font face for {face.Family} was ignored."));
            }
        }

        var fallbacks = (fallbackFamilies ?? builder.Keys.Select(key => key.Family))
            .ToImmutableArray();

        var reported = ImmutableArray.CreateBuilder<Diagnostic>();
        reported.AddRange(diagnostics ?? []);
        reported.AddRange(dropped);

        return new FontCatalog(builder.ToImmutable(), fallbacks, reported.ToImmutable());
    }

    public bool TryResolve(FontKey request, out FontFace face)
    {
        if (_faces.TryGetValue(request, out var exact))
        {
            face = exact;
            return true;
        }

        foreach (var candidate in EnumerateCandidates(request, includeFallbacks: true))
        {
            if (_faces.TryGetValue(candidate, out var match))
            {
                face = match;
                return true;
            }
        }

        face = null!;
        return false;
    }

    internal IEnumerable<FontKey> EnumerateCandidates(FontKey request, bool includeFallbacks)
    {
        yield return new FontKey(request.Family, request.Weight, request.Slant);
        yield return new FontKey(request.Family, request.Weight, FontSlant.Upright);
        yield return new FontKey(request.Family, FontWeight.Regular, FontSlant.Upright);

        if (includeFallbacks)
        {
            foreach (var family in FallbackFamilies)
            {
                if (string.Equals(family, request.Family, StringComparison.Ordinal))
                {
                    continue;
                }

                yield return new FontKey(family, request.Weight, request.Slant);
                yield return new FontKey(family, request.Weight, FontSlant.Upright);
                yield return new FontKey(family, FontWeight.Regular, FontSlant.Upright);
            }
        }
    }
}
