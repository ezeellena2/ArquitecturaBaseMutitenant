using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Forms;

/// <summary>
/// La carga de una vez (E12, C7 a C11) por el servicio, con la base real: la revisión no escribe y pagina sus listas; aplicar otro
/// archivo da <c>FileChanged</c>; las filas con error y más del 25 % de bajas piden su confirmación; una versión vieja da el 409
/// común en la revisión y al aplicar; reemplazar da de baja lo que no vino y pegar agrega al final; exportar con la barrera de la
/// persona tomada da <c>Busy</c>. Desde la E13, una opción de la fuente dada de baja no frena guardar, importar ni exportar el
/// catálogo que la tiene como valor (lección 1).
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class FormCatalogImportServiceTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_preview_does_not_write_and_pages_its_lists()
    {
        var organization = await OrganizationAsync();
        var catalog = await CreateManualAsync(organization, "Talles", [("S", "Chico"), ("M", "Mediano"), ("L", "Grande")]);
        var csv = Csv(["S;Chiquito", "L;Grande", .. Enumerable.Range(1, 12).Select(n => $"N{n:00};Nuevo {n}")]);

        var first = Value(await RunAsync(organization, service => service.PreviewAsync(Preview(catalog, csv, FormCatalogImportMode.Replace), Ct)));
        var second = Value(await RunAsync(organization, service =>
            service.PreviewAsync(Preview(catalog, csv, FormCatalogImportMode.Replace) with { Page = 2 }, Ct)));

        Assert.Equal((catalog.Version, Sha(csv), 14, 1), (first.Version, first.Sha256, first.RowsRead, first.Unchanged));
        Assert.Equal(new FormCatalogImportChangedRow(2, "S", "text", "Chico", "Chiquito"), Assert.Single(first.Changed.Items));
        Assert.Equal(new FormCatalogImportDeactivatedRow("M", "Mediano"), Assert.Single(first.Deactivated.Items));
        Assert.Equal((0.3333m, true, 0m, false), (first.DeactivationShare, first.ConfirmDeactivations, first.ErrorShare, first.ConfirmErrors));
        Assert.Equal(10, first.Added.Items.Count);
        Assert.Equal([new FormCatalogImportAddedRow(14, "N11", "Nuevo 11"), new FormCatalogImportAddedRow(15, "N12", "Nuevo 12")],
            second.Added.Items);
        Assert.Equal((12, 1, 1), (second.Added.TotalCount, second.Changed.TotalCount, second.Deactivated.TotalCount));
        Assert.Empty(second.Changed.Items);

        // Nada se guardó: la misma versión y las mismas opciones.
        Assert.Equal(catalog.Version, (await GetAsync(organization, catalog.Id)).Version);
        Assert.Equal([("S", "Chico", true), ("M", "Mediano", true), ("L", "Grande", true)], await ItemsAsync(organization, catalog.Id));
    }

    [Fact]
    public async Task Applying_another_file_than_the_reviewed_one_is_file_changed()
    {
        var organization = await OrganizationAsync();
        var catalog = await CreateManualAsync(organization, "Colores", [("R", "Rojo")]);
        var reviewed = Csv(["V;Verde"]);
        var other = Csv(["A;Azul"]);
        var preview = Value(await RunAsync(organization, service => service.PreviewAsync(Preview(catalog, reviewed, FormCatalogImportMode.Append), Ct)));

        var applied = await RunAsync(organization, service =>
            service.ApplyAsync(Apply(catalog, other, FormCatalogImportMode.Append, preview.Sha256), Ct));

        Assert.Equal(FormCatalogImportErrors.FileChangedCode, applied.Error.Code);
        Assert.Equal(ErrorType.Conflict, applied.Error.Type);
        Assert.Equal(catalog.Version, (await GetAsync(organization, catalog.Id)).Version);
    }

    [Fact]
    public async Task Rows_with_errors_need_skip_error_rows_and_are_left_out()
    {
        var organization = await OrganizationAsync();
        var catalog = await CreateManualAsync(organization, "Zonas", [("A", "Norte"), ("B", "Sur")]);
        // C es nueva y no trae texto: CellRequired.
        var csv = Csv(["A;Norte", "C;", "D;Oeste"]);
        var preview = Value(await RunAsync(organization, service => service.PreviewAsync(Preview(catalog, csv, FormCatalogImportMode.Append), Ct)));
        Assert.Equal(new FormCatalogImportErrorRow(3, "text", FormCatalogImportErrors.CellRequiredCode, null, null), Assert.Single(preview.Errors.Items));
        Assert.True(preview.ConfirmErrors);

        var refused = await RunAsync(organization, service =>
            service.ApplyAsync(Apply(catalog, csv, FormCatalogImportMode.Append, preview.Sha256), Ct));
        Assert.Equal((FormCatalogImportErrors.ConfirmationRequiredCode, ErrorType.Validation), (refused.Error.Code, refused.Error.Type));
        Assert.Equal(catalog.Version, (await GetAsync(organization, catalog.Id)).Version);

        var applied = Value(await RunAsync(organization, service =>
            service.ApplyAsync(Apply(catalog, csv, FormCatalogImportMode.Append, preview.Sha256) with { SkipErrorRows = true }, Ct)));
        Assert.NotEqual(catalog.Version, applied.Version);
        Assert.Equal([("A", "Norte", true), ("B", "Sur", true), ("D", "Oeste", true)], await ItemsAsync(organization, catalog.Id));
    }

    [Fact]
    public async Task More_than_a_quarter_of_deactivations_needs_their_exact_count()
    {
        var organization = await OrganizationAsync();
        var catalog = await CreateManualAsync(organization, "Medios", [("A", "Uno"), ("B", "Dos"), ("C", "Tres"), ("D", "Cuatro")]);
        var csv = Csv(["A;Uno", "B;Dos"]);
        var preview = Value(await RunAsync(organization, service => service.PreviewAsync(Preview(catalog, csv, FormCatalogImportMode.Replace), Ct)));
        Assert.Equal((0.5m, true), (preview.DeactivationShare, preview.ConfirmDeactivations));

        foreach (int? confirmed in new int?[] { null, 1, 3 })
        {
            var refused = await RunAsync(organization, service => service.ApplyAsync(
                Apply(catalog, csv, FormCatalogImportMode.Replace, preview.Sha256) with { ConfirmedDeactivations = confirmed }, Ct));
            Assert.Equal(FormCatalogImportErrors.ConfirmationRequiredCode, refused.Error.Code);
        }
        Assert.Equal(catalog.Version, (await GetAsync(organization, catalog.Id)).Version);

        Value(await RunAsync(organization, service => service.ApplyAsync(
            Apply(catalog, csv, FormCatalogImportMode.Replace, preview.Sha256) with { ConfirmedDeactivations = 2 }, Ct)));
        Assert.Equal([("A", "Uno", true), ("B", "Dos", true), ("C", "Tres", false), ("D", "Cuatro", false)],
            await ItemsAsync(organization, catalog.Id));
    }

    [Fact]
    public async Task An_old_version_is_a_concurrency_conflict_in_the_preview_and_when_applying()
    {
        var organization = await OrganizationAsync();
        var catalog = await CreateManualAsync(organization, "Turnos", [("M", "Mañana")]);
        var csv = Csv(["T;Tarde"]);
        var preview = Value(await RunAsync(organization, service => service.PreviewAsync(Preview(catalog, csv, FormCatalogImportMode.Append), Ct)));

        // Otra pantalla guardó el catálogo después de la revisión.
        Value(await RunAsync<IFormCatalogService, Result<FormCatalogUpdatedResponse>>(organization, service => service.UpdateAsync(
            new UpdateFormCatalogRequest(catalog.Id, catalog.Name, catalog.Description, true, catalog.Version, null, null, null, null, null,
                null, null), Ct)));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => RunAsync(organization, service =>
            service.PreviewAsync(Preview(catalog, csv, FormCatalogImportMode.Append), Ct)));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => RunAsync(organization, service =>
            service.ApplyAsync(Apply(catalog, csv, FormCatalogImportMode.Append, preview.Sha256), Ct)));
        Assert.Equal([("M", "Mañana", true)], await ItemsAsync(organization, catalog.Id));
    }

    [Fact]
    public async Task Replacing_deactivates_what_did_not_come_and_pasting_adds_at_the_end()
    {
        var organization = await OrganizationAsync();
        var catalog = await CreateManualAsync(organization, "Planes",
            [("A", "Uno"), ("B", "Dos"), ("C", "Tres"), ("D", "Cuatro"), ("E", "Cinco")]);

        // Una baja de cinco no pide confirmar.
        var replacement = Csv(["F;Seis", "D;Cuatro", "C;Tres", "B;Dos bis", "A;Uno"]);
        var replaced = await ApplyReviewedAsync(organization, catalog.Id, catalog.Version, replacement, FormCatalogImportMode.Replace);
        Assert.Equal([("A", "Uno", true), ("B", "Dos bis", true), ("C", "Tres", true), ("D", "Cuatro", true), ("E", "Cinco", false),
            ("F", "Seis", true)], await ItemsAsync(organization, catalog.Id));

        // Lo pegado agrega al final y cambia lo que ya estaba, sin dar de baja nada.
        var pasted = Csv(["G;Siete", "A;Uno bis"]);
        await ApplyReviewedAsync(organization, catalog.Id, replaced, pasted, FormCatalogImportMode.Append);
        Assert.Equal([("A", "Uno bis", true), ("B", "Dos bis", true), ("C", "Tres", true), ("D", "Cuatro", true), ("E", "Cinco", false),
            ("F", "Seis", true), ("G", "Siete", true)], await ItemsAsync(organization, catalog.Id));
    }

    [Fact]
    public async Task Exporting_with_the_barrier_taken_is_busy()
    {
        var organization = await OrganizationAsync();
        var catalog = await CreateManualAsync(organization, "Talles", [("S", "Chico")]);

        var busy = await RunAsync<IFormWorkLease, Result<FormCatalogFileResponse>>(organization, async leases =>
        {
            var lease = await leases.TryTakeAsync(organization.OwnerId, FormWorkLeaseKind.Preview, Ct);
            Assert.True(lease.IsSuccess);
            await using (lease.Value)
                return await RunAsync(organization, service => service.ExportAsync(catalog.Id, Ct));
        });
        Assert.Equal(FormCatalogImportErrors.BusyCode, busy.Error.Code);

        var exported = Value(await RunAsync(organization, service => service.ExportAsync(catalog.Id, Ct)));
        Assert.Equal(("Talles.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
            (exported.FileName, exported.MediaType));
        await exported.Content.DisposeAsync();
    }

    /// <summary>
    /// Lección 1 de la E13: dar de baja la opción de la fuente no frena al catálogo que la tiene como valor. «Proveedores» se sigue
    /// guardando, importando y exportando, y conserva ese valor: también cuando la columna «Zona» la nombra por su texto, mientras
    /// otra fila pasa a una opción activa por su código. Lo exportado se vuelve a revisar sin diferencias.
    /// </summary>
    [Fact]
    public async Task A_deactivated_source_option_does_not_block_saving_importing_or_exporting_and_its_value_is_kept()
    {
        var organization = await OrganizationAsync();
        var zones = await CreateManualAsync(organization, "Zonas", [("N", "Norte"), ("S", "Sur")]);
        var (north, south) = ((await RowsAsync(organization, zones.Id))[0], (await RowsAsync(organization, zones.Id))[1]);
        var created = Value(await RunAsync<IFormCatalogService, Result<FormCatalogCreatedResponse>>(organization, service => service.CreateAsync(
            new CreateFormCatalogRequest("Proveedores", null, FormCatalogSource.Manual, null,
                [new FormCatalogItemInput(null, "n0", "P1", "Uno", null, null, true)], null, null, null,
                [new FormCatalogFieldInput(null, "zona", null, "Zona", FormCatalogFieldType.Catalog, zones.Id, false, false, null, null, null)]),
            Ct))).Catalog;
        var zona = created.Fields[0].Id;
        var supplier = (await RowsAsync(organization, created.Id))[0];
        var version = await SaveAsync(organization, created, created.Version,
            [Edit(supplier, new Dictionary<Guid, JsonElement?> { [zona] = JsonSerializer.SerializeToElement(north.Id.ToString("D")) })]);
        await SaveAsync(organization, zones, zones.Version, [new FormCatalogItemInput(north.Id, null, north.Code, north.Text, null, null, false)]);

        version = await SaveAsync(organization, created, version, [Edit(supplier with { Text = "Uno editado" }, null)]);
        version = await ApplyReviewedAsync(organization, created.Id, version, Csv(["P1;Uno importado", "P2;Dos"]), FormCatalogImportMode.Append);
        version = await ApplyReviewedAsync(organization, created.Id, version,
            Encoding.UTF8.GetBytes("Código;Texto;Zona\r\nP1;Uno importado;Norte\r\nP2;Dos;s"), FormCatalogImportMode.Append);
        var exported = Value(await RunAsync(organization, service => service.ExportAsync(created.Id, Ct)));
        using var workbook = new MemoryStream();
        await exported.Content.CopyToAsync(workbook, Ct);
        await exported.Content.DisposeAsync();

        var rows = await RowsAsync(organization, created.Id);
        Assert.Equal(("Uno importado", north.Id.ToString("D")), (rows[0].Text, rows[0].Fields[zona].GetString()));
        Assert.Equal(south.Id.ToString("D"), rows[1].Fields[zona].GetString());
        Assert.Equal(2, rows.Count);
        var again = Value(await RunAsync(organization, service => service.PreviewAsync(new FormCatalogImportPreviewRequest
        {
            CatalogId = created.Id, FileName = "Proveedores.xlsx", Content = new MemoryStream(workbook.ToArray()),
            Mode = FormCatalogImportMode.Replace, Version = version,
        }, Ct)));
        Assert.Equal((2, 2, 0, 0), (again.RowsRead, again.Unchanged, again.Changed.TotalCount, again.Errors.TotalCount));
    }

    /// <summary>
    /// E13: la plantilla pedida con <c>catalogId</c> suma una columna por campo activo, con su nombre y antes del detalle, como la
    /// exportación (O14); un catálogo que no es de la organización es <c>NotFound</c>.
    /// </summary>
    [Fact]
    public async Task The_template_of_a_catalog_adds_its_field_columns_and_an_unknown_catalog_is_not_found()
    {
        var organization = await OrganizationAsync();
        var created = Value(await RunAsync<IFormCatalogService, Result<FormCatalogCreatedResponse>>(organization, service => service.CreateAsync(
            new CreateFormCatalogRequest("Proveedores", null, FormCatalogSource.Manual, null,
                [new FormCatalogItemInput(null, "n0", "P1", "Uno", null, null, true)], null, null, null,
                [new FormCatalogFieldInput(null, "cuit", null, "CUIT", FormCatalogFieldType.Text, null, false, false, null, null, null),
                    new FormCatalogFieldInput(null, "plazo", null, "Plazo de pago", FormCatalogFieldType.WholeNumber, null, false, false, null, null, null)]),
            Ct))).Catalog;

        var template = Value(await RunAsync(organization, service => service.GetTemplateAsync(new FormCatalogTemplateRequest(null, created.Id), Ct)));
        var exported = Value(await RunAsync(organization, service => service.ExportAsync(created.Id, Ct)));
        var missing = await RunAsync(organization, service => service.GetTemplateAsync(new FormCatalogTemplateRequest(null, Guid.NewGuid()), Ct));

        var headers = await HeadersAsync(organization, template);
        Assert.Equal(["CUIT", "Plazo de pago"], headers.Skip(2).Take(2));
        Assert.Equal(5, headers.Count);
        Assert.Equal(headers, await HeadersAsync(organization, exported));
        Assert.Equal(FormCatalogErrors.NotFoundCode, missing.Error.Code);
    }

    private Task<IReadOnlyList<string>> HeadersAsync(Organization organization, FormCatalogFileResponse file) =>
        RunAsync<IFormCatalogSpreadsheet, IReadOnlyList<string>>(organization, async spreadsheet =>
        {
            await using var content = file.Content;
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, Ct);
            return spreadsheet.Read(file.FileName, copy.ToArray(), FormCatalogFileResolver.Limits).Headers;
        });

    private async Task<uint> SaveAsync(Organization organization, FormCatalogResponse catalog, uint version, FormCatalogItemInput[] items) =>
        Value(await RunAsync<IFormCatalogService, Result<FormCatalogUpdatedResponse>>(organization, service => service.UpdateAsync(
            new UpdateFormCatalogRequest(catalog.Id, catalog.Name, null, true, version, items, null, null, null, null, null, null), Ct))).Version;

    private static FormCatalogItemInput Edit(FormCatalogItemRow item, Dictionary<Guid, JsonElement?>? fields) =>
        new(item.Id, null, item.Code, item.Text, item.Detail, item.Value, item.IsActive, fields);

    private async Task<uint> ApplyReviewedAsync(Organization organization, Guid catalogId, uint version, byte[] csv,
        FormCatalogImportMode mode)
    {
        var preview = Value(await RunAsync(organization, service => service.PreviewAsync(new FormCatalogImportPreviewRequest
        {
            CatalogId = catalogId, FileName = "pegado.csv", Content = new MemoryStream(csv), Mode = mode, Version = version,
        }, Ct)));
        Assert.False(preview.ConfirmDeactivations);
        return Value(await RunAsync(organization, service => service.ApplyAsync(new ApplyFormCatalogImportRequest(catalogId, "pegado.csv",
            new MemoryStream(csv), mode, preview.Sha256, version, null, null), Ct))).Version;
    }

    private static FormCatalogImportPreviewRequest Preview(FormCatalogResponse catalog, byte[] csv, FormCatalogImportMode mode) => new()
    {
        CatalogId = catalog.Id, FileName = "pegado.csv", Content = new MemoryStream(csv), Mode = mode, Version = catalog.Version,
    };

    private static ApplyFormCatalogImportRequest Apply(FormCatalogResponse catalog, byte[] csv, FormCatalogImportMode mode, string sha256) =>
        new(catalog.Id, "pegado.csv", new MemoryStream(csv), mode, sha256, catalog.Version, null, null);

    /// <summary>Lo pegado viaja como un CSV con los encabezados en la primera fila (C7).</summary>
    private static byte[] Csv(IEnumerable<string> rows) => Encoding.UTF8.GetBytes(string.Join("\r\n", ["Código;Texto", .. rows]));

    private static string Sha(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));

    private static T Value<T>(Result<T> result)
    {
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return result.Value;
    }

    private async Task<FormCatalogResponse> CreateManualAsync(Organization organization, string name, List<(string Code, string Text)> items)
    {
        var request = new CreateFormCatalogRequest(name, null, FormCatalogSource.Manual, null,
            [.. items.Select((item, index) => new FormCatalogItemInput(null, $"n{index}", item.Code, item.Text, null, null, true))], null, null,
            null);
        return Value(await RunAsync<IFormCatalogService, Result<FormCatalogCreatedResponse>>(organization,
            service => service.CreateAsync(request, Ct))).Catalog;
    }

    private async Task<FormCatalogResponse> GetAsync(Organization organization, Guid catalogId) =>
        Value(await RunAsync<IFormCatalogService, Result<FormCatalogResponse>>(organization, service => service.GetAsync(catalogId, Ct)));

    /// <summary>Las opciones por su posición, con el código, el texto y si están activas.</summary>
    private async Task<(string Code, string Text, bool IsActive)[]> ItemsAsync(Organization organization, Guid catalogId) =>
        [.. (await RowsAsync(organization, catalogId)).Select(row => (row.Code, row.Text, row.IsActive))];

    /// <summary>Las opciones por su posición, enteras.</summary>
    private async Task<IReadOnlyList<FormCatalogItemRow>> RowsAsync(Organization organization, Guid catalogId)
    {
        var rows = new List<FormCatalogItemRow>();
        for (var page = 1; ; page++)
        {
            var current = Value(await RunAsync<IFormCatalogService, Result<PagedResult<FormCatalogItemRow>>>(
                organization, service => service.ListItemsAsync(catalogId,
                    new ListFormCatalogItemsRequest { Page = page, PageSize = 10, Status = FormCatalogStatusFilter.All }, Ct)));
            rows.AddRange(current.Items);
            if (rows.Count >= current.TotalCount) break;
        }
        return [.. rows.OrderBy(row => row.Position)];
    }

    private async Task<Organization> OrganizationAsync()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Importar " + Guid.NewGuid().ToString("N")[..8], Ct);
        return new Organization(tenantId, owner.UserId);
    }

    private Task<T> RunAsync<T>(Organization organization, Func<IFormCatalogImportService, Task<T>> run) =>
        RunAsync<IFormCatalogImportService, T>(organization, run);

    /// <summary>
    /// Un alcance con la organización de la sesión de empresa y la persona dueña, como lo arma el pedido HTTP: aplicar revalida el
    /// permiso, que se lee solo con el acceso de empresa.
    /// </summary>
    private async Task<T> RunAsync<TService, T>(Organization organization, Func<TService, Task<T>> run) where TService : notnull
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var accessor = services.GetRequiredService<IHttpContextAccessor>();
        var previous = accessor.HttpContext;
        accessor.HttpContext = new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", organization.OwnerId.ToString())], "Integration")),
        };
        try
        {
            services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(organization.TenantId, TenantKind.Business);
            return await run(services.GetRequiredService<TService>());
        }
        finally { accessor.HttpContext = previous; }
    }

    private sealed record Organization(Guid TenantId, Guid OwnerId);
}
