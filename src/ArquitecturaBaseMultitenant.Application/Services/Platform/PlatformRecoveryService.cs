using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Text;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Phones;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>Bandeja Recuperaciones: lista y cuenta pedidos con los métodos enmascarados, como el tablero.</summary>
internal sealed class PlatformRecoveryService(IAccountRecoveryReader reader, IPhoneNumberParser phones,
    IRequestValidator validator, TimeProvider clock, ILogger<PlatformRecoveryService> logger) : IPlatformRecoveryService
{
    public Task<Result<PagedResult<PlatformRecoveryResponse>>> ListAsync(ListPlatformRecoveriesRequest request,
        CancellationToken ct) =>
        OperationLog.RunAsync<PagedResult<PlatformRecoveryResponse>>(logger, clock, "ListPlatformRecoveries", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            var page = await reader.ListAsync(request, ct);
            return new PagedResult<PlatformRecoveryResponse>([.. page.Items.Select(ToResponse)], page.Page,
                page.PageSize, page.TotalCount);
        });

    public Task<Result<PlatformRecoveryFilterCounts>> CountAsync(ListPlatformRecoveriesRequest request,
        CancellationToken ct) =>
        OperationLog.RunAsync<PlatformRecoveryFilterCounts>(logger, clock, "CountPlatformRecoveries", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            return await reader.CountAsync(request, ct);
        });

    private PlatformRecoveryResponse ToResponse(PlatformRecoveryRow row) => new(row.Id, row.CreatedAtUtc, row.AccountId,
        row.AccountName, row.PreviousMethodType, Mask(row.PreviousMethodType, row.PreviousMethodValue), row.NewMethodType,
        Mask(row.NewMethodType, row.NewMethodValue), row.Status, row.DecidedAtUtc, row.DecidedBy, row.Reason, row.Version);

    private string Mask(LoginMethodType type, string value) => type == LoginMethodType.Phone
        && PhoneNumber.Create(value) is { IsSuccess: true } phone
            ? phones.Mask(phone.Value)
            : PersonalDataMask.Email(value);
}
