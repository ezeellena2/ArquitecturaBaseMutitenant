using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// La configuración de WhatsApp de la organización del token (272): el plazo de sus chats, de 1 al máximo que fija la
/// plataforma en el momento de guardar. Sin fila vale el valor por defecto, recortado a ese máximo.
/// </summary>
internal sealed class WhatsAppSettingsService(IWhatsAppSettingsReader reader, IWhatsAppSettingsRepository repository,
    WhatsAppSettingsResolver resolver, IUnitOfWork unitOfWork, IRequestValidator validator, TimeProvider clock,
    ILogger<WhatsAppSettingsService> logger) : IWhatsAppSettingsService
{
    public Task<Result<WhatsAppOrganizationSettingsResponse>> GetAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppOrganizationSettingsResponse>(logger, clock, "WhatsAppSettingsGet", async () =>
        {
            var row = await reader.FindOrganizationAsync(cancellationToken);
            var (days, max) = await resolver.OrganizationChatRetentionAsync(cancellationToken);
            return new WhatsAppOrganizationSettingsResponse(days, max, row?.Version ?? 0);
        });

    public Task<Result<VersionResponse>> UpdateAsync(UpdateWhatsAppOrganizationSettingsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync<VersionResponse>(logger, clock, "WhatsAppSettingsUpdate", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var result = await unitOfWork.ExecuteInTransactionAsync<Result<WhatsAppOrganizationSettings>>(async ct =>
            {
                var row = await repository.GetOrganizationForUpdateAsync(request.Version!.Value, ct);
                // El máximo se lee adentro, bajo el lock de la organización: un cambio de la plataforma posterior lo recorta al leer.
                var max = (await reader.FindPlatformAsync(ct))?.MaxOrganizationChatRetentionDays
                    ?? WhatsAppSettingsLimits.DefaultMaxOrganizationChatRetentionDays;
                var created = row is null;
                row ??= WhatsAppOrganizationSettings.Create();
                if (!row.Update(request.ChatRetentionDays!.Value, max))
                    return FieldErrors.Validation(nameof(request.ChatRetentionDays), ValidationTexts.NumberRange);
                if (created) repository.Add(row); else repository.MarkUpdated(row);
                return row;
            }, CommitPolicy.OnSuccess, cancellationToken);
            return VersionResponse.Of(result);
        });
    }
}

/// <summary>
/// La configuración de WhatsApp de la plataforma (270 y 272): los países que reciben códigos y los plazos de los chats. Sin fila,
/// los países son los de <c>WhatsApp:AllowedCountries</c> (0060) y los plazos, los de defecto.
/// </summary>
internal sealed class PlatformWhatsAppSettingsService(IWhatsAppSettingsRepository repository, WhatsAppSettingsResolver resolver,
    IUnitOfWork unitOfWork, IRequestValidator validator, TimeProvider clock, ILogger<PlatformWhatsAppSettingsService> logger)
    : IPlatformWhatsAppSettingsService
{
    public Task<Result<PlatformWhatsAppSettingsResponse>> GetAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PlatformWhatsAppSettingsResponse>(logger, clock, "PlatformWhatsAppSettingsGet", async () =>
        {
            var settings = await resolver.PlatformAsync(cancellationToken);
            return new PlatformWhatsAppSettingsResponse(settings.AllowedCountries, settings.ChatRetentionDays,
                settings.MaxOrganizationChatRetentionDays, settings.Version);
        });

    public Task<Result<VersionResponse>> UpdateAsync(UpdatePlatformWhatsAppSettingsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync<VersionResponse>(logger, clock, "PlatformWhatsAppSettingsUpdate", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var result = await unitOfWork.ExecuteInTransactionAsync<Result<WhatsAppPlatformSettings>>(async ct =>
            {
                var row = await repository.GetPlatformForUpdateAsync(request.Version!.Value, ct);
                var created = row is null;
                row ??= WhatsAppPlatformSettings.Create(resolver.ConfiguredCountries);
                if (!row.Update(request.AllowedCountries!, request.ChatRetentionDays!.Value, request.MaxOrganizationChatRetentionDays!.Value))
                    return FieldErrors.Validation(nameof(request.AllowedCountries), ValidationTexts.CountryInvalid);
                if (created) repository.Add(row); else repository.MarkUpdated(row);
                return row;
            }, CommitPolicy.OnSuccess, cancellationToken);
            return VersionResponse.Of(result);
        });
    }
}
