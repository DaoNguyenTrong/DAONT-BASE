namespace StarterKit.Application.Services.Samples;

public interface ISampleService
{
    Task<SampleDto> CreateAsync(SampleRequest request, CancellationToken ct);

    Task<IReadOnlyList<SampleDto>> GetAllAsync(CancellationToken ct);

    Task<SampleDto> GetAsync(Guid id, CancellationToken ct);

    Task<SampleDto> UpdateAsync(Guid id, SampleRequest request, CancellationToken ct);

    Task DeleteAsync(Guid id, CancellationToken ct);
}
