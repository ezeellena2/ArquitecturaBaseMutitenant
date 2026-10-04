using ArquitecturaBaseMultitenant.Application.Models.Home;
using ArquitecturaBaseMultitenant.Application.Services.Home;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Home;

/// <summary>ADR 0040: en «Para resolver» va primero lo urgente y después por fecha; lo que no tiene fecha, al final.</summary>
public sealed class PendingItemOrderTests
{
    private static readonly DateTime Today = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Urgent_items_go_first_then_the_oldest_and_undated_ones_last()
    {
        var ordered = PendingItemOrder.Sort(
        [
            Item("publicPageDraft", urgent: false, since: null),
            Item("domainUnverified", urgent: false, since: Today.AddDays(-2)),
            Item("paymentMissing", urgent: true, since: Today.AddDays(-1)),
            Item("invitationExpired", urgent: false, since: Today.AddDays(-8)),
        ]);

        Assert.Equal(["paymentMissing", "invitationExpired", "domainUnverified", "publicPageDraft"],
            ordered.Select(item => item.Kind));
    }

    private static PendingItem Item(string kind, bool urgent, DateTime? since) =>
        new("users", kind, Subject: null, urgent, since, DueAtUtc: null, TargetId: null);
}
