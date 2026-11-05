using System.Collections.Immutable;
using MicroslopWords.Core.Diagnostics;
using MicroslopWords.Layout;

namespace MicroslopWords.App.Services;

public sealed class DirectoryFontFaceSource : IFontFaceSource
{
    private static readonly string[] SupportedExtensions = [".ttf", ".otf"];

    private readonly ImmutableArray<string> _directories;

    public DirectoryFontFaceSource(IEnumerable<string> directories)
    {
        ArgumentNullException.ThrowIfNull(directories);

        _directories = directories
            .Where(directory => !string.IsNullOrWhiteSpace(directory))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToImmutableArray();
    }

    public FontFaceSourceResult LoadFaces(ImmutableArray<string> fallbackFamilies)
    {
        var faces = ImmutableArray.CreateBuilder<FontFace>();
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var directory in _directories)
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            var files = Directory
                .EnumerateFiles(directory)
                .Where(file => SupportedExtensions.Contains(
                    Path.GetExtension(file),
                    StringComparer.OrdinalIgnoreCase))
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                var (family, weight, slant) = ParseFileName(file);

                if (family is null)
                {
                    continue;
                }

                byte[] bytes;

                try
                {
                    bytes = File.ReadAllBytes(file);
                }
                catch (IOException exception)
                {
                    diagnostics.Add(Rejected(file, exception.Message));
                    continue;
                }
                catch (UnauthorizedAccessException exception)
                {
                    diagnostics.Add(Rejected(file, exception.Message));
                    continue;
                }

                if (bytes.Length == 0)
                {
                    diagnostics.Add(Rejected(file, "Font file is empty."));
                    continue;
                }

                try
                {
                    faces.Add(new FontFace(family, weight, slant, bytes));
                }
                catch (ArgumentException exception)
                {
                    diagnostics.Add(Rejected(file, exception.Message));
                }
            }
        }

        return new FontFaceSourceResult(faces.ToImmutable(), diagnostics.ToImmutable());
    }

    private static Diagnostic Rejected(string file, string reason) =>
        new(
            LayoutDiagnosticCodes.FontUnreadable,
            DiagnosticSeverity.Warning,
            $"Font {Path.GetFileName(file)} was rejected: {reason}");

    private static (string? Family, FontWeight Weight, FontSlant Slant) ParseFileName(string file)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        var separatorIndex = name.LastIndexOf('-');

        if (separatorIndex < 0)
        {
            return (name, FontWeight.Regular, FontSlant.Upright);
        }

        var family = name[..separatorIndex];
        var style = name[(separatorIndex + 1)..];

        return style.ToLowerInvariant() switch
        {
            "regular" => (family, FontWeight.Regular, FontSlant.Upright),
            "bold" => (family, FontWeight.Bold, FontSlant.Upright),
            "italic" => (family, FontWeight.Regular, FontSlant.Italic),
            "bolditalic" => (family, FontWeight.Bold, FontSlant.Italic),
            _ => (name, FontWeight.Regular, FontSlant.Upright),
        };
    }
}
