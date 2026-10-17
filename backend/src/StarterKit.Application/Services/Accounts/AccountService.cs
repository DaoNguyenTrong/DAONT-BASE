using Microsoft.Extensions.Options;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Application.Resources;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Services.Accounts;

public sealed class AccountService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    IPasswordHasher passwordHasher,
    IPasswordAttemptStore passwordAttemptStore,
    IOptions<LoginLockoutSettings> lockoutOptions) : IAccountService
{
    private readonly LoginLockoutSettings lockoutSettings = lockoutOptions.Value;

    public async Task<ProfileDto> GetCurrentProfileAsync(CancellationToken cancellationToken)
    {
        Account account = await GetCurrentAccountAsync(cancellationToken);

        return AccountMapper.ToProfileDto(account);
    }

    public async Task<ProfileDto> UpdateCurrentProfileAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        IRepository<Account, Guid> repository = unitOfWork.Repository<Account, Guid>();
        Account account = await GetCurrentAccountAsync(cancellationToken);

        string email = Account.NormalizeEmail(request.Email);
        if (await repository.FirstOrDefaultAsync(
                candidate => candidate.Email == email && candidate.Id != account.Id,
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
            request.Address));

        repository.Update(account);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return AccountMapper.ToProfileDto(account);
    }

    public async Task ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        IRepository<Account, Guid> repository = unitOfWork.Repository<Account, Guid>();
        Account account = await GetCurrentAccountAsync(cancellationToken);

        // No password to guess: nothing counted.
        if (string.IsNullOrWhiteSpace(account.PasswordHash))
        {
            throw new UnauthorizedException(ApplicationMessages.InvalidCurrentPassword);
        }

        // Same scheme as sign-in (AuthService.LoginAsync), on its own counter: a stolen session can't brute-force
        // the current password here, and can't lock the owner out of signing in either. Reserved and reported
        // before the check, so parallel guesses can't slip past the limit and the lock confirms no guess.
        DateTime utcNow = DateTime.UtcNow;
        int? attempt = await passwordAttemptStore.ReserveAttemptAsync(
            PasswordAttemptPurpose.PasswordChange,
            account.Id,
            utcNow,
            TimeSpan.FromMinutes(lockoutSettings.WindowMinutes),
            cancellationToken);
        if (attempt is not { } attemptNumber || attemptNumber > lockoutSettings.MaxFailedAttempts)
        {
            throw new UnauthorizedException(ApplicationMessages.PasswordChangeTemporarilyLocked);
        }

        if (!passwordHasher.Verify(request.CurrentPassword, account.PasswordHash))
        {
            if (attemptNumber == lockoutSettings.MaxFailedAttempts)
            {
                // Not the request's token — an aborted request must not skip the lock.
                await passwordAttemptStore.LockAsync(
                    PasswordAttemptPurpose.PasswordChange,
                    account.Id,
                    utcNow.AddMinutes(lockoutSettings.LockDurationMinutes),
                    CancellationToken.None);
            }

            throw new UnauthorizedException(ApplicationMessages.InvalidCurrentPassword);
        }

        await passwordAttemptStore.ClearAsync(PasswordAttemptPurpose.PasswordChange, account.Id, cancellationToken);

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
