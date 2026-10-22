namespace MicroslopWords.Core.Diagnostics;

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

public sealed record Diagnostic(
    string Code,
    DiagnosticSeverity Severity,
    string Message);
