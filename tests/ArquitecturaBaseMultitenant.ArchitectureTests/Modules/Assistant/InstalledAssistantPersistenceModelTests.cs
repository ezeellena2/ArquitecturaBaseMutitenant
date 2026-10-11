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
