namespace StarterKit.Application.Services.DevData;

public interface IDevDataSeeder
{
    /// <summary>
    /// Creates the accounts and the organization in <paramref name="request"/> that do not exist yet. Existing rows
    /// are left as they are, so running it again changes nothing.
    /// </summary>
    Task SeedAsync(DevSeedRequest request, CancellationToken cancellationToken);
}
