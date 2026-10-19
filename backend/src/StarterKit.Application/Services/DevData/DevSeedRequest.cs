namespace StarterKit.Application.Services.DevData;

public sealed record DevSeedAccount(string Username, string Email, string Name, string Password);

public sealed record DevSeedRequest(
    string OrganizationName,
    string OrganizationSlug,
    DevSeedAccount Owner,
    DevSeedAccount Member);
