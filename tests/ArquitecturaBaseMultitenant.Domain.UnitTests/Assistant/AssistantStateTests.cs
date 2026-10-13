using ArquitecturaBaseMultitenant.Domain.Assistant;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Assistant;

public sealed class AssistantStateTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Undefined_statuses_cannot_be_saved_as_private_turn_or_message_state(bool platform)
    {
        var nowUtc = new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc);
        var conversationId = Guid.CreateVersion7();
        AssistantTurnBase turn = platform ? PlatformAssistantTurn.Create(conversationId, nowUtc) : AssistantTurn.Create(conversationId, nowUtc);
        AssistantMessageBase message = platform ? PlatformAssistantMessage.Create(conversationId, "Assistant", "Generating", null, nowUtc)
            : AssistantMessage.Create(conversationId, "Assistant", "Generating", null, nowUtc);
        Assert.Throws<ArgumentException>(() => turn.Finish("999"));
        Assert.Throws<ArgumentException>(() => message.Finish("999"));
        Assert.Equal(AssistantTurnStatus.Pending, turn.Status);
        Assert.Equal(AssistantPersistedMessageStatus.Generating, message.Status);
    }
}
