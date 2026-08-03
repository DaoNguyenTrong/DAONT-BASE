using StarterKit.Application.Common.Authorization;

namespace StarterKit.Application.Services.PermissionCatalog;

public sealed class PermissionCatalogService : IPermissionCatalogService
{
    public IReadOnlyList<PermissionDto> List() =>
        Permissions.All.Select(code => new PermissionDto(code)).ToList();
}
