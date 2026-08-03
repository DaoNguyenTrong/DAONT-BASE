namespace StarterKit.Domain.Entities;

public record RolePermissionParams(Guid RoleId, string PermissionCode);

public sealed class RolePermission : BaseEntity<Guid>
{
    private RolePermission()
    {
    }

    public static RolePermission Create(RolePermissionParams p)
    {
        RolePermission rolePermission = new()
        {
            Id = IdGenerator.NewUuidV7()
        };
        rolePermission.Update(p);
        return rolePermission;
    }

    public Guid RoleId { get; private set; }

    public string PermissionCode { get; private set; } = string.Empty;

    public void Update(RolePermissionParams p)
    {
        RoleId = p.RoleId;
        PermissionCode = p.PermissionCode;
    }
}
