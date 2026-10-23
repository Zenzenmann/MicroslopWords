using MicroslopWords.App.Services;
using MicroslopWords.Layout;
using Shouldly;

namespace MicroslopWords.Ui.Tests;

public sealed class DirectoryFontFaceSourceTests
{
    [Fact]
    public void LoadFaces_ignores_missing_directories()
    {
        var source = new DirectoryFontFaceSource([
            Path.Combine(Path.GetTempPath(), "msw-missing-" + Guid.NewGuid().ToString("N")),
        ]);

        source.LoadFaces([]).Faces.ShouldBeEmpty();
    }

    [Fact]
    public void LoadFaces_parses_family_and_style_from_file_name()
    {
        var fontBytes = FindSystemFont();

        if (fontBytes is null)
        {
            return;
        }

        var directory = CreateTemporaryDirectory();

        try
        {
            File.WriteAllBytes(Path.Combine(directory, "Contoso-Bold.ttf"), fontBytes);

            var source = new DirectoryFontFaceSource([directory]);
            var result = source.LoadFaces([]);
            var faces = result.Faces;

            faces.Length.ShouldBe(1);
            faces[0].Family.ShouldBe("Contoso");
            faces[0].Weight.ShouldBe(FontWeight.Bold);
            faces[0].Slant.ShouldBe(FontSlant.Upright);

            foreach (var face in faces)
            {
                face.Dispose();
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LoadFaces_prefers_regular_style_for_unstyled_file_name()
    {
        var fontBytes = FindSystemFont();

        if (fontBytes is null)
        {
            return;
        }

        var directory = CreateTemporaryDirectory();

        try
        {
            File.WriteAllBytes(Path.Combine(directory, "Contoso.otf"), fontBytes);

            var source = new DirectoryFontFaceSource([directory]);
            var result = source.LoadFaces([]);
            var faces = result.Faces;

            faces.Length.ShouldBe(1);
            faces[0].Family.ShouldBe("Contoso");
            faces[0].Weight.ShouldBe(FontWeight.Regular);

            foreach (var face in faces)
            {
                face.Dispose();
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "msw-fonts-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);

        return directory;
    }

    private static byte[]? FindSystemFont()
    {
        string[] candidates = OperatingSystem.IsWindows()
            ?
            [
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
                    "arial.ttf"),
            ]
            : OperatingSystem.IsMacOS()
                ? ["/System/Library/Fonts/Supplemental/Arial.ttf"]
                : ["/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"];

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return File.ReadAllBytes(candidate);
            }
        }

        return null;
    }
}
