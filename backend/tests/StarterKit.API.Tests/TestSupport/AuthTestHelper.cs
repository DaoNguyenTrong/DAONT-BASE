using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StarterKit.Application.Common.Settings;
using StarterKit.Application.Services.ApiKeys;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;
using StarterKit.Infrastructure.Services;

namespace StarterKit.API.Tests.TestSupport;

public static class AuthTestHelper
{
    // Mirrors backend/src/StarterKit.API/appsettings.json's JwtSettings so tokens minted here
    // validate against the real JwtBearer pipeline the test host boots.
    private static readonly JwtSettings JwtSettings = new()
    {
        SecretKey = "change-this-development-secret-key-32-chars-minimum",
        Issuer = "StarterKit-Auth",
        Audiences = ["StarterKit-API", "StarterKit-Web", "StarterKit-Mobile"],
        AccessTokenExpiryMinutes = 15,
        RefreshTokenExpiryDays = 7
    };

    public static async Task<Account> SeedConfirmedAccountAsync(
        AppDbContext context,
        string username = "test-user",
        string email = "test-user@example.com",
        string? passwordHash = "seeded-hash")
    {
        Account account = Account.Create(new AccountParams("Test User", username, email));
        if (passwordHash is not null)
        {
            account.SetPasswordHash(passwordHash);
        }
        account.ConfirmEmail();

        context.Accounts.Add(account);
        await context.SaveChangesAsync();

        return account;
    }

    public static string MintAccessToken(Account account)
    {
        JwtTokenService jwtTokenService = new(Options.Create(JwtSettings));
        return jwtTokenService.GenerateAccessToken(account);
    }

    public static async Task<(ApiKey ApiKey, string RawKey)> SeedActiveApiKeyAsync(AppDbContext context, string name = "Test Key")
    {
        ApiKeyService apiKeyServiceHelper = new(new PassthroughUnitOfWork(context));
        CreateApiKeyResult result =
            await apiKeyServiceHelper.CreateAsync(new CreateApiKeyRequest(name), CancellationToken.None);

        ApiKey apiKey = await context.ApiKeys.FirstAsync(k => k.Id == result.Key.Id);
        return (apiKey, result.RawKey);
    }

    private sealed class PassthroughUnitOfWork(AppDbContext context) : StarterKit.Application.Common.Interfaces.IUnitOfWork
    {
        public StarterKit.Domain.Interfaces.IRepository<T, TId> Repository<T, TId>()
            where T : BaseEntity<TId> where TId : notnull =>
            new StarterKit.Infrastructure.Persistence.Repositories.GenericRepository<T, TId>(context);

        public StarterKit.Domain.Interfaces.IRepository<T> Repository<T>() where T : BaseEntity =>
            new StarterKit.Infrastructure.Persistence.Repositories.GenericRepository<T>(context);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            context.SaveChangesAsync(cancellationToken);
    }
}
