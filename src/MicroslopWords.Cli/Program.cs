using MicroslopWords.Formats.Docx;

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine("MicroslopWords CLI");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  msw to-text <input.docx>   Write the document text to stdout");
    Console.WriteLine("  msw --help                 Show this help");
    return 0;
}

if (args[0] == "to-text")
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Missing input file.");
        return 2;
    }

    var path = args[1];
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"File not found: {path}");
        return 2;
    }

    using var stream = File.OpenRead(path);
    var document = DocxService.Load(stream, Path.GetFileNameWithoutExtension(path));
    Console.Out.Write(document.PlainText);
    return 0;
}

Console.Error.WriteLine($"Unknown command: {args[0]}");
return 2;
