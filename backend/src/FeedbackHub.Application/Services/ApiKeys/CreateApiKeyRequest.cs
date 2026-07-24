using System.ComponentModel.DataAnnotations;

namespace FeedbackHub.Application.Services.ApiKeys;

public sealed record CreateApiKeyRequest([Required] string Name);
