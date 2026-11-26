using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Resources;
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
/// tiene. Valida armando el mismo pedido que la pantalla. Lo que la ruta decide adentro (el último Dueño, una cuenta que ya es
/// miembro) lo sigue diciendo la ruta al guardar.
/// <list type="bullet">
/// <item>Un <c>Choices</c> se escribe como una lista: cada elemento es un nombre (sumar) o <c>{op: "add" | "remove", value}</c>;
/// al crear solo se suma. Se resuelve como <c>[{op, id, doubt?}]</c>.</item>
/// <item>Un nombre igual resuelve; uno parecido también, con «Revisá» si hay más de uno parecido o si no hay uno igual. Sin
/// coincidencia, vuelve al modelo con la ruta del elemento.</item>
/// <item>El destino es una persona de la organización por su nombre o su correo; con varias parecidas vuelve al modelo.</item>
/// </list>
/// </summary>
internal sealed class OrganizationUserActionSource(
    IOrganizationUserReader users,
    ICompanyReader companies,
    IRoleReader roles,
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
    private const string Add = "add";
    private const string Remove = "remove";
    private const int ChoicesPageSize = 20;
    private const int TargetCandidates = 20;
    private const int MaxNameLength = 200;

    private static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

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

    /// <summary>Los roles que quien pide puede dar: los de la organización, o los que valen en la empresa del campo del que depende.</summary>
    public async Task<Result<PagedResult<AssistantActionChoiceRow>>> ListChoicesAsync(AssistantActionChoiceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Field is not (RolesField or CompanyRolesField)) return Invalid(request.Field, ValidationError.ErrorCode);
        var context = await ReadContextAsync(cancellationToken);
        Guid? companyId = null;
        if (request.Field == CompanyRolesField && !string.IsNullOrWhiteSpace(request.DependsOnValue))
        {
            var company = await MatchCompanyAsync(request.DependsOnValue, cancellationToken);
            if (company.IsFailure) return company.Error;
            companyId = company.Value.Company.Id;
        }
        var rows = context.Roles.Values
            .Where(role => request.Field == RolesField ? FitsPlace(role, null) && context.CanGrant(role, null)
                : companyId is { } id ? FitsPlace(role, id) && context.CanGrant(role, id)
                : role.Scope != RoleScope.Organization && context.CanGrantInEveryCompany(role))
            .Select(role => new AssistantActionChoiceRow(role.Id.ToString("D"), RoleTexts.Name(role.Name)))
            .Where(row => string.IsNullOrWhiteSpace(request.Search) || Contains(row.Name, request.Search))
            .OrderBy(row => row.Name, StringComparer.CurrentCulture)
            .ToArray();
        var page = Math.Max(PagedRequest.DefaultPage, request.Page);
        return new PagedResult<AssistantActionChoiceRow>(
            [.. rows.Skip((page - 1) * ChoicesPageSize).Take(ChoicesPageSize)], page, ChoicesPageSize, rows.Length);
    }

    public async Task<Result<AssistantActionResolution>> ResolveAsync(AssistantActionDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var action = draft.Action;
        var context = await ReadContextAsync(cancellationToken);
        OrganizationUserRow? target = null;
        if (action.Target is not null)
        {
            var found = await MatchTargetAsync(draft.Target, cancellationToken);
            if (found.IsFailure) return Invalid("target", found.Error.Code);
            target = found.Value;
            if (!context.CanActOn(action, target)) return Invalid("target", MemberErrors.HasMorePermissionsCode);
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
                ? await MatchCompanyAsync(companyInput.Value.GetString()!, cancellationToken)
                : CompanyErrors.NotFound;
            if (company.IsFailure) errors[CompanyField] = [company.Error.Code];
            else
            {
                companyId = company.Value.Company.Id;
                values.Add(new(CompanyField, companyInput.Value, JsonSerializer.SerializeToElement(companyId.Value.ToString("D")), company.Value.Doubt));
            }
        }

        if (inputs.TryGetValue(RolesField, out var organizationRoles))
            ResolveRoles(organizationRoles, null, target?.Roles.Select(role => role.Id), action.Kind, context, values, errors);
        if (inputs.TryGetValue(CompanyRolesField, out var companyRoles))
        {
            if (!inputs.ContainsKey(CompanyField)) errors[CompanyField] = [CompanyErrors.NotFoundCode];
            else if (companyId is { } id)
                ResolveRoles(companyRoles, id, target?.Companies.Where(row => row.CompanyId == id).SelectMany(row => row.Roles).Select(role => role.Id),
                    action.Kind, context, values, errors);
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
    /// Vuelve a leer por id la persona, la empresa y cada rol, con lo que quien pide tiene ahora: lo que ya no está es
    /// <c>NotFound</c>, y lo que ya no puede dar, o una persona sobre la que ya no puede actuar, <c>Forbidden</c>.
    /// </summary>
    public async Task<Result<AssistantActionReview>> ReviewAsync(AssistantActionResolution resolution, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        var action = resolution.Action;
        var context = await ReadContextAsync(cancellationToken);
        AssistantActionTargetView? target = null;
        if (action.Target is not null)
        {
            if (resolution.TargetId is not { } memberId) return MemberErrors.NotFound;
            var row = await users.FindByIdAsync(memberId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
            if (row is null) return MemberErrors.NotFound;
            if (!context.CanActOn(action, row)) return MemberErrors.HasMorePermissions;
            target = new(row.MemberId, Label(row), row.Version);
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
                    if (IdOf(value.Resolved) is not { } id || await companies.FindByIdAsync(id, cancellationToken) is not { Status: CompanyStatus.Active } company)
                        return CompanyErrors.NotFound;
                    companyId = company.Id;
                    views.Add(new(CompanyField, JsonSerializer.SerializeToElement(company.Id.ToString("D")), company.Name, value.Doubt));
                    break;
            }
        }
        foreach (var value in resolution.Values.Where(value => value.Field is RolesField or CompanyRolesField))
        {
            var place = value.Field == RolesField ? null : companyId;
            if (value.Field == CompanyRolesField && place is null) return CompanyErrors.NotFound;
            foreach (var element in Elements(value.Resolved))
            {
                if (!context.Roles.TryGetValue(element.Id, out var role) || !FitsPlace(role, place)) return RoleErrors.NotFound;
                if (!context.CanGrant(role, place)) return MemberErrors.RolesNotHeld;
                views.Add(new(value.Field, JsonSerializer.SerializeToElement(new { op = element.Op, id = role.Id.ToString("D") }),
                    RoleTexts.Name(role.Name), element.Doubt));
            }
        }
        return new AssistantActionReview(target, views);
    }

    /// <summary>
    /// Arma el pedido de la pantalla con lo resuelto (al editar, las operaciones sobre lo que la persona tiene ahora), corre su
    /// validador y la <see cref="OrganizationDelegationPolicy"/> con lo que la ruta da y quita, sin guardar.
    /// </summary>
    public async Task<Result> ValidateAsync(AssistantActionResolution resolution, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        var action = resolution.Action;
        var context = await ReadContextAsync(cancellationToken);
        OrganizationUserRow? target = null;
        if (action.Target is not null)
        {
            target = resolution.TargetId is { } memberId
                ? await users.FindByIdAsync(memberId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken)
                : null;
            if (target is null) return Invalid("target", MemberErrors.NotFoundCode);
            if (!context.CanActOn(action, target)) return Invalid("target", MemberErrors.HasMorePermissionsCode);
        }
        if (action.Kind == AssistantActionKind.Remove) return Result.Success();

        var companyId = resolution.Values.SingleOrDefault(value => value.Field == CompanyField) is { } company ? IdOf(company.Resolved) : null;
        if (companyId is { } chosen && await companies.FindByIdAsync(chosen, cancellationToken) is not { Status: CompanyStatus.Active })
            return Invalid(CompanyField, CompanyErrors.NotFoundCode);
        var organizationOps = OpsOf(resolution, RolesField);
        var companyOps = OpsOf(resolution, CompanyRolesField);

        // Lo que tiene ahora: sus roles de organización y, por cada empresa activa donde está, los de esa empresa.
        var currentOrganization = target?.Roles.Select(role => role.Id).ToHashSet() ?? [];
        var currentCompanies = target?.Companies
            .Where(row => row.Status == CompanyMembershipStatus.Active && row.CompanyStatus is null or CompanyStatus.Active)
            .ToDictionary(row => row.CompanyId, row => row.Roles.Select(role => role.Id).ToHashSet()) ?? [];
        var desiredOrganization = Apply(currentOrganization, organizationOps);
        var desiredCompanies = currentCompanies.ToDictionary(pair => pair.Key, pair => pair.Value);
        if (companyId is { } edited) desiredCompanies[edited] = Apply(currentCompanies.GetValueOrDefault(edited) ?? [], companyOps);
        var companiesRequest = desiredCompanies
            .Select(pair => new InviteCompanyRolesRequest(pair.Key, [.. pair.Value])).ToArray();

        var validation = target is null
            ? await inviteValidator.ValidateAsync(new InviteOrganizationUserRequest(EmailOf(resolution), [.. desiredOrganization], companiesRequest), cancellationToken)
            : await updateValidator.ValidateAsync(new UpdateOrganizationUserRequest(target.MemberId, [.. desiredOrganization], companiesRequest, target.Version), cancellationToken);
        if (!validation.IsValid)
            return new ValidationError(validation.Errors.GroupBy(failure => FieldOf(failure.PropertyName), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, _ => new[] { ValidationError.ErrorCode }, StringComparer.Ordinal));

        // La regla del Guard de la ruta (0061): cada rol que se da, se quita o vuelve, lo tiene quien pide donde se da. A un
        // invitado se le da todo lo pedido; a quien ya entró, lo que suma.
        var desired = desiredOrganization.Select(roleId => (RoleId: roleId, CompanyId: (Guid?)null))
            .Concat(desiredCompanies.SelectMany(pair => pair.Value.Select(roleId => (RoleId: roleId, CompanyId: (Guid?)pair.Key))))
            .ToHashSet();
        var saved = Assignments(target).ToHashSet();
        var inactive = target?.Companies.Where(row => row.Status != CompanyMembershipStatus.Active).Select(row => row.CompanyId).ToHashSet() ?? [];
        var removed = saved.Where(row => !desired.Contains(row) && (row.CompanyId is null || !inactive.Contains(row.CompanyId.Value)));
        var revived = saved.Where(row => !desired.Contains(row) && row.CompanyId is { } id && inactive.Contains(id) && desiredCompanies.ContainsKey(id));
        var granted = target is null || IsInvited(target) ? desired : desired.Where(row => !saved.Contains(row));
        var changed = granted.Concat(removed).Concat(revived)
            .Select(row => new RoleGrant(context.Roles.TryGetValue(row.RoleId, out var role) ? role.Permissions : [], row.CompanyId));
        return OrganizationDelegationPolicy.CanChangeRoles(context.Held, changed)
            ? Result.Success()
            : Invalid(companyOps.Count > 0 ? CompanyRolesField : RolesField, MemberErrors.RolesNotHeldCode);
    }

    private async Task<SourceContext> ReadContextAsync(CancellationToken cancellationToken) => new(
        await delegation.ReadHeldAsync(cancellationToken),
        (await roles.ListAllAsync(cancellationToken)).ToDictionary(role => role.Id),
        currentUser.UserId);

    // Sumar elige entre los roles que valen en ese lugar; quitar, solo entre los que la persona tiene ahí.
    private static void ResolveRoles(AssistantActionInput input, Guid? companyId, IEnumerable<Guid>? current, AssistantActionKind kind,
        SourceContext context, List<AssistantActionResolvedValue> values, Dictionary<string, string[]> errors)
    {
        if (input.Value.ValueKind != JsonValueKind.Array)
        {
            errors[input.Field] = [ValidationError.ErrorCode];
            return;
        }
        var place = context.Roles.Values.Where(role => FitsPlace(role, companyId)).ToArray();
        var held = current?.ToHashSet() ?? [];
        var assigned = place.Where(role => held.Contains(role.Id)).ToArray();
        var resolved = new List<RoleOp>();
        var index = 0;
        foreach (var element in input.Value.EnumerateArray())
        {
            var path = $"{input.Field}[{index++}]";
            if (!TryReadOp(element, out var op, out var text) || (op == Remove && kind != AssistantActionKind.Edit))
            {
                errors[path] = [ValidationError.ErrorCode];
                continue;
            }
            var match = Match(op == Remove ? assigned : place, role => Names(role), text);
            if (match is null)
            {
                errors[path] = [RoleErrors.NotFoundCode];
                continue;
            }
            if (!context.CanGrant(match.Value.Item, companyId))
            {
                errors[path] = [MemberErrors.RolesNotHeldCode];
                continue;
            }
            resolved.Add(new(op, match.Value.Item.Id, match.Value.Doubt));
        }
        var doubt = resolved.Any(item => item.Doubt is not null) ? AssistantActionDoubts.Similar : null;
        values.Add(new(input.Field, input.Value, JsonSerializer.SerializeToElement(resolved, Json), doubt));
    }

    private async Task<Result<(CompanyRow Company, string? Doubt)>> MatchCompanyAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxNameLength) return CompanyErrors.NotFound;
        var page = await companies.ListAsync(new ListCompaniesRequest
        {
            Search = text.Trim()[..Math.Min(text.Trim().Length, PagedRequest.MaxSearchLength)],
            Status = CompanyStatus.Active,
            PageSize = PagedRequest.MaxPageSize,
        }, cancellationToken);
        var match = Match(page.Items.Where(company => company.Status == CompanyStatus.Active).ToArray(), company => [company.Name], text);
        if (match is not { } found) return CompanyErrors.NotFound;
        return (found.Item, found.Doubt);
    }

    private async Task<Result<OrganizationUserRow>> MatchTargetAsync(string? text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxNameLength) return MemberErrors.NotFound;
        var trimmed = text.Trim();
        var page = await users.ListAsync(new ListOrganizationUsersRequest
        {
            Search = trimmed[..Math.Min(trimmed.Length, PagedRequest.MaxSearchLength)],
            PageSize = TargetCandidates,
        }, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        var exact = page.Items.Where(row => Same(row.DisplayName, trimmed)
            || (row.Email is { } email && string.Equals(email.Value, trimmed, StringComparison.OrdinalIgnoreCase))).ToArray();
        var candidates = exact.Length > 0 ? exact : page.Items.Where(row => Contains(row.DisplayName, trimmed)).ToArray();
        return candidates.Length switch
        {
            1 => candidates[0],
            0 => MemberErrors.NotFound,
            // Con varias personas parecidas no se elige una: el modelo pregunta a quién.
            _ => Error.Validation(AssistantActionDoubts.Similar, "More than one person matches."),
        };
    }

    // Igual gana; parecido (lo contiene) también. La duda: varias parecidas, o ninguna igual.
    private static (T Item, string? Doubt)? Match<T>(IReadOnlyList<T> candidates, Func<T, IEnumerable<string>> names, string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0) return null;
        var similar = candidates.Where(item => names(item).Any(name => Contains(name, trimmed)))
            .OrderBy(item => names(item).First(), StringComparer.CurrentCulture).ToArray();
        if (similar.Length == 0) return null;
        var exact = similar.Where(item => names(item).Any(name => Same(name, trimmed))).ToArray();
        var chosen = exact.Length > 0 ? exact[0] : similar[0];
        return (chosen, exact.Length == 1 && similar.Length == 1 ? null : AssistantActionDoubts.Similar);
    }

    // Un rol de sistema se nombra como lo muestra la pantalla, en el idioma de la persona o en el de los recursos.
    private static IEnumerable<string> Names(RoleRow role) =>
        new[] { RoleTexts.Name(role.Name), RoleTexts.NameIn(role.Name, CultureInfo.InvariantCulture), role.Name }.Distinct(StringComparer.Ordinal);

    private static bool Same(string candidate, string text) =>
        CultureInfo.InvariantCulture.CompareInfo.Compare(candidate.Trim(), text, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == 0;

    private static bool Contains(string candidate, string text) =>
        CultureInfo.InvariantCulture.CompareInfo.IndexOf(candidate, text.Trim(), CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;

    // Dónde vale un rol: sin empresa, en la organización; con una, en esa (RoleAssignment.Check, la regla de la ruta).
    private static bool FitsPlace(RoleRow role, Guid? companyId) => RoleAssignment.Check(role.Scope, role.CompanyId, companyId) is null;

    private static bool TryReadOp(JsonElement element, out string op, out string text)
    {
        (op, text) = (Add, string.Empty);
        if (element.ValueKind == JsonValueKind.String) text = element.GetString()!;
        else if (element.ValueKind == JsonValueKind.Object && element.EnumerateObject().Count() == 2
            && element.TryGetProperty("op", out var kind) && kind.ValueKind == JsonValueKind.String && kind.GetString() is Add or Remove
            && element.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String)
            (op, text) = (kind.GetString()!, value.GetString()!);
        else return false;
        return !string.IsNullOrWhiteSpace(text) && text.Length <= MaxNameLength;
    }

    private static RoleOp[] Elements(JsonElement? resolved) =>
        resolved is { ValueKind: JsonValueKind.Array } array ? array.Deserialize<RoleOp[]>(Json) ?? [] : [];

    private static List<RoleOp> OpsOf(AssistantActionResolution resolution, string field) =>
        [.. resolution.Values.Where(value => value.Field == field).SelectMany(value => Elements(value.Resolved))];

    private static HashSet<Guid> Apply(IEnumerable<Guid> current, IEnumerable<RoleOp> ops)
    {
        var result = current.ToHashSet();
        foreach (var op in ops)
            if (op.Op == Remove) result.Remove(op.Id);
            else result.Add(op.Id);
        return result;
    }

    private static Email? EmailOf(AssistantActionResolution resolution) =>
        resolution.Values.SingleOrDefault(value => value.Field == EmailField)?.Resolved is { ValueKind: JsonValueKind.String } email
        && Email.Create(email.GetString()) is { IsSuccess: true } created ? created.Value : null;

    private static Guid? IdOf(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.String } id && Guid.TryParse(id.GetString(), out var parsed) ? parsed : null;

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
        _ => "target",
    };

    // Un invitado, con la invitación vigente o vencida: lo que tiene guardado no vale hasta que acepte.
    private static bool IsInvited(OrganizationUserRow row) => row.State is OrganizationUserState.Invited or OrganizationUserState.Expired;

    private static string Label(OrganizationUserRow row) =>
        row.DisplayName.Length > 0 ? row.DisplayName : row.Email?.DisplayValue ?? row.Phone?.Value ?? string.Empty;

    private static ValidationError Invalid(string path, string code) =>
        new(new Dictionary<string, string[]>(StringComparer.Ordinal) { [path] = [code] });

    /// <summary>Un elemento resuelto de un <c>Choices</c>: sumar o quitar, el id y su duda.</summary>
    private sealed record RoleOp(
        [property: JsonPropertyName("op")] string Op,
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("doubt")] string? Doubt);

    /// <summary>Lo que se lee una vez por llamada: lo que tiene quien pide, los roles de la organización y quién pide.</summary>
    private sealed record SourceContext(PermissionsByPlace Held, IReadOnlyDictionary<Guid, RoleRow> Roles, Guid? ActorId)
    {
        public bool CanGrant(RoleRow role, Guid? companyId) =>
            OrganizationDelegationPolicy.CanGrantRole(Held, role.Permissions, role.Scope == RoleScope.Organization ? null : companyId ?? role.CompanyId);

        public bool CanGrantInEveryCompany(RoleRow role) =>
            OrganizationDelegationPolicy.CanGrantListedRole(Held, role.Scope, role.CompanyId, role.Permissions, companyId: null);

        // Actuar sobre alguien (0061): tener todo lo que le dan sus roles, en cada lugar. Sobre uno mismo, siempre; quitar a un
        // invitado es revocar su invitación, que no pide nada.
        public bool CanActOn(AssistantActionDefinition action, OrganizationUserRow target)
        {
            var self = target.UserId is { } userId && userId == ActorId;
            if (self || (action.Kind == AssistantActionKind.Remove && IsInvited(target))) return true;
            var organization = target.Roles.SelectMany(role => PermissionsOf(role.Id));
            var byCompany = target.Companies.ToDictionary(company => company.CompanyId,
                company => company.Roles.SelectMany(role => PermissionsOf(role.Id)));
            return OrganizationDelegationPolicy.CanActOn(self, Held, PermissionsByPlace.Of(organization, byCompany));
        }

        private IEnumerable<string> PermissionsOf(Guid roleId) => Roles.TryGetValue(roleId, out var role) ? role.Permissions : [];
    }
}
