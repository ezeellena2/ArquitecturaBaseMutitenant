using System.Globalization;
using System.Runtime.CompilerServices;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Resources;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;

/// <summary>
/// La carga de una vez de un catálogo a mano (C7 a C11). La revisión lee lo pegado o el archivo y lo compara con lo guardado sin
/// abrir transacción. Aplicar vuelve a leer los mismos bytes fuera de la transacción, compara su SHA-256 y, en una sola UoW, toma
/// el bloqueo del catálogo, comprueba la versión, revalida el permiso y aplica todo junto: altas al final, cambios por código y,
/// al reemplazar, bajas de lo que no vino, con las confirmaciones que correspondan. Exportar toma la barrera de la persona, fuera
/// de toda UoW, y lee en lotes; la plantilla no la toma, porque no lee opciones.
/// </summary>
internal sealed class FormCatalogImportService(IFormCatalogReader reader, IFormCatalogRepository repository,
    FormCatalogFileResolver resolver, IFormCatalogSpreadsheet spreadsheet, IFormWorkLease leases, ICultureCatalog cultures,
    IPermissionService permissions, ICurrentUser user, IRequestValidator validator, IUnitOfWork unitOfWork, TimeProvider clock,
    ILogger<FormCatalogImportService> logger) : IFormCatalogImportService
{
    /// <summary>Las opciones que se leen por consulta al exportar o al revisar un reemplazo.</summary>
    internal const int BatchSize = 1_000;

    public Task<Result<FormCatalogImportPreviewResponse>> PreviewAsync(FormCatalogImportPreviewRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync<FormCatalogImportPreviewResponse>(logger, clock, "PreviewFormCatalogImport", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var catalog = await reader.FindAsync(request.CatalogId, cancellationToken);
            if (catalog is null) return FormCatalogErrors.NotFound;
            var allowed = CanChange(catalog);
            if (allowed.IsFailure) return allowed.Error;
            if (catalog.Version != request.Version) throw new ConcurrencyConflictException("The catalog changed since it was read.");

            var file = await resolver.ResolveAsync(request.FileName, request.Content!, catalog.ValueType, cancellationToken);
            if (file.IsFailure) return file.Error;
            var saved = request.Mode == FormCatalogImportMode.Replace
                ? await ReadAllAsync(catalog.Id, cancellationToken)
                : await ReadByCodesAsync(catalog.Id, file.Value, cancellationToken);
            var plan = FormCatalogImportPolicy.Decide(catalog.ValueType, request.Mode!.Value, file.Value, saved, catalog.ActiveItems);
            return plan.ToPreview(catalog.Version, file.Value.Sha256, file.Value.SheetName, request.Page, request.PageSize);
        });
    }

    public Task<Result<VersionResponse>> ApplyAsync(ApplyFormCatalogImportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync<VersionResponse>(logger, clock, "ApplyFormCatalogImport", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var userId = user.UserId ?? throw new InvalidOperationException("Applying an import needs a signed-in person.");
            // El tipo no cambia desde el alta: los bytes se leen antes de abrir la transacción.
            var summary = await reader.FindAsync(request.CatalogId, cancellationToken);
            if (summary is null) return FormCatalogErrors.NotFound;
            var file = await resolver.ResolveAsync(request.FileName, request.Content!, summary.ValueType, cancellationToken);
            if (file.IsFailure) return file.Error;
            if (!string.Equals(file.Value.Sha256, request.Sha256!.Trim(), StringComparison.OrdinalIgnoreCase))
                return FormCatalogImportErrors.FileChanged;

            var saved = await unitOfWork.ExecuteInTransactionAsync(ct => ApplyCoreAsync(request, userId, file.Value, ct),
                CommitPolicy.OnSuccess, cancellationToken);
            if (saved.IsFailure) return saved.Error;
            return new VersionResponse(saved.Value.Version);
        });
    }

    public Task<Result<FormCatalogFileResponse>> ExportAsync(Guid catalogId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<FormCatalogFileResponse>(logger, clock, "ExportFormCatalog", async () =>
        {
            var userId = user.UserId ?? throw new InvalidOperationException("Exporting a catalog needs a signed-in person.");
            var lease = await leases.TryTakeAsync(userId, FormWorkLeaseKind.Export, cancellationToken);
            if (lease.IsFailure) return lease.Error;
            await using (lease.Value)
            {
                var catalog = await reader.FindAsync(catalogId, cancellationToken);
                if (catalog is null) return FormCatalogErrors.NotFound;
                if (catalog.Source != FormCatalogSource.Manual) return FormCatalogErrors.WrongSource;
                var profile = await RequestProfileAsync(cancellationToken);
                var content = await spreadsheet.WriteAsync(FormCatalogExportBuilder.Layout(catalog.ValueType, withNotes: false),
                    ExportRowsAsync(catalog, profile, cancellationToken), cancellationToken);
                return new FormCatalogFileResponse(new MemoryStream(content), FormCatalogExportBuilder.MediaType,
                    FormCatalogExportBuilder.FileName(catalog.Name));
            }
        });

    public Task<Result<FormCatalogFileResponse>> GetTemplateAsync(FormCatalogTemplateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync<FormCatalogFileResponse>(logger, clock, "GetFormCatalogTemplate", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var valueType = request.ValueType ?? FormCatalogValueType.Text;
            var profile = await RequestProfileAsync(cancellationToken);
            var content = await spreadsheet.WriteAsync(FormCatalogExportBuilder.Layout(valueType, withNotes: true),
                Single(FormCatalogExportBuilder.Example(valueType, profile)), cancellationToken);
            return new FormCatalogFileResponse(new MemoryStream(content), FormCatalogExportBuilder.MediaType,
                FormsTexts.Get("CatalogImport.TemplateFileName"));
        });
    }

    /// <summary>
    /// Bloqueo, lecturas, reglas y escrituras, en ese orden. Lo que se escribe se vuelve a decidir con lo guardado bajo el bloqueo,
    /// con las mismas reglas que la revisión: la versión igual garantiza que es lo mismo que se revisó.
    /// </summary>
    private async Task<Result<FormCatalog>> ApplyCoreAsync(ApplyFormCatalogImportRequest request, Guid userId, FormCatalogFile file,
        CancellationToken ct)
    {
        var catalog = await repository.GetByIdAsync(request.CatalogId, request.Version!.Value, ct);
        if (catalog is null) return FormCatalogErrors.NotFound;
        if (catalog.Version != request.Version) throw new ConcurrencyConflictException("The catalog changed since it was reviewed.");
        if (!await permissions.HasPermissionAsync(userId, Permissions.Forms.CatalogsManage, ct)) return AccessErrors.Wrong;
        var allowed = catalog.EnsureItemsCanChange();
        if (allowed.IsFailure) return allowed.Error;

        var replace = request.Mode == FormCatalogImportMode.Replace;
        var items = replace
            ? await repository.ListAllItemsAsync(catalog.Id, ct)
            : await repository.ListItemsByCodesAsync(catalog.Id, Codes(file), ct);
        var saved = items.Select(Snapshot).ToList();
        var plan = FormCatalogImportPolicy.Decide(catalog.ValueType, request.Mode!.Value, file, saved, saved.Count(item => item.IsActive));
        if (plan.ErrorRows > 0 && request.SkipErrorRows != true) return FormCatalogImportErrors.ConfirmationRequired;
        if (plan.ConfirmDeactivations && request.ConfirmedDeactivations != plan.Deactivations.Count)
            return FormCatalogImportErrors.ConfirmationRequired;

        var additions = plan.Writes.Count(write => write.ItemId is null);
        var total = replace ? items.Count : await repository.CountItemsAsync(catalog.Id, ct);
        if (total + additions > FormCatalogLimits.MaxItems) return FormCatalogErrors.TooManyItems;

        var byId = items.ToDictionary(item => item.Id);
        var next = (replace ? (items.Count == 0 ? null : items.Max(item => item.Position)) : await repository.FindLastPositionAsync(catalog.Id, ct))
            ?? -1;
        foreach (var write in plan.Writes)
        {
            if (write.ItemId is { } itemId)
            {
                var edited = byId[itemId].Edit(catalog, write.Row, write.Code, write.Text, write.Detail, write.Value);
                if (edited.IsFailure) return edited.Error;
                continue;
            }

            var created = FormCatalogItem.Create(catalog, write.Row, write.Code, write.Text, write.Detail, write.Value, isActive: true,
                position: ++next);
            if (created.IsFailure) return created.Error;
            repository.Add(created.Value);
        }

        foreach (var itemId in plan.Deactivations) byId[itemId].Deactivate();
        repository.MarkUpdated(catalog);
        return catalog;
    }

    private static Result CanChange(FormCatalogStoredRow catalog)
    {
        if (catalog.Source != FormCatalogSource.Manual) return FormCatalogErrors.WrongSource;
        return catalog.IsActive ? Result.Success() : FormCatalogErrors.Inactive;
    }

    /// <summary>Todas las opciones, en lotes por posición: un reemplazo da de baja lo que no vino.</summary>
    private async Task<List<FormCatalogStoredItemRow>> ReadAllAsync(Guid catalogId, CancellationToken ct)
    {
        var all = new List<FormCatalogStoredItemRow>();
        await foreach (var batch in BatchesAsync(catalogId, ct)) all.AddRange(batch);
        return all;
    }

    /// <summary>Lo pegado agrega: solo hacen falta las opciones con los códigos que vinieron.</summary>
    private async Task<List<FormCatalogStoredItemRow>> ReadByCodesAsync(Guid catalogId, FormCatalogFile file, CancellationToken ct)
    {
        var found = new List<FormCatalogStoredItemRow>();
        foreach (var chunk in Codes(file).Chunk(BatchSize)) found.AddRange(await reader.ListItemsByCodesAsync(catalogId, chunk, ct));
        return found;
    }

    private async IAsyncEnumerable<IReadOnlyList<FormCatalogStoredItemRow>> BatchesAsync(Guid catalogId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        int? after = null;
        while (true)
        {
            var batch = await reader.ListItemBatchAsync(catalogId, after, BatchSize, ct);
            if (batch.Count > 0) yield return batch;
            if (batch.Count < BatchSize) yield break;
            after = batch[^1].Position;
        }
    }

    private async IAsyncEnumerable<IReadOnlyList<FormCatalogCell>> ExportRowsAsync(FormCatalogStoredRow catalog, CultureProfile profile,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var batch in BatchesAsync(catalog.Id, ct))
            foreach (var item in batch) yield return FormCatalogExportBuilder.Row(item, catalog.ValueType, profile);
    }

    private static async IAsyncEnumerable<IReadOnlyList<FormCatalogCell>> Single(IReadOnlyList<FormCatalogCell> row)
    {
        await Task.CompletedTask;
        yield return row;
    }

    private Task<CultureProfile> RequestProfileAsync(CancellationToken ct) =>
        new CultureProfiles(cultures).LoadAsync(CultureInfo.CurrentUICulture.Name, ct);

    private static string[] Codes(FormCatalogFile file) =>
        [.. file.Rows.Where(row => row.Code is { Length: <= FormsTextLimits.OptionCode }).Select(row => FormCatalogItem.NormalizeCode(row.Code!))
            .Distinct(StringComparer.Ordinal)];

    private static FormCatalogStoredItemRow Snapshot(FormCatalogItem item) => new(item.Id, item.Code, item.CodeNormalized, item.Text,
        item.Detail, item.ValueText(), item.Position, item.IsActive);
}
