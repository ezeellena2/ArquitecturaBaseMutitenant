using System.Globalization;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Common.Text;
using ArquitecturaBaseMultitenant.Application.Models.Auditing;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Auditing;

/// <summary>
/// Arma las entradas de la auditoría de la organización como el tablero Auditoria-Org, en el idioma de quien mira.
/// Un campo va al detalle solo con su nombre (<c>Field.*</c> de Audit.resx) y un valor que se puede mostrar: un estado
/// con las palabras de las pantallas, una zona o una moneda como las muestran, un idioma en su propio idioma (como su
/// selector), los permisos y los roles por nombre, y un correo o un teléfono enmascarados. Lo demás (identificadores, fechas técnicas, canales, estados sin
/// palabra) queda fuera del detalle.
/// </summary>
internal sealed class OrganizationAuditEntryBuilder(DisplayFormatter formatter)
{
    private static readonly Dictionary<string, Type> StatusTypes = new(StringComparer.Ordinal)
    {
        [nameof(Member)] = typeof(MemberStatus),
        [nameof(CompanyMembership)] = typeof(CompanyMembershipStatus),
        [nameof(Company)] = typeof(CompanyStatus),
        [nameof(Invitation)] = typeof(InvitationStatus),
        [nameof(Tenant)] = typeof(TenantStatus),
    };

    internal async Task<IReadOnlyList<OrganizationAuditEntryResponse>> BuildAsync(
        IReadOnlyList<OrganizationAuditRow> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0) return [];
        var context = await formatter.CreateForCurrentCultureAsync(TimeZoneInfo.Utc.Id, cancellationToken);
        var entries = new List<OrganizationAuditEntryResponse>(rows.Count);
        foreach (var row in rows) entries.Add(await BuildAsync(row, context, cancellationToken));
        return entries;
    }

    private async Task<OrganizationAuditEntryResponse> BuildAsync(OrganizationAuditRow row,
        DisplayFormatContext context, CancellationToken cancellationToken)
    {
        using var document = Parse(row.Changes);
        var changes = document?.RootElement ?? default;
        // Un rol de sistema se lee con su nombre en el idioma de quien mira («Dueño»).
        var affected = row.EntityType == nameof(Role) && row.AffectedDisplayName is { } roleName
            ? RoleTexts.Name(roleName)
            : row.AffectedDisplayName;
        return new OrganizationAuditEntryResponse(row.Id, row.OccurredAtUtc, row.Action.ToString(),
            AuditTexts.Summary(row.Action, row.EntityType, affected, Variant(row, changes)),
            row.EntityType, AuditTexts.Entity(row.EntityType), affected,
            row.CompanyName ?? AuditTexts.WholeOrganization(),
            // Solo una persona va con su identificador: qué operador de Plataforma actuó es un dato de Plataforma.
            row.ActorKind.ToString(), AuditTexts.Actor(row.ActorKind),
            row.ActorKind == AuditActorKind.User ? row.ActorId : null,
            row.ActorKind == AuditActorKind.User ? row.ActorDisplayName : null,
            await ChangesAsync(row, changes, context, cancellationToken),
            row.ActorKind == AuditActorKind.User ? row.ActorEmail : null);
    }

    /// <summary>
    /// Un «Qué» más preciso que la acción, si tiene su texto (<see cref="AuditTexts.Summary"/>): un cambio de estado
    /// («Deshabilitó a…», «Habilitó a…» y «Quitó a…», como Usuarios, o «Venció la invitación a…») o el campo de un
    /// cambio de un solo campo («Cambió la zona de…», como el tablero).
    /// </summary>
    private static string? Variant(OrganizationAuditRow row, JsonElement changes)
    {
        if (row.Action != AuditAction.Updated || changes.ValueKind != JsonValueKind.Object) return null;
        if (changes.TryGetProperty("Status", out var status) && TryOldNew(status, out var oldValue, out var newValue)
            && (row.EntityType, StatusName(row.EntityType, oldValue), StatusName(row.EntityType, newValue)) switch
            {
                (nameof(Member), _, nameof(MemberStatus.Removed)) => nameof(AuditAction.Deleted),
                (nameof(Member), nameof(MemberStatus.Active), nameof(MemberStatus.Inactive)) => "Disabled",
                (nameof(Member), nameof(MemberStatus.Inactive), nameof(MemberStatus.Active)) => "Enabled",
                (nameof(Invitation), _, AuditChangeKeys.ExpiredInvitationStatus) => "Expired",
                (nameof(Tenant), nameof(TenantStatus.PendingApproval), nameof(TenantStatus.Active)) => "Approved",
                _ => null,
            } is { } statusChange) return statusChange;

        string[] fields = [.. changes.EnumerateObject().Select(field => field.Name).Take(2)];
        return fields.Length == 1 ? fields[0] : null;
    }

    private async Task<List<OrganizationAuditChangeResponse>> ChangesAsync(OrganizationAuditRow row,
        JsonElement changes, DisplayFormatContext context, CancellationToken cancellationToken)
    {
        var rows = new List<OrganizationAuditChangeResponse>();
        // Tablero: «Invitó a …» empieza por el correo, enmascarado como en «Qué».
        if (row.EntityType == nameof(Invitation) && row.Action == AuditAction.Created
            && row.AffectedDisplayName is { } email)
            rows.Add(new OrganizationAuditChangeResponse(AuditTexts.Field("Email"), null, email));
        if (changes.ValueKind != JsonValueKind.Object) return rows;

        foreach (var property in changes.EnumerateObject())
        {
            if (!TryOldNew(property.Value, out var oldValue, out var newValue)) continue;
            switch (property.Name)
            {
                case AuditChangeKeys.OrganizationRoles or AuditChangeKeys.CompanyRoles:
                    rows.Add(new OrganizationAuditChangeResponse(AuditTexts.Field(property.Name),
                        RoleNames(oldValue), RoleNames(newValue)));
                    continue;
                case AuditChangeKeys.CompanyIds:
                    // Las empresas de una invitación, por nombre: son las de «Dónde». Solo al invitar, como el
                    // tablero; su vencimiento las muestra solo en «Dónde».
                    if (row.Action == AuditAction.Created && row.CompanyName is { } companies)
                        rows.Add(new OrganizationAuditChangeResponse(AuditTexts.Field("CompanyId"), null, companies));
                    continue;
            }

            if (Hidden(row.Action, property.Name) || AuditTexts.FieldOrNull(property.Name) is not { } field) continue;
            var (oldShown, oldText) = await ValueAsync(row.EntityType, property.Name, oldValue, context, cancellationToken);
            var (newShown, newText) = await ValueAsync(row.EntityType, property.Name, newValue, context, cancellationToken);
            if (!oldShown || !newShown || oldText is null && newText is null) continue;
            rows.Add(new OrganizationAuditChangeResponse(field, oldText, newText));
        }

        return rows;
    }

    // El alta ya dice el estado inicial, y el tablero no muestra la zona al crear una empresa: solo «Nombre».
    private static bool Hidden(AuditAction action, string field) => field switch
    {
        "Status" => action != AuditAction.Updated,
        "TimeZoneId" => action == AuditAction.Created,
        _ => false,
    };

    /// <returns>Si el valor se puede mostrar en el idioma de quien mira, y cómo; un lado vacío se muestra vacío.</returns>
    private async Task<(bool Shown, string? Text)> ValueAsync(string entityType, string field, JsonElement value,
        DisplayFormatContext context, CancellationToken cancellationToken)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return (true, null);
        if (field == "Status")
            return StatusName(entityType, value) is { } status && AuditTexts.Status(entityType, status) is { } word
                ? (true, word)
                : (false, null);
        if (field == "Permissions")
            return value.ValueKind == JsonValueKind.Array
                ? (true, string.Join(", ", value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => PermissionTexts.Permission(item.GetString()!))))
                : (false, null);
        switch (value.ValueKind)
        {
            case JsonValueKind.True or JsonValueKind.False:
                return (true, FormattingTexts.Get(value.GetBoolean() ? "Boolean.True" : "Boolean.False", context.Profile));
            case JsonValueKind.Number:
                return (true, value.GetRawText());
            case not JsonValueKind.String:
                return (false, null);
        }

        var text = value.GetString()!;
        // Un identificador interno no se muestra nunca: la persona, la empresa y el rol ya salen por nombre.
        if (Guid.TryParse(text, out _)) return (false, null);
        var shown = field switch
        {
            "DefaultTimeZoneId" or "TimeZoneId" => await formatter.FormatTimeZoneAsync(text, context, cancellationToken),
            // El idioma, en su propio idioma, como el selector de idioma y región y el tablero.
            "DefaultCulture" => await formatter.FormatCultureInItsOwnLanguageAsync(text, cancellationToken),
            "DefaultCurrency" => await formatter.FormatCurrencyAsync(text, context, cancellationToken),
            "TaxNumber" or "TaxId" => TaxNumber(text),
            "Name" when entityType == nameof(Role) => RoleTexts.Name(text),
            _ when field.Contains("Email", StringComparison.OrdinalIgnoreCase) || text.Contains('@') =>
                PersonalDataMask.Email(text),
            _ when field.Contains("Phone", StringComparison.OrdinalIgnoreCase) => PersonalDataMask.Phone(text),
            _ => text,
        };
        return (shown is not null, shown);
    }

    /// <summary>Tablero Auditoria-Org: «Roles en la empresa: Sin roles → Solo lectura», por nombre y en orden.</summary>
    private static string? RoleNames(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array) return null;
        string[] names =
        [
            .. value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => RoleTexts.Name(item.GetString()!))
                .Order(StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true)),
        ];
        return names.Length == 0 ? AuditTexts.NoRoles() : string.Join(", ", names);
    }

    private static string? StatusName(string entityType, JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when StatusTypes.TryGetValue(entityType, out var type) && value.TryGetInt32(out var number)
            => Enum.GetName(type, number),
        _ => null,
    };

    // No es correo ni teléfono: quedan los últimos cuatro dígitos.
    private static string TaxNumber(string text)
    {
        var digits = new string([.. text.Where(char.IsAsciiDigit)]);
        return digits.Length < 4 ? "***" : "***" + digits[^4..];
    }

    private static bool TryOldNew(JsonElement change, out JsonElement oldValue, out JsonElement newValue)
    {
        oldValue = default;
        newValue = default;
        return change.ValueKind == JsonValueKind.Object && change.TryGetProperty("old", out oldValue)
            && change.TryGetProperty("new", out newValue);
    }

    private static JsonDocument? Parse(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
