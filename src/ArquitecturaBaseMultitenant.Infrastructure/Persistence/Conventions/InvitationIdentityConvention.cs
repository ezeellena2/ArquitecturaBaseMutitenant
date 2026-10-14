using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Conventions;

/// <summary>
/// Persiste la identidad de una invitación como su valor, en la misma columna que antes tenía solo el correo: un correo
/// tiene «@» y un E.164 no, así que no hace falta otra columna (decisión 11 de la E11).
/// </summary>
internal static class InvitationIdentityConvention
{
    public static void Configure(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<InvitationIdentity>()
            .HaveConversion<InvitationIdentityValueConverter>()
            .HaveMaxLength(Email.MaxLength);
    }

    private sealed class InvitationIdentityValueConverter() : ValueConverter<InvitationIdentity, string>(
        identity => identity.Value,
        value => InvitationIdentity.Parse(value));
}
