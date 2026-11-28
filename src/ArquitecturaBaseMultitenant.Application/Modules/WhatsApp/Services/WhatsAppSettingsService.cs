using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// La configuración de WhatsApp de la organización del token (272): el plazo de sus chats, de 1 a su tope en el momento de
/// guardar (ADR 0069 §4: el que da el resolvedor de los topes, hoy el máximo de la plataforma). Sin fila vale el valor por
/// defecto, recortado a ese tope.
/// </summary>
internal sealed class WhatsAppSettingsService(IWhatsAppSettingsReader reader, IWhatsAppSettingsRepository repository,
    WhatsAppSettingsResolver resolver, IUnitOfWork unitOfWork, IRequestValidator validator, ICultureCatalog cultures, TimeProvider clock,
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
                // El tope se lee adentro, bajo el lock de la organización: un cambio de la plataforma posterior lo recorta al leer.
                // El validador del pedido mira solo de 1 a 3650; pasarse del tope es el 400 de siempre, en su campo.
                var max = (await resolver.OrganizationChatRetentionCeilingAsync(ct)).Count!.Value;
                var created = row is null;
                row ??= WhatsAppOrganizationSettings.Create();
                var updated = row.Update(request.ChatRetentionDays!.Value, max);
                if (updated.IsFailure) return updated.Error;
                if (created) repository.Add(row); else repository.MarkUpdated(row);
                return row;
            }, CommitPolicy.OnSuccess, cancellationToken);
            if (result.IsFailure && result.Error.Code == WhatsAppErrors.ChatRetentionOutOfRangeCode)
                return FieldErrors.On(result.Error, nameof(request.ChatRetentionDays), await OutOfRangeTextAsync(result.Error, cancellationToken));
            return VersionResponse.Of(result);
        });
    }

    /// <summary>
    /// El validador no conoce el máximo de la plataforma: el texto del campo lo dice, con los números de la cultura de la petición
    /// (sin una, la predeterminada del catálogo). El del código, sin números, queda para el detalle del problema y el contrato.
    /// </summary>
    private async Task<string> OutOfRangeTextAsync(Error error, CancellationToken ct)
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        var profile = await new CultureProfiles(cultures).LoadAsync(culture.Length == 0 ? null : culture, ct);
        return WhatsAppTexts.Format("WhatsApp.Settings.ChatRetentionRange", profile, WhatsAppSettingsLimits.MinChatRetentionDays,
            error.Metadata![WhatsAppErrors.MaxChatRetentionDaysKey]!);
    }
}

/// <summary>
/// La configuración de WhatsApp de la plataforma (270 y 272): los países que reciben códigos y los plazos de los chats. Sin fila,
/// los países son los de <c>WhatsApp:AllowedCountries</c> (0060) y los plazos, los de defecto. Es global (ADR 0050): cada
/// guardado deja su evento de seguridad en la misma transacción, como el bot y los equipos de Plataforma.
/// </summary>
internal sealed class PlatformWhatsAppSettingsService(IWhatsAppSettingsRepository repository, WhatsAppSettingsResolver resolver,
    PlatformWhatsAppAuditWriter audit, IWhatsAppRateReader rates, IUnitOfWork unitOfWork, IRequestValidator validator, TimeProvider clock,
    ILogger<PlatformWhatsAppSettingsService> logger) : IPlatformWhatsAppSettingsService
{
    private const string Reason = "WhatsApp platform settings changed";

    private static readonly WhatsAppPricingCategory[] TemplatePricingCategories =
    [
        WhatsAppPricingCategory.Marketing,
        WhatsAppPricingCategory.Utility,
        WhatsAppPricingCategory.Authentication,
    ];

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
                if (!await HasCoveredCountryAsync(request.AllowedCountries!, ct))
                    return WhatsAppErrors.CountriesWithoutRates;
                var created = row is null;
                row ??= WhatsAppPlatformSettings.Create(resolver.ConfiguredCountries);
                var changes = new SecurityEventChanges();
                changes.AddCount("whatsappAllowedCountries", row.AllowedCountries.Count, request.AllowedCountries!.Count);
                changes.AddCount("whatsappChatRetentionDays", row.ChatRetentionDays, request.ChatRetentionDays);
                changes.AddCount("whatsappMaxOrganizationChatRetentionDays", row.MaxOrganizationChatRetentionDays,
                    request.MaxOrganizationChatRetentionDays);
                if (!row.Update(request.AllowedCountries!, request.ChatRetentionDays!.Value, request.MaxOrganizationChatRetentionDays!.Value))
                    return FieldErrors.Validation(nameof(request.AllowedCountries), ValidationTexts.CountryInvalid);
                if (created) repository.Add(row); else repository.MarkUpdated(row);
                audit.Record(Reason, changes);
                return row;
            }, CommitPolicy.OnSuccess, cancellationToken);
            return VersionResponse.Of(result);
        });
    }

    /// <summary>Al menos un país habilitado debe cubrir las categorías de plantillas para que el estimador tenga respaldo.</summary>
    private async Task<bool> HasCoveredCountryAsync(IReadOnlyList<string> countries, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        foreach (var country in countries)
        {
            var covered = true;
            foreach (var category in TemplatePricingCategories)
            {
                if (await rates.FindAsync(country, category, today, ct) is null)
                {
                    covered = false;
                    break;
                }
            }
            if (covered) return true;
        }
        return false;
    }
}
