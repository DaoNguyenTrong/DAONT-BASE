using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Options;
using StarterKit.Application.Common.Authorization;
using StarterKit.Infrastructure.Services.Auth;

namespace StarterKit.Infrastructure.Tests.Services.Auth;

public class OrganizationPermissionPolicyProviderTests
{
    private const string RegisteredPolicyName = "registered-policy";

    private sealed record Fixture(
        OrganizationPermissionPolicyProvider Provider,
        AuthorizationOptions Options);

    private static Fixture CreateFixture(Action<AuthorizationOptions>? configure = null)
    {
        AuthorizationOptions options = new();
        configure?.Invoke(options);

        OrganizationPermissionPolicyProvider provider = new(Options.Create(options));

        return new Fixture(provider, options);
    }

    public static TheoryData<string> ActiveOrganizationScopedCodes() => [.. Permissions.ActiveOrganizationScoped];

    public static TheoryData<string> RouteScopedCodes() =>
        [.. Permissions.All.Except(Permissions.ActiveOrganizationScoped)];

    // GetPolicyAsync

    [Theory]
    [MemberData(nameof(ActiveOrganizationScopedCodes))]
    public async Task GetPolicyAsync_ActiveOrganizationScopedCode_BuildsActiveOrganizationRequirement(string code)
    {
        Fixture f = CreateFixture();

        AuthorizationPolicy? policy = await f.Provider.GetPolicyAsync(code);

        Assert.NotNull(policy);
        IAuthorizationRequirement requirement = Assert.Single(policy.Requirements);
        ActiveOrganizationPermissionRequirement typed = Assert.IsType<ActiveOrganizationPermissionRequirement>(requirement);
        Assert.Equal(code, typed.PermissionCode);
        Assert.Empty(policy.AuthenticationSchemes);
    }

    [Theory]
    [MemberData(nameof(RouteScopedCodes))]
    public async Task GetPolicyAsync_RouteScopedCode_BuildsRouteOrganizationRequirement(string code)
    {
        Fixture f = CreateFixture();

        AuthorizationPolicy? policy = await f.Provider.GetPolicyAsync(code);

        Assert.NotNull(policy);
        IAuthorizationRequirement requirement = Assert.Single(policy.Requirements);
        OrganizationPermissionRequirement typed = Assert.IsType<OrganizationPermissionRequirement>(requirement);
        Assert.Equal(code, typed.PermissionCode);
    }

    [Fact]
    public void Permissions_ActiveOrganizationScoped_IsSubsetOfAll()
    {
        // The provider checks ActiveOrganizationScoped first precisely because every scoped code is
        // also in All — this pins the overlap the precedence test below depends on.
        Assert.All(Permissions.ActiveOrganizationScoped, code => Assert.Contains(code, Permissions.All));
    }

    [Fact]
    public async Task GetPolicyAsync_ActiveOrganizationScopedCode_NeverResolvesToRouteRequirement()
    {
        Fixture f = CreateFixture();

        foreach (string code in Permissions.ActiveOrganizationScoped)
        {
            AuthorizationPolicy? policy = await f.Provider.GetPolicyAsync(code);

            Assert.DoesNotContain(policy!.Requirements, r => r is OrganizationPermissionRequirement);
        }
    }

    [Fact]
    public async Task GetPolicyAsync_StaticallyRegisteredPolicy_FallsThroughToOptions()
    {
        AuthorizationPolicy registered = new AuthorizationPolicyBuilder()
            .AddRequirements(new OrganizationMembershipRequirement())
            .Build();
        Fixture f = CreateFixture(options => options.AddPolicy(RegisteredPolicyName, registered));

        AuthorizationPolicy? policy = await f.Provider.GetPolicyAsync(RegisteredPolicyName);

        Assert.Same(registered, policy);
    }

    [Fact]
    public async Task GetPolicyAsync_RegisteredOrganizationMemberPolicy_FallsThroughToOptions()
    {
        Fixture f = CreateFixture(options => options.AddPolicy(
            AuthorizationPolicies.ActiveOrganizationMember,
            policy => policy.AddRequirements(new ActiveOrganizationMembershipRequirement())));

        AuthorizationPolicy? policy = await f.Provider.GetPolicyAsync(AuthorizationPolicies.ActiveOrganizationMember);

        Assert.NotNull(policy);
        Assert.IsType<ActiveOrganizationMembershipRequirement>(Assert.Single(policy.Requirements));
    }

    [Theory]
    [InlineData("unknown.permission")]
    [InlineData("FILES.MANAGE")]
    [InlineData("files")]
    [InlineData("")]
    public async Task GetPolicyAsync_UnknownName_ReturnsNull(string policyName)
    {
        Fixture f = CreateFixture();

        AuthorizationPolicy? policy = await f.Provider.GetPolicyAsync(policyName);

        Assert.Null(policy);
    }

    // GetDefaultPolicyAsync

    [Fact]
    public async Task GetDefaultPolicyAsync_ReturnsOptionsDefaultPolicy()
    {
        AuthorizationPolicy defaultPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddAuthenticationSchemes("Bearer", "ApiKey")
            .Build();
        Fixture f = CreateFixture(options => options.DefaultPolicy = defaultPolicy);

        AuthorizationPolicy policy = await f.Provider.GetDefaultPolicyAsync();

        Assert.Same(defaultPolicy, policy);
    }

    [Fact]
    public async Task GetDefaultPolicyAsync_NotConfigured_RequiresAuthenticatedUser()
    {
        Fixture f = CreateFixture();

        AuthorizationPolicy policy = await f.Provider.GetDefaultPolicyAsync();

        Assert.IsType<DenyAnonymousAuthorizationRequirement>(Assert.Single(policy.Requirements));
    }

    // GetFallbackPolicyAsync

    [Fact]
    public async Task GetFallbackPolicyAsync_NotConfigured_ReturnsNull()
    {
        Fixture f = CreateFixture();

        AuthorizationPolicy? policy = await f.Provider.GetFallbackPolicyAsync();

        Assert.Null(policy);
    }

    [Fact]
    public async Task GetFallbackPolicyAsync_Configured_ReturnsOptionsFallbackPolicy()
    {
        AuthorizationPolicy fallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        Fixture f = CreateFixture(options => options.FallbackPolicy = fallbackPolicy);

        AuthorizationPolicy? policy = await f.Provider.GetFallbackPolicyAsync();

        Assert.Same(fallbackPolicy, policy);
    }
}
