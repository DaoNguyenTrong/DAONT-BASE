using Microsoft.AspNetCore.Mvc;

namespace StarterKit.API.Common;

public sealed class CodedValidationProblemDetails(IDictionary<string, string[]> errors)
    : ValidationProblemDetails(errors)
{
    public required string Code { get; init; }
}
