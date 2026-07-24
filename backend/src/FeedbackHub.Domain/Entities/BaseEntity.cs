namespace FeedbackHub.Domain.Entities;

public interface IHasAuditFields
{
    DateTime CreatedAt { get; set; }

    DateTime? UpdatedAt { get; set; }

    string? CreatedBy { get; set; }

    string? UpdatedBy { get; set; }
}

public abstract class BaseEntity<TId> : IHasAuditFields
    where TId : notnull
{
    public TId Id { get; set; } = default!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }
}

public abstract class BaseEntity : BaseEntity<int>
{
}
