using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
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
        LoginMethodType newMethodType, string newMethodValue, Guid? userId, DateTime createdAtUtc)
    {
        LoginCodeId = loginCodeId;
        PreviousMethodType = previousMethodType;
        PreviousMethodValue = previousMethodValue;
        NewMethodType = newMethodType;
        NewMethodValue = newMethodValue;
        UserId = userId;
        Status = AccountRecoveryStatus.Pending;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>El código que verificó el método nuevo. Registra un solo pedido.</summary>
    public Guid LoginCodeId { get; private set; }

    public LoginMethodType PreviousMethodType { get; private set; }

    /// <summary>El correo normalizado o el teléfono en E.164 con el que la persona dice que entraba.</summary>
    public string PreviousMethodValue { get; private set; }

    /// <summary>Correo o teléfono ya probado con el código y nonce de recuperación.</summary>
    public LoginMethodType NewMethodType { get; private set; }

    public string NewMethodValue { get; private set; }

    /// <summary>La cuenta que tiene verificado el método de antes, o null si no hay ninguna.</summary>
    public Guid? UserId { get; private set; }

    public AccountRecoveryStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? DecidedAtUtc { get; private set; }

    public Guid? DecidedByOperatorId { get; private set; }

    /// <summary>El motivo interno del operador; la persona no lo ve.</summary>
    public string? DecisionReason { get; private set; }

    public uint Version { get; private set; }

    public static AccountRecoveryRequest Submit(Guid loginCodeId, LoginMethodType previousMethodType,
        string previousMethodValue, Email newEmail, Guid? userId, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(newEmail);
        return Submit(loginCodeId, previousMethodType, previousMethodValue, LoginMethodType.Email, newEmail.Value, userId, nowUtc);
    }

    public static AccountRecoveryRequest Submit(Guid loginCodeId, LoginMethodType previousMethodType,
        string previousMethodValue, PhoneNumber newPhone, Guid? userId, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(newPhone);
        return Submit(loginCodeId, previousMethodType, previousMethodValue, LoginMethodType.Phone, newPhone.Value, userId, nowUtc);
    }

    private static AccountRecoveryRequest Submit(Guid loginCodeId, LoginMethodType previousMethodType,
        string previousMethodValue, LoginMethodType newMethodType, string newMethodValue, Guid? userId, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(previousMethodValue);
        if (loginCodeId == Guid.Empty) throw new ArgumentException("The verified code is required.", nameof(loginCodeId));
        if (previousMethodType is not (LoginMethodType.Email or LoginMethodType.Phone))
            throw new ArgumentOutOfRangeException(nameof(previousMethodType), previousMethodType,
                "Only an email or a phone can be the previous method.");
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("The instant must be UTC.", nameof(nowUtc));
        return new AccountRecoveryRequest(loginCodeId, previousMethodType, previousMethodValue, newMethodType, newMethodValue, userId, nowUtc);
    }

    /// <summary>Aprobar exige una cuenta encontrada; el caso de uso suma el método nuevo como principal.</summary>
    public Result Approve(Guid operatorId, string reason, DateTime nowUtc)
    {
        if (Status != AccountRecoveryStatus.Pending) return AccountRecoveryErrors.AlreadyResolved;
        if (UserId is null) return AccountRecoveryErrors.NoAccount;
        Decide(AccountRecoveryStatus.Approved, operatorId, reason, nowUtc);
        return Result.Success();
    }

    public Result Reject(Guid operatorId, string reason, DateTime nowUtc)
    {
        if (Status != AccountRecoveryStatus.Pending) return AccountRecoveryErrors.AlreadyResolved;
        Decide(AccountRecoveryStatus.Rejected, operatorId, reason, nowUtc);
        return Result.Success();
    }

    private void Decide(AccountRecoveryStatus status, Guid operatorId, string reason, DateTime nowUtc)
    {
        if (operatorId == Guid.Empty) throw new ArgumentException("The operator is required.", nameof(operatorId));
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("The instant must be UTC.", nameof(nowUtc));
        Status = status;
        DecidedByOperatorId = operatorId;
        DecisionReason = reason;
        DecidedAtUtc = nowUtc;
    }
}
