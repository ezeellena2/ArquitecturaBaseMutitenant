using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>
/// A dónde va un código de ingreso: un correo o un número provisto por un módulo.
/// Solo se arma a partir de un <see cref="Email"/> o de un <see cref="PhoneNumber"/>, así que el valor ya llega
/// validado y normalizado: es lo que guarda la fila del código y lo que identifica al destino en el lock y en los
/// límites. A propósito no redefine <c>ToString</c>: un destino que termine en un log no deja el número a la vista.
/// </summary>
public sealed class LoginCodeDestination : ValueObject
{
    private LoginCodeDestination(string channel, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        Channel = channel;
        Value = value;
    }

    public string Channel { get; }

    /// <summary>El correo normalizado o el número en formato internacional.</summary>
    public string Value { get; }

    public static LoginCodeDestination ForEmail(Email email)
    {
        ArgumentNullException.ThrowIfNull(email);

        return new LoginCodeDestination(LoginCodeChannel.Email, email.Value);
    }

    public static LoginCodeDestination ForPhone(PhoneNumber phone, string channel)
    {
        ArgumentNullException.ThrowIfNull(phone);
        if (string.Equals(channel, LoginCodeChannel.Email, StringComparison.Ordinal))
        {
            throw new ArgumentException("A phone destination cannot use the email channel.", nameof(channel));
        }

        return new LoginCodeDestination(channel, phone.Value);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Channel;
        yield return Value;
    }
}
