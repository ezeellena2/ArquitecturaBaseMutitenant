using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Text;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Time;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>
/// Plataforma › Auditoría: lista por cursor los SecurityEvent y los ingresos, y muestra uno. Traduce qué pasó, sobre
/// qué y quién al leer; una cuenta eliminada se muestra como «Cuenta eliminada» y una sin nombre, con su correo
/// enmascarado. «Hoy» es el día del operador, en su zona horaria.
/// </summary>
internal sealed class PlatformAuditService(ISecurityEventReader reader, IUserRepository users, ICurrentUser currentUser,
    ITimeZoneService timeZones, IRequestValidator validator, TimeProvider clock, ILogger<PlatformAuditService> logger)
    : IPlatformAuditService
{
    public Task<Result<CursorResult<PlatformAuditEventResponse>>> ListAsync(ListPlatformAuditRequest request,
        CancellationToken ct) =>
        OperationLog.RunAsync<CursorResult<PlatformAuditEventResponse>>(logger, clock, "ListPlatformAudit", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            var since = request.FromUtc ?? await SinceAsync(request.Period, ct);
            var types = EventTypes(request.Type);
            // Los ingresos no tienen organización: con ese filtro no se suman.
            var signIns = request.Type is null or PlatformAuditCategories.SignIns && request.TenantId is null;
            var page = await reader.ListAsync(request, since, types, signIns, ct);
            return new CursorResult<PlatformAuditEventResponse>([.. page.Items.Select(ToResponse)], page.NextCursor,
                page.HasMore);
        });

    public Task<Result<PlatformAuditEventResponse>> FindAsync(Guid id, CancellationToken ct) =>
        OperationLog.RunAsync<PlatformAuditEventResponse>(logger, clock, "FindPlatformAudit", async () =>
            await reader.FindAsync(id, ct) is { } row ? ToResponse(row) : PlatformAuditErrors.NotFound);

    public Task<Result<IReadOnlyList<PlatformAuditOrganizationResponse>>> ListOrganizationsAsync(
        ListPlatformAuditRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<IReadOnlyList<PlatformAuditOrganizationResponse>>(logger, clock,
            "ListPlatformAuditOrganizations", async () =>
            {
                ArgumentNullException.ThrowIfNull(request);
                if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
                var since = request.FromUtc ?? await SinceAsync(request.Period, ct);
                return Result.Success(await reader.ListOrganizationsAsync(request, since, EventTypes(request.Type), ct));
            });

    private static IReadOnlyCollection<SecurityEventType>? EventTypes(string? category) => category switch
    {
        null => null,
        PlatformAuditCategories.SignIns => [],
        var type => PlatformAuditCategories.TypesOf(type),
    };

    private async Task<DateTime?> SinceAsync(string? period, CancellationToken ct)
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        switch (period)
        {
            case "0":
                var zone = currentUser.UserId is { } id ? (await users.GetByIdAsync(id, ct))?.TimeZoneId : null;
                zone ??= TimeZoneInfo.Utc.Id;
                return timeZones.GetDayRangeUtc(DateOnly.FromDateTime(timeZones.ConvertToLocal(nowUtc, zone)), zone).StartUtc;
            case "7":
                return nowUtc.AddDays(-7);
            case "30":
                return nowUtc.AddDays(-30);
            default:
                return null;
        }
    }

    private static PlatformAuditEventResponse ToResponse(PlatformAuditRow row)
    {
        if (row.EventType is not { } type)
        {
            var summary = row.SignInSucceeded
                ? AuditTexts.SignIn(row.SignInMethod!.Value)
                : AuditTexts.SignInFailed(row.FailureCode is { } code ? ErrorTexts.Find(code) ?? code : string.Empty);
            return new PlatformAuditEventResponse(row.Id, row.OccurredAtUtc, PlatformAuditCategories.SignIns,
                row.SignInSucceeded ? "SignIn" : "SignInFailed", summary, Name(row.TargetAccount),
                null, row.TargetAccountId, Name(row.Actor),
                AuditTexts.ActorRole(row.Actor?.IsOperator == true ? AuditActorKind.PlatformOperator : AuditActorKind.User),
                null, null);
        }

        var target = row.TargetTenantName ?? (row.TargetAccountId is not null ? Name(row.TargetAccount) : AuditTexts.PlatformTarget());
        var actor = row.ActorKind == AuditActorKind.System ? AuditTexts.Actor(AuditActorKind.System) : Name(row.Actor);
        return new PlatformAuditEventResponse(row.Id, row.OccurredAtUtc, PlatformAuditCategories.Of(type), type.ToString(),
            AuditTexts.SecurityEvent(type), target, row.TargetTenantId, row.TargetAccountId, actor,
            AuditTexts.ActorRole(row.ActorKind), row.Reason, row.MaskedIpAddress);
    }

    private static string Name(PlatformAuditAccount? account) => account switch
    {
        null => AuditTexts.UnknownAccount(),
        { IsDeleted: true } => ErrorTexts.Get("Legal.AccountDeletion.DeletedDisplayName"),
        { DisplayName: { Length: > 0 } name } => name,
        { PrimaryEmail: { } email } => PersonalDataMask.Email(email.Value),
        _ => AuditTexts.UnknownAccount(),
    };
}
