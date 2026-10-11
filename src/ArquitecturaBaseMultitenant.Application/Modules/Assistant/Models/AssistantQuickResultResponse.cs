using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantQuickResultResponse(string Text, IReadOnlyList<AssistantLinkResponse> Links, AssistantQuickDataResponse Data)
{
    [JsonIgnore]
    public IReadOnlyList<AssistantSnapshotSubjectCopy> SubjectCopies { get; init; } = [];
}
