using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Legal;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Identity;

public sealed class AccountDeletionPolicyTests
{
    private static DateTime NowUtc => new FakeTimeProvider(new DateTimeOffset(2026, 9, 30, 15, 0, 0, TimeSpan.Zero))
        .GetUtcNow().UtcDateTime;

    [Fact]
    public void Request_keeps_methods_reserved_and_cancel_clears_all_deletion_metadata()
    {
        var user = ApplicationUser.Create("Ana", "es-AR", "America/Argentina/Buenos_Aires").Value;
        user.Email = "ana@example.test";
        Assert.True(user.RequestDeletion("Ya no la uso", NowUtc, 30).IsSuccess);
        Assert.Equal(UserStatus.PendingDeletion, user.Status);
        Assert.Equal(NowUtc.AddDays(30), user.DeletionScheduledForUtc);
        Assert.Equal("ana@example.test", user.Email);
        Assert.Equal(AccountDeletionErrors.AlreadyPending.Code,
            user.RequestDeletion("Otra vez", NowUtc, 30).Error.Code);
        Assert.True(user.CancelDeletion(NowUtc.AddDays(29)).IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Null(user.DeletionRequestedAtUtc);
        Assert.Null(user.DeletionScheduledForUtc);
        Assert.Null(user.DeletionReason);
        Assert.Null(user.DeletionRequestedByOperatorId);
    }

    [Fact]
    public void Expiration_cannot_be_cancelled_and_anonymization_keeps_identity_id()
    {
        var user = ApplicationUser.Create("Ana", "en-US", "America/New_York").Value;
        var id = user.Id;
        user.Email = "ana@example.test";
        user.PhoneNumber = "+5491112345678";
        user.RememberBusinessTenant(Guid.CreateVersion7());
        Assert.True(user.RequestDeletion("Motivo personal", NowUtc, 30).IsSuccess);
        Assert.Equal(AccountDeletionErrors.GraceExpired.Code, user.CancelDeletion(NowUtc.AddDays(30)).Error.Code);
        Assert.True(user.CompleteDeletion(NowUtc.AddDays(30), "Cuenta eliminada").IsSuccess);
        Assert.Equal(id, user.Id);
        Assert.Equal(UserStatus.Deleted, user.Status);
        Assert.Equal("Cuenta eliminada", user.DisplayName);
        Assert.Null(user.Email);
        Assert.Null(user.PhoneNumber);
        Assert.Null(user.LastBusinessTenantId);
        Assert.Null(user.DeletionReason);
        Assert.Empty(user.Culture);
        Assert.Empty(user.TimeZoneId);
    }

    [Fact]
    public void Suspended_deletion_cannot_cancel_and_reactivation_returns_to_pending()
    {
        var user = ApplicationUser.Create(null, "es-AR", "America/Argentina/Buenos_Aires").Value;
        user.Suspend();
        Assert.True(user.RequestDeletion("Pedido de plataforma", NowUtc, 30, Guid.CreateVersion7()).IsSuccess);
        Assert.Equal(UserStatus.Suspended, user.Status);
        Assert.Equal(AccountErrors.Suspended.Code, user.CancelDeletion(NowUtc.AddDays(1)).Error.Code);
        user.Reactivate();
        Assert.Equal(UserStatus.PendingDeletion, user.Status);
        Assert.True(user.CancelDeletion(NowUtc.AddDays(1)).IsSuccess);
    }

    [Theory]
    [InlineData(true, UserStatus.Active, "Legal.AccountDeletion.PlatformOperator")]
    [InlineData(false, UserStatus.PendingDeletion, "Legal.AccountDeletion.AlreadyPending")]
    [InlineData(false, UserStatus.Suspended, "Identity.Account.Suspended")]
    public void Self_deletion_policy_rejects_protected_states(bool platformOperator, UserStatus status, string code)
    {
        var row = new UserAccountRow(Guid.CreateVersion7(), null, "es-AR", "America/Argentina/Buenos_Aires",
            status, platformOperator, null, null);
        Assert.Equal(code, AccountDeletionPolicy.CheckRequest(row)!.Code);
    }
}
