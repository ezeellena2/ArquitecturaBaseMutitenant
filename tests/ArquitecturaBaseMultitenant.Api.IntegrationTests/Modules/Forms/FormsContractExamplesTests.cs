using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Catalogs;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Domain.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Forms;

/// <summary>
/// Los ejemplos de una corrida real de los catálogos (api-http, desde la E12): cada respuesta JSON de cada ruta y cada error de
/// negocio que documenta openapi.json, grabados en <c>docs/contracts/examples/forms.json</c>, que el front valida con zod estricto
/// (<c>npm run contracts:check</c>). La guarda <c>FormsContractExamplesCoverageTests</c> pide uno por cada respuesta.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class FormsContractExamplesTests(ApiFactory factory)
{
    private const string Root = FormsTestData.Catalogs;
    private const string WithId = Root + "/{id}";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly ContractExampleRecorder _recorder = new("forms.json");
    private HttpClient _client = null!;

    [Fact]
    public async Task A_real_run_records_every_response_and_business_error_of_the_catalogs()
    {
        var (client, tenantId, ownerId) = await FormsTestData.BusinessAsync(factory, "Ejemplos de catálogos");
        using var http = client;
        _client = client;
        using (var me = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct))
            if (me!.RootElement.GetProperty("displayName").GetString() is { Length: > 0 } ownerName) _recorder.Replace(ownerName, "Laura Ríos");
        var companyId = await OrganizationJourney.CreateCompanyAsync(client, "Sucursal Norte", Ct);
        await OrganizationJourney.AddNamedMemberAsync(factory, tenantId, "Ana Gómez", Ct);

        // Las fuentes con filtros.
        await GetAsync("La fuente «Miembros»", Root + "/sources/members", Root + "/sources/members");
        var roles = await GetAsync("Roles", Root + "/sources/members/roles", Root + "/sources/members/roles?pageSize=10");
        var roleId = roles.GetProperty("items")[0].GetProperty("id").GetGuid();
        await GetAsync("Roles con un orden que no existe", Root + "/sources/members/roles", Root + "/sources/members/roles?sort=text");
        await GetAsync("Empresas", Root + "/sources/members/companies", Root + "/sources/members/companies");
        await GetAsync("Empresas con un orden que no existe", Root + "/sources/members/companies", Root + "/sources/members/companies?sort=text");
        await GetAsync("Etiquetas de los contactos", Root + "/sources/contacts/tags", Root + "/sources/contacts/tags");
        await GetAsync("Etiquetas con un orden que no existe", Root + "/sources/contacts/tags", Root + "/sources/contacts/tags?sort=text");
        var samples = await GetAsync("Personas de ejemplo", Root + "/sample-members", Root + "/sample-members?search=Ana");
        var memberId = samples.GetProperty("items")[0].GetProperty("memberId").GetGuid();
        await GetAsync("Personas de ejemplo con un orden que no existe", Root + "/sample-members", Root + "/sample-members?sort=text");
        await GetAsync("Clientes de ejemplo", Root + "/sample-contacts", Root + "/sample-contacts");
        await GetAsync("Clientes de ejemplo con un orden que no existe", Root + "/sample-contacts", Root + "/sample-contacts?sort=text");

        object Filters(string column = MembersCatalogSource.Company) => new
        {
            sourceVersion = MembersCatalogSource.Version,
            conditions = new[] { new { column, @operator = FormCatalogOperators.HasAny, values = new[] { companyId.ToString() } } },
            excludeRecipient = true, detail = MembersCatalogSource.Role, sort = new { column = MembersCatalogSource.Name, descending = false },
        };
        object ContactFilters() => new
        {
            sourceVersion = ContactsCatalogSource.Version,
            conditions = new[] { new { column = ContactsCatalogSource.Tags, @operator = FormCatalogOperators.HasAny, values = new[] { "VIP" } } },
            excludeRecipient = false,
        };

        // La vista previa de unos filtros sin guardar.
        const string Previews = Root + "/previews";
        await PostAsync("Vista previa", Previews, Previews, new
        {
            source = "Members",
            filters = new
            {
                sourceVersion = MembersCatalogSource.Version,
                conditions = new[] { new { column = MembersCatalogSource.Name, @operator = FormCatalogOperators.Contains, values = new[] { "Gómez" } } },
                excludeRecipient = false, detail = MembersCatalogSource.Company,
            },
            sampleMemberId = memberId,
        });
        await PostAsync("Vista previa sin la persona de ejemplo", Previews, Previews, new { source = "Members", filters = Filters() });
        await PostAsync("Vista previa con una columna que la fuente no tiene", Previews, Previews,
            new { source = "Members", filters = Filters(column: "email"), sampleMemberId = memberId });
        await PostAsync("Vista previa con una persona de ejemplo que no está", Previews, Previews,
            new { source = "Members", filters = Filters(), sampleMemberId = Guid.NewGuid() });
        await PostAsync("Vista previa con un cliente de ejemplo que no está", Previews, Previews,
            new { source = "Contacts", filters = ContactFilters(), sampleContactId = Guid.NewGuid() });
        await PostAsync("Vista previa sin origen", Previews, Previews, new { filters = Filters(), sampleMemberId = memberId });

        // El alta.
        var created = await PostAsync("Alta a mano", Root, Root, new
        {
            name = "Talles", description = "Los talles de la ropa de trabajo", source = "Manual",
            items = new object[]
            {
                new { localId = "n1", code = "S", text = "Chico", isActive = true },
                new { localId = "n2", code = "M", text = "Mediano", detail = "El más pedido", isActive = true },
                new { localId = "n3", code = "L", text = "Grande", isActive = true },
            },
        }, idempotent: true);
        var catalogId = created.GetProperty("catalog").GetProperty("id").GetGuid();
        var catalogVersion = created.GetProperty("catalog").GetProperty("version").GetUInt32();
        var members = await PostAsync("Alta de miembros con filtros", Root, Root,
            new { name = "Equipo del Norte", source = "Members", filters = Filters(), sampleMemberId = memberId }, idempotent: true);
        var membersId = members.GetProperty("catalog").GetProperty("id").GetGuid();
        var openMembers = await PostAsync("Alta de miembros sin condiciones", Root, Root, new
        {
            name = "Todo el equipo", source = "Members",
            filters = new { sourceVersion = MembersCatalogSource.Version, conditions = Array.Empty<object>(), excludeRecipient = false },
        }, idempotent: true);
        var contacts = await PostAsync("Alta de contactos", Root, Root, new
        {
            name = "Clientes VIP", source = "Contacts",
            filters = new { sourceVersion = ContactsCatalogSource.Version, conditions = Array.Empty<object>(), excludeRecipient = false },
        }, idempotent: true);
        var typed = await PostAsync("Alta con valores enteros", Root, Root, new
        {
            name = "Cuotas", source = "Manual", valueType = "WholeNumber",
            items = new[] { new { localId = "n1", code = "3", text = "Tres cuotas", value = "3", isActive = true } },
        }, idempotent: true);
        await PostAsync("Alta con un código repetido", Root, Root, Manual(("A", "Uno"), ("A", "Otro")), idempotent: true);
        await PostAsync("Alta con una columna que la fuente no tiene", Root, Root,
            new { name = "Mal filtrado", source = "Members", filters = Filters(column: "email"), sampleMemberId = memberId }, idempotent: true);
        await PostAsync("Alta con un detalle de más de 300 caracteres", Root, Root, new
        {
            name = "Detalle largo", source = "Manual",
            items = new[] { new { localId = "n1", code = "A", text = "Uno", detail = new string('d', 301), isActive = true } },
        }, idempotent: true);
        await PostAsync("Alta con un texto de más de 30 caracteres", Root, Root, Manual(("A", new string('t', 31))), idempotent: true);
        await PostAsync("Alta con un cliente de ejemplo que no está", Root, Root,
            new { name = "Clientes", source = "Contacts", filters = ContactFilters(), sampleContactId = Guid.NewGuid() }, idempotent: true);
        await PostAsync("Alta con una persona de ejemplo que no está", Root, Root,
            new { name = "Equipo", source = "Members", filters = Filters(), sampleMemberId = Guid.NewGuid() }, idempotent: true);
        await PostAsync("Alta de miembros con condiciones y sin persona de ejemplo", Root, Root,
            new { name = "Equipo", source = "Members", filters = Filters() }, idempotent: true);
        await PostAsync("Alta con un valor que no es un número entero", Root, Root, new
        {
            name = "Cuotas", source = "Manual", valueType = "WholeNumber",
            items = new[] { new { localId = "n1", code = "A", text = "Uno", value = "tres", isActive = true } },
        }, idempotent: true);
        await PostAsync("Alta sin el valor de una opción", Root, Root, new
        {
            name = "Cuotas", source = "Manual", valueType = "WholeNumber",
            items = new[] { new { localId = "n1", code = "A", text = "Uno", isActive = true } },
        }, idempotent: true);
        await PostAsync("Alta sin nombre", Root, Root, new { source = "Manual" }, idempotent: true);
        await PostAsync("Alta de miembros con opciones", Root, Root,
            new { name = "Equipo", source = "Members", filters = Filters(), sampleMemberId = memberId, items = Manual(("A", "Uno")).Items },
            idempotent: true);
        await PostAsync("Alta con más de 10.000 opciones", Root, Root,
            Manual([.. Enumerable.Range(1, FormCatalogLimits.MaxItems + 1).Select(index => ($"C{index}", $"Opción {index}"))]), idempotent: true);

        // El listado, su resumen, el detalle y las opciones.
        await GetAsync("Listado", Root, Root + "?sort=name");
        await GetAsync("Listado con un orden que no existe", Root, Root + "?sort=color");
        await GetAsync("Resumen del listado", Root + "/filter-counts", Root + "/filter-counts");
        await GetAsync("Detalle", WithId, $"{Root}/{catalogId}");
        await GetAsync("Detalle de miembros", WithId, $"{Root}/{membersId}");
        await GetAsync("Detalle de un catálogo que no está", WithId, $"{Root}/{Guid.NewGuid()}");
        var items = await GetAsync("Opciones", WithId + "/items", $"{Root}/{catalogId}/items?status=All");
        var firstItem = items.GetProperty("items")[0].GetProperty("id").GetGuid();
        await GetAsync("Opciones con un tamaño de página que no existe", WithId + "/items", $"{Root}/{catalogId}/items?pageSize=7");
        await GetAsync("Opciones de un catálogo que no está", WithId + "/items", $"{Root}/{Guid.NewGuid()}/items");

        // Guardar todo junto.
        var saved = await PutAsync("Guardar", catalogId, new
        {
            name = "Talles de ropa", description = "Los talles de la ropa de trabajo", isActive = true, version = catalogVersion,
            items = new object[] { new { localId = "n4", code = "XL", text = "Muy grande", isActive = true } },
        });
        catalogVersion = saved.GetProperty("version").GetUInt32();
        object Save(object? items = null, object? moves = null, object? deleteItems = null, uint? version = null, bool isActive = true) => new
        {
            name = "Talles de ropa", isActive, version = version ?? catalogVersion, items, moves, deleteItems,
        };
        await PutAsync("Guardar con un código repetido", catalogId, Save(items: new[] { Item("S", "Otro chico") }));
        await PutAsync("Guardar con un detalle de más de 300 caracteres", catalogId,
            Save(items: new[] { new { localId = "n9", code = "XS", text = "Muy chico", detail = new string('d', 301), isActive = true } }));
        await PutAsync("Guardar con un texto de más de 30 caracteres", catalogId, Save(items: new[] { Item("XS", new string('t', 31)) }));
        await PutAsync("Guardar sin el estado", catalogId, new { name = "Talles de ropa", version = catalogVersion });
        await PutAsync("Guardar borrando una opción que no está", catalogId, Save(deleteItems: new[] { Guid.NewGuid() }));
        await PutAsync("Guardar un catálogo que no está", Guid.NewGuid(), Save());
        await PutAsync("Guardar subiendo la primera opción", catalogId, Save(moves: new[] { new { itemId = firstItem, direction = "Up" } }));
        await PutAsync("Guardar con filtros un catálogo a mano", catalogId, new
        {
            name = "Talles de ropa", isActive = true, version = catalogVersion, filters = Filters(), sampleMemberId = memberId,
        });
        await PutAsync("Guardar con una versión vieja", catalogId, Save(version: 1));
        var membersVersion = members.GetProperty("catalog").GetProperty("version").GetUInt32();
        await PutAsync("Guardar con una columna que la fuente no tiene", membersId, new
        {
            name = "Equipo del Norte", isActive = true, version = membersVersion, filters = Filters(column: "email"),
        });
        await PutAsync("Guardar con una persona de ejemplo que no está", membersId, new
        {
            name = "Equipo del Norte", isActive = true, version = membersVersion, filters = Filters(), sampleMemberId = Guid.NewGuid(),
        });
        var openMembersCatalog = openMembers.GetProperty("catalog");
        await PutAsync("Guardar condiciones sin persona de ejemplo", openMembersCatalog.GetProperty("id").GetGuid(), new
        {
            name = "Todo el equipo", isActive = true, version = openMembersCatalog.GetProperty("version").GetUInt32(), filters = Filters(),
        });
        var contactsCatalog = contacts.GetProperty("catalog");
        await PutAsync("Guardar con un cliente de ejemplo que no está", contactsCatalog.GetProperty("id").GetGuid(), new
        {
            name = "Clientes VIP", isActive = true, version = contactsCatalog.GetProperty("version").GetUInt32(), filters = ContactFilters(),
            sampleContactId = Guid.NewGuid(),
        });
        var typedCatalog = typed.GetProperty("catalog");
        var typedId = typedCatalog.GetProperty("id").GetGuid();
        var typedVersion = typedCatalog.GetProperty("version").GetUInt32();
        await PutAsync("Guardar un valor que no es un número entero", typedId, new
        {
            name = "Cuotas", isActive = true, version = typedVersion,
            items = new[] { new { localId = "n2", code = "6", text = "Seis cuotas", value = "seis", isActive = true } },
        });
        await PutAsync("Guardar sin el valor de una opción", typedId, new
        {
            name = "Cuotas", isActive = true, version = typedVersion, items = new[] { Item("6", "Seis cuotas") },
        });
        var inactive = await FormsTestData.CreateManualAsync(factory, tenantId, "Viejo", ("A", "Uno"));
        var inactiveVersion = (await PutAsync(null, inactive.Id, new { name = "Viejo", isActive = false, version = inactive.Version }))
            .GetProperty("version").GetUInt32();
        await PutAsync("Guardar opciones de un catálogo dado de baja", inactive.Id, new
        {
            name = "Viejo", isActive = false, version = inactiveVersion, items = new[] { Item("B", "Dos") },
        });
        var full = await FormsTestData.CreateManualAsync(factory, tenantId, "Lleno",
            [.. Enumerable.Range(1, FormCatalogLimits.MaxItems).Select(index => ($"C{index}", $"Opción {index}"))]);
        await PutAsync("Guardar una opción más en un catálogo con 10.000", full.Id, new
        {
            name = "Lleno", isActive = true, version = full.Version, items = new[] { Item("NUEVA", "Una más") },
        });

        // Importar: lo pegado viaja como un CSV.
        var pasted = Csv("\"Code\",\"Text\"", "\"XXL\",\"Enorme\"");
        var review = await PreviewAsync("Revisar lo pegado", catalogId, pasted, "Append", catalogVersion);
        var sha = review.GetProperty("sha256").GetString()!;
        catalogVersion = review.GetProperty("version").GetUInt32();
        var withErrors = Csv("\"Code\",\"Text\"", "\"XS\",\"Muy chico\"", "\"XS\",\"Repetido\"");
        await PreviewAsync("Revisar con una fila con errores", catalogId, withErrors, "Append", catalogVersion);
        await PreviewAsync("Revisar un archivo que no es una planilla", catalogId, "no es una planilla"u8.ToArray(), "Replace", catalogVersion,
            fileName: "opciones.xlsx");
        await PreviewAsync("Revisar sin el modo", catalogId, pasted, null, catalogVersion);
        await PreviewAsync("Revisar en un catálogo que no está", Guid.NewGuid(), pasted, "Append", catalogVersion);
        await PreviewAsync("Revisar en un catálogo dado de baja", inactive.Id, pasted, "Append", inactiveVersion);
        await PreviewAsync("Revisar en un catálogo de miembros", membersId, pasted, "Append", membersVersion);
        await PreviewAsync("Revisar con una versión vieja", catalogId, pasted, "Append", 1);
        await using (await TakeAsync(tenantId, ownerId, FormWorkLeaseKind.Export))
        {
            await PreviewAsync("Revisar con otra operación de archivo en curso", catalogId, pasted, "Append", catalogVersion);
            await ApplyAsync("Aplicar con otra operación de archivo en curso", catalogId, pasted, "Append", sha, catalogVersion);
            await GetAsync("Exportar con otra operación de archivo en curso", WithId + "/export", $"{Root}/{catalogId}/export");
        }
        await using (await TakeAsync(tenantId, Guid.NewGuid(), FormWorkLeaseKind.Preview))
        await using (await TakeAsync(tenantId, Guid.NewGuid(), FormWorkLeaseKind.Export))
        {
            await PreviewAsync("Revisar con la organización sin lugar", catalogId, pasted, "Append", catalogVersion);
            await ApplyAsync("Aplicar con la organización sin lugar", catalogId, pasted, "Append", sha, catalogVersion);
            await GetAsync("Exportar con la organización sin lugar", WithId + "/export", $"{Root}/{catalogId}/export");
        }

        var errorsReview = await PreviewAsync(null, catalogId, withErrors, "Append", catalogVersion);
        await ApplyAsync("Aplicar filas con errores sin confirmarlas", catalogId, withErrors, "Append",
            errorsReview.GetProperty("sha256").GetString()!, catalogVersion);
        await ApplyAsync("Aplicar un archivo que no es una planilla", catalogId, "no es una planilla"u8.ToArray(), "Replace", sha, catalogVersion,
            fileName: "opciones.xlsx");
        await ApplyAsync("Aplicar sin el SHA-256 de la revisión", catalogId, pasted, "Append", null, catalogVersion);
        await ApplyAsync("Aplicar en un catálogo que no está", Guid.NewGuid(), pasted, "Append", sha, catalogVersion);
        await ApplyAsync("Aplicar en un catálogo dado de baja", inactive.Id, pasted, "Append", sha, inactiveVersion);
        await ApplyAsync("Aplicar en un catálogo de miembros", membersId, pasted, "Append", sha, membersVersion);
        await ApplyAsync("Aplicar otro archivo que el revisado", catalogId, withErrors, "Append", sha, catalogVersion);
        await ApplyAsync("Aplicar con una versión vieja", catalogId, pasted, "Append", sha, 1);
        var fullReview = await PreviewAsync(null, full.Id, pasted, "Append", full.Version);
        await ApplyAsync("Aplicar una opción más en un catálogo con 10.000", full.Id, pasted, "Append",
            fullReview.GetProperty("sha256").GetString()!, full.Version);
        await ApplyAsync("Aplicar lo revisado", catalogId, pasted, "Append", sha, catalogVersion);

        // Exportar y la plantilla: el archivo no es JSON y no se graba; sus errores sí.
        await GetAsync("Exportar un catálogo que no está", WithId + "/export", $"{Root}/{Guid.NewGuid()}/export");
        await GetAsync("Exportar un catálogo de miembros", WithId + "/export", $"{Root}/{membersId}/export");
        await GetAsync("Plantilla de un tipo que no existe", Root + "/template", Root + "/template?valueType=9");

        _recorder.Publish();
    }

    private static object Item(string code, string text) => new { localId = "n" + code, code, text, isActive = true };

    private static ManualRequest Manual(params (string Code, string Text)[] items) =>
        new("Catálogo", "Manual", [.. items.Select((item, index) => new ManualItem($"n{index}", item.Code, item.Text, true))]);

    private sealed record ManualRequest(string Name, string Source, ManualItem[] Items);

    private sealed record ManualItem(string LocalId, string Code, string Text, bool IsActive);

    private static byte[] Csv(params string[] rows) => Encoding.UTF8.GetBytes(string.Join("\r\n", rows) + "\r\n");

    private async Task<JsonElement> GetAsync(string name, string path, string url)
    {
        using var response = await _client.GetAsync(url, Ct);
        return await _recorder.RecordAsync(name, "GET", path, response, Ct);
    }

    private async Task<JsonElement> PostAsync(string name, string path, string url, object body, bool idempotent = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        if (idempotent) request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var response = await _client.SendAsync(request, Ct);
        return await _recorder.RecordAsync(name, "POST", path, response, Ct);
    }

    /// <summary>Un guardado; sin nombre, prepara la corrida y no se graba.</summary>
    private async Task<JsonElement> PutAsync(string? name, Guid catalogId, object body)
    {
        using var response = await _client.PutAsJsonAsync($"{Root}/{catalogId}", body, Ct);
        if (name is not null) return await _recorder.RecordAsync(name, "PUT", WithId, response, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.Clone();
    }

    private async Task<JsonElement> PreviewAsync(string? name, Guid catalogId, byte[] file, string? mode, uint version,
        string fileName = "pasted-rows.csv")
    {
        using var form = Form(file, fileName, mode, version);
        using var response = await _client.PostAsync($"{Root}/{catalogId}/imports/previews", form, Ct);
        if (name is not null) return await _recorder.RecordAsync(name, "POST", WithId + "/imports/previews", response, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.Clone();
    }

    private async Task ApplyAsync(string name, Guid catalogId, byte[] file, string mode, string? sha256, uint version,
        string fileName = "pasted-rows.csv")
    {
        using var form = Form(file, fileName, mode, version);
        if (sha256 is not null) form.Add(new StringContent(sha256), "sha256");
        form.Add(new StringContent("false"), "skipErrorRows");
        form.Add(new StringContent("0"), "confirmedDeactivations");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Root}/{catalogId}/imports") { Content = form };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var response = await _client.SendAsync(request, Ct);
        await _recorder.RecordAsync(name, "POST", WithId + "/imports", response, Ct);
    }

    private static MultipartFormDataContent Form(byte[] file, string fileName, string? mode, uint version)
    {
        var form = new MultipartFormDataContent { { new ByteArrayContent(file), "file", fileName } };
        if (mode is not null) form.Add(new StringContent(mode), "mode");
        form.Add(new StringContent(version.ToString(System.Globalization.CultureInfo.InvariantCulture)), "version");
        return form;
    }

    private async Task<IAsyncDisposable> TakeAsync(Guid tenantId, Guid userId, FormWorkLeaseKind kind)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        var taken = await scope.ServiceProvider.GetRequiredService<IFormWorkLease>().TryTakeAsync(userId, kind, Ct);
        Assert.True(taken.IsSuccess, taken.IsFailure ? taken.Error.Code : null);
        return taken.Value;
    }
}
