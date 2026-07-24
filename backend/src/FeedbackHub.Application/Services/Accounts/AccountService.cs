using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Mappings;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Services.Accounts;

public sealed class AccountService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    IPasswordHasher passwordHasher) : IAccountService
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 10;

    public async Task<PagedResult<AccountDto>> GetAllAsync(
        PaginationRequest request,
        CancellationToken cancellationToken)
    {
        int pageNumber = request.PageNumber < 1 ? DefaultPageNumber : request.PageNumber;
        int pageSize = request.PageSize < 1 ? DefaultPageSize : request.PageSize;
        string? searchTerm = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();

        IRepository<Account, Guid> repository = unitOfWork.Repository<Account, Guid>();

        (IReadOnlyList<Account> accounts, int totalCount) = await repository.ListPagedAsync(
            _ => true,
            searchTerm,
            [account => account.Name, account => account.Username, account => account.Email],
            pageNumber, pageSize, cancellationToken);

        return new PagedResult<AccountDto>(
            accounts.Select(EntityMapper.ToDto).ToList(),
            totalCount,
            pageNumber,
            pageSize);
    }

    public async Task<AccountDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        Account account = await unitOfWork.Repository<Account, Guid>().GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), id);

        return EntityMapper.ToDto(account);
    }

    public async Task<AccountDto> CreateAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        Account account = Account.Create(request.ToParams());
        account.SetPasswordHash(passwordHasher.Hash(request.Password));

        await unitOfWork.Repository<Account, Guid>().AddAsync(account, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return EntityMapper.ToDto(account);
    }

    public async Task<AccountDto> UpdateAsync(
        Guid id,
        UpdateAccountRequest request,
        CancellationToken cancellationToken)
    {
        IRepository<Account, Guid> repository = unitOfWork.Repository<Account, Guid>();
        Account account = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), id);

        account.Update(request.ToParams());

        repository.Update(account);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return EntityMapper.ToDto(account);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        IRepository<Account, Guid> repository = unitOfWork.Repository<Account, Guid>();
        Account account = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), id);

        repository.Delete(account);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

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
