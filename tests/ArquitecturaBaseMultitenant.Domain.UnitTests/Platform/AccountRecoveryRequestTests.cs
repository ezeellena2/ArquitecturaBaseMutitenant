using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Platform;

/// <summary>El pedido de «Recuperar mi cuenta» nace pendiente, con el método de antes y el nuevo ya verificado.</summary>
public sealed class AccountRecoveryRequestTests
{
    private static readonly DateTime NowUtc = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_request_starts_pending_with_both_methods_and_the_account_found()
    {
        var codeId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();

        var request = AccountRecoveryRequest.Submit(codeId, LoginMethodType.Phone, "+5491123456789",
            Email.Create("Nuevo@Example.com").Value, userId, NowUtc);

        Assert.Equal(AccountRecoveryStatus.Pending, request.Status);
        Assert.Equal(codeId, request.LoginCodeId);
        Assert.Equal(LoginMethodType.Phone, request.PreviousMethodType);
        Assert.Equal("+5491123456789", request.PreviousMethodValue);
        Assert.Equal(LoginMethodType.Email, request.NewMethodType);
        Assert.Equal("nuevo@example.com", request.NewMethodValue);
        Assert.Equal(userId, request.UserId);
        Assert.Equal(NowUtc, request.CreatedAtUtc);
    }

    [Fact]
    public void Without_an_account_the_request_is_kept_and_google_is_not_a_previous_method()
    {
        var email = Email.Create("nuevo@example.com").Value;

        Assert.Null(AccountRecoveryRequest.Submit(Guid.CreateVersion7(), LoginMethodType.Email, "antes@example.com",
            email, null, NowUtc).UserId);
        Assert.Throws<ArgumentOutOfRangeException>(() => AccountRecoveryRequest.Submit(Guid.CreateVersion7(),
            LoginMethodType.Google, "subject", email, null, NowUtc));
        Assert.Throws<ArgumentException>(() => AccountRecoveryRequest.Submit(Guid.CreateVersion7(),
            LoginMethodType.Email, "antes@example.com", email, null, DateTime.SpecifyKind(NowUtc, DateTimeKind.Local)));
    }
}
