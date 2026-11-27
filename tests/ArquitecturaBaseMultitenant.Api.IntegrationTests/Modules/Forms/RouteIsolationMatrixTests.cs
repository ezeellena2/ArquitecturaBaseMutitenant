using ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Forms;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// La parte de Formularios en la matriz (E12): el catálogo de otra organización responde 404 en cada ruta con id, igual que uno
/// inventado, sin datos ni efectos; la revisión y aplicar una importación también, con el archivo que mandaría el front.
/// </summary>
public sealed partial class RouteIsolationMatrixTests
{
    private const string FormsRoot = "/api/organization/forms/catalogs";

    static partial void AddFormsCases(Dictionary<string, RouteCase> cases)
    {
        static string ForeignPath(World w) => $"{FormsRoot}/{w.B("forms.catalog")}";
        static Probe As(string path, object? body = null) => new(Caller.Business, path, body);

        cases[$"GET {FormsRoot}/{{id:guid}}"] = Foreign(w => As(ForeignPath(w)));
        cases[$"PUT {FormsRoot}/{{id:guid}}"] = Foreign(w => As(ForeignPath(w), new { name = "Matriz", isActive = true, version = 1 }));
        cases[$"GET {FormsRoot}/{{id:guid}}/items"] = Foreign(w => As(ForeignPath(w) + "/items"));
        cases[$"POST {FormsRoot}/{{id:guid}}/imports/previews"] = Foreign(w => As(ForeignPath(w) + "/imports/previews", ImportForm(null)));
        cases[$"POST {FormsRoot}/{{id:guid}}/imports"] = Foreign(w => As(ForeignPath(w) + "/imports", ImportForm(new string('0', 64))));
        cases[$"GET {FormsRoot}/{{id:guid}}/export"] = Foreign(w => As(ForeignPath(w) + "/export"));
    }

    static partial void AddFormsSeed(List<Func<World, Task>> seeds) => seeds.Add(SeedFormsAsync);

    private static async Task SeedFormsAsync(World world)
    {
        await FormsTestData.EnableAsync(world.Factory, world.OrganizationA);
        await FormsTestData.EnableAsync(world.Factory, world.OrganizationB);
        world.Own["forms.catalog"] = (await FormsTestData.CreateManualAsync(world.Factory, world.OrganizationA, "Matriz propia",
            ("A", "Matriz propia"))).Id;
        world.Foreign["forms.catalog"] = (await FormsTestData.CreateManualAsync(world.Factory, world.OrganizationB, world.OrganizationMark,
            ("B", "Matriz ajena"))).Id;
        world.OpenDoors.Add((Caller.Business, $"{FormsRoot}/{world.A("forms.catalog")}"));
        world.SeededTables.AddRange(["tenant.FormCatalogs", "tenant.FormCatalogItems"]);
    }

    /// <summary>Lo pegado, como lo manda el front: un CSV con sus encabezados, la versión y, al aplicar, el SHA-256 de la revisión.</summary>
    private static MultipartFormDataContent ImportForm(string? sha256)
    {
        var form = new MultipartFormDataContent
        {
            { new ByteArrayContent("\"Code\",\"Text\"\r\n\"Z\",\"Matriz\"\r\n"u8.ToArray()), "file", "pasted-rows.csv" },
            { new StringContent("Append"), "mode" },
            { new StringContent("1"), "version" },
        };
        if (sha256 is not null) form.Add(new StringContent(sha256), "sha256");
        return form;
    }
}
