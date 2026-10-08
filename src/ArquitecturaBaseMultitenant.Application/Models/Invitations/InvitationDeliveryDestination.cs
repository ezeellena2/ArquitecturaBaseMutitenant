using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Invitations;

/// <summary>Transporte de una invitación cuya identidad y prueba siguen siendo su Email original.</summary>
public sealed record InvitationDeliveryDestination
{
    private InvitationDeliveryDestination(LoginMethodType type, string value)
    {
        Type = type;
        Value = value;
    }

    public LoginMethodType Type { get; }
    public string Value { get; }
    public static InvitationDeliveryDestination ForEmail(Email email) => new(LoginMethodType.Email, email.Value);
    public static InvitationDeliveryDestination ForPhone(PhoneNumber phone) => new(LoginMethodType.Phone, phone.Value);
    public override string ToString() => nameof(InvitationDeliveryDestination);
}
