namespace FeedbackHub.Domain;

/// <summary>
/// Convention: all new entity primary keys use UUID v7 (time-ordered, not guessable).
/// Do not use for non-entity randomness (JWT jti, storage hashes, etc.).
/// </summary>
public static class IdGenerator
{
    public static Guid NewUuidV7() => Guid.CreateVersion7();
}
