using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.StructuredData;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.StructuredData;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Catalogs;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Domain.Forms;
using CatalogCondition = ArquitecturaBaseMultitenant.Domain.Forms.FormCatalogCondition;
using CatalogFilters = ArquitecturaBaseMultitenant.Domain.Forms.FormCatalogFilters;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;

/// <summary>
/// Arma la vista previa de unos filtros ya validados (§4, §4b y flujo 6 de la E12), con la persona o el cliente de ejemplo como
/// destinatario, sin tomar su identidad ni escribir nada. Trae hasta 200 opciones: la fuente lee una fila más para avisar
/// <c>exceedsLimit</c>, y solo entonces cuenta el total exacto. Un nombre de más de 30 caracteres se corta con la regla de la 36
/// y va entero en el detalle; el detalle pedido (roles, empresas o etiquetas) va separado por comas.
/// </summary>
internal sealed class FormCatalogPreviewBuilder(IMemberDirectoryReader members, IStructuredContactSource contacts)
{
    private const string Separator = " · ";

    public async Task<FormCatalogPreviewResponse> BuildAsync(FormCatalogSource source, CatalogFilters filters, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filters);
        if (source == FormCatalogSource.Members)
        {
            var list = await members.ListAsync(MembersQuery(filters, FormCatalogLimits.PreviewOptions), ct);
            return new FormCatalogPreviewResponse([.. list.Rows.Select(row => Option(row.Name, row.Details))], list.Total, list.ExceedsLimit);
        }

        if (source != FormCatalogSource.Contacts) throw new ArgumentOutOfRangeException(nameof(source), source, "Only a filtered catalog has a preview.");
        var page = await contacts.ListAsync(ContactsQuery(filters, FormCatalogLimits.PreviewOptions), ct);
        var withTags = filters.Detail == ContactsCatalogSource.Tags;
        return new FormCatalogPreviewResponse([.. page.Contacts.Select(contact => Option(contact.Name, withTags ? contact.Tags : []))],
            page.Total, page.ExceedsLimit);
    }

    /// <summary>Las condiciones de «Miembros» en la consulta del directorio; las columnas y los valores ya los validó la fuente.</summary>
    public static MemberDirectoryQuery MembersQuery(CatalogFilters filters, int limit)
    {
        ArgumentNullException.ThrowIfNull(filters);
        var conditions = filters.Conditions;
        return new MemberDirectoryQuery(limit)
        {
            Names = [.. conditions.Where(condition => condition.Column == MembersCatalogSource.Name)
                .Select(condition => new MemberDirectoryNameCondition(condition.Values[0], condition.Operator == FormCatalogOperators.EqualTo))],
            Roles = [.. Ids(conditions, MembersCatalogSource.Role)],
            Companies = [.. Ids(conditions, MembersCatalogSource.Company)],
            Active = [.. conditions.Where(condition => condition.Column == MembersCatalogSource.Active)
                .Select(condition => condition.Values[0] == "true")],
            ExcludedMemberId = filters.ExcludeRecipient ? filters.SampleId : null,
            Detail = filters.Detail switch
            {
                MembersCatalogSource.Role => MemberDirectoryDetail.Roles,
                MembersCatalogSource.Company => MemberDirectoryDetail.Companies,
                _ => MemberDirectoryDetail.None,
            },
            Descending = filters.Sort?.Descending ?? false,
        };
    }

    /// <summary>Las condiciones de «Contactos» en la consulta del canal: por etiqueta, sin distinguir mayúsculas.</summary>
    public static StructuredContactQuery ContactsQuery(CatalogFilters filters, int limit)
    {
        ArgumentNullException.ThrowIfNull(filters);
        return new StructuredContactQuery(limit)
        {
            Tags = [.. filters.Conditions.Where(condition => condition.Column == ContactsCatalogSource.Tags)
                .Select(condition => new StructuredContactTagCondition(condition.Values, condition.Operator == FormCatalogOperators.HasAny))],
            ExcludedContactId = filters.ExcludeRecipient ? filters.SampleId : null,
            Descending = filters.Sort?.Descending ?? false,
        };
    }

    /// <summary>
    /// Una opción: el nombre como título, cortado con «…» si pasa los 30 caracteres (36), con el nombre entero en el detalle (11 y
    /// 21) delante de lo que se pidió mostrar. El detalle no pasa sus 300 caracteres.
    /// </summary>
    public static FormCatalogPreviewOption Option(string name, IReadOnlyList<string> details)
    {
        ArgumentNullException.ThrowIfNull(details);
        var title = FormOptionTitle.From(name);
        var shown = details.Count == 0 ? null : string.Join(", ", details);
        var detail = title.IsCut ? (shown is null ? name.Trim() : name.Trim() + Separator + shown) : shown;
        return new FormCatalogPreviewOption(title.Text, Cut(detail));
    }

    private static string? Cut(string? detail)
    {
        if (detail is null || detail.Length <= FormsTextLimits.OptionDetail) return detail;
        var length = FormsTextLimits.OptionDetail - FormOptionTitle.Ellipsis.Length;
        if (char.IsHighSurrogate(detail[length - 1])) length--;
        return detail[..length].TrimEnd() + FormOptionTitle.Ellipsis;
    }

    private static IEnumerable<MemberDirectoryIdCondition> Ids(IReadOnlyList<CatalogCondition> conditions, string column) =>
        conditions.Where(condition => condition.Column == column).Select(condition => new MemberDirectoryIdCondition(
            [.. condition.Values.Select(value => Guid.Parse(value, CultureInfo.InvariantCulture))], condition.Operator == FormCatalogOperators.HasAny));
}
