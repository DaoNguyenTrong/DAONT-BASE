using Microsoft.AspNetCore.DataProtection;
using StarterKit.Application.Common.Interfaces;

namespace StarterKit.Infrastructure.Services;

public sealed class DataProtectionSecretProtector(IDataProtectionProvider dataProtectionProvider) : ISecretProtector
{
    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector("StarterKit.Secrets");

    public string Protect(string plainText) => _protector.Protect(plainText);

    public string Unprotect(string protectedText) => _protector.Unprotect(protectedText);
}
