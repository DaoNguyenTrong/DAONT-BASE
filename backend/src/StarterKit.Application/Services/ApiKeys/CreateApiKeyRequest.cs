using System.ComponentModel.DataAnnotations;

namespace StarterKit.Application.Services.ApiKeys;

public sealed record CreateApiKeyRequest([Required] string Name);
