using System.Runtime.InteropServices;

namespace MicroslopWords.Layout.Tests;

internal static class TestFonts
{
    private static readonly string[] CandidatePaths = BuildCandidates();

    public static byte[]? TryLoadBytes()
    {
        foreach (var path in CandidatePaths)
        {
            if (File.Exists(path))
            {
                try
                {
                    return File.ReadAllBytes(path);
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
            }
        }

        return null;
    }

    private static string[] BuildCandidates()
    {
        var fromEnv = Environment.GetEnvironmentVariable("MSW_TEST_FONT");
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            candidates.Add(fromEnv);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            candidates.Add(Path.Combine(fonts, "arial.ttf"));
            candidates.Add(Path.Combine(fonts, "segoeui.ttf"));
            candidates.Add(Path.Combine(fonts, "calibri.ttf"));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            candidates.Add("/System/Library/Fonts/Supplemental/Arial.ttf");
            candidates.Add("/System/Library/Fonts/Helvetica.ttc");
        }
        else
        {
            candidates.Add("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf");
            candidates.Add("/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf");
            candidates.Add("/usr/share/fonts/TTF/DejaVuSans.ttf");
        }

        return candidates.ToArray();
    }
}
