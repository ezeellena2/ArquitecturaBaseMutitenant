using ArquitecturaBaseMultitenant.Domain.Legal;
namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Legal;
/// <summary>La prueba de ingreso para cancelar baja sigue ligada a cuenta, retorno y vencimiento, con un solo uso.</summary>
public sealed class AccountDeletionCancelTicketTests
{
    private static readonly DateTime IssuedAtUtc = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);
    [Fact]
    public void Proof_expires_at_five_minutes_and_cannot_be_reused()
    {
        var userId = Guid.CreateVersion7();
        var ticket = AccountDeletionCancelTicket.Issue(userId, "hash", IssuedAtUtc, "/connect/authorize?access=consumer");
        Assert.True(ticket.Consume(userId, IssuedAtUtc.AddMinutes(4)).IsSuccess);
        Assert.Equal(AccountDeletionErrors.CancelProofInvalid, ticket.Consume(userId, IssuedAtUtc.AddMinutes(4)).Error);
        var expired = AccountDeletionCancelTicket.Issue(userId, "hash", IssuedAtUtc, "/connect/authorize?access=consumer");
        Assert.Equal(AccountDeletionErrors.CancelProofInvalid, expired.Consume(userId, IssuedAtUtc.AddMinutes(5)).Error);
        Assert.Null(expired.ConsumedAtUtc);
    }
    [Fact]
    public void Wrong_account_or_time_never_consumes_the_proof()
    {
        var userId = Guid.CreateVersion7();
        const string returnUrl = "/connect/authorize?access=business";
        var ticket = AccountDeletionCancelTicket.Issue(userId, "hash", IssuedAtUtc, returnUrl);
        Assert.True(ticket.Consume(Guid.CreateVersion7(), IssuedAtUtc).IsFailure);
        Assert.True(ticket.Consume(userId, IssuedAtUtc.AddSeconds(-1)).IsFailure);
        Assert.Null(ticket.ConsumedAtUtc);
        Assert.True(ticket.Consume(userId, IssuedAtUtc).IsSuccess);
        Assert.Equal(returnUrl, ticket.ReturnUrl);
        Assert.DoesNotContain("hash", ticket.ToString(), StringComparison.Ordinal);
    }
}
