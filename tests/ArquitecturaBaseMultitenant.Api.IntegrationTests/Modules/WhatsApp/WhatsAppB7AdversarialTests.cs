using System.Net;
using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// Revisión adversarial de la B7 (ADR 0066): lo que no llegó a Meta no queda «Sin confirmar», los códigos con que Meta limita
/// se reintentan como límite, y el tope de dos reintentos de WhatsApp no se pierde por configurar solo su espera. Todo con
/// fakes: ninguna llamada a Meta.
/// </summary>
public sealed class WhatsAppB7AdversarialTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// Antes del POST, el transporte busca el número y descifra su token. Si la base no responde o el token no se puede abrir
    /// (otro anillo de claves), la excepción sale del cliente y la cola la toma como una falla durante la llamada:
    /// «Sin confirmar», que nadie repite. Meta no recibió nada: tiene que volver a la cola o fallar, nunca quedar dudoso.
    /// </summary>
    [Theory]
    [InlineData("database")]
    [InlineData("token")]
    [InlineData("canceled")]
    public async Task A_failure_before_the_POST_sent_nothing_and_never_stays_unconfirmed(string failure)
    {
        using var handler = new Counting();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://graph.example.test/") };
        var protector = new WhatsAppTokenProtector(new EphemeralDataProtectionProvider());
        var credentials = new WhatsAppChannelCredentials(new FailingReader(failure), protector);
        var client = new WhatsAppCloudClient(http, Options.Create(new WhatsAppOptions { AccessToken = "fixture-token" }), TimeProvider.System, credentials);
        var message = new WhatsAppOutboundMessage("number", "+5491134567890", "", "es", [], Body: "Hola",
            ReplyExpiresAtUtc: TimeProvider.System.GetUtcNow().UtcDateTime.AddHours(1), ChannelId: Guid.NewGuid());

        OutboxDeliveryReceipt receipt;
        try { receipt = await client.SendAsync(message, Ct); }
        // Lo que hace OutboxDeliveryProcessor con una excepción del transporte: pudo haber salido, «Sin confirmar».
        catch (Exception) { receipt = OutboxDeliveryReceipt.Uncertain(OutboxDeliveryProcessor.TransportFailedCode); }

        Assert.Equal(0, handler.Calls);
        Assert.NotEqual(OutboxAttemptOutcome.Uncertain, receipt.Outcome);
        // No salió: vuelve a la cola, que puede tomarlo otra copia.
        Assert.Equal((OutboxAttemptOutcome.Retry, WhatsAppSendFailures.Unavailable), (receipt.Outcome, receipt.SafeCode));
    }

    /// <summary>
    /// Los caminos hermanos de antes del POST: un payload que no se puede leer o un mensaje que no se puede armar no salen nunca.
    /// Fallan sin llamar a Meta, como el payload ilegible del correo, y no quedan «Sin confirmar».
    /// </summary>
    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{\"PhoneNumberId\":\"number\",\"To\":\"not-a-phone\",\"TemplateName\":\"codigo\",\"Language\":\"es\",\"BodyParameters\":[]}")]
    public async Task A_payload_that_cannot_be_read_or_built_fails_without_calling_meta(string payload)
    {
        using var handler = new Counting();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://graph.example.test/") };
        var sender = new WhatsAppChannelSender(new WhatsAppCloudClient(http,
            Options.Create(new WhatsAppOptions { AccessToken = "fixture-token" }), TimeProvider.System));

        var receipt = await sender.SendWithReceiptAsync(payload, Ct);

        Assert.Equal(0, handler.Calls);
        Assert.Equal((OutboxAttemptOutcome.Rejected, WhatsAppSendFailures.Rejected), (receipt.Outcome, receipt.SafeCode));
    }

    /// <summary>
    /// Los errores de límite de Meta («Throttling errors» de la Cloud API) llegan con un 400: 4 (API Too Many Calls) y 131048
    /// (Spam rate limit hit), junto a 80007, 130429 y 131056, que ya se toman. Meta limitó y no salió: se reintenta con
    /// «WhatsApp frenó el envío por su límite», no falla como «WhatsApp rechazó el mensaje».
    /// </summary>
    [Theory]
    [InlineData(4)]
    [InlineData(131048)]
    public void Every_meta_throttling_code_is_retried_as_rate_limited(int code)
    {
        var receipt = WhatsAppCloudClient.Classify(HttpStatusCode.BadRequest, code);
        Assert.Equal((OutboxAttemptOutcome.Retry, WhatsAppSendFailures.RateLimited), (receipt.Outcome, receipt.SafeCode));
    }

    /// <summary>
    /// configuracion.md §5: el módulo deja WhatsApp en tres intentos «si Messaging:Outbox:Channels:whatsapp:MaxAttempts no dice
    /// otra cosa». Con solo la espera propia del canal (la clave que documenta y prueba OutboxOptionsValidatorTests), el
    /// TryAdd no entra y WhatsApp toma los cinco intentos generales: cuatro reintentos en vez de dos.
    /// </summary>
    [Theory]
    [InlineData("whatsapp")]
    [InlineData("WhatsApp")]
    public void Whatsapp_keeps_its_two_retries_when_only_its_wait_is_configured(string key)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"Messaging:Outbox:Channels:{key}:DeliveryTimeoutSeconds"] = "20",
            ["WhatsApp:PhoneNumberId"] = "fixture-number",
            ["WhatsApp:AccessToken"] = "fixture-token",
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<OutboxOptions>().BindConfiguration(OutboxOptions.SectionName);
        services.AddWhatsAppModule(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<OutboxOptions>>().Value;

        Assert.Equal(TimeSpan.FromSeconds(20), options.DeliveryTimeoutFor("whatsapp"));
        Assert.Equal(WhatsAppCloudClient.MaxAttempts, options.MaxAttemptsFor("whatsapp"));
    }

    /// <summary>Una clave propia de <c>MaxAttempts</c> sigue mandando sobre el tope del módulo.</summary>
    [Fact]
    public void An_own_attempts_key_still_wins_over_the_module()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Messaging:Outbox:Channels:whatsapp:MaxAttempts"] = "1",
            ["WhatsApp:PhoneNumberId"] = "fixture-number",
            ["WhatsApp:AccessToken"] = "fixture-token",
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<OutboxOptions>().BindConfiguration(OutboxOptions.SectionName);
        services.AddWhatsAppModule(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<OutboxOptions>>().Value;

        Assert.Equal(1, options.MaxAttemptsFor("whatsapp"));
        Assert.Equal(TimeSpan.FromSeconds(15), options.DeliveryTimeoutFor("whatsapp"));
    }

    private sealed class Counting : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"messages\":[{\"id\":\"wamid.fixture\"}]}") });
        }
    }

    /// <summary>La base que no responde, o el número con un token que este anillo de claves no abre.</summary>
    private sealed class FailingReader(string failure) : IWhatsAppChannelReader
    {
        public Task<WhatsAppSendingChannelRow?> FindSendingAsync(Guid? channelId, string phoneNumberId, CancellationToken ct)
        {
            if (failure == "database") throw new TimeoutException("The database did not answer.");
            // La espera de la cola se vence mientras se lee el canal.
            if (failure == "canceled") throw new OperationCanceledException(ct);
            var id = channelId ?? Guid.NewGuid();
            var foreign = new WhatsAppTokenProtector(new EphemeralDataProtectionProvider()).Protect(id, "fixture-token");
            return Task.FromResult<WhatsAppSendingChannelRow?>(new(id, phoneNumberId, "waba", null, WhatsAppNumberStatus.Connected, foreign));
        }
        public Task<IReadOnlyList<WhatsAppTemplateAccountRow>> ListTemplateAccountsAsync(bool platform, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<WhatsAppTemplateAccountRow>> ListAllTemplateAccountsAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> HasActiveAccountAsync(string wabaId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ArquitecturaBaseMultitenant.Application.Common.Pagination.PagedResult<WhatsAppNumberResponse>> ListOrganizationAsync(WhatsAppNumberListRequest request, DateTime nowUtc, CancellationToken ct) => throw new NotSupportedException();
        public Task<WhatsAppNumberResponse?> FindOrganizationAsync(Guid id, DateTime nowUtc, CancellationToken ct) => throw new NotSupportedException();
        public Task<int> CountOrganizationAsync(WhatsAppNumberStatus? status, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<WhatsAppNumberPendingRow>> ListPaymentMissingAsync(int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task<WhatsAppPlatformNumberRow?> FindPlatformAsync(DateTime nowUtc, CancellationToken ct) => throw new NotSupportedException();
        public Task<WhatsAppPlatformChannelRow?> FindPlatformStateAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> IsActiveAsync(string phoneNumberId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> ListOrganizationIdsAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<WhatsAppInboundChannel?> FindInboundAsync(Guid? channelId, string phoneNumberId, CancellationToken ct) => throw new NotSupportedException();
    }
}
