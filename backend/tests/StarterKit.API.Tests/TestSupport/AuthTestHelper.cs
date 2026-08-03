using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StarterKit.Application.Common.Authorization;
using StarterKit.Application.Common.Settings;
using StarterKit.Application.Services.ApiKeys;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;
using StarterKit.Infrastructure.Services.Auth;

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

    public static string MintAccessToken(Account account, Guid? organizationId = null)
    {
        JwtTokenService jwtTokenService = new(Options.Create(JwtSettings));
        return jwtTokenService.GenerateAccessToken(account, organizationId);
    }

    public static async Task<Organization> SeedOrganizationAsync(
        AppDbContext context,
        string name = "Acme Inc",
        string? slug = null)
    {
        Organization organization = Organization.Create(new OrganizationParams(name, slug ?? $"acme-{Guid.NewGuid():N}"));

        context.Organizations.Add(organization);
        await context.SaveChangesAsync();

        return organization;
    }

    public static async Task<OrganizationMember> SeedOrganizationMemberAsync(
        AppDbContext context,
        Guid organizationId,
        Guid accountId,
        SystemRoleKind role = SystemRoleKind.Owner)
    {
        IReadOnlyDictionary<SystemRoleKind, Role> systemRoles = await SeedSystemRolesAsync(context, organizationId);

        OrganizationMember member = OrganizationMember.Create(new OrganizationMemberParams(organizationId, accountId));
        context.OrganizationMembers.Add(member);
        context.OrganizationMemberRoles.Add(
            OrganizationMemberRole.Create(new OrganizationMemberRoleParams(member.Id, systemRoles[role].Id)));

        await context.SaveChangesAsync();

        return member;
    }

    // Idempotent: seeds the 3 system roles for an org (mirrors RoleService.SeedSystemRolesAsync,
    // hand-rolled against the raw AppDbContext since pulling in the full DI-wired service isn't
    // worth it in a test helper) or returns the existing ones if already seeded.
    public static async Task<IReadOnlyDictionary<SystemRoleKind, Role>> SeedSystemRolesAsync(
        AppDbContext context, Guid organizationId)
    {
        List<Role> existing = await context.Roles
            .Where(role => role.OrganizationId == organizationId && role.SystemRoleKind != null)
            .ToListAsync();

        if (existing.Count == 3)
        {
            return existing.ToDictionary(role => role.SystemRoleKind!.Value);
        }

        Dictionary<SystemRoleKind, Role> roles = [];

        foreach (SystemRoleKind kind in Enum.GetValues<SystemRoleKind>())
        {
            Role role = Role.Create(new RoleParams(organizationId, kind.ToString(), kind));
            context.Roles.Add(role);
            roles[kind] = role;

            if (kind == SystemRoleKind.Admin)
            {
                context.RolePermissions.Add(
                    RolePermission.Create(new RolePermissionParams(role.Id, Permissions.OrganizationMembersManage)));
            }
        }

        await context.SaveChangesAsync();

        return roles;
    }

    public static async Task<Role> SeedCustomRoleAsync(
        AppDbContext context, Guid organizationId, string name, IReadOnlyList<string> permissionCodes)
    {
        Role role = Role.Create(new RoleParams(organizationId, name));
        context.Roles.Add(role);

        foreach (string code in permissionCodes)
        {
            context.RolePermissions.Add(RolePermission.Create(new RolePermissionParams(role.Id, code)));
        }

        await context.SaveChangesAsync();

        return role;
    }

    public static async Task AssignRoleAsync(AppDbContext context, Guid organizationMemberId, Guid roleId)
    {
        context.OrganizationMemberRoles.Add(
            OrganizationMemberRole.Create(new OrganizationMemberRoleParams(organizationMemberId, roleId)));

        await context.SaveChangesAsync();
    }

    // Signed with the real JwtSettings.SecretKey but already-expired — exercises the JwtBearer
    // pipeline's lifetime validation, distinct from a token that's simply missing.
    public static string MintExpiredAccessToken(Account account) =>
        MintAccessToken(account, JwtSettings.SecretKey, DateTime.UtcNow.AddMinutes(-1));

    // Correct claims/expiry but signed with a different key — exercises signature validation.
    public static string MintTamperedAccessToken(Account account) =>
        MintAccessToken(account, "tampered-secret-key-also-32-chars-minimum", DateTime.UtcNow.AddMinutes(15));

    private static string MintAccessToken(Account account, string secretKey, DateTime expires)
    {
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(secretKey));
        SigningCredentials credentials = new(key, SecurityAlgorithms.HmacSha256);
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Name, account.Username),
            new(ClaimTypes.Email, account.Email),
            new(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        ];

        JwtSecurityToken token = new(
            issuer: JwtSettings.Issuer,
            audience: JwtSettings.Audiences[0],
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
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
