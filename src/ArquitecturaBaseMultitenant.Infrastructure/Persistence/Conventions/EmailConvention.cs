using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Conventions;

/// <summary>Persiste el value object Email como su valor canónico en todas las entidades. Centraliza la conversión para que cada configuración EF no la repita.</summary>
internal static class EmailConvention
{
    public static void Configure(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Email>()
            .HaveConversion<EmailValueConverter>()
            .HaveMaxLength(Email.MaxLength);
    }

    private sealed class EmailValueConverter() : ValueConverter<Email, string>(
        email => email.Value,
        value => Email.Create(value).Value);
}
