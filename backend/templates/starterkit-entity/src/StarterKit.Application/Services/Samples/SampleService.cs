using StarterKit.Application.Common.Interfaces;
//#if (OrgScoped)
using StarterKit.Application.Resources;
//#endif
using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Services.Samples;

//#if (OrgScoped)
public sealed class SampleService(
    IUnitOfWork unitOfWork,
    ICurrentTenantProvider currentTenantProvider) : ISampleService
//#else
public sealed class SampleService(IUnitOfWork unitOfWork) : ISampleService
//#endif
{
    public async Task<SampleDto> CreateAsync(SampleRequest request, CancellationToken ct)
    {
//#if (OrgScoped)
        Sample entity = Sample.Create(SampleMapper.ToParams(request), RequireOrganizationId());
//#else
        Sample entity = Sample.Create(SampleMapper.ToParams(request));
//#endif
        await unitOfWork.Repository<Sample, Guid>().AddAsync(entity, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return SampleMapper.ToDto(entity);
    }

    public async Task<IReadOnlyList<SampleDto>> GetAllAsync(CancellationToken ct)
    {
//#if (OrgScoped)
        Guid organizationId = RequireOrganizationId();
        IReadOnlyList<Sample> entities = await unitOfWork.Repository<Sample, Guid>()
            .ListAsync(e => e.OrganizationId == organizationId, ct);
//#else
        IReadOnlyList<Sample> entities = await unitOfWork.Repository<Sample, Guid>().ListAsync(ct);
//#endif

        return entities
            .OrderByDescending(e => e.CreatedAt)
            .Select(SampleMapper.ToDto)
            .ToList();
    }

    public async Task<SampleDto> GetAsync(Guid id, CancellationToken ct) =>
        SampleMapper.ToDto(await FindAsync(id, ct));

    public async Task<SampleDto> UpdateAsync(Guid id, SampleRequest request, CancellationToken ct)
    {
        Sample entity = await FindAsync(id, ct);

        entity.Update(SampleMapper.ToParams(request));
        unitOfWork.Repository<Sample, Guid>().Update(entity);
        await unitOfWork.SaveChangesAsync(ct);

        return SampleMapper.ToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        Sample entity = await FindAsync(id, ct);

        unitOfWork.Repository<Sample, Guid>().Delete(entity);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<Sample> FindAsync(Guid id, CancellationToken ct)
    {
//#if (OrgScoped)
        Guid organizationId = RequireOrganizationId();
        Sample? entity = await unitOfWork.Repository<Sample, Guid>()
            .FirstOrDefaultAsync(e => e.Id == id && e.OrganizationId == organizationId, ct);
//#else
        Sample? entity = await unitOfWork.Repository<Sample, Guid>().GetByIdAsync(id, ct);
//#endif

        return entity ?? throw new NotFoundException(nameof(Sample), id);
    }
//#if (OrgScoped)

    private Guid RequireOrganizationId() =>
        currentTenantProvider.OrganizationId
            ?? throw new ForbiddenException(ApplicationMessages.OrganizationAccessDenied);
//#endif
}
