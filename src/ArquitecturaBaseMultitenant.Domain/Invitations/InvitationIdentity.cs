using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.Invitations;

/// <summary>
/// A quién va una invitación y qué hay que probar para aceptarla: un correo o un teléfono en E.164 (decisión 11 de la
/// E11). Se guarda como su valor: un correo siempre tiene «@» y un E.164 nunca. Un correo se convierte solo, así que lo
/// que invita por correo no cambia.
/// </summary>
public sealed class InvitationIdentity : ValueObject
{
    private InvitationIdentity(Email? email, PhoneNumber? phone)
    {
        Email = email;
        Phone = phone;
    }

    public Email? Email { get; }
    public PhoneNumber? Phone { get; }
    public bool IsPhone => Phone is not null;
    public string Value => Email?.Value ?? Phone!.Value;
    /// <summary>Para nombrar a quien todavía no tiene cuenta: el correo como lo escribieron o el teléfono en E.164.</summary>
    public string DisplayValue => Email?.DisplayValue ?? Phone!.Value;

    public static InvitationIdentity ForEmail(Email email)
    {
        ArgumentNullException.ThrowIfNull(email);
        return new InvitationIdentity(email, null);
    }

    public static InvitationIdentity ForPhone(PhoneNumber phone)
    {
        ArgumentNullException.ThrowIfNull(phone);
        return new InvitationIdentity(null, phone);
    }

    /// <summary>Lee el valor guardado. Uno que no es correo ni E.164 es un dato roto: es un bug, no una regla.</summary>
    public static InvitationIdentity Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Contains('@', StringComparison.Ordinal)
            ? ForEmail(ValueObjects.Email.Create(value).Value)
            : ForPhone(PhoneNumber.Create(value).Value);
    }

    public static implicit operator InvitationIdentity(Email email) => ForEmail(email);

    // Propios para que comparar con un Email no compile en silencio como dos ValueObject distintos.
    public static bool operator ==(InvitationIdentity? left, InvitationIdentity? right) => Equals(left, right);
    public static bool operator !=(InvitationIdentity? left, InvitationIdentity? right) => !Equals(left, right);

    public override bool Equals(object? obj) => base.Equals(obj);
    public override int GetHashCode() => base.GetHashCode();
    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
