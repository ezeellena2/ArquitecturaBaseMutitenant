using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>Contrato de la segunda parte de la base de la 12 (K1): la configuración de la organización responde 501 hasta la B18.</summary>
internal sealed class WhatsAppSettingsService(TimeProvider clock, ILogger<WhatsAppSettingsService> logger) : IWhatsAppSettingsService
{
    public Task<Result<WhatsAppOrganizationSettingsResponse>> GetAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppSettingsGet",
            () => Task.FromResult(Result.Failure<WhatsAppOrganizationSettingsResponse>(ContractErrors.NotImplemented)));

    public Task<Result<VersionResponse>> UpdateAsync(UpdateWhatsAppOrganizationSettingsRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppSettingsUpdate",
            () => Task.FromResult(Result.Failure<VersionResponse>(ContractErrors.NotImplemented)));
}

/// <summary>Contrato de la segunda parte de la base de la 12 (K1): la configuración de la plataforma responde 501 hasta la B18.</summary>
internal sealed class PlatformWhatsAppSettingsService(TimeProvider clock, ILogger<PlatformWhatsAppSettingsService> logger) : IPlatformWhatsAppSettingsService
{
    public Task<Result<PlatformWhatsAppSettingsResponse>> GetAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "PlatformWhatsAppSettingsGet",
            () => Task.FromResult(Result.Failure<PlatformWhatsAppSettingsResponse>(ContractErrors.NotImplemented)));

    public Task<Result<VersionResponse>> UpdateAsync(UpdatePlatformWhatsAppSettingsRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "PlatformWhatsAppSettingsUpdate",
            () => Task.FromResult(Result.Failure<VersionResponse>(ContractErrors.NotImplemented)));
}
