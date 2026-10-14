using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Logging;
using WhatsAppUniqueKeys = ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.UniqueKeys;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>Números de la organización del claim: alta embebida, medio de pago, a quién atienden y desconexión (D7).</summary>
internal sealed class WhatsAppNumberService(IWhatsAppChannelReader reader, IWhatsAppChannelRepository channels, IWhatsAppGraphClient graph,
    WhatsAppChannelCreator creator, WhatsAppChannelAuditWriter audit, ICompanyReader companies, IWhatsAppTokenProtector protector,
    IWhatsAppChannelSettings settings, IUnitOfWork unitOfWork, IRequestValidator validator, TimeProvider clock,
    ILogger<WhatsAppNumberService> logger) : IWhatsAppNumberService
{
    public Task<Result<WhatsAppNumberListResponse>> ListAsync(WhatsAppNumberListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppNumberListResponse>(logger, clock, "WhatsAppNumberList", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var numbers = await reader.ListOrganizationAsync(request, clock.GetUtcNow().UtcDateTime, cancellationToken);
            return new WhatsAppNumberListResponse(numbers,
                await reader.CountOrganizationAsync(WhatsAppNumberStatus.PaymentMissing, cancellationToken), settings.Signup);
        });

    public Task<Result<WhatsAppNumberResponse>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppNumberResponse>(logger, clock, "WhatsAppNumberGet", async () =>
            await reader.FindOrganizationAsync(id, clock.GetUtcNow().UtcDateTime, cancellationToken) is { } number
                ? number : WhatsAppErrors.NumberNotFound);

    public Task<Result<PagedResult<WhatsAppReferenceResponse>>> ListCompanyOptionsAsync(WhatsAppCompanyOptionListRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PagedResult<WhatsAppReferenceResponse>>(logger, clock, "WhatsAppNumberCompanyOptions", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var page = await companies.ListAsync(new ListCompaniesRequest
            {
                Page = request.Page, PageSize = request.PageSize, Search = request.Search, Status = CompanyStatus.Active,
            }, cancellationToken);
            return new PagedResult<WhatsAppReferenceResponse>([.. page.Items.Select(company => new WhatsAppReferenceResponse(company.Id, company.Name))],
                page.Page, page.PageSize, page.TotalCount);
        });

    public Task<Result<WhatsAppNumberResponse>> ConnectAsync(ConnectWhatsAppNumberRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppNumberResponse>(logger, clock, "WhatsAppNumberConnect", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (request.CompanyId is { } companyId && !await IsActiveCompanyAsync(companyId, cancellationToken)) return CompanyErrors.NotFound;
            var input = new WhatsAppSignupInput(request.Code!, NullIfEmpty(request.PhoneNumberId), request.WabaId!.Trim(), request.Coexistence ?? false);
            // El código vive 30 segundos y Meta lo canjea una sola vez: un número ya vigente no lo gasta.
            if (input.PhoneNumberId is { } known && await reader.IsActiveAsync(known, cancellationToken)) return WhatsAppErrors.NumberAlreadyConnected;
            var signup = await graph.CompleteSignupAsync(input, cancellationToken);
            if (signup.IsFailure) return signup.Error;
            Result<WhatsAppChannel> saved;
            try
            {
                saved = await unitOfWork.ExecuteInTransactionAsync(ct => creator.ConnectOrganizationAsync(request.CompanyId, input, signup.Value, ct),
                    CommitPolicy.OnSuccess, cancellationToken);
            }
            catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == WhatsAppUniqueKeys.ActiveChannelNumber)
            { return WhatsAppErrors.NumberAlreadyConnected; }
            if (saved.IsFailure) return saved.Error;
            return await FindAsync(saved.Value.Id, cancellationToken);
        });

    public Task<Result<WhatsAppNumberResponse>> CheckAsync(Guid id, CheckWhatsAppNumberRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppNumberResponse>(logger, clock, "WhatsAppNumberCheck", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (await reader.FindOrganizationAsync(id, clock.GetUtcNow().UtcDateTime, cancellationToken) is null
                || await reader.FindSendingAsync(id, string.Empty, cancellationToken) is not { } sending) return WhatsAppErrors.NumberNotFound;
            var payment = await graph.HasPaymentAsync(sending.WabaId, protector.Unprotect(sending.Id, sending.EncryptedToken), cancellationToken);
            if (payment.IsFailure) return payment.Error;
            var saved = await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var channel = await channels.GetOrganizationAsync(id, request.Version, ct);
                if (channel is null) return Result.Failure(WhatsAppErrors.NumberNotFound);
                var before = channel.Status;
                if (!channel.ApplyPayment(payment.Value)) return Result.Success();
                channels.MarkUpdated(channel);
                audit.Record(channel, "WhatsApp number payment checked", WhatsAppChannelAuditWriter.Status(before, channel.Status));
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
            return saved.IsFailure ? saved.Error : await FindAsync(id, cancellationToken);
        });

    public Task<Result<WhatsAppNumberResponse>> UpdateAsync(Guid id, UpdateWhatsAppNumberRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppNumberResponse>(logger, clock, "WhatsAppNumberUpdate", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (request.CompanyId is { } companyId && !await IsActiveCompanyAsync(companyId, cancellationToken)) return CompanyErrors.NotFound;
            var saved = await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var channel = await channels.GetOrganizationAsync(id, request.Version, ct);
                if (channel is null) return Result.Failure(WhatsAppErrors.NumberNotFound);
                var changes = new SecurityEventChanges();
                changes.AddBoolean("whatsappNumberServesCompany", channel.CompanyId is not null, request.CompanyId is not null);
                channel.ServeCompany(request.CompanyId);
                channels.MarkUpdated(channel);
                audit.Record(channel, "WhatsApp number company changed", changes);
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
            return saved.IsFailure ? saved.Error : await FindAsync(id, cancellationToken);
        });

    public Task<Result> DisconnectAsync(Guid id, DeleteWhatsAppNumberRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppNumberDisconnect", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var channel = await channels.GetOrganizationAsync(id, request.Version, ct);
                if (channel is null) return Result.Failure(WhatsAppErrors.NumberNotFound);
                var before = channel.Status;
                channel.Disconnect(clock.GetUtcNow().UtcDateTime);
                channels.MarkUpdated(channel);
                audit.Record(channel, "WhatsApp number disconnected", WhatsAppChannelAuditWriter.Status(before, channel.Status));
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result<WhatsAppBotResponse>> GetBotAsync(Guid id, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppNumberGetBot", () => Task.FromResult(Result.Failure<WhatsAppBotResponse>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppBotResponse>> UpdateBotAsync(Guid id, UpdateWhatsAppBotRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppNumberUpdateBot", () => Task.FromResult(Result.Failure<WhatsAppBotResponse>(ContractErrors.NotImplemented)));

    private async Task<Result<WhatsAppNumberResponse>> FindAsync(Guid id, CancellationToken ct) =>
        await reader.FindOrganizationAsync(id, clock.GetUtcNow().UtcDateTime, ct) is { } number ? number : WhatsAppErrors.NumberNotFound;

    internal static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<bool> IsActiveCompanyAsync(Guid companyId, CancellationToken ct) =>
        await companies.FindByIdAsync(companyId, ct) is { Status: CompanyStatus.Active };
}
