using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

// Internal provider projections. These do not extend the frozen HTTP Quick variants.
public abstract record AssistantToolData;
public sealed record AssistantQuickToolData(AssistantQuickDataResponse Data) : AssistantToolData
{
    [JsonIgnore]
    public IReadOnlyList<AssistantSnapshotSubjectCopy> SubjectCopies { get; init; } = [];
}
public sealed record AssistantRolesToolData(IReadOnlyList<AssistantRoleToolRow> Roles, int TotalCount, bool HasMore) : AssistantToolData;
public sealed record AssistantRoleToolRow(Guid Id, string Name, string Scope, Guid? CompanyId, IReadOnlyList<string> Permissions);
public sealed record AssistantCompaniesToolData(IReadOnlyList<AssistantCompanyToolRow> Companies, int TotalCount, bool HasMore) : AssistantToolData;
public sealed record AssistantCompanyToolRow(Guid Id, string Name, string Status, int MemberCount);
public sealed record AssistantPendingToolData(IReadOnlyList<AssistantPendingToolRow> Pending, bool HasMore) : AssistantToolData
{
    [JsonIgnore]
    public IReadOnlyList<Guid> SubjectUserIds { get; init; } = [];
    [JsonIgnore]
    public bool PrivacySubjectsKnown { get; init; }
}
public sealed record AssistantPendingToolRow(string Area, string Kind, string? Subject, bool Urgent);
public sealed record AssistantHelpToolData(IReadOnlyList<AssistantHelpArticleResponse> Articles) : AssistantToolData;

/// <summary>
/// Lo que devuelven <c>assistant_actions</c>, <c>read_action</c> y <c>propose_action</c> (ADR 0068 §7): tipos de acción, campos
/// y nombres de definiciones, nunca registros, personas ni lo resuelto. Un rechazo vuelve como dato y no corta el turno.
/// </summary>
public sealed record AssistantActionToolData : AssistantToolData
{
    public const string Accepted = "accepted";
    public const string Rejected = "rejected";

    /// <summary>Las acciones: la lista precargada o las de un área.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<AssistantActionToolRow>? Actions { get; init; }

    /// <summary>Las áreas, en lugar de las acciones, cuando son más de las que entran en la lista precargada.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Areas { get; init; }

    /// <summary>Una acción con sus campos, su tipo y sus límites.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AssistantActionToolAction? Action { get; init; }

    /// <summary>Los nombres de las definiciones de un <c>Choice</c> o un <c>Choices</c>, de a una página.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Choices { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? HasMore { get; init; }

    /// <summary><c>accepted</c> o <c>rejected</c>, solo en <c>propose_action</c> o en un pedido que no se pudo leer.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Outcome { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ProposalId { get; init; }

    /// <summary>El motivo de un rechazo: <c>Assistant.Action.NotAvailable</c>, <c>.Invalid</c> u <c>.OnePerTurn</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; init; }

    /// <summary>La ruta de cada campo rechazado, sin listar registros, para que el modelo lo corrija en el mismo turno.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<AssistantActionToolError>? Errors { get; init; }

    /// <summary>Lo aceptado, ya resuelto: lo guarda la propuesta y nunca va al proveedor.</summary>
    [JsonIgnore]
    public ArquitecturaBaseMultitenant.Application.Models.AssistantActions.AssistantActionResolution? Resolution { get; init; }

    public static AssistantActionToolData Reject(string code, IReadOnlyList<AssistantActionToolError>? errors = null) =>
        new() { Outcome = Rejected, Code = code, Errors = errors };
}
public sealed record AssistantActionToolRow(string Action, string Name);
public sealed record AssistantActionToolAction(string Action, string Name, string Description, string Kind, bool HasTarget,
    IReadOnlyList<AssistantActionToolField> Fields);
public sealed record AssistantActionToolField(string Name, string Label,
    ArquitecturaBaseMultitenant.Application.Models.AssistantActions.AssistantActionFieldType Type, bool Required)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? MaxLength { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? MaxItems { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public decimal? Minimum { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public decimal? Maximum { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? DependsOn { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ItemKey { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public IReadOnlyList<AssistantActionToolField>? ItemFields { get; init; }
}
public sealed record AssistantActionToolError(string Path, string Code);
