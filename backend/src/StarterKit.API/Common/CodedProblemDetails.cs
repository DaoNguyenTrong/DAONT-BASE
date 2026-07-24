using Microsoft.AspNetCore.Mvc;

namespace StarterKit.API.Common;

public sealed class CodedProblemDetails : ProblemDetails
{
    public required string Code { get; init; }
}
