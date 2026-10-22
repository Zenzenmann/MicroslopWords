using MicroslopWords.Core.Diagnostics;
using Shouldly;

namespace MicroslopWords.Core.Tests;

public sealed class OperationResultTests
{
    [Fact]
    public void Success_exposes_value_and_diagnostics()
    {
        var diagnostic = new Diagnostic("TEST001", DiagnosticSeverity.Warning, "Warning");

        var result = OperationResult.Success("value", [diagnostic]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("value");
        result.Diagnostics.ShouldContain(diagnostic);
        result.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public void Failure_exposes_error_diagnostic()
    {
        var result = OperationResult.Failure<string>("TEST002", "Broken");

        result.IsSuccess.ShouldBeFalse();
        result.Value.ShouldBeNull();
        result.Diagnostics.Single().Code.ShouldBe("TEST002");
        result.HasErrors.ShouldBeTrue();
    }
}
