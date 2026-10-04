using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Conventions;

/// <summary>Persiste el value object PhoneNumber como su E.164 en una columna varchar(16) (telefonos.md), como EmailConvention con los correos.</summary>
internal static class PhoneNumberConvention
{
    public static void Configure(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<PhoneNumber>()
            .HaveConversion<PhoneNumberValueConverter>()
            .HaveMaxLength(PhoneNumber.MaxLength);
    }

    private sealed class PhoneNumberValueConverter() : ValueConverter<PhoneNumber, string>(
        phone => phone.Value,
        value => PhoneNumber.Create(value).Value);
}
