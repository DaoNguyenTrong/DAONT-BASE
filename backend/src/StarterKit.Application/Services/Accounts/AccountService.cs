using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Mappings;
using StarterKit.Application.Resources;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Services.Accounts;

public sealed class AccountService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    IPasswordHasher passwordHasher) : IAccountService
{
    public async Task<ProfileDto> GetCurrentProfileAsync(CancellationToken cancellationToken)
    {
        Account account = await GetCurrentAccountAsync(cancellationToken);

        return EntityMapper.ToProfileDto(account);
    }

    public async Task<ProfileDto> UpdateCurrentProfileAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        IRepository<Account, Guid> repository = unitOfWork.Repository<Account, Guid>();
        Account account = await GetCurrentAccountAsync(cancellationToken);

        if (await repository.FirstOrDefaultAsync(
                candidate => candidate.Email == request.Email && candidate.Id != account.Id,
                cancellationToken) is not null)
        {
            throw new ConflictException(ApplicationMessages.AccountEmailAlreadyExists);
        }

        account.Update(new AccountParams(
            request.Name,
            account.Username,
            request.Email,
            request.Phone,
            request.Position,
            request.Address,
            account.Status));

        repository.Update(account);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return EntityMapper.ToProfileDto(account);
    }

    public async Task ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        IRepository<Account, Guid> repository = unitOfWork.Repository<Account, Guid>();
        Account account = await GetCurrentAccountAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(account.PasswordHash) ||
            !passwordHasher.Verify(request.CurrentPassword, account.PasswordHash))
        {
            throw new UnauthorizedException(ApplicationMessages.InvalidCurrentPassword);
        }

        account.SetPasswordHash(passwordHasher.Hash(request.NewPassword));

        repository.Update(account);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Account> GetCurrentAccountAsync(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUserService.UserId, out Guid accountId))
        {
            throw new UnauthorizedException(ApplicationMessages.AuthenticatedUserRequired);
        }

        return await unitOfWork.Repository<Account, Guid>().GetByIdAsync(accountId, cancellationToken)
            ?? throw new UnauthorizedException(ApplicationMessages.AuthenticatedUserRequired);
    }
}
