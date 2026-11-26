using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Assistant;

/// <summary>ADR 0068 §6: el turno sella sus acciones una vez, y un turno con acciones va en la versión 2 del motor (0064 §6).</summary>
public sealed class AssistantTurnOfferedActionsTests
{
    private static readonly DateTime Now = new(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    public void A_turn_with_actions_is_sealed_with_engine_version_two_and_one_without_with_one(bool offersActions, int version)
    {
        var work = AssistantTurnWork.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Access.Business, Guid.CreateVersion7(),
            TenantKind.Business, "es-AR", "America/Argentina/Buenos_Aires", null, Guid.CreateVersion7(), "fake-chat-model",
            "Low", AssistantKeyFunding.PlatformKey, Now, Now.AddMinutes(5), offersActions);

        Assert.Equal(version, work.EngineVersion);
        Assert.Equal(2, AssistantTurnWork.CurrentEngineVersion);
    }

    [Fact]
    public void The_offered_actions_are_sealed_once()
    {
        var turn = AssistantTurn.Create(Guid.CreateVersion7(), Now);

        turn.OfferActions(["users.invite@1", "roles.create@1", "users.invite@1"]);

        Assert.Equal(new[] { "users.invite@1", "roles.create@1" }, turn.ReadOfferedActions());
        Assert.Throws<InvalidOperationException>(() => turn.OfferActions(["roles.edit@1"]));
    }

    [Fact]
    public void A_turn_without_actions_seals_nothing()
    {
        var turn = AssistantTurn.Create(Guid.CreateVersion7(), Now);

        turn.OfferActions([]);

        Assert.Null(turn.OfferedActions);
        Assert.Empty(turn.ReadOfferedActions());
    }
}
