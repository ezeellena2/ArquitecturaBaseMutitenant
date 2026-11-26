using System.Globalization;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Application.Services.Companies;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Services.Roles;

/// <summary>
/// Las acciones de Roles (ADR 0068): crear, cambiar y dar de baja un rol, con el requisito de su ruta. Solo lee, con los
/// readers del área, lo que tiene quien pide (<see cref="DelegationPermissionsResolver"/>) y la misma
/// <see cref="OrganizationDelegationPolicy"/> que usa el <c>Guard</c> de la ruta (0061): un permiso se resuelve solo si es del
/// catálogo del alcance del rol y quien pide lo tiene en la organización, con lo incluido (260). Valida armando el mismo
/// pedido que la pantalla. Lo que la ruta decide adentro (un rol con personas no se da de baja) lo sigue diciendo la ruta.
/// <list type="bullet">
/// <item>Al crear se proponen el alcance (por su nombre en pantalla o su valor), la empresa si es de una empresa y los
/// permisos; al editar, el nombre, la descripción y sumar o quitar permisos, nunca el alcance ni la empresa.</item>
/// <item>Un permiso se escribe por su nombre o su código; quitar elige solo entre los que el rol tiene. Quitar uno que sigue
/// incluido por otro que queda vuelve al modelo con su ruta y <see cref="PermissionIncludedCode"/>: la ruta lo volvería a
/// guardar.</item>
/// <item>El destino es un rol de la organización por su nombre, buscado con el lector (<see cref="RoleActionResolver"/>); con
/// varios parecidos vuelve al modelo con <see cref="RoleErrors.AmbiguousCode"/>, y uno de sistema, que la ruta no deja cambiar
/// ni dar de baja, también.</item>
/// <item>La empresa se resuelve y se nombra solo con <c>companies.read</c>, lo que pide la lista de empresas del editor
/// (<see cref="CompanyActionResolver"/>): <c>roles.manage</c> no lo trae.</item>
/// </list>
/// Elegir por nombre, leer un elemento y aplicar las operaciones es de <see cref="AssistantActionInputs"/>, igual para todas las
/// fuentes.
/// </summary>
internal sealed class RoleActionSource(
    IRoleReader roles,
    RoleActionResolver roleNames,
    CompanyActionResolver companies,
    DelegationPermissionsResolver delegation,
    IValidator<CreateRoleRequest> createValidator,
    IValidator<UpdateRoleRequest> updateValidator) : IAssistantActionSource
{
    internal const string NameField = "name";
    internal const string DescriptionField = "description";
    internal const string ScopeField = "scope";
    internal const string CompanyField = "company";
    internal const string PermissionsField = "permissions";

    /// <summary>Quitar un permiso que sigue incluido por otro que queda (260); solo vuelve al modelo, nunca como HTTP.</summary>
    internal const string PermissionIncludedCode = RoleErrors.PermissionIncludedCode;

    private const string TargetPath = "target";

    private static readonly AssistantActionField NameInput = new()
    { Name = NameField, Type = AssistantActionFieldType.Text, Required = true, MaxLength = TextLimits.ShortName };

    private static readonly AssistantActionField DescriptionInput = new()
    { Name = DescriptionField, Type = AssistantActionFieldType.LongText, MaxLength = TextLimits.Description };

    private static readonly AssistantActionTarget RoleTarget = new("GET api/roles/{id}");

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
                NameInput,
                DescriptionInput,
                new() { Name = ScopeField, Type = AssistantActionFieldType.Choice, Required = true },
                new() { Name = CompanyField, Type = AssistantActionFieldType.Record, DependsOn = ScopeField },
                new() { Name = PermissionsField, Type = AssistantActionFieldType.Choices, Required = true, DependsOn = ScopeField },
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
            Target = RoleTarget,
            Fields = [NameInput, DescriptionInput, new() { Name = PermissionsField, Type = AssistantActionFieldType.Choices }],
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
            Target = RoleTarget,
        },
    ];

    public IReadOnlyList<AssistantActionDefinition> Definitions => Actions;

    /// <summary>
    /// Los alcances, o los permisos que quien pide puede dar: los del catálogo del alcance del campo del que depende, o de los
    /// dos catálogos si no hay.
    /// </summary>
    public async Task<Result<PagedResult<AssistantActionChoiceRow>>> ListChoicesAsync(AssistantActionChoiceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        AssistantActionChoiceRow[] rows;
        if (request.Field == ScopeField)
            rows = [.. OfferedScopes.Select(scope => new AssistantActionChoiceRow(scope.ToString(), ScopeLabel(request.Action, scope)))];
        else if (request.Field == PermissionsField)
        {
            RoleScope? scope = null;
            if (!string.IsNullOrWhiteSpace(request.DependsOnValue))
            {
                if (MatchScope(request.Action, request.DependsOnValue) is not { } match) return Invalid(ScopeField, RoleErrors.InvalidScopeCode);
                scope = match.Item;
            }
            var held = await delegation.ReadHeldAsync(cancellationToken);
            rows = [.. Catalog(scope).Where(code => CanGive(held, code))
                .Select(code => new AssistantActionChoiceRow(code, PermissionTexts.Permission(code)))
                .OrderBy(row => row.Name, StringComparer.CurrentCulture)];
        }
        else return Invalid(request.Field, ValidationError.ErrorCode);
        // Los alcances y los permisos son catálogos cerrados, no una tabla.
        return AssistantActionInputs.PageOf(rows, request.Search, request.Page);
    }

    public async Task<Result<AssistantActionResolution>> ResolveAsync(AssistantActionDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var action = draft.Action;
        var held = await delegation.ReadHeldAsync(cancellationToken);
        RoleRow? target = null;
        if (action.Target is not null)
        {
            var found = await MatchTargetAsync(draft.Target, cancellationToken);
            if (found.IsFailure) return Invalid(TargetPath, found.Error.Code);
            target = found.Value;
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var values = new List<AssistantActionResolvedValue>();
        var inputs = draft.Values.ToDictionary(value => value.Field, StringComparer.Ordinal);
        ResolveText(inputs, NameField, RoleErrors.NameRequiredCode, values, errors);
        ResolveText(inputs, DescriptionField, ValidationError.ErrorCode, values, errors);

        // Al editar vale el alcance del rol: la pantalla no deja cambiarlo.
        var scope = target?.Scope;
        if (inputs.TryGetValue(ScopeField, out var scopeInput))
        {
            if (scopeInput.Value.ValueKind != JsonValueKind.String || MatchScope(action, scopeInput.Value.GetString()!) is not { } match)
                errors[ScopeField] = [RoleErrors.InvalidScopeCode];
            else
            {
                scope = match.Item;
                values.Add(new(ScopeField, scopeInput.Value, JsonSerializer.SerializeToElement(match.Item.ToString()), match.Doubt));
            }
        }
        else if (action.Kind == AssistantActionKind.Create) errors[ScopeField] = [RoleErrors.InvalidScopeCode];

        if (inputs.TryGetValue(CompanyField, out var companyInput))
        {
            var company = scope is not (null or RoleScope.SpecificCompany) ? RoleErrors.CompanyNotAllowed
                : companyInput.Value.ValueKind == JsonValueKind.String ? await companies.MatchAsync(held, companyInput.Value.GetString(), cancellationToken)
                : CompanyErrors.NotFound;
            if (company.IsFailure) errors[CompanyField] = [company.Error.Code];
            else values.Add(new(CompanyField, companyInput.Value, JsonSerializer.SerializeToElement(company.Value.Company.Id.ToString("D")), company.Value.Doubt));
        }
        else if (action.Kind == AssistantActionKind.Create && scope == RoleScope.SpecificCompany) errors[CompanyField] = [RoleErrors.CompanyRequiredCode];

        if (inputs.TryGetValue(PermissionsField, out var permissions) && scope is { } place)
            ResolvePermissions(permissions, place, target?.Permissions ?? [], action.Kind, held, values, errors);

        if (errors.Count > 0) return new ValidationError(errors);
        return new AssistantActionResolution(action, target?.Id, target?.Version, values);
    }

    // Sus campos son fijos: ninguna de sus acciones arma los campos desde su destino.
    public Task<Result<IReadOnlyList<AssistantActionField>>> ReadTargetFieldsAsync(AssistantActionDefinition action, string target, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure<IReadOnlyList<AssistantActionField>>(ContractErrors.NotImplemented));

    /// <summary>
    /// Vuelve a leer por id el rol, la empresa y cada permiso, con lo que quien pide tiene ahora: lo que ya no está (o ya no es
    /// del catálogo del alcance) es <c>NotFound</c>, y un permiso que ya no puede dar o quitar, <c>Forbidden</c>.
    /// </summary>
    public async Task<Result<AssistantActionReview>> ReviewAsync(AssistantActionResolution resolution, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        var action = resolution.Action;
        var held = await delegation.ReadHeldAsync(cancellationToken);
        AssistantActionTargetView? target = null;
        RoleScope? scope = null;
        if (action.Target is not null)
        {
            if (await FindTargetAsync(resolution.TargetId, cancellationToken) is not { } role) return RoleErrors.NotFound;
            target = new(role.Id, RoleTexts.Name(role.Name), role.Version);
            scope = role.Scope;
        }

        var views = new List<AssistantActionValueView>();
        foreach (var value in resolution.Values)
        {
            switch (value.Field)
            {
                case NameField or DescriptionField:
                    views.Add(new(value.Field, value.Resolved ?? value.Written, null, value.Doubt));
                    break;
                case ScopeField:
                    if (ScopeOf(value.Resolved) is not { } chosen) return RoleErrors.NotFound;
                    scope = chosen;
                    views.Add(new(ScopeField, JsonSerializer.SerializeToElement(chosen.ToString()), ScopeLabel(action, chosen), value.Doubt));
                    break;
                case CompanyField:
                    if (await companies.FindActiveAsync(held, AssistantActionInputs.IdOf(value.Resolved), cancellationToken) is not { } company)
                        return CompanyErrors.NotFound;
                    views.Add(new(CompanyField, JsonSerializer.SerializeToElement(company.Id.ToString("D")), company.Name, value.Doubt));
                    break;
            }
        }

        foreach (var element in OpsOf(resolution))
        {
            if (scope is not { } place || !Catalog(place).Contains(element.Id, StringComparer.Ordinal)) return RoleErrors.NotFound;
            if (!CanGive(held, element.Id)) return RoleErrors.PermissionsNotHeld;
            views.Add(new(PermissionsField, JsonSerializer.SerializeToElement(new { op = element.Op, id = element.Id }),
                PermissionTexts.Permission(element.Id), element.Doubt));
        }
        return new AssistantActionReview(target, views);
    }

    /// <summary>
    /// Arma el pedido de la pantalla con lo resuelto (al editar, las operaciones sobre los permisos que el rol tiene ahora, con
    /// su alcance y su empresa), corre su validador, las reglas del rol, el nombre repetido y la
    /// <see cref="OrganizationDelegationPolicy"/> con lo que entra y sale, sin guardar.
    /// </summary>
    public async Task<Result> ValidateAsync(AssistantActionResolution resolution, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        var action = resolution.Action;
        var held = await delegation.ReadHeldAsync(cancellationToken);
        RoleRow? target = null;
        if (action.Target is not null)
        {
            target = await FindTargetAsync(resolution.TargetId, cancellationToken);
            if (target is null) return Invalid(TargetPath, RoleErrors.NotFoundCode);
        }
        if (action.Kind == AssistantActionKind.Remove) return Result.Success();

        var name = TextOf(resolution, NameField) ?? target?.Name;
        var description = TextOf(resolution, DescriptionField) is { } written
            ? string.IsNullOrWhiteSpace(written) ? null : written
            : target?.Description;
        var scope = target is null ? ScopeOf(ResolvedOf(resolution, ScopeField)) : target.Scope;
        var companyId = target is null ? AssistantActionInputs.IdOf(ResolvedOf(resolution, CompanyField)) : target.CompanyId;
        if (target is null && companyId is not null && await companies.FindActiveAsync(held, companyId, cancellationToken) is null)
            return Invalid(CompanyField, CompanyErrors.NotFoundCode);
        var before = target?.Permissions ?? [];
        // Como guarda la ruta: lo pedido más lo que incluye (260).
        string[] after = [.. Permissions.Inclusions.WithIncluded(AssistantActionInputs.Apply(before, OpsOf(resolution))).Order(StringComparer.Ordinal)];

        var validation = target is null
            ? await createValidator.ValidateAsync(new CreateRoleRequest(name, description, scope, companyId, after), cancellationToken)
            : await updateValidator.ValidateAsync(new UpdateRoleRequest(target.Id, name, description, target.Scope, target.CompanyId, after, target.Version), cancellationToken);
        if (!validation.IsValid)
            return new ValidationError(validation.Errors.GroupBy(failure => FieldOfProperty(failure.PropertyName), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, _ => new[] { ValidationError.ErrorCode }, StringComparer.Ordinal));

        // Las reglas del rol que corre la ruta (dónde vale, su empresa y el catálogo de su alcance), en memoria.
        var role = Role.Create(name, description, scope!.Value, companyId, after);
        if (role.IsFailure) return Invalid(FieldOfCode(role.Error.Code), role.Error.Code);
        if (await roles.ExistsByNameAsync(role.Value.Name, companyId, target?.Id, cancellationToken))
            return Invalid(NameField, RoleErrors.AlreadyExistsCode);
        return OrganizationDelegationPolicy.CanChangePermissions(held, before, after)
            ? Result.Success()
            : Invalid(PermissionsField, RoleErrors.PermissionsNotHeldCode);
    }

    private static void ResolveText(Dictionary<string, AssistantActionInput> inputs, string field, string invalidCode,
        List<AssistantActionResolvedValue> values, Dictionary<string, string[]> errors)
    {
        if (!inputs.TryGetValue(field, out var input)) return;
        if (input.Value.ValueKind != JsonValueKind.String || (field == NameField && string.IsNullOrWhiteSpace(input.Value.GetString())))
            errors[field] = [invalidCode];
        else values.Add(new(field, input.Value, JsonSerializer.SerializeToElement(input.Value.GetString()!.Trim()), null));
    }

    // Sumar elige entre los permisos del catálogo del alcance; quitar, solo entre los que el rol tiene. Cada uno, si quien pide
    // lo puede dar o quitar.
    private static void ResolvePermissions(AssistantActionInput input, RoleScope scope, IReadOnlyCollection<string> current,
        AssistantActionKind kind, PermissionsByPlace held, List<AssistantActionResolvedValue> values, Dictionary<string, string[]> errors)
    {
        if (input.Value.ValueKind != JsonValueKind.Array)
        {
            errors[input.Field] = [ValidationError.ErrorCode];
            return;
        }
        var catalog = Catalog(scope).ToArray();
        var assigned = catalog.Where(code => current.Contains(code, StringComparer.Ordinal)).ToArray();
        var resolved = new List<(AssistantActionOp<string> Op, string Path)>();
        var index = 0;
        foreach (var element in input.Value.EnumerateArray())
        {
            var path = $"{input.Field}[{index++}]";
            if (!AssistantActionInputs.TryReadOp(element, out var op, out var text) || (op == AssistantActionInputs.Remove && kind != AssistantActionKind.Edit))
                errors[path] = [ValidationError.ErrorCode];
            else if (AssistantActionInputs.Match(op == AssistantActionInputs.Remove ? assigned : catalog, Names, text) is not { } match)
                errors[path] = [RoleErrors.PermissionNotFoundCode];
            else if (!CanGive(held, match.Item))
                errors[path] = [RoleErrors.PermissionsNotHeldCode];
            else resolved.Add((new(op, match.Item, match.Doubt), path));
        }

        // 260: lo que sigue incluido por otro que queda, la ruta lo vuelve a guardar; quitarlo no cambia nada y vuelve al modelo.
        var kept = AssistantActionInputs.Apply(current, resolved.Select(item => item.Op));
        foreach (var (op, path) in resolved.Where(item => item.Op.Op == AssistantActionInputs.Remove))
            if (kept.Any(code => Permissions.Inclusions.Of(code).Contains(op.Id, StringComparer.Ordinal)))
                errors[path] = [PermissionIncludedCode];

        var doubt = resolved.Any(item => item.Op.Doubt is not null) ? AssistantActionDoubts.Similar : null;
        values.Add(new(input.Field, input.Value,
            JsonSerializer.SerializeToElement(resolved.Select(item => item.Op).ToArray(), AssistantActionInputs.Json), doubt));
    }

    // Lo busca el lector con su búsqueda; entre lo encontrado, igual gana y, si no hay, uno solo parecido.
    private async Task<Result<RoleRow>> MatchTargetAsync(string? text, CancellationToken cancellationToken)
    {
        if (!AssistantActionInputs.IsWritten(text)) return RoleErrors.NotFound;
        var found = await roleNames.SearchAsync(text, new ListRolesRequest(), cancellationToken);
        var exact = found.Where(role => RoleActionResolver.Names(role).Any(name => AssistantActionInputs.Same(name, text))).ToArray();
        var candidates = exact.Length > 0 ? exact
            : found.Where(role => RoleActionResolver.Names(role).Any(name => AssistantActionInputs.Contains(name, text))).ToArray();
        return candidates.Length switch
        {
            0 => RoleErrors.NotFound,
            // La ruta no deja cambiar ni dar de baja un rol de sistema.
            1 when candidates[0].IsSystemRole => RoleErrors.SystemRoleCannotChange,
            1 => candidates[0],
            // Con varios roles parecidos no se elige uno: el modelo pregunta cuál.
            _ => RoleErrors.Ambiguous,
        };
    }

    // El rol de una propuesta guardada, por su id; uno de sistema nunca es destino.
    private async Task<RoleRow?> FindTargetAsync(Guid? roleId, CancellationToken cancellationToken) =>
        roleId is { } id && await roles.FindByIdAsync(id, cancellationToken) is { IsSystemRole: false } role ? role : null;

    // Los alcances que el editor deja elegir al crear: «Cada empresa» queda solo en la API y en los roles de sistema
    // (docs/features/roles.md), así que una propuesta con ese alcance no se podría confirmar en la pantalla de siempre.
    private static readonly RoleScope[] OfferedScopes = [RoleScope.Organization, RoleScope.SpecificCompany];

    private static (RoleScope Item, string? Doubt)? MatchScope(AssistantActionDefinition action, string text) =>
        AssistantActionInputs.Match(OfferedScopes, scope => new[]
        {
            ScopeLabel(action, scope), ActionTexts.OptionIn(action, ScopeField, scope.ToString(), CultureInfo.InvariantCulture), scope.ToString(),
        }.Distinct(StringComparer.Ordinal), text);

    private static string ScopeLabel(AssistantActionDefinition action, RoleScope scope) => ActionTexts.Option(action, ScopeField, scope.ToString());

    // Un permiso se nombra como lo muestra la pantalla, en el idioma de la persona o en el de los recursos, o por su código.
    private static IEnumerable<string> Names(string permission) =>
        new[] { PermissionTexts.Permission(permission), PermissionTexts.PermissionIn(permission, CultureInfo.InvariantCulture), permission }
            .Distinct(StringComparer.Ordinal);

    // El catálogo del alcance, como lo ofrece el editor: el de la organización o el de la empresa; sin alcance, los dos.
    private static IReadOnlyCollection<string> Catalog(RoleScope? scope) => scope switch
    {
        null => Permissions.All,
        RoleScope.Organization => Permissions.OrganizationScoped,
        _ => Permissions.CompanyScoped,
    };

    // Dar o quitar un permiso en un rol: lo tiene en la organización, con lo que incluye (la regla del Guard de la ruta).
    private static bool CanGive(PermissionsByPlace held, string permission) =>
        OrganizationDelegationPolicy.CanChangePermissions(held, [], [permission]);

    private static AssistantActionOp<string>[] OpsOf(AssistantActionResolution resolution) =>
        AssistantActionInputs.OpsOf<string>(resolution, PermissionsField);

    private static JsonElement? ResolvedOf(AssistantActionResolution resolution, string field) =>
        AssistantActionInputs.ResolvedOf(resolution, field);

    private static string? TextOf(AssistantActionResolution resolution, string field) =>
        ResolvedOf(resolution, field) is { ValueKind: JsonValueKind.String } text ? text.GetString() : null;

    private static RoleScope? ScopeOf(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.String } text && Enum.TryParse<RoleScope>(text.GetString(), out var scope) && Enum.IsDefined(scope)
            ? scope : null;

    // El pedido nombra los campos del HTTP; la propuesta, los de la pantalla.
    private static string FieldOfProperty(string property) => property switch
    {
        nameof(CreateRoleRequest.Name) => NameField,
        nameof(CreateRoleRequest.Description) => DescriptionField,
        nameof(CreateRoleRequest.Scope) => ScopeField,
        nameof(CreateRoleRequest.CompanyId) => CompanyField,
        _ when property.StartsWith(nameof(CreateRoleRequest.Permissions), StringComparison.Ordinal) => PermissionsField,
        _ => TargetPath,
    };

    private static string FieldOfCode(string code) => code switch
    {
        RoleErrors.NameRequiredCode or RoleErrors.NameTooLongCode or RoleErrors.SystemRoleNameReservedCode => NameField,
        RoleErrors.DescriptionTooLongCode => DescriptionField,
        RoleErrors.InvalidScopeCode => ScopeField,
        RoleErrors.CompanyRequiredCode or RoleErrors.CompanyNotAllowedCode => CompanyField,
        _ => PermissionsField,
    };

    private static ValidationError Invalid(string path, string code) => AssistantActionInputs.Invalid(path, code);
}
