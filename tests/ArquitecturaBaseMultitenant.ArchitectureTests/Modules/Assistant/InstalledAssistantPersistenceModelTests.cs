using ArquitecturaBaseMultitenant.Domain.Assistant;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed class InstalledAssistantPersistenceModelTests
{
    [Fact]
    public void Installed_assistant_maps_private_conversations_and_global_operator_conversations()
    {
        var entities = ArchitectureModel.EntityTypes().Where(value => value.ClrType.Namespace == typeof(AssistantErrors).Namespace).ToArray();
        Assert.Contains(entities, value => value.ClrType.Name == "AssistantConversation" && value.GetSchema() == "tenant");
        Assert.Contains(entities, value => value.ClrType.Name == "PlatformAssistantConversation" && value.GetSchema() == "platform");
    }

    [Fact]
    public void Retention_index_contains_only_global_scheduling_metadata()
    {
        var index = ArchitectureModel.EntityTypes().Single(value => value.ClrType == typeof(AssistantSnapshotRetentionIndex));
        Assert.Equal("platform", index.GetSchema());
        Assert.Equal(new[] { "ExpiresAtUtc", "Id", "OperationId", "ScopeTenantId" }, index.GetProperties().Select(value => value.Name).Order());
        Assert.Contains(index.GetIndexes(), value => value.IsUnique && value.Properties.Select(property => property.Name).SequenceEqual(["OperationId"]));
    }

    [Fact]
    public void Turn_discovery_contains_only_actor_selection_and_lease_metadata_without_chat_content()
    {
        var work = ArchitectureModel.EntityTypes().Single(value => value.ClrType == typeof(AssistantTurnWork));
        Assert.Equal("platform", work.GetSchema());
        var expected = new[]
        {
            "Access", "CallSequence", "CallStarted", "CreatedAtUtc", "Culture", "DeadlineAtUtc", "Effort", "Funding",
            "Id", "LeaseExpiresAtUtc", "LeaseId", "ModelId", "OwnerUserId", "ProviderModelId", "ScopeTenantId",
            "SessionStartedAtUtc", "Status", "StopRequested", "TenantKind", "TimeZoneId",
        };
        Assert.Equal(expected.Order(), work.GetProperties().Select(value => value.Name).Order());
        Assert.Contains(work.GetIndexes(), value => value.Properties.Select(property => property.Name)
            .SequenceEqual(["Status", "LeaseExpiresAtUtc", "CreatedAtUtc", "Id"]));
    }

    [Theory]
    [InlineData(typeof(AssistantTurn), "tenant")]
    [InlineData(typeof(PlatformAssistantTurn), "platform")]
    public void Private_events_stay_with_the_turn_and_database_prevents_two_active_turns(Type type, string schema)
    {
        var turn = ArchitectureModel.EntityTypes().Single(value => value.ClrType == type);
        Assert.Equal(schema, turn.GetSchema());
        Assert.Equal("jsonb", turn.FindProperty("EventsJson")!.GetColumnType());
        var scope = schema == "tenant" ? new[] { "TenantId", "ConversationId" } : ["ConversationId"];
        var active = Assert.Single(turn.GetIndexes(), value => value.IsUnique
            && value.Properties.Select(property => property.Name).SequenceEqual(scope));
        Assert.Equal("\"Status\" = 'Pending' OR \"Status\" = 'Running'", active.GetFilter());
    }

    [Theory]
    [InlineData(typeof(AssistantMessage))]
    [InlineData(typeof(PlatformAssistantMessage))]
    public void Historical_subject_proof_is_private_message_metadata_independent_from_replay_retention(Type type)
    {
        var message = ArchitectureModel.EntityTypes().Single(value => value.ClrType == type);
        Assert.Equal("uuid[]", message.FindProperty("SubjectUserIds")!.GetColumnType());
        var known = message.FindProperty("PrivacySubjectsKnown")!;
        Assert.Equal(typeof(bool), known.ClrType);
        Assert.False(known.IsNullable);
        Assert.Contains(message.GetIndexes(), value => value.Properties.Select(property => property.Name).SequenceEqual(["SubjectUserIds"])
            && Equals("gin", value.FindAnnotation("Npgsql:IndexMethod")?.Value));
    }

    [Theory]
    [InlineData(typeof(AssistantDurableOperation))]
    [InlineData(typeof(PlatformAssistantDurableOperation))]
    public void Subject_lookup_and_original_replay_version_are_independent_from_concurrency_xmin(Type type)
    {
        var entity = ArchitectureModel.EntityTypes().Single(value => value.ClrType == type);
        var original = entity.FindProperty("OriginalSnapshotVersion")!;
        Assert.Equal("bigint", original.GetColumnType());
        Assert.False(original.IsConcurrencyToken);
        Assert.True(entity.FindProperty("Version")!.IsConcurrencyToken);
        Assert.Equal("uuid[]", entity.FindProperty("SubjectUserIds")!.GetColumnType());
        Assert.Contains(entity.GetIndexes(), value => value.Properties.Select(property => property.Name).SequenceEqual(["SubjectUserIds"])
            && Equals("gin", value.FindAnnotation("Npgsql:IndexMethod")?.Value));
    }

}
