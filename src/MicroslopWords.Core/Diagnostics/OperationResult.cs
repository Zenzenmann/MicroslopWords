using System.Collections.Immutable;

namespace MicroslopWords.Core.Diagnostics;

public sealed class OperationResult<T>
{
    internal OperationResult(bool isSuccess, T? value, ImmutableArray<Diagnostic> diagnostics)
    {
        IsSuccess = isSuccess;
        Value = value;
        Diagnostics = diagnostics;
    }

    public bool IsSuccess { get; }

    public T? Value { get; }

    public ImmutableArray<Diagnostic> Diagnostics { get; }

    public bool HasErrors =>
        Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
}

public static class OperationResult
{
    public static OperationResult<T> Success<T>(
        T value,
        IEnumerable<Diagnostic>? diagnostics = null) =>
        new(true, value, diagnostics?.ToImmutableArray() ?? ImmutableArray<Diagnostic>.Empty);

    public static OperationResult<T> Failure<T>(params Diagnostic[] diagnostics)
    {
        if (diagnostics.Length == 0)
        {
            throw new ArgumentException("At least one diagnostic is required.", nameof(diagnostics));
        }

        return new(false, default, diagnostics.ToImmutableArray());
    }

    public static OperationResult<T> Failure<T>(string code, string message) =>
        Failure<T>(new Diagnostic(code, DiagnosticSeverity.Error, message));
}
