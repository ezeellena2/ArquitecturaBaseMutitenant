using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Logging;
using WhatsAppUniqueKeys = ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.UniqueKeys;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// El número de Plataforma (decisión 3): conectarlo desde la pantalla reemplaza al anterior; sin número conectado,
/// códigos y avisos salen por correo. Después de cada cambio se relee el estado que usan ingreso y avisos.
/// </summary>
internal sealed class PlatformWhatsAppNumberService(IWhatsAppChannelReader reader, IWhatsAppChannelRepository channels,
    IWhatsAppGraphClient graph, WhatsAppChannelCreator creator, WhatsAppChannelAuditWriter audit, IWhatsAppTokenProtector protector,
    IWhatsAppChannelSettings settings, IWhatsAppChannelCache cache, IUnitOfWork unitOfWork, IRequestValidator validator,
    TimeProvider clock, ILogger<PlatformWhatsAppNumberService> logger) : IPlatformWhatsAppNumberService
{
    public Task<Result<PlatformWhatsAppNumberResponse>> GetAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PlatformWhatsAppNumberResponse>(logger, clock, "PlatformWhatsAppNumberGet", async () =>
            await ResponseAsync(cancellationToken));

    public Task<Result<PlatformWhatsAppNumberResponse>> ConnectAsync(ConnectPlatformWhatsAppNumberRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PlatformWhatsAppNumberResponse>(logger, clock, "PlatformWhatsAppNumberConnect", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var input = new WhatsAppSignupInput(request.Code!, WhatsAppNumberService.NullIfEmpty(request.PhoneNumberId), request.WabaId!.Trim(), request.Coexistence ?? false);
            if (input.PhoneNumberId is { } known && await reader.IsActiveAsync(known, cancellationToken)
                && (await reader.FindPlatformStateAsync(cancellationToken))?.PhoneNumberId != known)
                return WhatsAppErrors.NumberAlreadyConnected;
            var signup = await graph.CompleteSignupAsync(input, cancellationToken);
            if (signup.IsFailure) return signup.Error;
            Result<WhatsAppChannel> saved;
            try
            {
                saved = await unitOfWork.ExecuteInTransactionAsync(ct => creator.ConnectPlatformAsync(input, signup.Value, ct),
                    CommitPolicy.OnSuccess, cancellationToken);
            }
            catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == WhatsAppUniqueKeys.ActiveChannelNumber)
            { return WhatsAppErrors.NumberAlreadyConnected; }
            if (saved.IsFailure) return saved.Error;
            await cache.RefreshAsync(cancellationToken);
            return await ResponseAsync(cancellationToken);
        });

    public Task<Result<PlatformWhatsAppNumberResponse>> CheckAsync(CheckWhatsAppNumberRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PlatformWhatsAppNumberResponse>(logger, clock, "PlatformWhatsAppNumberCheck", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (await reader.FindPlatformStateAsync(cancellationToken) is not { } state
                || await reader.FindSendingAsync(state.Id, state.PhoneNumberId, cancellationToken) is not { WabaId.Length: > 0 } sending)
                return WhatsAppErrors.NumberNotFound;
            var payment = await graph.HasPaymentAsync(sending.WabaId, protector.Unprotect(sending.Id, sending.EncryptedToken), cancellationToken);
            if (payment.IsFailure) return payment.Error;
            var saved = await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var channel = await channels.GetPlatformAsync(request.Version, ct);
                if (channel is null || channel.Id != state.Id) return Result.Failure(WhatsAppErrors.NumberNotFound);
                var before = channel.Status;
                if (!channel.ApplyPayment(payment.Value)) return Result.Success();
                channels.MarkUpdated(channel);
                audit.Record(channel, "WhatsApp number payment checked", WhatsAppChannelAuditWriter.Status(before, channel.Status));
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
            if (saved.IsFailure) return saved.Error;
            await cache.RefreshAsync(cancellationToken);
            return await ResponseAsync(cancellationToken);
        });

    public Task<Result<PlatformWhatsAppNumberResponse>> UpdateAsync(UpdatePlatformWhatsAppNumberRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PlatformWhatsAppNumberResponse>(logger, clock, "PlatformWhatsAppNumberUpdate", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var saved = await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var channel = await channels.GetPlatformAsync(request.Version, ct);
                if (channel is null) return Result.Failure(WhatsAppErrors.NumberNotFound);
                var changes = new SecurityEventChanges();
                changes.AddBoolean("whatsappConsultationsEnabled", channel.ConsultationsEnabled, request.ConsultationsEnabled);
                channel.SetConsultations(request.ConsultationsEnabled!.Value);
                channels.MarkUpdated(channel);
                audit.Record(channel, "WhatsApp platform consultations changed", changes);
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
            if (saved.IsFailure) return saved.Error;
            await cache.RefreshAsync(cancellationToken);
            return await ResponseAsync(cancellationToken);
        });

    private async Task<PlatformWhatsAppNumberResponse> ResponseAsync(CancellationToken ct)
    {
        var row = await reader.FindPlatformAsync(clock.GetUtcNow().UtcDateTime, ct);
        return new PlatformWhatsAppNumberResponse(row?.Number, row?.ConsultationsEnabled ?? false, row?.SystemMessagesThisMonth ?? 0, settings.Signup);
    }
}
