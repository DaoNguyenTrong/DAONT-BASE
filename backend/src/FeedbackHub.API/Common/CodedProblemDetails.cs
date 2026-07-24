using Microsoft.AspNetCore.Mvc;

namespace FeedbackHub.API.Common;

public sealed class CodedProblemDetails : ProblemDetails
{
    public required string Code { get; init; }
}
