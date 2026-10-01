using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.Json;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Json;

/// <summary>
/// Comprueba que el value object de correo siga viajando como string JSON. Mantiene el contrato público
/// pese a la representación interna tipada.
/// </summary>
public sealed class EmailJsonTests
{
    [Fact]
    public void Email_value_object_keeps_the_public_json_string_contract()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        JsonConfiguration.ConfigureJson(options);
        var email = Email.Create("  ANA@Example.TEST  ").Value;

        var json = JsonSerializer.Serialize(new EmailEnvelope(email), options);
        var restored = JsonSerializer.Deserialize<EmailEnvelope>(json, options);

        Assert.Equal("{\"email\":\"ana@example.test\"}", json);
        Assert.Equal(email, restored?.Email);
    }

    private sealed record EmailEnvelope(Email? Email);
}
