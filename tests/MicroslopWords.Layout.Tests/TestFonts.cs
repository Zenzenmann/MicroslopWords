using System.Runtime.InteropServices;

namespace MicroslopWords.Layout.Tests;

internal static class TestFonts
{
    private static readonly string[] CandidatePaths = BuildCandidates(
        Environment.GetEnvironmentVariable("MSW_TEST_FONT"),
        ["arial.ttf", "segoeui.ttf", "calibri.ttf"],
        ["/System/Library/Fonts/Supplemental/Arial.ttf", "/System/Library/Fonts/Helvetica.ttc"],
        [
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
            "/usr/share/fonts/TTF/DejaVuSans.ttf",
        ]);

    private static readonly string[] ComplexScriptCandidatePaths = BuildCandidates(
        Environment.GetEnvironmentVariable("MSW_TEST_COMPLEX_SCRIPT_FONT"),
        ["Nirmala.ttc", "mangal.ttf"],
        [
            "/System/Library/Fonts/Supplemental/Kohinoor Devanagari.ttc",
            "/System/Library/Fonts/Supplemental/Devanagari Sangam MN.ttc",
        ],
        [
            "/usr/share/fonts/truetype/noto/NotoSansDevanagari-Regular.ttf",
            "/usr/share/fonts/truetype/lohit-devanagari/Lohit-Devanagari.ttf",
        ]);

    private static readonly string[] EmojiCandidatePaths = BuildCandidates(
        Environment.GetEnvironmentVariable("MSW_TEST_EMOJI_FONT"),
        ["seguiemj.ttf"],
        ["/System/Library/Fonts/Apple Color Emoji.ttc"],
        [
            "/usr/share/fonts/truetype/noto/NotoColorEmoji.ttf",
            "/usr/share/fonts/noto/NotoColorEmoji.ttf",
        ]);

    public static byte[]? TryLoadBytes() => TryLoadBytes(CandidatePaths);

    public static byte[]? TryLoadComplexScriptBytes() => TryLoadBytes(ComplexScriptCandidatePaths);

    public static byte[]? TryLoadEmojiBytes() => TryLoadBytes(EmojiCandidatePaths);

    private static byte[]? TryLoadBytes(IEnumerable<string> candidatePaths)
    {
        foreach (var path in candidatePaths)
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

    private static string[] BuildCandidates(
        string? fromEnvironment,
        string[] windowsFileNames,
        string[] macOSPaths,
        string[] linuxPaths)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            candidates.Add(fromEnvironment);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            candidates.AddRange(windowsFileNames.Select(fileName => Path.Combine(fonts, fileName)));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            candidates.AddRange(macOSPaths);
        }
        else
        {
            candidates.AddRange(linuxPaths);
        }

        return candidates.ToArray();
    }
}
