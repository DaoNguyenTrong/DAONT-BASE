using StarterKit.Infrastructure.Services.Auth.External;

namespace StarterKit.Infrastructure.Tests.Services.Auth.External;

public class MicrosoftJwtValidatorTests
{
    [Fact]
    public void BuildIssuerPattern_MultiTenantTemplate_MatchesRealTenantIssuer()
    {
        System.Text.RegularExpressions.Regex pattern = MicrosoftJwtValidator.BuildIssuerPattern(
            "https://login.microsoftonline.com/{tenantid}/v2.0");

        Assert.Matches(
            pattern, "https://login.microsoftonline.com/9188040d-6c67-4c5b-b112-36a304b66dad/v2.0");
    }

    [Fact]
    public void BuildIssuerPattern_MultiTenantTemplate_RejectsUnrelatedIssuer()
    {
        System.Text.RegularExpressions.Regex pattern = MicrosoftJwtValidator.BuildIssuerPattern(
            "https://login.microsoftonline.com/{tenantid}/v2.0");

        Assert.DoesNotMatch(pattern, "https://evil.example.com/v2.0");
    }

    [Fact]
    public void BuildIssuerPattern_FixedTenantIssuer_MatchesItself()
    {
        const string issuer = "https://login.microsoftonline.com/9188040d-6c67-4c5b-b112-36a304b66dad/v2.0";

        System.Text.RegularExpressions.Regex pattern = MicrosoftJwtValidator.BuildIssuerPattern(issuer);

        Assert.Matches(pattern, issuer);
    }
}
