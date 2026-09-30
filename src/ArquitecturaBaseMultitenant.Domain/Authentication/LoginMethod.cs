using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>Un método de ingreso de una identidad global; no registra ni habilita canales.</summary>
public sealed class LoginMethod : Entity
{
    private LoginMethod() { }

    private LoginMethod(Guid userId, LoginMethodType type, string value)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("The user id cannot be empty.", nameof(userId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        UserId = userId;
        Type = type;
        Value = value;
    }

    public Guid UserId { get; private set; }
    public LoginMethodType Type { get; private set; }
    public string Value { get; private set; } = string.Empty;
    public bool IsPrimary { get; private set; }
    public DateTime? VerifiedAtUtc { get; private set; }
    public Guid? ManagedByTenantId { get; private set; }
    /// <summary>Correo que Google probó; Value conserva siempre el subject del proveedor.</summary>
    public Email? ContactEmail { get; private set; }

    public static LoginMethod CreateEmail(Guid userId, Email email)
    {
        ArgumentNullException.ThrowIfNull(email);
        return new LoginMethod(userId, LoginMethodType.Email, email.Value);
    }

    public static LoginMethod CreatePhone(Guid userId, PhoneNumber phoneNumber)
    {
        ArgumentNullException.ThrowIfNull(phoneNumber);
        return new LoginMethod(userId, LoginMethodType.Phone, phoneNumber.Value);
    }

    public static LoginMethod CreateGoogle(Guid userId, string providerSubject, Email? contactEmail = null) =>
        new(userId, LoginMethodType.Google, providerSubject) { ContactEmail = contactEmail };

    public void UpdateGoogleContact(Email contactEmail)
    {
        ArgumentNullException.ThrowIfNull(contactEmail);
        if (Type != LoginMethodType.Google)
            throw new InvalidOperationException("Only a Google method has a provider contact.");
        ContactEmail = contactEmail;
    }

    public void ClearPrimary() => IsPrimary = false;

    public Result Verify(DateTime verifiedAtUtc)
    {
        if (verifiedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The verification instant must be UTC.", nameof(verifiedAtUtc));
        }

        VerifiedAtUtc ??= verifiedAtUtc;
        return Result.Success();
    }

    public Result MakePrimary()
    {
        if (VerifiedAtUtc is null)
        {
            return LoginMethodErrors.NotVerified;
        }

        IsPrimary = true;
        return Result.Success();
    }

    public bool CanSignIn(bool channelAvailable, bool managedMembershipActive) =>
        VerifiedAtUtc is not null
        && channelAvailable
        && (ManagedByTenantId is null || managedMembershipActive);
}
