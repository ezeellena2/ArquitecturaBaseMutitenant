using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Users;
using System.Globalization;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantUsageService(IAssistantUsageReader reader, AssistantUsageBuilder builder,
    AssistantUsagePolicy policy, IRequestValidator validator, TimeProvider clock, ILogger<AssistantUsageService> logger) : IAssistantUsageService
{
    public Task<Result<AssistantUsagePageResponse>> ListOrganizationAsync(OrganizationAssistantUsageRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<AssistantUsagePageResponse>(logger, clock, "AssistantUsageListOrganization", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var authorized = await policy.CheckAsync(Access.Business, cancellationToken);
            if (authorized.IsFailure) return authorized.Error;
            var page = await reader.ListOrganizationAsync(ParseMonth(request.Month!), request.GroupBy ?? AssistantUsageGrouping.Day,
                request.Page ?? 1, request.PageSize ?? 20, clock.GetUtcNow().UtcDateTime, cancellationToken);
            var response = await builder.BuildAsync(request.Month!, page, true, cancellationToken);
            authorized = await policy.CheckAsync(Access.Business, cancellationToken);
            return authorized.IsFailure ? authorized.Error : response;
        });

    public Task<Result<AssistantUsagePageResponse>> ListPlatformAsync(PlatformAssistantUsageRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<AssistantUsagePageResponse>(logger, clock, "AssistantUsageListPlatform", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var authorized = await policy.CheckAsync(Access.Platform, cancellationToken);
            if (authorized.IsFailure) return authorized.Error;
            var page = await reader.ListPlatformAsync(ParseMonth(request.Month!), request.Page ?? 1, request.PageSize ?? 20, cancellationToken);
            var response = await builder.BuildAsync(request.Month!, page, false, cancellationToken);
            authorized = await policy.CheckAsync(Access.Platform, cancellationToken);
            return authorized.IsFailure ? authorized.Error : response;
        });

    private static DateOnly ParseMonth(string month) => new(int.Parse(month.AsSpan(0, 4), CultureInfo.InvariantCulture),
        int.Parse(month.AsSpan(5, 2), CultureInfo.InvariantCulture), 1);
}
