using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

public sealed partial class PlatformOrganizationPhoneNoticeTests : IAsyncLifetime
{
    // El aviso por correo supone que no hay número de Plataforma, y la variante por WhatsApp conecta uno desde la
    // configuración (decisión 3 de la E11): cada caso empieza y termina sin número.
    public async ValueTask InitializeAsync() => await PlatformWhatsAppNumberReset.DisconnectAsync(factory, Ct);

    public async ValueTask DisposeAsync() => await PlatformWhatsAppNumberReset.DisconnectAsync(factory, Ct);

    [Theory]
    [InlineData("approve", "organizacion_aprobada")]
    [InlineData("reject", "organizacion_rechazada")]
    [InlineData("unpublish", "pagina_publica_moderada")]
    [InlineData("allow-publish", "pagina_publica_moderada")]
    public Task Real_actions_notify_phone_when_enabled_and_replay_does_not_duplicate(string action, string expected) =>
        AssertNoticeAsync(action, expected, "whatsapp", builder =>
        {
            builder.UseSetting("WhatsApp:PhoneNumberId", "notice-fixture");
            builder.UseSetting("WhatsApp:AccessToken", "fixture-token");
        }, (payload, phone, tenant) =>
        {
            var outbound = JsonSerializer.Deserialize<WhatsAppOutboundMessage>(payload)!;
            Assert.Equal(expected, outbound.TemplateName); Assert.Equal(phone.Value, outbound.To);
            Assert.Equal(tenant.Name, outbound.BodyParameters[0]); Assert.Null(outbound.ButtonParameter);
            if (action == "allow-publish") Assert.Contains("permitió", outbound.BodyParameters[1]);
            if (action == "unpublish") Assert.Contains("despublicó", outbound.BodyParameters[1]);
        });
}
