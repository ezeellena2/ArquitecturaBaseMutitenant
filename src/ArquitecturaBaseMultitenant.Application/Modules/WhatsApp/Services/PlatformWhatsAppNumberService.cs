using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

internal sealed class PlatformWhatsAppNumberService(TimeProvider clock, ILogger<PlatformWhatsAppNumberService> logger) : IPlatformWhatsAppNumberService
{
    public Task<Result<PlatformWhatsAppNumberResponse>> GetAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "PlatformWhatsAppNumberGet", () => Task.FromResult(Result.Failure<PlatformWhatsAppNumberResponse>(ContractErrors.NotImplemented)));

    public Task<Result<PlatformWhatsAppNumberResponse>> ConnectAsync(ConnectPlatformWhatsAppNumberRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "PlatformWhatsAppNumberConnect", () => Task.FromResult(Result.Failure<PlatformWhatsAppNumberResponse>(ContractErrors.NotImplemented)));

    public Task<Result<PlatformWhatsAppNumberResponse>> CheckAsync(CheckWhatsAppNumberRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "PlatformWhatsAppNumberCheck", () => Task.FromResult(Result.Failure<PlatformWhatsAppNumberResponse>(ContractErrors.NotImplemented)));

    public Task<Result<PlatformWhatsAppNumberResponse>> UpdateAsync(UpdatePlatformWhatsAppNumberRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "PlatformWhatsAppNumberUpdate", () => Task.FromResult(Result.Failure<PlatformWhatsAppNumberResponse>(ContractErrors.NotImplemented)));

    public Task<Result<PlatformWhatsAppBotResponse>> GetBotAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "PlatformWhatsAppNumberGetBot", () => Task.FromResult(Result.Failure<PlatformWhatsAppBotResponse>(ContractErrors.NotImplemented)));

    public Task<Result<PlatformWhatsAppBotResponse>> UpdateBotAsync(UpdatePlatformWhatsAppBotRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "PlatformWhatsAppNumberUpdateBot", () => Task.FromResult(Result.Failure<PlatformWhatsAppBotResponse>(ContractErrors.NotImplemented)));
}
