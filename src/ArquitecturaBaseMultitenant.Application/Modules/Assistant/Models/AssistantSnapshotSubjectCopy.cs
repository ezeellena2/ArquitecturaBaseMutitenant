namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

// Internal correspondence to a typed personal field, never a wire or snapshot payload.
public enum AssistantSnapshotCopyKind
{
    PeopleDisplayName,
    InvitationDisplayName,
    RecoveryDisplayName,
    AuditActorDisplayName,
    SecurityActorDisplayName,
}

public sealed record AssistantSnapshotSubjectCopy(Guid AccountId, AssistantSnapshotCopyKind CopyKind, Guid RowId);
