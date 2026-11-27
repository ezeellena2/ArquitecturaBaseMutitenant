using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Catalogs;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;
using ArquitecturaBaseMultitenant.Domain.Forms;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Forms;

/// <summary>
/// Las opciones de la vista previa (§4 y flujo 6 de la E12): un nombre de más de 30 caracteres se corta con «…» (36) y va entero
/// en el detalle (11 y 21), delante de lo que se pidió mostrar; el detalle pedido va separado por comas y no pasa sus 300
/// caracteres; las condiciones llegan al directorio con «Sin incluir a quien lo recibe».
/// </summary>
public sealed class FormCatalogPreviewBuilderTests
{
    [Fact]
    public void A_long_name_is_cut_and_goes_whole_in_the_detail_before_what_was_asked_to_show()
    {
        const string name = "María de los Ángeles Fernández Ruiz";

        var cut = FormCatalogPreviewBuilder.Option(name, []);
        Assert.Equal(30, cut.Text.Length);
        Assert.EndsWith("…", cut.Text, StringComparison.Ordinal);
        Assert.Equal(name, cut.Detail);

        var withRoles = FormCatalogPreviewBuilder.Option(name, ["Ventas", "Compras"]);
        Assert.Equal(name + " · Ventas, Compras", withRoles.Detail);

        var shortName = FormCatalogPreviewBuilder.Option("Laura Ríos", ["VIP", "Mayorista"]);
        Assert.Equal(("Laura Ríos", "VIP, Mayorista"), (shortName.Text, shortName.Detail));
        Assert.Null(FormCatalogPreviewBuilder.Option("Laura Ríos", []).Detail);
    }

    [Fact]
    public void The_detail_never_passes_its_300_characters()
    {
        var detail = FormCatalogPreviewBuilder.Option("Laura", [.. Enumerable.Range(0, 60).Select(index => $"Rol {index:00}")]).Detail!;

        Assert.Equal(FormsTextLimits.OptionDetail, detail.Length);
        Assert.EndsWith(FormOptionTitle.Ellipsis, detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Member_conditions_reach_the_directory_with_the_recipient_left_out()
    {
        var sales = Guid.NewGuid();
        var laura = Guid.NewGuid();
        var filters = new FormCatalogFilters(MembersCatalogSource.Version,
        [
            new FormCatalogCondition(MembersCatalogSource.Role, FormCatalogOperators.HasNone, [sales.ToString()]),
            new FormCatalogCondition(MembersCatalogSource.Name, FormCatalogOperators.EqualTo, ["Laura Ríos"]),
            new FormCatalogCondition(MembersCatalogSource.Active, FormCatalogOperators.Is, ["false"]),
        ], ExcludeRecipient: true, MembersCatalogSource.Company, new FormCatalogFilterSort(MembersCatalogSource.Name, true), laura);

        var query = FormCatalogPreviewBuilder.MembersQuery(filters, 200);

        Assert.Equal(200, query.Limit);
        Assert.Equal(laura, query.ExcludedMemberId);
        Assert.Equal([sales], Assert.Single(query.Roles).Ids);
        Assert.False(query.Roles[0].Include);
        Assert.Equal(new MemberDirectoryNameCondition("Laura Ríos", Exact: true), Assert.Single(query.Names));
        Assert.Equal([false], query.Active);
        Assert.Equal(MemberDirectoryDetail.Companies, query.Detail);
        Assert.True(query.Descending);
        Assert.Null(FormCatalogPreviewBuilder.MembersQuery(filters with { ExcludeRecipient = false }, 200).ExcludedMemberId);
    }
}
