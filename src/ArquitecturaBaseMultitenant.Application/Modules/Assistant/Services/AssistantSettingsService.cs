using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.Assistant;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantSettingsService(IAssistantSettingsReader reader, AssistantSettingsBuilder builder,
    AssistantSettingsWriter writer, AssistantSettingsSelectionPolicy selection, IChatClientFactory clients, ICurrencyCatalog currencies, IUnitOfWork unitOfWork,
    IRequestValidator validator, TimeProvider clock, ILogger<AssistantSettingsService> logger) : IAssistantSettingsService
{
    public Task<Result<OrganizationAssistantSettingsResponse>> GetOrganizationAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<OrganizationAssistantSettingsResponse>(logger, clock, "AssistantSettingsGetOrganization", async () => await builder.BuildAsync(cancellationToken));

    public Task<Result<VersionResponse>> UpdateOrganizationAsync(UpdateOrganizationAssistantSettingsRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<VersionResponse>(logger, clock, "AssistantSettingsUpdateOrganization", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (request.Mode == AssistantMode.OwnKey && request.MonthlyBudget is null && (await reader.FindOrganizationAsync(cancellationToken))?.MonthlyBudget is null)
                request = request with { MonthlyBudget = (await reader.FindPlatformAsync(cancellationToken)).MonthlyOrganizationBudget };
            if (request.MonthlyBudget is not null)
            {
                var currency = await currencies.FindAsync(request.MonthlyBudget.Currency.Value, cancellationToken);
                if (currency is not { IsEnabled: true, MinorUnits: { } units }) return CurrencyCodeErrors.Invalid;
                request = request with { MonthlyBudget = request.MonthlyBudget.Round(units) };
                if (await validator.ValidateAsync(request, cancellationToken) is { } normalizedInvalid) return normalizedInvalid;
            }
            if (request.KeyAction == AssistantKeyAction.Replace)
            {
                var current = await builder.BuildAsync(cancellationToken);
                if (request.Mode != AssistantMode.OwnKey) return AssistantErrors.SettingsKeyRejected;
                if (!current.AllowOwnKey) return AssistantErrors.SettingsOwnKeyNotAllowed;
                if (!current.HasAlertRecipient) return AssistantErrors.SettingsAlertRecipientRequired;
                var eligible = selection.ValidateOrganization(request, current);
                if (eligible.IsFailure) return eligible.Error;
                var accepted = await clients.ValidateKeyAsync(request.ApiKey!, cancellationToken);
                if (accepted.IsFailure) return accepted.Error;
            }
            var result = await unitOfWork.ExecuteInTransactionAsync(ct => writer.UpdateOrganizationAsync(request, ct), CommitPolicy.OnSuccess, cancellationToken);
            return VersionResponse.Of(result);
        });

    public Task<Result<PlatformAssistantSettingsResponse>> GetPlatformAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PlatformAssistantSettingsResponse>(logger, clock, "AssistantSettingsGetPlatform", async () => await reader.FindPlatformAsync(cancellationToken));

    public Task<Result<VersionResponse>> UpdatePlatformAsync(UpdatePlatformAssistantSettingsRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<VersionResponse>(logger, clock, "AssistantSettingsUpdatePlatform", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var currency = await currencies.FindAsync(request.MonthlyOrganizationBudget!.Currency.Value, cancellationToken);
            if (currency is not { IsEnabled: true, MinorUnits: { } units }) return CurrencyCodeErrors.Invalid;
            request = request with { MonthlyOrganizationBudget = request.MonthlyOrganizationBudget.Round(units) };
            if (await validator.ValidateAsync(request, cancellationToken) is { } normalizedInvalid) return normalizedInvalid;
            if (request.KeyAction == AssistantKeyAction.Replace)
            {
                var eligible = await selection.ValidatePlatformAsync(request, cancellationToken);
                if (eligible.IsFailure) return eligible.Error;
                var accepted = await clients.ValidateKeyAsync(request.ApiKey!, cancellationToken);
                if (accepted.IsFailure) return accepted.Error;
            }
            var result = await unitOfWork.ExecuteInTransactionAsync(ct => writer.UpdatePlatformAsync(request, ct), CommitPolicy.OnSuccess, cancellationToken);
            return VersionResponse.Of(result);
        });
}
