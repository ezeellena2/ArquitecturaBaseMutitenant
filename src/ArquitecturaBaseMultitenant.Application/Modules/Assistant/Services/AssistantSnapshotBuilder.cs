using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed record AssistantDurableSnapshot(AssistantConversationSummaryResponse Conversation,
    IReadOnlyList<AssistantMessageResponse> Messages, AssistantQuickResultResponse? Result);

internal static class AssistantSnapshotBuilder
{
    // Storage-only discriminators; the frozen HTTP shape remains untouched.
    private static readonly JsonSerializerOptions Json = CreateOptions();
    private static JsonSerializerOptions CreateOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != typeof(AssistantQuickDataResponse)) return;
            info.PolymorphismOptions = new JsonPolymorphismOptions
            {
                DerivedTypes =
                {
                    new JsonDerivedType(typeof(AssistantPeopleQuickDataResponse), "people"),
                    new JsonDerivedType(typeof(AssistantInvitationsQuickDataResponse), "invitations"),
                    new JsonDerivedType(typeof(AssistantAuditQuickDataResponse), "audit"),
                    new JsonDerivedType(typeof(AssistantOrganizationsQuickDataResponse), "organizations"),
                    new JsonDerivedType(typeof(AssistantRecoveriesQuickDataResponse), "recoveries"),
                    new JsonDerivedType(typeof(AssistantSecurityQuickDataResponse), "security"),
                    new JsonDerivedType(typeof(AssistantUsageQuickDataResponse), "usage"),
                    new JsonDerivedType(typeof(AssistantAccountQuickDataResponse), "account"),
                    new JsonDerivedType(typeof(AssistantInteractionsQuickDataResponse), "interactions"),
                },
            };
        });
        return new JsonSerializerOptions { TypeInfoResolver = resolver, AllowOutOfOrderMetadataProperties = true };
    }
    internal static string Serialize(AssistantDurableSnapshot snapshot) => JsonSerializer.Serialize(snapshot, Json);
    internal static AssistantDurableSnapshot Restore(string json, uint version)
    {
        var snapshot = JsonSerializer.Deserialize<AssistantDurableSnapshot>(json, Json)
            ?? throw new InvalidOperationException("A committed assistant snapshot is missing.");
        return snapshot with { Conversation = snapshot.Conversation with { Version = version },
            Messages = snapshot.Messages.Select(message => message with { Version = version }).ToArray() };
    }
    internal static AssistantConversationSummaryResponse Summary(IAssistantConversation row) =>
        new(row.Id, row.Title, row.IsSaved, row.Version, row.CreatedAtUtc, row.LastActivityAtUtc);
}
