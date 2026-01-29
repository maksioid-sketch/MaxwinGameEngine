namespace Engine.Core.Validation;

public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}

public sealed class ValidationIssue
{
    public ValidationSeverity Severity { get; init; } = ValidationSeverity.Error;
    public string Code { get; init; } = "GEN";
    public string Message { get; init; } = "";
    public string? EntityName { get; init; }
}
