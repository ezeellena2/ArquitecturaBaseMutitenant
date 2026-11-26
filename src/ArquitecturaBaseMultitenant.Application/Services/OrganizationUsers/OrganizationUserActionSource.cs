using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Application.Services.Companies;
using ArquitecturaBaseMultitenant.Application.Services.Roles;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>
/// Las acciones de Usuarios (ADR 0068): invitar, cambiar roles y quitar de la organización, con el requisito de su ruta.
/// Solo lee, con los readers del área, lo que tiene quien pide (<see cref="DelegationPermissionsResolver"/>) y la misma
/// <see cref="OrganizationDelegationPolicy"/> que usa el <c>Guard</c> de la ruta (0061): un rol se resuelve solo si quien pide
/// lo puede dar donde se da (en la organización, o en esa empresa), y una persona, solo si quien pide tiene todo lo que ella
/// tiene. Qué roles da, quita o vuelve a dar un cambio lo decide <see cref="OrganizationUserRoleChangePolicy"/>, la misma
/// regla que la ruta. Valida armando el mismo pedido que la pantalla. Lo que la ruta decide adentro (el último Dueño, una
/// cuenta que ya es miembro) lo sigue diciendo la ruta al guardar.
/// <list type="bullet">
/// <item>Un <c>Choices</c> se escribe como una lista: cada elemento es un nombre (sumar) o <c>{op: "add" | "remove", value}</c>;
/// al crear solo se suma. Se resuelve como <c>[{op, id, doubt?}]</c>.</item>
/// <item>Un nombre igual resuelve; uno parecido también, con «Revisá» si hay más de uno parecido o si no hay uno igual. Sin
/// coincidencia, vuelve al modelo con la ruta del elemento. Sumar busca con el lector (<see cref="RoleActionResolver"/>);
/// quitar elige entre los que la persona tiene ahí.</item>
/// <item>La empresa y los roles se resuelven y se nombran solo con lo que piden sus listas en el diálogo de siempre
/// (<c>companies.read</c> y <c>roles.read</c>), que <c>users.invite</c> y <c>users.manage</c> no traen: sin eso, como si no
/// existieran.</item>
/// <item>El destino es una persona de la organización por su nombre o su correo; con varias parecidas vuelve al modelo con
/// <see cref="MemberErrors.AmbiguousCode"/>.</item>
/// </list>
/// Elegir por nombre, leer un elemento y aplicar las operaciones es de <see cref="AssistantActionInputs"/>, igual para todas las
/// fuentes.
/// </summary>
internal sealed class OrganizationUserActionSource(
    IOrganizationUserReader users,
    CompanyActionResolver companies,
    RoleActionResolver roles,
    DelegationPermissionsResolver delegation,
    IValidator<InviteOrganizationUserRequest> inviteValidator,
    IValidator<UpdateOrganizationUserRequest> updateValidator,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IAssistantActionSource
{
    internal const string EmailField = "email";
    internal const string RolesField = "roles";
    internal const string CompanyField = "company";
    internal const string CompanyRolesField = "companyRoles";
    private const string TargetPath = "target";

    private static readonly AssistantActionField[] RoleFields =
    [
        new() { Name = RolesField, Type = AssistantActionFieldType.Choices },
        new() { Name = CompanyField, Type = AssistantActionFieldType.Record },
        new() { Name = CompanyRolesField, Type = AssistantActionFieldType.Choices, DependsOn = CompanyField },
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
            Fields = [new() { Name = EmailField, Type = AssistantActionFieldType.Email, Required = true, MaxLength = Email.MaxLength }, .. RoleFields],
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

    /// <summary>
    /// Los roles que quien pide puede dar: los de la organización, o los que valen en la empresa del campo del que depende. La
    /// página es la del lector, con su búsqueda; de ella quedan los que puede dar ahí. Sin <c>roles.read</c>, ninguno.
    /// </summary>
    public async Task<Result<PagedResult<AssistantActionChoiceRow>>> ListChoicesAsync(AssistantActionChoiceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Field is not (RolesField or CompanyRolesField)) return Invalid(request.Field, ValidationError.ErrorCode);
        var held = await delegation.ReadHeldAsync(cancellationToken);
        var page = Math.Max(PagedRequest.DefaultPage, request.Page);
        if (!RoleActionResolver.CanRead(held)) return new PagedResult<AssistantActionChoiceRow>([], page, AssistantActionLimits.ChoicesPerPage, 0);
        Guid? companyId = null;
        if (request.Field == CompanyRolesField && !string.IsNullOrWhiteSpace(request.DependsOnValue))
        {
            var company = await companies.MatchAsync(held, request.DependsOnValue, cancellationToken);
            if (company.IsFailure) return company.Error;
            companyId = company.Value.Company.Id;
        }
        var listed = await roles.PageAsync(Place(request.Field == RolesField, companyId) with
        {
            Search = string.IsNullOrWhiteSpace(request.Search) ? null : AssistantActionInputs.SearchOf(request.Search),
            Page = page,
            PageSize = AssistantActionLimits.ChoicesPerPage,
        }, cancellationToken);
        var rows = listed.Items
            .Where(role => request.Field == RolesField || companyId is not null ? FitsPlace(role, companyId) && CanGrant(held, role, companyId)
                : OrganizationDelegationPolicy.CanGrantListedRole(held, role.Scope, role.CompanyId, role.Permissions, companyId: null))
            .Select(role => new AssistantActionChoiceRow(role.Id.ToString("D"), RoleTexts.Name(role.Name)))
            .ToArray();
        return new PagedResult<AssistantActionChoiceRow>(rows, listed.Page, listed.PageSize, listed.TotalCount);
    }

    public async Task<Result<AssistantActionResolution>> ResolveAsync(AssistantActionDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var action = draft.Action;
        var held = await delegation.ReadHeldAsync(cancellationToken);
        OrganizationUserRow? target = null;
        if (action.Target is not null)
        {
            var found = await MatchTargetAsync(draft.Target, cancellationToken);
            if (found.IsFailure) return Invalid(TargetPath, found.Error.Code);
            target = found.Value;
            if (!await CanActOnAsync(held, action, target, cancellationToken)) return Invalid(TargetPath, MemberErrors.HasMorePermissionsCode);
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var values = new List<AssistantActionResolvedValue>();
        var inputs = draft.Values.ToDictionary(value => value.Field, StringComparer.Ordinal);
        if (inputs.TryGetValue(EmailField, out var email))
        {
            var created = Email.Create(email.Value.ValueKind == JsonValueKind.String ? email.Value.GetString() : null);
            if (created.IsFailure) errors[EmailField] = [EmailErrors.InvalidCode];
            else values.Add(new(EmailField, email.Value, JsonSerializer.SerializeToElement(created.Value.Value), null));
        }

        Guid? companyId = null;
        if (inputs.TryGetValue(CompanyField, out var companyInput))
        {
            var company = companyInput.Value.ValueKind == JsonValueKind.String
                ? await companies.MatchAsync(held, companyInput.Value.GetString(), cancellationToken)
                : CompanyErrors.NotFound;
            if (company.IsFailure) errors[CompanyField] = [company.Error.Code];
            else
            {
                companyId = company.Value.Company.Id;
                values.Add(new(CompanyField, companyInput.Value, JsonSerializer.SerializeToElement(companyId.Value.ToString("D")), company.Value.Doubt));
            }
        }

        var context = new RoleInputContext(held, action.Kind, values, errors);
        if (inputs.TryGetValue(RolesField, out var organizationRoles))
            await ResolveRolesAsync(organizationRoles, null, target?.Roles, context, cancellationToken);
        if (inputs.TryGetValue(CompanyRolesField, out var companyRoles))
        {
            if (!inputs.ContainsKey(CompanyField)) errors[CompanyField] = [CompanyErrors.NotFoundCode];
            else if (companyId is { } id)
                await ResolveRolesAsync(companyRoles, id, target?.Companies.Where(row => row.CompanyId == id).SelectMany(row => row.Roles),
                    context, cancellationToken);
        }

        if (errors.Count > 0) return new ValidationError(errors);
        return new AssistantActionResolution(action, target?.MemberId, target?.Version, values)
        {
            SubjectUserIds = target?.UserId is { } userId ? [userId] : [],
        };
    }

    // Sus campos son fijos: ninguna de sus acciones arma los campos desde su destino.
    public Task<Result<IReadOnlyList<AssistantActionField>>> ReadTargetFieldsAsync(AssistantActionDefinition action, string target, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure<IReadOnlyList<AssistantActionField>>(ContractErrors.NotImplemented));

    /// <summary>
    /// Vuelve a leer por id la persona, la empresa y cada rol, con lo que quien pide tiene ahora: lo que ya no está, o lo que
    /// ya no puede ver, es <c>NotFound</c>, y lo que ya no puede dar, o una persona sobre la que ya no puede actuar,
    /// <c>Forbidden</c>.
    /// </summary>
    public async Task<Result<AssistantActionReview>> ReviewAsync(AssistantActionResolution resolution, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        var action = resolution.Action;
        var held = await delegation.ReadHeldAsync(cancellationToken);
        AssistantActionTargetView? target = null;
        if (action.Target is not null)
        {
            if (resolution.TargetId is not { } memberId) return MemberErrors.NotFound;
            var row = await users.FindByIdAsync(memberId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
            if (row is null) return MemberErrors.NotFound;
            if (!await CanActOnAsync(held, action, row, cancellationToken)) return MemberErrors.HasMorePermissions;
            target = new(row.MemberId, Label(row), row.Version, Detail(row));
        }

        var views = new List<AssistantActionValueView>();
        Guid? companyId = null;
        foreach (var value in resolution.Values)
        {
            switch (value.Field)
            {
                case EmailField:
                    views.Add(new(EmailField, value.Resolved ?? value.Written, null, value.Doubt));
                    break;
                case CompanyField:
                    if (await companies.FindActiveAsync(held, AssistantActionInputs.IdOf(value.Resolved), cancellationToken) is not { } company)
                        return CompanyErrors.NotFound;
                    companyId = company.Id;
                    views.Add(new(CompanyField, JsonSerializer.SerializeToElement(company.Id.ToString("D")), company.Name, value.Doubt));
                    break;
            }
        }
        var roleValues = resolution.Values.Where(value => value.Field is RolesField or CompanyRolesField)
            .Select(value => (value.Field, Ops: AssistantActionInputs.OpsOf<Guid>(value.Resolved))).ToArray();
        if (roleValues.Any(value => value.Field == CompanyRolesField) && companyId is null) return CompanyErrors.NotFound;
        if (roleValues.All(value => value.Ops.Length == 0)) return new AssistantActionReview(target, views);
        if (!RoleActionResolver.CanRead(held)) return RoleErrors.NotFound;
        var known = await roles.ListByIdsAsync(roleValues.SelectMany(value => value.Ops).Select(op => op.Id), cancellationToken);
        foreach (var value in roleValues)
        {
            var place = value.Field == RolesField ? null : companyId;
            foreach (var element in value.Ops)
            {
                if (!known.TryGetValue(element.Id, out var role) || !FitsPlace(role, place)) return RoleErrors.NotFound;
                if (!CanGrant(held, role, place)) return MemberErrors.RolesNotHeld;
                views.Add(new(value.Field, JsonSerializer.SerializeToElement(new { op = element.Op, id = role.Id.ToString("D") }),
                    RoleTexts.Name(role.Name), element.Doubt));
            }
        }
        return new AssistantActionReview(target, views);
    }

    /// <summary>
    /// Arma el pedido de la pantalla con lo resuelto (al editar, las operaciones sobre lo que la persona tiene ahora), corre su
    /// validador y la <see cref="OrganizationDelegationPolicy"/> con lo que la ruta da, quita y vuelve a dar, sin guardar.
    /// </summary>
    public async Task<Result> ValidateAsync(AssistantActionResolution resolution, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        var action = resolution.Action;
        var held = await delegation.ReadHeldAsync(cancellationToken);
        OrganizationUserRow? target = null;
        if (action.Target is not null)
        {
            target = resolution.TargetId is { } memberId
                ? await users.FindByIdAsync(memberId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken)
                : null;
            if (target is null) return Invalid(TargetPath, MemberErrors.NotFoundCode);
            if (!await CanActOnAsync(held, action, target, cancellationToken)) return Invalid(TargetPath, MemberErrors.HasMorePermissionsCode);
        }
        if (action.Kind == AssistantActionKind.Remove) return Result.Success();

        var companyId = AssistantActionInputs.IdOf(AssistantActionInputs.ResolvedOf(resolution, CompanyField));
        if (companyId is not null && await companies.FindActiveAsync(held, companyId, cancellationToken) is null)
            return Invalid(CompanyField, CompanyErrors.NotFoundCode);
        var companyOps = AssistantActionInputs.OpsOf<Guid>(resolution, CompanyRolesField);
        var desired = Desired(target, AssistantActionInputs.OpsOf<Guid>(resolution, RolesField), companyId, companyOps);

        var validation = target is null
            ? await inviteValidator.ValidateAsync(new InviteOrganizationUserRequest(EmailOf(resolution), [.. desired.Organization], desired.Companies), cancellationToken)
            : await updateValidator.ValidateAsync(new UpdateOrganizationUserRequest(target.MemberId, [.. desired.Organization], desired.Companies, target.Version), cancellationToken);
        if (!validation.IsValid)
            return new ValidationError(validation.Errors.GroupBy(failure => FieldOf(failure.PropertyName), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, _ => new[] { ValidationError.ErrorCode }, StringComparer.Ordinal));

        // La regla del Guard de la ruta (0061): cada rol que se da, se quita o vuelve, lo tiene quien pide donde se da.
        var changes = OrganizationUserRoleChangePolicy.Decide(Assignments(target).ToArray(), desired.Keys, key => key,
            target?.Companies.Where(row => row.Status != CompanyMembershipStatus.Active).Select(row => row.CompanyId).ToHashSet() ?? [],
            desired.Companies.Select(row => row.CompanyId).ToArray(), target is null || IsInvited(target));
        (Guid RoleId, Guid? CompanyId)[] changed = [.. changes.Granted, .. changes.Removed, .. changes.Revived];
        var permissions = await roles.ListPermissionsAsync(changed.Select(row => row.RoleId), cancellationToken);
        return OrganizationDelegationPolicy.CanChangeRoles(held,
                changed.Select(row => new RoleGrant(permissions.GetValueOrDefault(row.RoleId) ?? [], row.CompanyId)))
            ? Result.Success()
            : Invalid(companyOps.Length > 0 ? CompanyRolesField : RolesField, MemberErrors.RolesNotHeldCode);
    }

    // Lo que pide la pantalla: sus roles de organización y, por cada empresa activa donde está, los de esa empresa, con las
    // operaciones aplicadas a la de la propuesta.
    private static DesiredRoles Desired(OrganizationUserRow? target, IEnumerable<AssistantActionOp<Guid>> organizationOps, Guid? companyId,
        IEnumerable<AssistantActionOp<Guid>> companyOps)
    {
        var organization = AssistantActionInputs.Apply(target?.Roles.Select(role => role.Id) ?? [], organizationOps);
        var current = target?.Companies
            .Where(row => row.Status == CompanyMembershipStatus.Active && row.CompanyStatus is null or CompanyStatus.Active)
            .ToDictionary(row => row.CompanyId, row => row.Roles.Select(role => role.Id).ToHashSet()) ?? [];
        if (companyId is { } edited) current[edited] = AssistantActionInputs.Apply(current.GetValueOrDefault(edited) ?? [], companyOps);
        InviteCompanyRolesRequest[] byCompany = [.. current.Select(pair => new InviteCompanyRolesRequest(pair.Key, [.. pair.Value]))];
        (Guid RoleId, Guid? CompanyId)[] keys = [.. organization.Select(roleId => (roleId, (Guid?)null)),
            .. current.SelectMany(pair => pair.Value.Select(roleId => (roleId, (Guid?)pair.Key)))];
        return new(organization, byCompany, keys);
    }

    // Sumar busca entre los roles que valen en ese lugar; quitar, solo entre los que la persona tiene ahí. Cada uno, si quien
    // pide lo puede dar ahí. Sin ver los roles, ninguno se resuelve.
    private async Task ResolveRolesAsync(AssistantActionInput input, Guid? companyId, IEnumerable<OrganizationUserRoleRow>? current,
        RoleInputContext context, CancellationToken cancellationToken)
    {
        if (input.Value.ValueKind != JsonValueKind.Array)
        {
            context.Errors[input.Field] = [ValidationError.ErrorCode];
            return;
        }
        if (!RoleActionResolver.CanRead(context.Held))
        {
            context.Errors[input.Field] = [RoleErrors.NotFoundCode];
            return;
        }
        var assigned = (await roles.ListByIdsAsync(current?.Select(role => role.Id) ?? [], cancellationToken)).Values
            .Where(role => FitsPlace(role, companyId)).ToArray();
        var resolved = new List<AssistantActionOp<Guid>>();
        var index = 0;
        foreach (var element in input.Value.EnumerateArray())
        {
            var path = $"{input.Field}[{index++}]";
            if (!AssistantActionInputs.TryReadOp(element, out var op, out var text)
                || (op == AssistantActionInputs.Remove && context.Kind != AssistantActionKind.Edit))
            {
                context.Errors[path] = [ValidationError.ErrorCode];
                continue;
            }
            var candidates = op == AssistantActionInputs.Remove ? assigned
                : (await roles.SearchAsync(text, Place(companyId is null, companyId), cancellationToken)).Where(role => FitsPlace(role, companyId)).ToArray();
            if (AssistantActionInputs.Match(candidates, RoleActionResolver.Names, text) is not { } match)
                context.Errors[path] = [RoleErrors.NotFoundCode];
            else if (!CanGrant(context.Held, match.Item, companyId))
                context.Errors[path] = [MemberErrors.RolesNotHeldCode];
            else resolved.Add(new(op, match.Item.Id, match.Doubt));
        }
        var doubt = resolved.Any(item => item.Doubt is not null) ? AssistantActionDoubts.Similar : null;
        context.Values.Add(new(input.Field, input.Value, JsonSerializer.SerializeToElement(resolved, AssistantActionInputs.Json), doubt));
    }

    private async Task<Result<OrganizationUserRow>> MatchTargetAsync(string? text, CancellationToken cancellationToken)
    {
        if (!AssistantActionInputs.IsWritten(text)) return MemberErrors.NotFound;
        var trimmed = text.Trim();
        var page = await users.ListAsync(new ListOrganizationUsersRequest
        {
            Search = AssistantActionInputs.SearchOf(trimmed),
            PageSize = AssistantActionLimits.Candidates,
        }, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        var exact = page.Items.Where(row => AssistantActionInputs.Same(row.DisplayName, trimmed)
            || (row.Email is { } email && string.Equals(email.Value, trimmed, StringComparison.OrdinalIgnoreCase))).ToArray();
        var candidates = exact.Length > 0 ? exact : page.Items.Where(row => AssistantActionInputs.Contains(row.DisplayName, trimmed)).ToArray();
        return candidates.Length switch
        {
            1 => candidates[0],
            0 => MemberErrors.NotFound,
            // Con varias personas parecidas no se elige una: el modelo pregunta a quién.
            _ => MemberErrors.Ambiguous,
        };
    }

    // Actuar sobre alguien (0061): tener todo lo que le dan sus roles, en cada lugar. Sobre uno mismo, siempre; quitar a un
    // invitado es revocar su invitación, que no pide nada. Se leen solo los permisos de sus roles.
    private async Task<bool> CanActOnAsync(PermissionsByPlace held, AssistantActionDefinition action, OrganizationUserRow target,
        CancellationToken cancellationToken)
    {
        var self = target.UserId is { } userId && userId == currentUser.UserId;
        if (self || (action.Kind == AssistantActionKind.Remove && IsInvited(target))) return true;
        var permissions = await roles.ListPermissionsAsync(Assignments(target).Select(row => row.RoleId), cancellationToken);
        var organization = target.Roles.SelectMany(role => PermissionsOf(role.Id));
        var byCompany = target.Companies.ToDictionary(company => company.CompanyId,
            company => company.Roles.SelectMany(role => PermissionsOf(role.Id)));
        return OrganizationDelegationPolicy.CanActOn(self, held, PermissionsByPlace.Of(organization, byCompany));

        IEnumerable<string> PermissionsOf(Guid roleId) => permissions.GetValueOrDefault(roleId) ?? [];
    }

    // Los roles de un lugar, como los filtra la lista de la pantalla: los de la organización, o los que valen en una empresa.
    private static ListRolesRequest Place(bool organization, Guid? companyId) => organization
        ? new ListRolesRequest { Scope = RoleScopeFilter.Organization }
        : new ListRolesRequest { Scope = RoleScopeFilter.Company, CompanyId = companyId };

    // Dónde vale un rol: sin empresa, en la organización; con una, en esa (RoleAssignment.Check, la regla de la ruta).
    private static bool FitsPlace(RoleRow role, Guid? companyId) => RoleAssignment.Check(role.Scope, role.CompanyId, companyId) is null;

    private static bool CanGrant(PermissionsByPlace held, RoleRow role, Guid? companyId) =>
        OrganizationDelegationPolicy.CanGrantRole(held, role.Permissions, role.Scope == RoleScope.Organization ? null : companyId ?? role.CompanyId);

    private static Email? EmailOf(AssistantActionResolution resolution) =>
        AssistantActionInputs.ResolvedOf(resolution, EmailField) is { ValueKind: JsonValueKind.String } email
        && Email.Create(email.GetString()) is { IsSuccess: true } created ? created.Value : null;

    private static IEnumerable<(Guid RoleId, Guid? CompanyId)> Assignments(OrganizationUserRow? row) => row is null ? []
        : row.Roles.Select(role => (role.Id, (Guid?)null))
            .Concat(row.Companies.SelectMany(company => company.Roles.Select(role => (role.Id, (Guid?)company.CompanyId))));

    // El pedido nombra los campos del HTTP; la propuesta, los de la pantalla.
    private static string FieldOf(string property) => property switch
    {
        nameof(InviteOrganizationUserRequest.Email) => EmailField,
        nameof(InviteOrganizationUserRequest.RoleIds) => RolesField,
        _ when property.StartsWith(nameof(InviteOrganizationUserRequest.RoleIds), StringComparison.Ordinal) => RolesField,
        _ when property.StartsWith(nameof(InviteOrganizationUserRequest.Companies), StringComparison.Ordinal) => CompanyRolesField,
        _ => TargetPath,
    };

    // Un invitado, con la invitación vigente o vencida: lo que tiene guardado no vale hasta que acepte.
    private static bool IsInvited(OrganizationUserRow row) => row.State is OrganizationUserState.Invited or OrganizationUserState.Expired;

    private static string Label(OrganizationUserRow row) =>
        row.DisplayName.Length > 0 ? row.DisplayName : row.Email?.DisplayValue ?? row.Phone?.Value ?? string.Empty;

    // Con nombre, el correo la distingue de otra con el mismo nombre; sin nombre, el correo ya es su nombre.
    private static string? Detail(OrganizationUserRow row) => row.DisplayName.Length > 0 ? row.Email?.DisplayValue : null;

    private static ValidationError Invalid(string path, string code) => AssistantActionInputs.Invalid(path, code);

    /// <summary>Lo que lee una vez la resolución para cada <c>Choices</c>: lo que tiene quien pide, la acción y dónde anota.</summary>
    private sealed record RoleInputContext(PermissionsByPlace Held, AssistantActionKind Kind, List<AssistantActionResolvedValue> Values,
        Dictionary<string, string[]> Errors);

    /// <summary>Lo que pide la pantalla: los roles de organización, los de cada empresa y cada par rol y empresa.</summary>
    private sealed record DesiredRoles(HashSet<Guid> Organization, InviteCompanyRolesRequest[] Companies,
        (Guid RoleId, Guid? CompanyId)[] Keys);
}
