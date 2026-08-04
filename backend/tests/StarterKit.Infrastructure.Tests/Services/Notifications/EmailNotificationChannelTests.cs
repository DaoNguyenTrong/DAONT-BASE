using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Services.Notifications;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Interfaces;
using StarterKit.Infrastructure.Services.Notifications;

namespace StarterKit.Infrastructure.Tests.Services.Notifications;

public class EmailNotificationChannelTests
{
    private sealed record Fixture(
        EmailNotificationChannel Channel,
        IEmailSender EmailSender,
        IRepository<Account, Guid> AccountRepo);

    private static Fixture CreateFixture()
    {
        IEmailSender emailSender = Substitute.For<IEmailSender>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<Account, Guid> accountRepo = Substitute.For<IRepository<Account, Guid>>();
        unitOfWork.Repository<Account, Guid>().Returns(accountRepo);

        EmailNotificationChannel channel = new(
            emailSender, unitOfWork, NullLogger<EmailNotificationChannel>.Instance);

        return new Fixture(channel, emailSender, accountRepo);
    }

    private static Account CreateAccount(string email = "member@example.com") =>
        Account.Create(new AccountParams("Member", $"member-{Guid.NewGuid():N}", email));

    [Fact]
    public async Task SendAsync_KnownTypeAndAccountExists_SendsEmailToAccountEmail()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        Notification notification = Notification.Create(
            new NotificationParams(account.Id, NotificationTypes.OrganizationMemberAdded, """{"organizationName":"Acme"}"""));
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        await f.Channel.SendAsync(notification, CancellationToken.None);

        await f.EmailSender.Received(1).SendAsync(
            account.Email, Arg.Any<string>(), Arg.Is<string>(body => body.Contains("Acme")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_AccountNotFound_SkipsWithoutCallingEmailSender()
    {
        Fixture f = CreateFixture();
        Notification notification = Notification.Create(
            new NotificationParams(Guid.NewGuid(), NotificationTypes.OrganizationMemberAdded));
        f.AccountRepo.GetByIdAsync(notification.AccountId, Arg.Any<CancellationToken>()).Returns((Account?)null);

        await f.Channel.SendAsync(notification, CancellationToken.None);

        await f.EmailSender.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_UnknownType_SkipsWithoutCallingEmailSender()
    {
        Fixture f = CreateFixture();
        Notification notification = Notification.Create(new NotificationParams(Guid.NewGuid(), "SomeUnknownType"));

        await f.Channel.SendAsync(notification, CancellationToken.None);

        await f.EmailSender.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await f.AccountRepo.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
