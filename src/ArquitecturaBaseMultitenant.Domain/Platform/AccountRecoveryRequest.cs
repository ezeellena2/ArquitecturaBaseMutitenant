using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.Platform;

/// <summary>
/// Pedido público de «Recuperar mi cuenta» (multitenancy.md §3.1): el método con el que la persona entraba y uno
/// nuevo, ya verificado con código. Guarda la cuenta encontrada por el método de antes, o ninguna, sin revelarlo a
/// quien lo pide. Un operador lo aprueba o lo rechaza. Los valores son datos personales: nunca van a logs ni a la
/// auditoría.
/// </summary>
public sealed class AccountRecoveryRequest : Entity, IVersioned
{
    // Para EF Core.
    private AccountRecoveryRequest()
    {
        PreviousMethodValue = string.Empty;
        NewMethodValue = string.Empty;
    }

    private AccountRecoveryRequest(Guid loginCodeId, LoginMethodType previousMethodType, string previousMethodValue,
        Email newEmail, Guid? userId, DateTime createdAtUtc)
    {
        LoginCodeId = loginCodeId;
        PreviousMethodType = previousMethodType;
        PreviousMethodValue = previousMethodValue;
        NewMethodType = LoginMethodType.Email;
        NewMethodValue = newEmail.Value;
        UserId = userId;
        Status = AccountRecoveryStatus.Pending;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>El código que verificó el método nuevo. Registra un solo pedido.</summary>
    public Guid LoginCodeId { get; private set; }

    public LoginMethodType PreviousMethodType { get; private set; }

    /// <summary>El correo normalizado o el teléfono en E.164 con el que la persona dice que entraba.</summary>
    public string PreviousMethodValue { get; private set; }

    /// <summary>Por ahora, solo correo: el teléfono como método nuevo llega con el canal de WhatsApp.</summary>
    public LoginMethodType NewMethodType { get; private set; }

    public string NewMethodValue { get; private set; }

    /// <summary>La cuenta que tiene verificado el método de antes, o null si no hay ninguna.</summary>
    public Guid? UserId { get; private set; }

    public AccountRecoveryStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public uint Version { get; private set; }

    public static AccountRecoveryRequest Submit(Guid loginCodeId, LoginMethodType previousMethodType,
        string previousMethodValue, Email newEmail, Guid? userId, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(newEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(previousMethodValue);
        if (loginCodeId == Guid.Empty) throw new ArgumentException("The verified code is required.", nameof(loginCodeId));
        if (previousMethodType is not (LoginMethodType.Email or LoginMethodType.Phone))
            throw new ArgumentOutOfRangeException(nameof(previousMethodType), previousMethodType,
                "Only an email or a phone can be the previous method.");
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("The instant must be UTC.", nameof(nowUtc));
        return new AccountRecoveryRequest(loginCodeId, previousMethodType, previousMethodValue, newEmail, userId, nowUtc);
    }
}
