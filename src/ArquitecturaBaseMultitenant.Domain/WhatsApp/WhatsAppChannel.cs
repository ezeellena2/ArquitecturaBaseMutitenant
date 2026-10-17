using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>
/// Un número conectado (decisión 2 de la E11): de una organización o de Plataforma (<see cref="OrganizationId"/> null).
/// Es global: el webhook y el outbox lo ubican por <see cref="PhoneNumberId"/> sin entrar a una organización.
/// Desconectado conserva la fila para que sus chats se lean (D7); el número se puede volver a conectar en otra fila.
/// </summary>
public sealed class WhatsAppChannel : Entity, IGlobalSecurityAudited, IVersioned
{
    private WhatsAppChannel() { }
    SecurityEventType IGlobalSecurityAudited.SecurityEventType =>
        OrganizationId is null ? SecurityEventType.PlatformSettingsChanged : SecurityEventType.TenantFeaturesChanged;
    Guid? IGlobalSecurityAudited.SecurityEventTargetTenantId => OrganizationId;
    public uint Version { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public Guid? OrganizationId { get; private set; }
    /// <summary>La empresa que atiende el número; null atiende a toda la organización.</summary>
    public Guid? CompanyId { get; private set; }
    public string PhoneNumberId { get; private set; } = string.Empty;
    public string WabaId { get; private set; } = string.Empty;
    /// <summary>El número en E.164; el arranque desde la configuración puede no tenerlo.</summary>
    public string? DisplayNumber { get; private set; }
    public string? VerifiedName { get; private set; }
    public WhatsAppNumberStatus Status { get; private set; }
    public bool Coexistence { get; private set; }
    public WhatsAppChannelSource Source { get; private set; }
    /// <summary>Plataforma: si el número también atiende consultas (además de códigos y avisos).</summary>
    public bool ConsultationsEnabled { get; private set; }
    /// <summary>Zona del corte mensual del cupo gratis.</summary>
    public string UsageTimeZoneId { get; private set; } = string.Empty;
    [NotAudited] public byte[] EncryptedToken { get; private set; } = [];
    [NotAudited] public byte[]? EncryptedPin { get; private set; }
    public DateTime ConnectedAtUtc { get; private set; }
    public DateTime? DisconnectedAtUtc { get; private set; }
    public bool IsDisconnected => Status == WhatsAppNumberStatus.Disconnected;

    public static WhatsAppChannel Connect(Guid? organizationId, Guid? companyId, string phoneNumberId, string wabaId,
        PhoneNumber? displayNumber, string? verifiedName, bool coexistence, bool hasPayment, WhatsAppChannelSource source,
        string usageTimeZoneId, DateTime nowUtc)
    {
        if (organizationId == Guid.Empty || companyId == Guid.Empty) throw new ArgumentException("Empty identifiers are invalid.");
        if (organizationId is null && companyId is not null) throw new ArgumentException("Platform numbers serve no company.", nameof(companyId));
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumberId);
        if (source == WhatsAppChannelSource.Signup && (string.IsNullOrWhiteSpace(wabaId) || displayNumber is null))
            throw new ArgumentException("The signup requires the Meta account and the number.");
        ArgumentNullException.ThrowIfNull(wabaId);
        if (organizationId is not null && source != WhatsAppChannelSource.Signup) throw new ArgumentException("Organizations connect with the signup.", nameof(source));
        ArgumentException.ThrowIfNullOrWhiteSpace(usageTimeZoneId);
        if (phoneNumberId.Length > TextLimits.ShortName || wabaId.Length > TextLimits.ShortName)
            throw new ArgumentException("Meta identifiers are too long.");
        if (verifiedName?.Length > TextLimits.OrganizationName) throw new ArgumentException("The verified name is too long.", nameof(verifiedName));
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.", nameof(nowUtc));
        return new WhatsAppChannel
        {
            OrganizationId = organizationId, CompanyId = companyId, PhoneNumberId = phoneNumberId, WabaId = wabaId,
            DisplayNumber = displayNumber?.Value, VerifiedName = string.IsNullOrWhiteSpace(verifiedName) ? null : verifiedName.Trim(),
            Coexistence = coexistence, Source = source, UsageTimeZoneId = usageTimeZoneId, ConnectedAtUtc = nowUtc,
            Status = hasPayment ? WhatsAppNumberStatus.Connected : WhatsAppNumberStatus.PaymentMissing,
            // El de Plataforma conectado por la pantalla atiende consultas; el que sale de la configuración (una instalación de la
            // Etapa 8) sigue solo con el bot de ingreso hasta que un operador las prende.
            ConsultationsEnabled = organizationId is null && source == WhatsAppChannelSource.Signup,
        };
    }

    public void ProtectSecrets(byte[] encryptedToken, byte[]? encryptedPin)
    {
        if (encryptedToken is not { Length: > 0 }) throw new ArgumentException("An encrypted token is required.", nameof(encryptedToken));
        EncryptedToken = encryptedToken;
        EncryptedPin = encryptedPin;
    }

    /// <summary>El arranque vuelve a leer la configuración: un número que salió de ahí la sigue mientras nadie lo conecte por el alta.</summary>
    public bool RefreshFromConfiguration(string wabaId, PhoneNumber? displayNumber)
    {
        ArgumentNullException.ThrowIfNull(wabaId);
        if (Source != WhatsAppChannelSource.Configuration || IsDisconnected) throw new InvalidOperationException("Only configured numbers follow the configuration.");
        if (wabaId.Length > TextLimits.ShortName) throw new ArgumentException("Meta identifiers are too long.", nameof(wabaId));
        if (WabaId == wabaId && DisplayNumber == displayNumber?.Value) return false;
        WabaId = wabaId;
        DisplayNumber = displayNumber?.Value;
        return true;
    }

    /// <summary>Lo que dijo Meta sobre el medio de pago; un número desconectado no cambia.</summary>
    public bool ApplyPayment(bool hasPayment)
    {
        if (IsDisconnected) return false;
        var next = hasPayment ? WhatsAppNumberStatus.Connected : WhatsAppNumberStatus.PaymentMissing;
        if (Status == next) return false;
        Status = next;
        return true;
    }

    public void ServeCompany(Guid? companyId)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("Empty company id.", nameof(companyId));
        if (OrganizationId is null && companyId is not null) throw new InvalidOperationException("Platform numbers serve no company.");
        CompanyId = companyId;
    }

    public void SetConsultations(bool enabled)
    {
        if (OrganizationId is not null) throw new InvalidOperationException("Only the platform number has consultations.");
        ConsultationsEnabled = enabled;
    }

    /// <summary>D7: deja los chats para leer. Las credenciales se descartan.</summary>
    public void Disconnect(DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.", nameof(nowUtc));
        if (IsDisconnected) return;
        Status = WhatsAppNumberStatus.Disconnected;
        DisconnectedAtUtc = nowUtc;
        EncryptedToken = [];
        EncryptedPin = null;
    }

    /// <summary>El primer día del mes del cupo, con la fecha local del número.</summary>
    public static DateOnly UsageMonth(DateTime local) => new(local.Year, local.Month, 1);
}
