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

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;

/// <summary>
/// Los catálogos de la organización (E12). Cada escritura valida afuera y usa una sola UoW: el alta, y guardar todo junto con la
/// versión del catálogo (C3), que toma su bloqueo, cambia solo las opciones que vienen y toca siempre la fila del catálogo, así
/// un guardado sin cambios compara la versión (409 común) sin tocar ni auditar opciones. Los filtros de uno de miembros o de
/// contactos se validan contra su fuente, y el estado «No se puede usar» se calcula al leer con la regla del dominio y lo que dan
/// las fuentes (<see cref="FormCatalogFiltersPolicy"/>).
/// </summary>
internal sealed class FormCatalogService(IFormCatalogReader reader, IFormCatalogRepository repository, FormCatalogItemEditor editor,
    FormCatalogFiltersPolicy filters, IUserDisplayNameReader names, IPermissionService permissions, ICurrentUser user,
    IRequestValidator validator, IUnitOfWork unitOfWork, TimeProvider clock, ILogger<FormCatalogService> logger) : IFormCatalogService
{
    public Task<Result<PagedResult<FormCatalogRow>>> ListAsync(ListFormCatalogsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, clock, "ListFormCatalogs", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var page = await reader.ListAsync(request, cancellationToken);
            var states = await filters.DecideStatesAsync(page.Items, cancellationToken);
            return Result.Success(new PagedResult<FormCatalogRow>([.. page.Items.Select(catalog => ToRow(catalog, states[catalog.Id].Reason))],
                page.Page, page.PageSize, page.TotalCount));
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
            var catalog = await NewCatalogAsync(request, cancellationToken);
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
            if (catalog.Source == FormCatalogSource.Manual) return FormCatalogErrors.WrongSource;
            var sample = FormCatalogFiltersPolicy.SampleOf(catalog.Source, request.SampleMemberId, request.SampleContactId)
                ?? catalog.ReadFilters()?.SampleId;
            var decided = await filters.DecideAsync(catalog.Source, request.Filters, sample, ct);
            if (decided.IsFailure) return decided.Error;
            var changed = catalog.ChangeFilters(decided.Value);
            if (changed.IsFailure) return changed.Error;
        }

        if (!request.IsActive.Value) catalog.Deactivate();
        repository.MarkUpdated(catalog);
        return (catalog, identities);
    }

    /// <summary>
    /// El catálogo del alta, con el origen y el tipo que ya no cambian. Uno a mano no lleva filtros; uno de miembros o de contactos
    /// es siempre de texto, no lleva opciones (<see cref="FormCatalogErrors.WrongSource"/>) y sus filtros se validan contra su fuente.
    /// </summary>
    private async Task<Result<FormCatalog>> NewCatalogAsync(CreateFormCatalogRequest request, CancellationToken ct)
    {
        var source = request.Source!.Value;
        if (source == FormCatalogSource.Manual)
        {
            if (request.Filters is not null) return FormCatalogErrors.WrongSource;
            return FormCatalog.CreateManual(request.Name!, request.Description, request.ValueType ?? FormCatalogValueType.Text);
        }

        if (request.Items is { Count: > 0 } || request.ValueType is not (null or FormCatalogValueType.Text)) return FormCatalogErrors.WrongSource;
        var decided = await filters.DecideAsync(source, request.Filters!,
            FormCatalogFiltersPolicy.SampleOf(source, request.SampleMemberId, request.SampleContactId), ct);
        if (decided.IsFailure) return decided.Error;
        return source == FormCatalogSource.Members
            ? FormCatalog.CreateMembers(request.Name!, request.Description, decided.Value)
            : FormCatalog.CreateContacts(request.Name!, request.Description, decided.Value);
    }

    private static FormCatalogRow ToRow(FormCatalogSummary catalog, FormCatalogUnusableReason? reason) =>
        new FormCatalogRow(catalog.Id, catalog.Name, catalog.Description, catalog.Source, catalog.ValueType,
            catalog.Source == FormCatalogSource.Manual ? catalog.TotalItems : null, catalog.IsActive, catalog.IsActive && reason is null,
            reason);

    private async Task<FormCatalogResponse> BuildAsync(FormCatalogSummary catalog, CancellationToken ct)
    {
        var state = (await filters.DecideStatesAsync([catalog], ct))[catalog.Id];
        var reason = state.Reason;
        var sample = catalog.Filters?.SampleId is { } sampleId ? await filters.FindSampleAsync(catalog.Source, sampleId, ct) : null;
        string? modifiedBy = null;
        if (catalog.ModifiedBy is { } modifierId) modifiedBy = (await names.FindAsync([modifierId], ct)).GetValueOrDefault(modifierId);
        var canManage = user.UserId is { } me && await permissions.HasPermissionAsync(me, Permissions.Forms.CatalogsManage, ct);
        return new FormCatalogResponse(catalog.Id, catalog.Name, catalog.Description, catalog.Source, catalog.ValueType,
            ToResponse(catalog.Filters), state.Roles, state.Companies, sample, catalog.IsActive, catalog.IsActive && reason is null,
            reason, canManage, new FormCatalogItemCounts(catalog.TotalItems, catalog.ActiveItems), catalog.ModifiedAtUtc, modifiedBy,
            catalog.Version);
    }

    private static Models.FormCatalogFilters? ToResponse(CatalogFilters? saved) => saved is null ? null : new(saved.SourceVersion,
        [.. saved.Conditions.Select(condition => new Models.FormCatalogCondition(condition.Column, condition.Operator, condition.Values))],
        saved.ExcludeRecipient, saved.Detail,
        saved.Sort is null ? null : new Models.FormCatalogFilterSort(saved.Sort.Column, saved.Sort.Descending));
}
