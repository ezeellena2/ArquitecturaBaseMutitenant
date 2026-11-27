using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;
using CatalogFilters = ArquitecturaBaseMultitenant.Domain.Forms.FormCatalogFilters;
using CatalogCondition = ArquitecturaBaseMultitenant.Domain.Forms.FormCatalogCondition;
using CatalogFilterSort = ArquitecturaBaseMultitenant.Domain.Forms.FormCatalogFilterSort;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;

/// <summary>
/// Los catálogos de la organización (E12). Cada escritura valida afuera y usa una sola UoW: el alta, y guardar todo junto con la
/// versión del catálogo (C3), que toma su bloqueo, cambia solo las opciones que vienen y toca siempre la fila del catálogo, así
/// un guardado sin cambios compara la versión (409 común) sin tocar ni auditar opciones. El estado «No se puede usar» se calcula
/// al leer con la regla del dominio.
/// </summary>
internal sealed class FormCatalogService(IFormCatalogReader reader, IFormCatalogRepository repository, FormCatalogItemEditor editor,
    IUserDisplayNameReader names, IPermissionService permissions, ICurrentUser user, IRequestValidator validator, IUnitOfWork unitOfWork,
    TimeProvider clock, ILogger<FormCatalogService> logger) : IFormCatalogService
{
    public Task<Result<PagedResult<FormCatalogRow>>> ListAsync(ListFormCatalogsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, clock, "ListFormCatalogs", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var page = await reader.ListAsync(request, cancellationToken);
            return Result.Success(new PagedResult<FormCatalogRow>([.. page.Items.Select(ToRow)], page.Page, page.PageSize, page.TotalCount));
        });
    }

    public Task<Result<FormCatalogFilterCountsResponse>> GetFilterCountsAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "GetFormCatalogFilterCounts",
            async () => Result.Success(await reader.CountFiltersAsync(cancellationToken)));

    public Task<Result<FormCatalogResponse>> GetAsync(Guid catalogId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<FormCatalogResponse>(logger, clock, "GetFormCatalog", async () =>
        {
            var catalog = await reader.FindAsync(catalogId, cancellationToken);
            if (catalog is null) return FormCatalogErrors.NotFound;
            return await BuildAsync(catalog, cancellationToken);
        });

    public Task<Result<PagedResult<FormCatalogItemRow>>> ListItemsAsync(Guid catalogId, ListFormCatalogItemsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync<PagedResult<FormCatalogItemRow>>(logger, clock, "ListFormCatalogItems", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (await reader.FindAsync(catalogId, cancellationToken) is null) return FormCatalogErrors.NotFound;
            return await reader.ListItemsAsync(catalogId, request, cancellationToken);
        });
    }

    public Task<Result<FormCatalogCreatedResponse>> CreateAsync(CreateFormCatalogRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync<FormCatalogCreatedResponse>(logger, clock, "CreateFormCatalog", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var catalog = NewCatalog(request);
            if (catalog.IsFailure) return catalog.Error;
            var saved = await unitOfWork.ExecuteInTransactionAsync(
                ct => CreateCoreAsync(catalog.Value, FormCatalogItemChanges.Of(request), ct), CommitPolicy.OnSuccess, cancellationToken);
            if (saved.IsFailure) return saved.Error;
            // La versión es la que trajo el INSERT, nunca la de una relectura (concurrencia.md).
            var summary = await reader.FindAsync(catalog.Value.Id, cancellationToken)
                ?? throw new InvalidOperationException("The catalog that was just created can't be read.");
            return new FormCatalogCreatedResponse(await BuildAsync(summary with { Version = catalog.Value.Version }, cancellationToken),
                saved.Value);
        });
    }

    public Task<Result<FormCatalogUpdatedResponse>> UpdateAsync(UpdateFormCatalogRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync<FormCatalogUpdatedResponse>(logger, clock, "UpdateFormCatalog", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var saved = await unitOfWork.ExecuteInTransactionAsync(ct => UpdateCoreAsync(request, ct), CommitPolicy.OnSuccess,
                cancellationToken);
            if (saved.IsFailure) return saved.Error;
            return new FormCatalogUpdatedResponse(saved.Value.Catalog.Version, saved.Value.Identities);
        });
    }

    private async Task<Result<IReadOnlyList<FormCatalogItemIdentity>>> CreateCoreAsync(FormCatalog catalog, FormCatalogItemChanges changes,
        CancellationToken ct)
    {
        repository.Add(catalog);
        if (changes.Items.Count == 0) return Result.Success<IReadOnlyList<FormCatalogItemIdentity>>([]);
        return await editor.ApplyAsync(catalog, changes, ct);
    }

    /// <summary>
    /// Bloqueo, lecturas, reglas y escrituras, en ese orden. «Dar de baja» y «Reactivar» mandan el mismo guardado con lo pendiente:
    /// reactivar va antes de cambiar opciones y dar de baja, después, así un catálogo que sigue de baja no cambia sus opciones.
    /// </summary>
    private async Task<Result<(FormCatalog Catalog, IReadOnlyList<FormCatalogItemIdentity> Identities)>> UpdateCoreAsync(
        UpdateFormCatalogRequest request, CancellationToken ct)
    {
        var catalog = await repository.GetByIdAsync(request.CatalogId, request.Version!.Value, ct);
        if (catalog is null) return FormCatalogErrors.NotFound;
        if (request.IsActive!.Value) catalog.Reactivate();
        catalog.Rename(request.Name!, request.Description);

        IReadOnlyList<FormCatalogItemIdentity> identities = [];
        if (request.ChangesItems)
        {
            var applied = await editor.ApplyAsync(catalog, FormCatalogItemChanges.Of(request), ct);
            if (applied.IsFailure) return applied.Error;
            identities = applied.Value;
        }

        if (request.Filters is not null)
        {
            var sample = SampleOf(catalog.Source, request.SampleMemberId, request.SampleContactId) ?? catalog.ReadFilters()?.SampleId;
            var changed = catalog.ChangeFilters(ToFilters(request.Filters, sample));
            if (changed.IsFailure) return changed.Error;
        }

        if (!request.IsActive.Value) catalog.Deactivate();
        repository.MarkUpdated(catalog);
        return (catalog, identities);
    }

    /// <summary>
    /// El catálogo del alta, con el origen y el tipo que ya no cambian. Uno a mano no lleva filtros; uno de miembros o de contactos
    /// es siempre de texto y no lleva opciones (<see cref="FormCatalogErrors.WrongSource"/>).
    /// </summary>
    private static Result<FormCatalog> NewCatalog(CreateFormCatalogRequest request)
    {
        var source = request.Source!.Value;
        if (source == FormCatalogSource.Manual)
        {
            if (request.Filters is not null) return FormCatalogErrors.WrongSource;
            return FormCatalog.CreateManual(request.Name!, request.Description, request.ValueType ?? FormCatalogValueType.Text);
        }

        if (request.Items is { Count: > 0 } || request.ValueType is not (null or FormCatalogValueType.Text)) return FormCatalogErrors.WrongSource;
        var filters = ToFilters(request.Filters!, SampleOf(source, request.SampleMemberId, request.SampleContactId));
        return source == FormCatalogSource.Members
            ? FormCatalog.CreateMembers(request.Name!, request.Description, filters)
            : FormCatalog.CreateContacts(request.Name!, request.Description, filters);
    }

    /// <summary>La persona de ejemplo en uno de miembros y el cliente de ejemplo en uno de contactos; se guarda en los filtros.</summary>
    private static Guid? SampleOf(FormCatalogSource source, Guid? sampleMemberId, Guid? sampleContactId) => source switch
    {
        FormCatalogSource.Members => sampleMemberId,
        FormCatalogSource.Contacts => sampleContactId,
        _ => null,
    };

    private static CatalogFilters ToFilters(FormCatalogFiltersInput input, Guid? sampleId) => new(input.SourceVersion!.Value,
        [.. input.Conditions!.Select(condition => new CatalogCondition(condition.Column!, condition.Operator!, [.. condition.Values!]))],
        input.ExcludeRecipient!.Value, string.IsNullOrWhiteSpace(input.Detail) ? null : input.Detail,
        input.Sort is null ? null : new CatalogFilterSort(input.Sort.Column!, input.Sort.Descending!.Value), sampleId);

    private static FormCatalogRow ToRow(FormCatalogSummary catalog)
    {
        var reason = UnusableReason(catalog);
        return new FormCatalogRow(catalog.Id, catalog.Name, catalog.Description, catalog.Source, catalog.ValueType,
            catalog.Source == FormCatalogSource.Manual ? catalog.TotalItems : null, catalog.IsActive, catalog.IsActive && reason is null,
            reason);
    }

    private async Task<FormCatalogResponse> BuildAsync(FormCatalogSummary catalog, CancellationToken ct)
    {
        var reason = UnusableReason(catalog);
        string? modifiedBy = null;
        if (catalog.ModifiedBy is { } modifierId) modifiedBy = (await names.FindAsync([modifierId], ct)).GetValueOrDefault(modifierId);
        var canManage = user.UserId is { } me && await permissions.HasPermissionAsync(me, Permissions.Forms.CatalogsManage, ct);
        return new FormCatalogResponse(catalog.Id, catalog.Name, catalog.Description, catalog.Source, catalog.ValueType,
            ToResponse(catalog.Filters), Roles: null, Companies: null, Sample: null, catalog.IsActive, catalog.IsActive && reason is null,
            reason, canManage, new FormCatalogItemCounts(catalog.TotalItems, catalog.ActiveItems), catalog.ModifiedAtUtc, modifiedBy,
            catalog.Version);
    }

    /// <summary>
    /// «No se puede usar», con la regla del dominio. Uno a mano mira sus opciones activas. En uno de miembros o de contactos, la
    /// versión de hoy de la fuente, el canal y los roles y empresas que faltan los dan las fuentes (§4 y §4b); mientras no se leen,
    /// se toma la versión que guardó y nada faltante.
    /// </summary>
    private static FormCatalogUnusableReason? UnusableReason(FormCatalogSummary catalog) =>
        FormCatalog.UnusableReason(new FormCatalogState(catalog.Source, catalog.IsActive, catalog.Filters), catalog.ActiveItems,
            currentSourceVersion: catalog.Filters?.SourceVersion ?? 0, channelAvailable: true, roleMissing: false, companyMissing: false);

    private static Models.FormCatalogFilters? ToResponse(CatalogFilters? filters) => filters is null ? null : new(filters.SourceVersion,
        [.. filters.Conditions.Select(condition => new Models.FormCatalogCondition(condition.Column, condition.Operator, condition.Values))],
        filters.ExcludeRecipient, filters.Detail,
        filters.Sort is null ? null : new Models.FormCatalogFilterSort(filters.Sort.Column, filters.Sort.Descending));
}
