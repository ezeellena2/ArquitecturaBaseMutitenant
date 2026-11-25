using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Time.Testing;
namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.WhatsApp;

public sealed class WhatsAppBotTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Theory]
    [InlineData("es-AR", "Para entrar, usá la web.")]
    [InlineData("en-US", "To sign in, use the website.")]
    public async Task Operator_reply_is_exact_text_without_footer_or_actions(string culture, string expected)
    {
        var reply = await new WhatsAppBotReplyBuilder(new JsonReferenceDataCatalog()).BuildAsync("number", "+5491134567890", "Fixture", culture,
            "Bot.OperatorNeutralReply", new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc), Ct);
        Assert.Equal(expected, reply.Body); Assert.Null(reply.Footer); Assert.Null(reply.LinkLabel); Assert.Null(reply.LinkUrl); Assert.Null(reply.ReplyButtons);
    }
    [Theory]
    [InlineData("es-AR", "Lucía", "Hola, Lucía. Para entrar a Mi sitio, tocá «Entrar a mi cuenta» y pedí el código en la página.", "Entrar a mi cuenta")]
    [InlineData("es-AR", null, "Hola. Para entrar a Mi sitio, tocá «Entrar a mi cuenta» y pedí el código en la página.", "Entrar a mi cuenta")]
    [InlineData("en-US", "Lucy", "Hi, Lucy. To sign in to Mi sitio, tap “Go to my account” and request the code on the page.", "Go to my account")]
    public async Task Sign_in_reply_says_where_to_ask_for_the_code_and_its_button_fits_whatsapp(string culture, string? name,
        string expected, string label)
    {
        var reply = await new WhatsAppBotReplyBuilder(new JsonReferenceDataCatalog()).BuildAsync("number", "+5491134567890", "Mi sitio",
            culture, name is null ? "Bot.EnterAccountWithoutName" : "Bot.EnterAccount", new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc), Ct,
            name, "https://app.example/login", "Bot.EnterAccountButton", footer: false);
        Assert.Equal(expected, reply.Body); Assert.Equal(label, reply.LinkLabel); Assert.Null(reply.Footer);
        // Meta corta el texto de un botón cta_url a los 20 caracteres.
        Assert.InRange(reply.LinkLabel!.Length, 1, 20);
    }
    [Fact]
    public async Task Disabled_channel_consumes_no_reply()
    {
        var settings = new WhatsAppCodeQuotaTests.Settings { IsEnabled = false };
        var outbox = new Outbox();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero));
        var now = clock.GetUtcNow().UtcDateTime;
        var contact = WhatsAppContact.Create(PhoneNumber.Create("+59899123456").Value, now);
        contact.RecordInbound(now);
        var message = WhatsAppMessage.Create("fixture-number", "fixture-message", "inbound", WhatsAppMessageDirection.Inbound,
            WhatsAppMessageKind.Text, WhatsAppMessageStatus.Pending, now, now, contact.Id);
        await new WhatsAppBotWriter(settings, new WhatsAppSignInReplyIssuer(settings, outbox, null!, null!, null!, null!), clock)
            .ConsumeAsync(new(message, contact, null, null, false), Ct);
        Assert.Equal(0, outbox.Calls);
    }
    [Fact]
    public async Task Closed_signup_buttons_have_only_public_action_ids_and_configured_platform_name()
    {
        var reply = await new WhatsAppBotReplyBuilder(new JsonReferenceDataCatalog()).BuildAsync("number", "+5491134567890", "Mi sitio", "es-AR",
            "Bot.Closed", new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc), Ct, choices: true);
        Assert.Contains("Mi sitio", reply.Body); Assert.Null(reply.LinkUrl);
        Assert.Equal(new[] { BotButtons.SignIn, BotButtons.Recover }, reply.ReplyButtons!.Select(value => value.Id));
        Assert.Equal(new[] { "Ingresar", "Recuperar mi cuenta" }, reply.ReplyButtons!.Select(value => value.Title));
    }
    private sealed class Outbox : IOutbox
    {
        public int Calls { get; private set; }
        public Guid Enqueue(string channel, string payload, Guid? userId = null, ArquitecturaBaseMultitenant.Application.Models.Messaging.OutboxEnqueueOptions? options = null) { Calls++; return Guid.CreateVersion7(); }
    }
}
