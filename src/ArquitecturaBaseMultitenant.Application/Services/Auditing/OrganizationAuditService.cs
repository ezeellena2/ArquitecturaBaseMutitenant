using System.Globalization;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Text;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Time;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auditing;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auditing;

internal sealed class OrganizationAuditService(IOrganizationAuditReader reader,
    ITenantSettingsReader settingsReader, ITimeZoneService timeZoneService,
    IRequestValidator validator, TimeProvider timeProvider, ILogger<OrganizationAuditService> logger)
    : IOrganizationAuditService
{
    public Task<Result<CursorResult<OrganizationAuditEntryResponse>>> ListAsync(
        ListOrganizationAuditRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "ListOrganizationAudit", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var (startUtc, endUtc) = await GetPeriodAsync(request, cancellationToken);
            var page = await reader.ListAsync(request, startUtc, endUtc, cancellationToken);
            return Result.Success(new CursorResult<OrganizationAuditEntryResponse>(
                page.Items.Select(ToResponse).ToArray(), page.NextCursor, page.HasMore));
        });
    }

    public Task<Result<IReadOnlyList<OrganizationAuditActorResponse>>> ListActorsAsync(
        ListOrganizationAuditRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync<IReadOnlyList<OrganizationAuditActorResponse>>(logger, timeProvider,
            "ListOrganizationAuditActors", async () =>
            {
                if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
                var (startUtc, endUtc) = await GetPeriodAsync(request, cancellationToken);
                var actors = await reader.ListActorsAsync(request, startUtc, endUtc, cancellationToken);
                return Result.Success<IReadOnlyList<OrganizationAuditActorResponse>>(actors.Select(actor =>
                    new OrganizationAuditActorResponse(actor.ActorId, actor.ActorKind.ToString(),
                        AuditTexts.Actor(actor.ActorKind), actor.DisplayName)).ToArray());
            });
    }

    private async Task<(DateTime? StartUtc, DateTime? EndUtc)> GetPeriodAsync(
        ListOrganizationAuditRequest request, CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (request.Period == "0")
        {
            var settings = await settingsReader.FindCurrentAsync(cancellationToken);
            var timeZoneId = settings?.DefaultTimeZoneId ?? TimeZoneInfo.Utc.Id;
            var localDate = DateOnly.FromDateTime(timeZoneService.ConvertToLocal(nowUtc, timeZoneId));
            var range = timeZoneService.GetDayRangeUtc(localDate, timeZoneId);
            return (range.StartUtc, range.EndUtc);
        }

        return request.Period switch
        {
            "7" => (nowUtc.AddDays(-7), nowUtc),
            "30" => (nowUtc.AddDays(-30), nowUtc),
            _ => (null, null),
        };
    }

    private static OrganizationAuditEntryResponse ToResponse(OrganizationAuditRow row)
    {
        var actorDisplayName = row.ActorKind == AuditActorKind.User ? row.ActorDisplayName : null;
        return new OrganizationAuditEntryResponse(row.Id, row.OccurredAtUtc, row.Action.ToString(),
            AuditTexts.Summary(row.Action, row.EntityType, row.AffectedDisplayName),
            row.EntityType, AuditTexts.Entity(row.EntityType), row.AffectedDisplayName,
            row.CompanyName ?? AuditTexts.WholeOrganization(),
            row.ActorKind.ToString(), AuditTexts.Actor(row.ActorKind), row.ActorId, actorDisplayName,
            ReadChanges(row.Changes), row.ActorKind == AuditActorKind.User ? row.ActorEmail : null);
    }

    private static List<OrganizationAuditChangeResponse> ReadChanges(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return [];
            var changes = new List<OrganizationAuditChangeResponse>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name is "TenantId" or "Id" or "Version" ||
                    property.Name.Equals("Fields", StringComparison.OrdinalIgnoreCase))
                {
                    if (property.Name.Equals("Fields", StringComparison.OrdinalIgnoreCase) &&
                        property.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var field in property.Value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String))
                        {
                            var fieldName = field.GetString()!;
                            changes.Add(new OrganizationAuditChangeResponse(AuditTexts.Field(fieldName), null, null));
                        }
                    }

                    continue;
                }

                if (property.Value.ValueKind != JsonValueKind.Object ||
                    !property.Value.TryGetProperty("old", out var oldValue) ||
                    !property.Value.TryGetProperty("new", out var newValue))
                {
                    continue;
                }

                if (property.Name is AuditChangeKeys.OrganizationRoles or AuditChangeKeys.CompanyRoles)
                {
                    changes.Add(new OrganizationAuditChangeResponse(AuditTexts.Field(property.Name),
                        RoleNames(oldValue), RoleNames(newValue)));
                    continue;
                }

                // Un identificador interno no se muestra nunca: la persona, la empresa y el rol ya salen por nombre.
                if (IsInternalId(oldValue) || IsInternalId(newValue)) continue;

                var oldText = SafeValue(property.Name, oldValue);
                var newText = SafeValue(property.Name, newValue);
                if (oldText is null && newText is null) continue;
                changes.Add(new OrganizationAuditChangeResponse(AuditTexts.Field(property.Name), oldText, newText));
            }

            return changes;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static bool IsInternalId(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out _);

    /// <summary>Tablero Auditoria-Org: «Roles en la empresa: Sin roles → Solo lectura», por nombre y en orden.</summary>
    private static string RoleNames(JsonElement value)
    {
        string[] names = value.ValueKind == JsonValueKind.Array
            ?
            [
                .. value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => RoleTexts.Name(item.GetString()!))
                    .Order(StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true)),
            ]
            : [];
        return names.Length == 0 ? AuditTexts.NoRoles() : string.Join(", ", names);
    }

    private static string? SafeValue(string field, JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.Array => JsonSerializer.Serialize(value.GetArrayLength()),
            _ => null,
        };
        if (text is null) return null;
        if (field.Contains("Email", StringComparison.OrdinalIgnoreCase) || text.Contains('@'))
            return PersonalDataMask.Email(text);

        if (field.Contains("Phone", StringComparison.OrdinalIgnoreCase) || field.Contains("Tel", StringComparison.OrdinalIgnoreCase))
            return PersonalDataMask.Phone(text);

        if (field.Contains("TaxId", StringComparison.OrdinalIgnoreCase))
        {
            var digits = new string(text.Where(char.IsAsciiDigit).ToArray());
            return digits.Length < 4 ? "***" : "***" + digits[^4..];
        }

        return text;
    }
}
