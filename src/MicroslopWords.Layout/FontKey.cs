namespace MicroslopWords.Layout;

public enum FontWeight
{
    Regular = 400,
    Bold = 700,
}

public enum FontSlant
{
    Upright,
    Italic,
}

public readonly record struct FontKey(
    string Family,
    FontWeight Weight = FontWeight.Regular,
    FontSlant Slant = FontSlant.Upright);
