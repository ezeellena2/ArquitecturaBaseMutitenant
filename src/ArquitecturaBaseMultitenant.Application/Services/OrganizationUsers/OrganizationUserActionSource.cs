using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>
/// Las acciones de Usuarios (ADR 0068): invitar, cambiar roles y quitar de la organización, con el requisito de su ruta.
/// Las declaraciones son estáticas; resolver y validar responden «no disponible» hasta que se programen (B6).
/// </summary>
internal sealed class OrganizationUserActionSource : IAssistantActionSource
{
    private static readonly AssistantActionField[] RoleFields =
    [
        new() { Name = "roles", Type = AssistantActionFieldType.Choices },
        new() { Name = "company", Type = AssistantActionFieldType.Record },
        new() { Name = "companyRoles", Type = AssistantActionFieldType.Choices, DependsOn = "company" },
    ];

    private static readonly AssistantActionTarget Member = new("GET api/users/{memberId}");

    public static IReadOnlyList<AssistantActionDefinition> Actions { get; } =
    [
        new()
        {
            Id = "users.invite",
            Version = 1,
            Kind = AssistantActionKind.Create,
            Requirement = AssistantActionRequirement.Organization(Permissions.Users.Invite),
            SaveRoute = "POST api/users/invitations",
            Confirmation = AssistantActionConfirmation.Dialog,
            Screen = AssistantActionScreen.InviteUser,
            Effect = AssistantActionEffect.SendsMessage,
            Fields = [new() { Name = "email", Type = AssistantActionFieldType.Email, Required = true, MaxLength = Email.MaxLength }, .. RoleFields],
        },
        new()
        {
            Id = "users.edit",
            Version = 1,
            Kind = AssistantActionKind.Edit,
            Requirement = AssistantActionRequirement.Organization(Permissions.Users.Manage),
            SaveRoute = "PUT api/users/{memberId}",
            Confirmation = AssistantActionConfirmation.Page,
            Screen = AssistantActionScreen.UserDetail,
            Target = Member,
            Fields = RoleFields,
        },
        new()
        {
            Id = "users.remove",
            Version = 1,
            Kind = AssistantActionKind.Remove,
            Requirement = AssistantActionRequirement.Organization(Permissions.Users.Manage),
            SaveRoute = "DELETE api/users/{memberId}",
            Confirmation = AssistantActionConfirmation.RemoveConfirmation,
            Screen = AssistantActionScreen.UserDetail,
            // Quitar le avisa a la persona que deja la organización (OrganizationMembershipEndNotifier).
            Effect = AssistantActionEffect.SendsMessage,
            Target = Member,
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
