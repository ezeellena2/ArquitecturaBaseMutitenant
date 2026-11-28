using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
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
/// las fuentes (<see cref="FormCatalogFiltersPolicy"/>). Desde la E13, uno a mano guarda también sus campos, su presentación y los
/// valores de sus opciones (<see cref="FormCatalogFieldEditor"/>), bajo el bloqueo del catálogo y de las fuentes de sus campos, y lo
/// que no entra en WhatsApp se calcula al leer (<see cref="FormCatalogPresentationBuilder"/>).
/// </summary>
internal sealed class FormCatalogService(IFormCatalogReader reader, IFormCatalogRepository repository, FormCatalogItemEditor editor,
    FormCatalogFieldEditor fieldEditor, FormCatalogPresentationBuilder presentations, FormCatalogFiltersPolicy filters,
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
            if (await reader.FindAsync(catalogId, cancellationToken) is not { } catalog) return FormCatalogErrors.NotFound;
            return await presentations.WithIssuesAsync(catalog, await reader.ListItemsAsync(catalogId, request, cancellationToken),
                cancellationToken);
        });
    }

    public Task<Result<FormCatalogCreatedResponse>> CreateAsync(CreateFormCatalogRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync<FormCatalogCreatedResponse>(logger, clock, "CreateFormCatalog", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var saved = await unitOfWork.ExecuteInTransactionAsync(ct => CreateCoreAsync(request, ct), CommitPolicy.OnSuccess,
                cancellationToken);
            if (saved.IsFailure) return saved.Error;
            var (catalog, identities) = saved.Value;
            // La versión es la que trajo el INSERT, nunca la de una relectura (concurrencia.md).
            var summary = await reader.FindAsync(catalog.Id, cancellationToken)
                ?? throw new InvalidOperationException("The catalog that was just created can't be read.");
            return new FormCatalogCreatedResponse(await BuildAsync(summary with { Version = catalog.Version }, cancellationToken), identities);
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
            return new FormCatalogUpdatedResponse(saved.Value.Catalog.Version, saved.Value.Identities, saved.Value.FieldIdentities);
        });
    }

    /// <summary>
    /// Bloqueos, lecturas, reglas y escrituras, en ese orden: las fuentes de los campos se bloquean antes de leerlas, y los filtros
    /// se deciden contra su fuente dentro de la transacción. Los campos nacen sin valores.
    /// </summary>
    private async Task<Result<(FormCatalog Catalog, IReadOnlyList<FormCatalogItemIdentity> Identities)>> CreateCoreAsync(
        CreateFormCatalogRequest request, CancellationToken ct)
    {
        var sources = FormCatalogFieldEditor.SourcesOf(request.Fields);
        if (sources.Count > 0) await repository.LockAsync(sources, ct);
        var catalog = await NewCatalogAsync(request, ct);
        if (catalog.IsFailure) return catalog.Error;
        repository.Add(catalog.Value);
        var changes = FormCatalogItemChanges.Of(request);
        var fields = await fieldEditor.ChangeAsync(catalog.Value, request.Fields, request.Presentation, changes, [], ct);
        if (fields.IsFailure) return fields.Error;
        IReadOnlyList<FormCatalogItemIdentity> none = [];
        if (changes.Items.Count == 0) return (catalog.Value, none);
        var applied = await editor.ApplyAsync(catalog.Value, changes, ct);
        if (applied.IsFailure) return applied.Error;
        return (catalog.Value, applied.Value);
    }

    /// <summary>
    /// Bloqueo, lecturas, reglas y escrituras, en ese orden. «Dar de baja» y «Reactivar» mandan el mismo guardado con lo pendiente:
    /// reactivar va antes de cambiar opciones y dar de baja, después, así un catálogo que sigue de baja no cambia sus opciones. Los
    /// campos y la presentación van antes de las opciones, que guardan los valores de los campos activos de este guardado; un campo
    /// que cambia de tipo o de regla convierte y comprueba antes los valores guardados de todas las opciones. Desde la E13, uno que es
    /// fuente de campos activos de otros catálogos no se da de baja, quitar un campo con valores se confirma y una opción que es valor
    /// de otra no se borra (<see cref="FormCatalogFieldEditor"/>).
    /// </summary>
    private async Task<Result<(FormCatalog Catalog, IReadOnlyList<FormCatalogItemIdentity> Identities,
        IReadOnlyList<FormCatalogFieldIdentity> FieldIdentities)>> UpdateCoreAsync(UpdateFormCatalogRequest request, CancellationToken ct)
    {
        await repository.LockWithSourcesAsync(request.CatalogId, FormCatalogFieldEditor.SourcesOf(request.Fields), ct);
        var catalog = await repository.GetByIdAsync(request.CatalogId, request.Version!.Value, ct);
        if (catalog is null) return FormCatalogErrors.NotFound;
        // Con una versión vieja, el 409 común antes de cualquier regla de las opciones: la pantalla abre el conflicto (concurrencia.md).
        if (catalog.Version != request.Version) throw new ConcurrencyConflictException("The catalog changed since it was read.");
        if (request.IsActive!.Value) catalog.Reactivate();
        else
        {
            // Dar de baja una fuente de campos activos es un error que se corrige, no un conflicto (lección 2).
            var deactivable = await fieldEditor.CheckDeactivationAsync(catalog, ct);
            if (deactivable.IsFailure) return deactivable.Error;
        }

        catalog.Rename(request.Name!, request.Description);
        var changes = FormCatalogItemChanges.Of(request);
        var fields = await fieldEditor.ChangeAsync(catalog, request.Fields, request.Presentation, changes,
            request.ConfirmedFieldRemovals ?? [], ct);
        if (fields.IsFailure) return fields.Error;

        IReadOnlyList<FormCatalogItemIdentity> identities = [];
        if (request.ChangesItems)
        {
            var applied = await editor.ApplyAsync(catalog, changes, ct);
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
        return (catalog, identities, fields.Value);
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

    private static FormCatalogRow ToRow(FormCatalogStoredRow catalog, FormCatalogUnusableReason? reason) =>
        new FormCatalogRow(catalog.Id, catalog.Name, catalog.Description, catalog.Source, catalog.ValueType,
            catalog.Source == FormCatalogSource.Manual ? catalog.TotalItems : null, catalog.IsActive, catalog.IsActive && reason is null,
            reason);

    private async Task<FormCatalogResponse> BuildAsync(FormCatalogStoredRow catalog, CancellationToken ct)
    {
        var state = (await filters.DecideStatesAsync([catalog], ct))[catalog.Id];
        var reason = state.Reason;
        var sample = catalog.Filters?.SampleId is { } sampleId ? await filters.FindSampleAsync(catalog.Source, sampleId, ct) : null;
        string? modifiedBy = null;
        if (catalog.ModifiedBy is { } modifierId) modifiedBy = (await names.FindAsync([modifierId], ct)).GetValueOrDefault(modifierId);
        var canManage = user.UserId is { } me && await permissions.HasPermissionAsync(me, Permissions.Forms.CatalogsManage, ct);
        var (fields, presentation, issues) = await FieldsOfAsync(catalog, ct);
        return new FormCatalogResponse(catalog.Id, catalog.Name, catalog.Description, catalog.Source, catalog.ValueType,
            ToResponse(catalog.Filters), state.Roles, state.Companies, sample, catalog.IsActive, catalog.IsActive && reason is null,
            reason, canManage, new FormCatalogItemCounts(catalog.TotalItems, catalog.ActiveItems), catalog.ModifiedAtUtc, modifiedBy,
            catalog.Version, fields, presentation, issues);
    }

    /// <summary>
    /// Los campos del detalle en su orden, primero los activos y después los quitados, con el nombre de su fuente y cuántas opciones
    /// tienen un valor; lo que arma el título y la descripción en WhatsApp, y cuántas opciones activas no entran.
    /// </summary>
    private async Task<(IReadOnlyList<FormCatalogFieldDetail> Fields, FormCatalogPresentationDetail Presentation,
        FormCatalogPresentationIssues Issues)> FieldsOfAsync(FormCatalogStoredRow catalog, CancellationToken ct)
    {
        var stored = await reader.FindFieldsAsync(catalog.Id, ct);
        if (stored is null) return ([], FormCatalogPresentationDetail.Default, FormCatalogPresentationIssues.None);
        var fields = FormCatalogField.ListFromJson(stored.FieldsJson).OrderBy(field => field.Position).ToList();
        var presentation = FormCatalogPresentation.FromJson(stored.Presentation);
        Guid[] sourceIds = [.. fields.Select(field => field.SourceCatalogId).OfType<Guid>().Distinct()];
        var sources = (await reader.ListFieldSourcesAsync(sourceIds, ct)).ToDictionary(source => source.Id, source => source.Name);
        return ([.. fields.Select(field => new FormCatalogFieldDetail(field.Id, field.Key, field.Name, field.Type, field.SourceCatalogId,
                field.SourceCatalogId is { } sourceId ? sources.GetValueOrDefault(sourceId) : null, field.Required, field.Unique,
                field.MaxLength, field.Min, field.Max, field.IsActive, stored.ValuesCount.GetValueOrDefault(field.Key)))],
            new FormCatalogPresentationDetail(presentation.Title, presentation.Description),
            await presentations.IssuesAsync(catalog, presentation, fields, ct));
    }

    private static Models.FormCatalogFilters? ToResponse(CatalogFilters? saved) => saved is null ? null : new(saved.SourceVersion,
        [.. saved.Conditions.Select(condition => new Models.FormCatalogCondition(condition.Column, condition.Operator, condition.Values))],
        saved.ExcludeRecipient, saved.Detail,
        saved.Sort is null ? null : new Models.FormCatalogFilterSort(saved.Sort.Column, saved.Sort.Descending));
}
