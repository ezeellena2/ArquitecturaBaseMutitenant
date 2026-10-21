using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Assistant;

public sealed class AssistantConversationActivityTests
{
    [Theory]
    [InlineData(Access.Consumer)]
    [InlineData(Access.Business)]
    [InlineData(Access.Platform)]
    public void Activity_shares_the_conversation_identity_and_never_replaces_the_manual_title(Access access)
    {
        var owner = Guid.CreateVersion7();
        var created = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        AssistantConversationBase conversation = access == Access.Platform
            ? PlatformAssistantConversation.Create(owner, created) : AssistantConversation.Create(owner, access, created);
        var activity = conversation switch
        {
            AssistantConversation value => (AssistantConversationActivityBase)value.Activity,
            PlatformAssistantConversation value => value.Activity,
            _ => throw new InvalidOperationException(),
        };
        Assert.Equal(conversation.Id, activity.Id);
        Assert.Equal(owner, activity.OwnerUserId);
        Assert.Equal(access, activity.Access);
        Assert.Equal(created, activity.LastActivityAtUtc);
        Assert.Null(conversation.Title);
        Assert.True(conversation.TrySetAutomaticTitle("Automatic", conversation.Version, created.AddMinutes(1)));
        Assert.Null(conversation.ManualTitle);
        Assert.Equal("Automatic", activity.AutomaticTitle);
        Assert.Equal("Automatic", conversation.Title);
        conversation.Update("Manual", true, created.AddMinutes(2));
        conversation.RecordQuick("Quick", created.AddMinutes(3));
        Assert.False(conversation.TrySetAutomaticTitle("Late", conversation.Version, created.AddMinutes(4)));
        Assert.Equal("Manual", conversation.Title);
        Assert.Equal("Automatic", activity.AutomaticTitle);
        Assert.True(conversation.IsSaved);
        Assert.True(conversation.IsTitleManual);
        Assert.Equal(created.AddMinutes(3), conversation.LastActivityAtUtc);
        Assert.Equal(created, conversation.CreatedAtUtc);
    }
}
