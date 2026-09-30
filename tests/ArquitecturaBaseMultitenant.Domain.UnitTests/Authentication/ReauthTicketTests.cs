using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Authentication;

public sealed class ReauthTicketTests
{
    private static readonly DateTime IssuedAtUtc = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Another_verified_method_is_required_for_a_method_change()
    {
        var methodId = Guid.CreateVersion7();
        var issued = ReauthTicket.Issue(Guid.CreateVersion7(), ReauthAction.RemoveMethod,
            methodId, methodId, "hash", IssuedAtUtc);

        Assert.Equal(ReauthErrors.OtherMethodRequiredCode, issued.Error.Code);
    }

    [Fact]
    public void Ticket_expires_exactly_after_five_minutes_and_cannot_be_reused()
    {
        var userId = Guid.CreateVersion7();
        var target = Guid.CreateVersion7();
        var ticket = ReauthTicket.Issue(userId, ReauthAction.RemoveMethod,
            Guid.CreateVersion7(), target, "hash", IssuedAtUtc).Value;

        Assert.True(ticket.Consume(userId, ReauthAction.RemoveMethod, target, IssuedAtUtc.AddMinutes(4)).IsSuccess);
        Assert.Equal(ReauthErrors.InvalidCode,
            ticket.Consume(userId, ReauthAction.RemoveMethod, target, IssuedAtUtc.AddMinutes(4)).Error.Code);
        var expired = ReauthTicket.Issue(userId, ReauthAction.DeleteAccount,
            Guid.CreateVersion7(), null, "hash", IssuedAtUtc).Value;
        Assert.Equal(ReauthErrors.ExpiredCode,
            expired.Consume(userId, ReauthAction.DeleteAccount, null, IssuedAtUtc.AddMinutes(5)).Error.Code);
    }

    [Fact]
    public void Wrong_user_action_or_target_never_consumes_the_ticket()
    {
        var userId = Guid.CreateVersion7();
        var target = Guid.CreateVersion7();
        var ticket = ReauthTicket.Issue(userId, ReauthAction.MakePrimary,
            Guid.CreateVersion7(), target, "hash", IssuedAtUtc).Value;

        Assert.True(ticket.Consume(Guid.CreateVersion7(), ReauthAction.MakePrimary, target, IssuedAtUtc).IsFailure);
        Assert.True(ticket.Consume(userId, ReauthAction.RemoveMethod, target, IssuedAtUtc).IsFailure);
        Assert.True(ticket.Consume(userId, ReauthAction.MakePrimary, Guid.CreateVersion7(), IssuedAtUtc).IsFailure);
        Assert.Null(ticket.ConsumedAtUtc);
        Assert.True(ticket.Consume(userId, ReauthAction.MakePrimary, target, IssuedAtUtc).IsSuccess);
    }

    [Fact]
    public void Cancellation_ticket_preserves_the_selected_authorize_return_url()
    {
        const string returnUrl = "/connect/authorize?access=business";
        var ticket = ReauthTicket.Issue(Guid.CreateVersion7(), ReauthAction.CancelDeletion,
            null, null, "hash", IssuedAtUtc, returnUrl).Value;

        Assert.Equal(returnUrl, ticket.ReturnUrl);
        Assert.DoesNotContain("hash", ticket.ToString(), StringComparison.Ordinal);
    }
}

