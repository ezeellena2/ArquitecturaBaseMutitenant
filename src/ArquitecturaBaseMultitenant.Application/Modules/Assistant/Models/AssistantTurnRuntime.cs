using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Application.Models.Request;
using Microsoft.Extensions.AI;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantTurnAdmission(IdempotentReference Reference, Guid TurnId);
public sealed record AssistantTurnFrame(string Event, int Sequence, JsonElement Data);
public sealed record AssistantTurnRead(IReadOnlyList<AssistantTurnFrame> Frames, bool Terminal);
public sealed record AssistantTurnExecution(Guid TurnId, Guid LeaseId, IReadOnlyList<ChatMessage> Messages,
    IReadOnlyList<AITool> Tools, AssistantChatBinding Binding, bool RecoverOnly,
    Guid ConversationId = default, IReadOnlyList<AssistantMessageResponse>? ContextMessages = null);
public sealed record AssistantToolExecution(string Data, IReadOnlyList<Guid> SubjectUserIds);
public sealed record AssistantChatBinding(AssistantMode Mode, Guid ModelId, string ProviderModelId, AssistantEffort Effort);
public sealed record AssistantHistoryMessageRow(Guid Id, string Role, bool PrivacySubjectsKnown, Guid[] SubjectUserIds);
public sealed record AssistantTurnDiscovery(Guid TurnId, ExecutionActor Actor);
public sealed record AssistantTitleCall(AssistantChatBinding Binding, uint ConversationVersion, int Sequence,
    IReadOnlyList<ChatMessage> Messages);

/// <summary>Storage-only envelopes, never returned as SSE payloads.</summary>
public static class AssistantTurnJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(), new AssistantTurnMoneyConverter() },
    };
    public static AssistantTurnFrame Frame(string kind, int sequence, object data) =>
        new(kind, sequence, JsonSerializer.SerializeToElement(data, data.GetType(), Options));
    public static string Serialize<T>(T data) => JsonSerializer.Serialize(data, Options);
    public static T Restore<T>(string data) => JsonSerializer.Deserialize<T>(data, Options)
        ?? throw new InvalidOperationException("A durable turn snapshot is missing.");
}

/// <summary>Private storage and SSE JsonElement use the same amount/ISO shape as the frozen wire contract.</summary>
internal sealed class AssistantTurnMoneyConverter : System.Text.Json.Serialization.JsonConverter<Domain.ValueObjects.Money>
{
    public override Domain.ValueObjects.Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var value = document.RootElement;
        var currency = Domain.ValueObjects.CurrencyCode.Create(value.GetProperty("currency").GetString());
        if (currency.IsFailure) throw new JsonException("The stored currency is invalid.");
        return new Domain.ValueObjects.Money(value.GetProperty("amount").GetDecimal(), currency.Value);
    }
    public override void Write(Utf8JsonWriter writer, Domain.ValueObjects.Money value, JsonSerializerOptions options)
    {
        writer.WriteStartObject(); writer.WriteNumber("amount", value.Amount);
        writer.WriteString("currency", value.Currency.Value); writer.WriteEndObject();
    }
}
