using System.Text.Json;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Recorrido;

/// <summary>
/// Los argumentos de una orden privada del recorrido: solo los que admite y con su forma, sobre destinos de prueba, o la orden
/// se rechaza. Nunca sale del host de Testing.
/// </summary>
public static class RecorridoCommandArguments
{
    public static void RequireTesting(IServiceProvider services)
    {
        if (!services.GetRequiredService<IHostEnvironment>().IsEnvironment("Testing"))
            throw new InvalidOperationException("Private commands require isolated Testing.");
    }

    public static void Allow(JsonElement command, params string[] names)
    {
        if (command.ValueKind != JsonValueKind.Object
            || command.EnumerateObject().Any(property => !names.Contains(property.Name, StringComparer.Ordinal)))
            throw new InvalidOperationException("Private command parameters invalid.");
    }

    public static string Text(JsonElement command, string name) =>
        OptionalText(command, name) ?? throw new InvalidOperationException($"Private command requires {name}.");

    public static string? OptionalText(JsonElement command, string name)
    {
        if (!command.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        return value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 and <= 200 } text
            ? text : throw new InvalidOperationException($"Private command {name} invalid.");
    }

    public static Guid? OptionalGuid(JsonElement command, string name) =>
        OptionalText(command, name) is not { } text ? null
        : Guid.TryParse(text, out var id) && id != Guid.Empty ? id : throw new InvalidOperationException($"Private command {name} invalid.");

    /// <summary>Un correo del dominio de prueba.</summary>
    public static Email SyntheticEmail(string value)
    {
        var email = Email.Create(value);
        return email.IsSuccess && email.Value.Value.EndsWith("@example.test", StringComparison.Ordinal)
            ? email.Value : throw new InvalidOperationException("Only an isolated test mailbox is allowed.");
    }

    /// <summary>Un teléfono sintético de la fixture (+54911345678NN), como los del canal privado de WhatsApp.</summary>
    public static PhoneNumber SyntheticPhone(string value) =>
        value.Length == 14 && value.StartsWith("+54911345678", StringComparison.Ordinal) && value[1..].All(char.IsAsciiDigit)
            ? PhoneNumber.Create(value).Value : throw new InvalidOperationException("Only a synthetic fixture phone is allowed.");

    /// <summary>La cuenta de un correo verificado: quien invita, el actor de la orden.</summary>
    public static Task<Guid> UserIdAsync(ApplicationDbContext db, Email email, CancellationToken ct) =>
        db.LoginMethods.AsNoTracking().Where(method => method.Value == email.Value && method.VerifiedAtUtc != null)
            .Select(method => method.UserId).Distinct().SingleAsync(ct);
}
