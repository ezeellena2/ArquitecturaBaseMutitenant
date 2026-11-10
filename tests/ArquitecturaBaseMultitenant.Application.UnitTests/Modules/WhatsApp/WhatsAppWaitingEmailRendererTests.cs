using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Phones;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.WhatsApp;

/// <summary>
/// Tablero Mensajes, «Chats esperando» y «Consultas esperando» (D6): el resumen para el equipo, con cuántos esperan, hace
/// cuánto espera el más viejo y de qué organización, el botón a la bandeja y el pie que dice por qué le llega. Nunca
/// teléfonos ni textos de los chats. El armado HTML es del núcleo (EmailTemplateTests).
/// </summary>
public sealed class WhatsAppWaitingEmailRendererTests
{
    [Fact]
    public async Task Chats_waiting_tells_the_team_like_the_board()
    {
        var (to, notice) = await RenderAsync(new WhatsAppWaitingNotice("lucia@example.test", "Lucía", "Ventas", 3,
            TimeSpan.FromMinutes(25.5), "Grupo Delta", "https://example.test/org/whatsapp/chats", false, "es-AR", "America/Argentina/Buenos_Aires"));

        Assert.Equal("lucia@example.test", to);
        Assert.Equal("Tenés 3 chats esperando en Ventas", notice.Subject);
        Assert.Equal("3 chats esperando en Ventas", notice.Title);
        Assert.Equal(["Hola, Lucía. Hay 3 chats de WhatsApp sin tomar en Ventas, de Grupo Delta. El más viejo espera hace 25 minutos."],
            notice.Paragraphs);
        Assert.Empty(notice.Details);
        Assert.Equal(new EmailNoticeAction("Ver los chats", "https://example.test/org/whatsapp/chats"), notice.Action);
        Assert.Empty(notice.Notes);
        Assert.Equal("Te llega porque estás en el equipo Ventas. Quien administra WhatsApp elige cuándo se manda este aviso.", notice.Footer);
    }

    [Fact]
    public async Task Consultations_waiting_tells_the_platform_team_like_the_board()
    {
        var (_, notice) = await RenderAsync(new WhatsAppWaitingNotice("martin@example.test", "Martín", "Soporte", 2,
            TimeSpan.FromMinutes(15), "ArquitecturaBase", "https://example.test/plataforma/whatsapp", true, "es-AR", "UTC"));

        Assert.Equal("Tenés 2 consultas esperando en Soporte", notice.Subject);
        Assert.Equal("2 consultas esperando en Soporte", notice.Title);
        Assert.Equal(["Hola, Martín. Hay 2 consultas de WhatsApp sin tomar en Soporte, de ArquitecturaBase. La más vieja espera hace 15 minutos."],
            notice.Paragraphs);
        Assert.Equal(new EmailNoticeAction("Ver las consultas", "https://example.test/plataforma/whatsapp"), notice.Action);
        Assert.Equal("Te llega porque estás en el equipo Soporte. Quien administra el WhatsApp de Plataforma elige cuándo se manda este aviso.",
            notice.Footer);
    }

    [Fact]
    public async Task One_waiting_chat_reads_in_singular_and_a_long_wait_in_days()
    {
        var chats = (await RenderAsync(new WhatsAppWaitingNotice("lucia@example.test", null, "Ventas", 1, TimeSpan.FromDays(3.5),
            "Grupo Delta", "https://example.test/org/whatsapp/chats", false, "es-AR", "UTC"))).Notice;
        var consultations = (await RenderAsync(new WhatsAppWaitingNotice("martin@example.test", "Martín", "Soporte", 1,
            TimeSpan.FromHours(1), "ArquitecturaBase", "https://example.test/plataforma/whatsapp", true, "es-AR", "UTC"))).Notice;

        Assert.Equal("Tenés 1 chat esperando en Ventas", chats.Subject);
        Assert.Equal("1 chat esperando en Ventas", chats.Title);
        Assert.Equal(["Hola. Hay 1 chat de WhatsApp sin tomar en Ventas, de Grupo Delta. Espera hace 3 días."], chats.Paragraphs);
        Assert.Equal("Tenés 1 consulta esperando en Soporte", consultations.Subject);
        Assert.Equal(["Hola, Martín. Hay 1 consulta de WhatsApp sin tomar en Soporte, de ArquitecturaBase. Espera hace 1 hora."],
            consultations.Paragraphs);
    }

    [Fact]
    public async Task English_member_gets_the_same_summary_in_english()
    {
        var (_, notice) = await RenderAsync(new WhatsAppWaitingNotice("lucia@example.test", "Lucía", "Sales", 1200,
            TimeSpan.FromMinutes(25), "Grupo Delta", "https://example.test/org/whatsapp/chats", false, "en-US", "UTC"));

        Assert.Equal("You have 1,200 chats waiting in Sales", notice.Subject);
        Assert.Equal("1,200 chats waiting in Sales", notice.Title);
        Assert.Equal(["Hello, Lucía. There are 1,200 unassigned WhatsApp chats in Sales, at Grupo Delta. The oldest one has been waiting for 25 minutes."],
            notice.Paragraphs);
        Assert.Equal(new EmailNoticeAction("See the chats", "https://example.test/org/whatsapp/chats"), notice.Action);
        Assert.Equal("You're getting this because you're on the Sales team. Whoever manages WhatsApp chooses when this alert is sent.",
            notice.Footer);
    }

    private static async Task<(string To, EmailNotice Notice)> RenderAsync(WhatsAppWaitingNotice waiting)
    {
        var catalog = new JsonReferenceDataCatalog();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 24, 15, 0, 0, TimeSpan.Zero));
        var services = new ServiceCollection();
        services.AddSingleton(new DisplayFormatter(catalog, catalog, catalog, catalog, catalog,
            new LibPhoneNumberDisplayFormatter(), new TimeZoneService(clock), clock));
        services.AddSingleton<ICultureCatalog>(catalog);
        var emails = new CapturingEmails();
        services.AddSingleton<IEmailTemplateRenderer>(emails);
        services.AddWhatsAppModule(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var message = await scope.ServiceProvider.GetRequiredService<IWhatsAppWaitingEmailRenderer>()
            .RenderAsync(waiting, TestContext.Current.CancellationToken);
        var (notice, profile) = emails.Last ?? throw new InvalidOperationException("Nothing was rendered.");
        Assert.Equal(waiting.Culture, profile.Entry.Code);
        Assert.Equal(notice.Subject, message.Subject);
        return (message.To, notice);
    }

    private sealed class CapturingEmails : IEmailTemplateRenderer
    {
        public (EmailNotice Notice, CultureProfile Culture)? Last { get; private set; }

        public EmailMessage RenderNotice(string to, EmailNotice notice, CultureProfile culture)
        {
            Last = (notice, culture);
            return new EmailMessage(to, notice.Subject, "html", "text");
        }

        public EmailMessage RenderLoginCode(string to, string code, int lifetimeMinutes, CultureProfile culture) => throw new NotSupportedException();
        public EmailMessage RenderSignupCode(string to, string code, int lifetimeMinutes, CultureProfile culture) => throw new NotSupportedException();
        public EmailMessage RenderVerifyEmailCode(string to, string code, int lifetimeMinutes, CultureProfile culture) => throw new NotSupportedException();
        public Task<EmailMessage> RenderInvitationAsync(string to, InvitationNotice notice, CultureProfile culture, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<EmailMessage> RenderAccountNoticeAsync(string to, AccountNotice notice, CultureProfile culture, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
