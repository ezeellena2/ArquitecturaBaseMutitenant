using System.Globalization;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Forms;

/// <summary>
/// La acción de las opciones de un catálogo (§17 de la E12) resuelve las opciones que nombra el modelo con una sola lectura de sus
/// parecidos, aunque nombre las 500 que admite: una lectura por opción eran 1.500 consultas en un pedido.
/// </summary>
public sealed class FormCatalogActionSourceTests
{
    private const string Catalog = "Colores";
    private const int Named = FormCatalogLimits.MaxDeleteItems;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Naming_the_most_options_reads_their_candidates_once()
    {
        var reader = new CatalogReader(Enumerable.Range(1, Named + 20)
            .Select(index => new FormCatalogItemCandidateRow(string.Empty, Guid.NewGuid(), $"C{index:D3}", $"Opción {index:D3}")).ToArray());
        var source = new FormCatalogActionSource(reader, []);
        var options = Enumerable.Range(1, Named).Select(index => new { op = "change", key = $"c{index:D3}", row = new { active = false } });
        var draft = new AssistantActionDraft(FormCatalogActionSource.Actions[0], Catalog,
            [new(FormCatalogActionSource.OptionsField, JsonSerializer.SerializeToElement(options))]);

        var resolved = await source.ResolveAsync(draft, Ct);

        Assert.True(resolved.IsSuccess, resolved.IsFailure ? resolved.Error.Code : null);
        Assert.Equal(1, reader.CandidateReads);
        Assert.Equal(0, reader.ItemPages);
        var ids = resolved.Value.Values.Single().Resolved!.Value.EnumerateArray().Select(op => op.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(reader.Items.Take(Named).Select(item => item.Id), ids);
    }

    [Fact]
    public async Task A_missing_option_goes_back_to_the_model_and_the_others_use_the_same_read()
    {
        CatalogReader reader = new([new(string.Empty, Guid.NewGuid(), "AZU", "Azul"), new(string.Empty, Guid.NewGuid(), "AZM", "Azul marino")]);
        var source = new FormCatalogActionSource(reader, []);
        object[] options = [new { op = "change", key = "azul", row = new { detail = "El del cielo" } }, new { op = "remove", key = "Verde" }];
        var draft = new AssistantActionDraft(FormCatalogActionSource.Actions[0], Catalog,
            [new(FormCatalogActionSource.OptionsField, JsonSerializer.SerializeToElement(options))]);

        var resolved = await source.ResolveAsync(draft, Ct);

        // «Verde» no está: la segunda fila vuelve al modelo, y la primera se resolvió con la misma lectura.
        Assert.True(resolved.IsFailure);
        var errors = Assert.IsType<ValidationError>(resolved.Error).Errors;
        Assert.Equal([FormCatalogErrors.ItemNotFoundCode], Assert.Single(errors, error => error.Key == $"{FormCatalogActionSource.OptionsField}[1]").Value);
        Assert.Single(errors);
        Assert.Equal(1, reader.CandidateReads);
    }

    private sealed class CatalogReader(IReadOnlyList<FormCatalogItemCandidateRow> items) : IFormCatalogReader
    {
        private readonly FormCatalogStoredRow _catalog = new(Guid.NewGuid(), Catalog, null, FormCatalogSource.Manual, FormCatalogValueType.Text,
            null, IsActive: true, items.Count, items.Count, new DateTime(2026, 11, 27, 12, 0, 0, DateTimeKind.Utc), null, 7);

        internal IReadOnlyList<FormCatalogItemCandidateRow> Items => items;

        internal int CandidateReads { get; private set; }

        internal int ItemPages { get; private set; }

        public Task<PagedResult<FormCatalogStoredRow>> ListAsync(ListFormCatalogsRequest request, CancellationToken ct) =>
            Task.FromResult(new PagedResult<FormCatalogStoredRow>([_catalog], 1, request.PageSize, 1));

        public Task<FormCatalogStoredRow?> FindAsync(Guid catalogId, CancellationToken ct) =>
            Task.FromResult<FormCatalogStoredRow?>(catalogId == _catalog.Id ? _catalog : null);

        public Task<PagedResult<FormCatalogItemRow>> ListItemsAsync(Guid catalogId, ListFormCatalogItemsRequest request, CancellationToken ct)
        {
            ItemPages++;
            FormCatalogItemRow[] found = [.. Matching(request.Search!).Take(request.PageSize)
                .Select(item => new FormCatalogItemRow(item.Id, item.Code, item.Text, null, null, 0, true, false, FormCatalogItemRow.NoFields, null))];
            return Task.FromResult(new PagedResult<FormCatalogItemRow>(found, 1, request.PageSize, found.Length));
        }

        public Task<IReadOnlyList<FormCatalogItemCandidateRow>> ListItemCandidatesAsync(Guid catalogId, IReadOnlyCollection<string> searches,
            int perSearch, CancellationToken ct)
        {
            CandidateReads++;
            return Task.FromResult<IReadOnlyList<FormCatalogItemCandidateRow>>([.. searches.Distinct(StringComparer.Ordinal)
                .SelectMany(search => Matching(search).Take(perSearch).Select(item => item with { Search = search }))]);
        }

        public Task<FormCatalogFilterCountsResponse> CountFiltersAsync(CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<FormCatalogStoredItemRow>> ListItemBatchAsync(Guid catalogId, int? afterPosition, int take,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<FormCatalogStoredItemRow>> ListItemsByIdsAsync(Guid catalogId, IReadOnlyCollection<Guid> itemIds,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<FormCatalogStoredItemRow>> ListItemsByCodesAsync(Guid catalogId, IReadOnlyCollection<string> normalizedCodes,
            CancellationToken ct) => throw new NotSupportedException();

        private IEnumerable<FormCatalogItemCandidateRow> Matching(string search) => items.Where(item =>
            CultureInfo.InvariantCulture.CompareInfo.IndexOf(item.Code, search, CompareOptions.IgnoreCase) >= 0
            || CultureInfo.InvariantCulture.CompareInfo.IndexOf(item.Text, search, CompareOptions.IgnoreCase) >= 0);
    }
}
