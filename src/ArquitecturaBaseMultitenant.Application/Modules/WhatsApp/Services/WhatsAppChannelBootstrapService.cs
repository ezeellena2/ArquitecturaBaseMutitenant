using System.Security.Cryptography;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Logging;
using WhatsAppUniqueKeys = ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.UniqueKeys;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

internal sealed partial class WhatsAppChannelBootstrapService(IWhatsAppChannelRepository channels, IWhatsAppTokenProtector protector,
    WhatsAppChannelAuditWriter audit, IUnitOfWork unitOfWork, TimeProvider clock, ILogger<WhatsAppChannelBootstrapService> logger)
    : IWhatsAppChannelBootstrapService
{
    private const string Reason = "WhatsApp platform number from configuration";
    /// <summary>Plataforma no tiene zona propia: su cupo se corta en UTC.</summary>
    internal static string PlatformUsageTimeZoneId => TimeZoneInfo.Utc.Id;

    public Task<Result> EnsurePlatformAsync(WhatsAppPlatformConfiguration configuration, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppPlatformBootstrap", async () =>
        {
            ArgumentNullException.ThrowIfNull(configuration);
            if (string.IsNullOrWhiteSpace(configuration.PhoneNumberId) || string.IsNullOrWhiteSpace(configuration.AccessToken)) return Result.Success();
            try { return await unitOfWork.ExecuteInTransactionAsync(ct => EnsureCoreAsync(configuration, ct), CommitPolicy.OnSuccess, cancellationToken); }
            catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == WhatsAppUniqueKeys.ActiveChannelNumber)
            {
                NumberTaken(logger);
                return Result.Success();
            }
        });

    private async Task<Result> EnsureCoreAsync(WhatsAppPlatformConfiguration configuration, CancellationToken ct)
    {
        var phoneNumberId = configuration.PhoneNumberId!.Trim();
        var token = configuration.AccessToken!;
        var wabaId = configuration.WabaId?.Trim() ?? string.Empty;
        var display = Display(configuration.DisplayPhoneNumber);
        await channels.LockNumberAsync(phoneNumberId, ct);
        var current = await channels.GetPlatformAsync(null, ct);
        if (current is { Source: WhatsAppChannelSource.Signup }) return Result.Success();
        if (current is not null && current.PhoneNumberId == phoneNumberId)
        {
            var changed = current.RefreshFromConfiguration(wabaId, display);
            if (!SameToken(current, token)) { current.ProtectSecrets(protector.Protect(current.Id, token), null); changed = true; }
            if (!changed) return Result.Success();
            channels.MarkUpdated(current);
            audit.Record(current, Reason, new SecurityEventChanges(), bySystem: true);
            return Result.Success();
        }
        if (await channels.GetActiveAsync(phoneNumberId, ct) is { OrganizationId: not null })
        {
            NumberTaken(logger);
            return Result.Success();
        }
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        if (current is not null)
        {
            var before = current.Status;
            current.Disconnect(nowUtc);
            channels.MarkUpdated(current);
            audit.Record(current, Reason, WhatsAppChannelAuditWriter.Status(before, current.Status), bySystem: true);
        }
        var channel = WhatsAppChannel.Connect(null, null, phoneNumberId, wabaId, display, verifiedName: null, coexistence: false,
            hasPayment: true, WhatsAppChannelSource.Configuration, PlatformUsageTimeZoneId, nowUtc);
        channel.ProtectSecrets(protector.Protect(channel.Id, token), null);
        channels.Add(channel);
        audit.Record(channel, Reason, WhatsAppChannelAuditWriter.Status(null, channel.Status), bySystem: true);
        return Result.Success();
    }

    private bool SameToken(WhatsAppChannel channel, string token)
    {
        try { return channel.EncryptedToken.Length > 0 && protector.Unprotect(channel.Id, channel.EncryptedToken) == token; }
        catch (CryptographicException) { return false; }
    }

    /// <summary>El número público que muestra la configuración (con o sin «+» y espacios), en E.164.</summary>
    internal static PhoneNumber? Display(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var normalized = string.Concat(raw.Where(char.IsAsciiDigit));
        var phone = PhoneNumber.Create("+" + normalized);
        return phone.IsSuccess ? phone.Value : null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The configured WhatsApp platform number is connected to an organization; the configuration is ignored.")]
    private static partial void NumberTaken(ILogger logger);
}
