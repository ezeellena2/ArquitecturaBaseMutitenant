using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Time;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auditing;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auditing;

internal sealed class OrganizationAuditService(IOrganizationAuditReader reader,
    ITenantSettingsReader settingsReader, ITimeZoneService timeZoneService, OrganizationAuditEntryBuilder entries,
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
                await entries.BuildAsync(page.Items, cancellationToken), page.NextCursor, page.HasMore));
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
}
