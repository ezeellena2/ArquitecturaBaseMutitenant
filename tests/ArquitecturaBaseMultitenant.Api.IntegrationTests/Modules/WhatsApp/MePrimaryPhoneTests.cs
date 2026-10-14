using ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

public sealed partial class MePrimaryPhoneTests : IAsyncLifetime
{
    // Los canales de estas pruebas son los suyos: el de WhatsApp no tiene que estar disponible por un número de
    // Plataforma que dejó conectado otra prueba de la colección (decisión 3 de la E11).
    public async ValueTask InitializeAsync() => await PlatformWhatsAppNumberReset.DisconnectAsync(factory, Ct);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
