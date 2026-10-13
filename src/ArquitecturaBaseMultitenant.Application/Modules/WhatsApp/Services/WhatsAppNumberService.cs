using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

internal sealed class WhatsAppNumberService(TimeProvider clock, ILogger<WhatsAppNumberService> logger) : IWhatsAppNumberService
{
    public Task<Result<WhatsAppNumberListResponse>> ListAsync(WhatsAppNumberListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppNumberList", () => Task.FromResult(Result.Failure<WhatsAppNumberListResponse>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppNumberResponse>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppNumberGet", () => Task.FromResult(Result.Failure<WhatsAppNumberResponse>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppNumberResponse>> ConnectAsync(ConnectWhatsAppNumberRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppNumberConnect", () => Task.FromResult(Result.Failure<WhatsAppNumberResponse>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppNumberResponse>> CheckAsync(Guid id, CheckWhatsAppNumberRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppNumberCheck", () => Task.FromResult(Result.Failure<WhatsAppNumberResponse>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppNumberResponse>> UpdateAsync(Guid id, UpdateWhatsAppNumberRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppNumberUpdate", () => Task.FromResult(Result.Failure<WhatsAppNumberResponse>(ContractErrors.NotImplemented)));

    public Task<Result> DisconnectAsync(Guid id, DeleteWhatsAppNumberRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppNumberDisconnect", () => Task.FromResult(Result.Failure(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppBotResponse>> GetBotAsync(Guid id, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppNumberGetBot", () => Task.FromResult(Result.Failure<WhatsAppBotResponse>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppBotResponse>> UpdateBotAsync(Guid id, UpdateWhatsAppBotRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppNumberUpdateBot", () => Task.FromResult(Result.Failure<WhatsAppBotResponse>(ContractErrors.NotImplemented)));
}
