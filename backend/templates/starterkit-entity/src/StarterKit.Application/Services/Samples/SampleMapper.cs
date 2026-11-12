using Riok.Mapperly.Abstractions;
using StarterKit.Domain.Entities;

namespace StarterKit.Application.Services.Samples;

[Mapper]
public static partial class SampleMapper
{
    [MapperIgnoreSource(nameof(Sample.UpdatedAt))]
    [MapperIgnoreSource(nameof(Sample.CreatedBy))]
    [MapperIgnoreSource(nameof(Sample.UpdatedBy))]
//#if (OrgScoped)
    [MapperIgnoreSource(nameof(Sample.OrganizationId))]
//#endif
    public static partial SampleDto ToDto(Sample entity);

    public static partial SampleParams ToParams(SampleRequest request);
}
