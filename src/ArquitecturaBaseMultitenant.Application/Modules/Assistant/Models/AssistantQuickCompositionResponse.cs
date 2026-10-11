using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

// Local composition only. Conversation admission, binding and replay remain T04b/T05.
public sealed record AssistantQuickCompositionResponse(string Title, AssistantQuickResultResponse Result)
{
    [JsonIgnore]
    public IReadOnlyList<AssistantSnapshotSubjectCopy> SubjectCopies { get; init; } = [];
}
