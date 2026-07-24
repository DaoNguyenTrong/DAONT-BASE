using Microsoft.AspNetCore.DataProtection;
using FeedbackHub.Application.Common.Interfaces;

namespace FeedbackHub.Infrastructure.Services;

public sealed class DataProtectionSecretProtector(IDataProtectionProvider dataProtectionProvider) : ISecretProtector
{
    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector("FeedbackHub.Secrets");

    public string Protect(string plainText) => _protector.Protect(plainText);

    public string Unprotect(string protectedText) => _protector.Unprotect(protectedText);
}
