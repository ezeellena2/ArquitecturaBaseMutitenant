using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Roles;

/// <summary>
/// Las acciones de Roles (ADR 0068): crear, cambiar y dar de baja un rol, con el requisito de su ruta. Las declaraciones son
/// estáticas; resolver y validar responden «no disponible» hasta que se programen (B7).
/// </summary>
internal sealed class RoleActionSource : IAssistantActionSource
{
    private static readonly AssistantActionField NameField = new()
    { Name = "name", Type = AssistantActionFieldType.Text, Required = true, MaxLength = TextLimits.ShortName };

    private static readonly AssistantActionField DescriptionField = new()
    { Name = "description", Type = AssistantActionFieldType.LongText, MaxLength = TextLimits.Description };

    private static readonly AssistantActionTarget Role = new("GET api/roles/{id}");

    public static IReadOnlyList<AssistantActionDefinition> Actions { get; } =
    [
        new()
        {
            Id = "roles.create",
            Version = 1,
            Kind = AssistantActionKind.Create,
            Requirement = AssistantActionRequirement.Organization(Permissions.Roles.Manage),
            SaveRoute = "POST api/roles",
            Confirmation = AssistantActionConfirmation.Page,
            Screen = AssistantActionScreen.RoleEditor,
            Fields =
            [
                NameField,
                DescriptionField,
                new() { Name = "scope", Type = AssistantActionFieldType.Choice, Required = true },
                new() { Name = "company", Type = AssistantActionFieldType.Record, DependsOn = "scope" },
                new() { Name = "permissions", Type = AssistantActionFieldType.Choices, Required = true, DependsOn = "scope" },
            ],
        },
        new()
        {
            Id = "roles.edit",
            Version = 1,
            Kind = AssistantActionKind.Edit,
            Requirement = AssistantActionRequirement.Organization(Permissions.Roles.Manage),
            SaveRoute = "PUT api/roles/{id}",
            Confirmation = AssistantActionConfirmation.Page,
            Screen = AssistantActionScreen.RoleEditor,
            Target = Role,
            Fields = [NameField, DescriptionField, new() { Name = "permissions", Type = AssistantActionFieldType.Choices }],
        },
        new()
        {
            Id = "roles.remove",
            Version = 1,
            Kind = AssistantActionKind.Remove,
            Requirement = AssistantActionRequirement.Organization(Permissions.Roles.Manage),
            SaveRoute = "DELETE api/roles/{id}",
            Confirmation = AssistantActionConfirmation.RemoveConfirmation,
            Screen = AssistantActionScreen.Roles,
            Target = Role,
        },
    ];

    public IReadOnlyList<AssistantActionDefinition> Definitions => Actions;

    public Task<Result<PagedResult<AssistantActionChoiceRow>>> ListChoicesAsync(AssistantActionChoiceRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure<PagedResult<AssistantActionChoiceRow>>(ContractErrors.NotImplemented));

    public Task<Result<AssistantActionResolution>> ResolveAsync(AssistantActionDraft draft, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure<AssistantActionResolution>(ContractErrors.NotImplemented));

    // Sus campos son fijos: ninguna de sus acciones arma los campos desde su destino.
    public Task<Result<IReadOnlyList<AssistantActionField>>> ReadTargetFieldsAsync(AssistantActionDefinition action, string target, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure<IReadOnlyList<AssistantActionField>>(ContractErrors.NotImplemented));

    public Task<Result> ValidateAsync(AssistantActionResolution resolution, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(ContractErrors.NotImplemented));
}
