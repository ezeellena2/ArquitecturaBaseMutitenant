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
    Guid ConversationId = default, IReadOnlyList<AssistantMessageResponse>? ContextMessages = null, DateTime DeadlineAtUtc = default,
    AssistantRequestPlace? Requester = null);
/// <summary>La cultura y la zona del perfil de quien pidió el turno, con las que va la hora del pedido (0064 §9).</summary>
public sealed record AssistantRequestPlace(string Culture, string TimeZoneId);
public sealed record AssistantToolExecution(string Data, IReadOnlyList<Guid> SubjectUserIds);
public sealed record AssistantChatBinding(AssistantMode Mode, Guid ModelId, string ProviderModelId, AssistantEffort Effort);
/// <summary><c>Readable</c>: false si su rol o su estado los escribió una versión más nueva; la conversación no se arma (0064 §6).</summary>
public sealed record AssistantHistoryMessageRow(Guid Id, string Role, bool PrivacySubjectsKnown, Guid[] SubjectUserIds, bool Readable);
/// <summary><c>KnownEngine</c>: false si lo selló un motor más nuevo; solo llega vencido, para cerrarlo y devolver el cupo (0064 §6).</summary>
public sealed record AssistantTurnDiscovery(Guid TurnId, ExecutionActor Actor, bool KnownEngine);
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
