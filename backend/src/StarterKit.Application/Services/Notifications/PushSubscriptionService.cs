using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Resources;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Services.Notifications;

public sealed class PushSubscriptionService(
    IUnitOfWork unitOfWork, ICurrentUserService currentUserService) : IPushSubscriptionService
{
    public async Task RegisterAsync(RegisterPushSubscriptionRequest request, CancellationToken cancellationToken)
    {
        Guid accountId = GetCurrentAccountId();
        IRepository<PushSubscription, Guid> repository = unitOfWork.Repository<PushSubscription, Guid>();

        PushSubscription? existing = await repository.FirstOrDefaultAsync(
            s => s.Token == request.Token, cancellationToken);

        if (existing is null)
        {
            PushSubscription subscription = PushSubscription.Create(
                new PushSubscriptionParams(accountId, request.Token, request.Platform));
            await repository.AddAsync(subscription, cancellationToken);
        }
        else if (existing.AccountId != accountId)
        {
            existing.ReassignTo(accountId);
            repository.Update(existing);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(string token, CancellationToken cancellationToken)
    {
        Guid accountId = GetCurrentAccountId();
        IRepository<PushSubscription, Guid> repository = unitOfWork.Repository<PushSubscription, Guid>();

        PushSubscription? existing = await repository.FirstOrDefaultAsync(
            s => s.Token == token && s.AccountId == accountId, cancellationToken);

        if (existing is null)
        {
            return;
        }

        repository.Delete(existing);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<PushSubscriptionStatusResponse> GetStatusAsync(CancellationToken cancellationToken)
    {
        Guid accountId = GetCurrentAccountId();

        PushSubscription? existing = await unitOfWork.Repository<PushSubscription, Guid>()
            .FirstOrDefaultAsync(s => s.AccountId == accountId, cancellationToken);

        return new PushSubscriptionStatusResponse(existing is not null);
    }

    private Guid GetCurrentAccountId()
    {
        if (!Guid.TryParse(currentUserService.UserId, out Guid accountId))
        {
            throw new UnauthorizedException(ApplicationMessages.AuthenticatedUserRequired);
        }

        return accountId;
    }
}
