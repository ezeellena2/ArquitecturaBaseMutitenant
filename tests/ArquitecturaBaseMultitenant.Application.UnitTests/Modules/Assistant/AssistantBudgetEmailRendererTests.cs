using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Phones;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Assistant;

/// <summary>
/// Tablero Mensajes, los cuatro avisos del tope del asistente (D23): cada uno con su asunto, título, párrafos, datos y
/// botón, con los montos, el mes y la fecha formateados por DisplayFormatter en la cultura de quien lo recibe. El armado
/// HTML es del núcleo (EmailTemplateTests).
/// </summary>
public sealed class AssistantBudgetEmailRendererTests
{
    private const string Url = "https://example.test/org/asistente/uso";
    private static readonly DateOnly October = new(2026, 10, 1);

    [Fact]
    public async Task Near_the_own_key_limit_tells_the_owner_like_the_board()
    {
        var (to, notice) = await RenderAsync(AssistantKeyFunding.OwnKey, 80, 160m, 200m, "Lucía");

        Assert.Equal("lucia@example.test", to);
        Assert.Equal("El asistente de Grupo Delta usó el 80% del tope del mes", notice.Subject);
        Assert.Equal("El asistente usó el 80% del tope del mes", notice.Title);
        Assert.Equal(["Hola, Lucía. Grupo Delta ya usó US$ 160,00 de los US$ 200,00 de tope del 01/10/2026 al 31/10/2026.",
            "Si llega al tope, el asistente deja de responder hasta el 01/11/2026. Podés subir el tope en la configuración del asistente."],
            notice.Paragraphs);
        AssertDetails(notice, ("Organización", "Grupo Delta"), ("Usado", "US$ 160,00 de US$ 200,00"), ("Mes", "01/10/2026 – 31/10/2026"));
        Assert.Equal(new EmailNoticeAction("Ver el uso", Url), notice.Action);
        Assert.Empty(notice.Notes);
        Assert.Null(notice.Footer);
    }

    [Fact]
    public async Task At_the_own_key_limit_sends_the_owner_to_the_settings()
    {
        var (_, notice) = await RenderAsync(AssistantKeyFunding.OwnKey, 100, 203.17m, 200m, "Lucía");

        Assert.Equal("El asistente de Grupo Delta llegó al tope del mes", notice.Subject);
        Assert.Equal("El asistente llegó al tope del mes", notice.Title);
        Assert.Equal(["Hola, Lucía. Grupo Delta usó los US$ 200,00 de tope del 01/10/2026 al 31/10/2026 y el asistente dejó de responder.",
            "Vuelve a responder el 01/11/2026, o antes si subís el tope en la configuración del asistente."], notice.Paragraphs);
        AssertDetails(notice, ("Organización", "Grupo Delta"), ("Usado", "US$ 203,17 de US$ 200,00"), ("Mes", "01/10/2026 – 31/10/2026"));
        Assert.Equal(new EmailNoticeAction("Revisar configuración", Url), notice.Action);
    }

    [Fact]
    public async Task Near_the_platform_key_limit_tells_platform_support_without_a_greeting()
    {
        var (to, notice) = await RenderAsync(AssistantKeyFunding.PlatformKey, 80, 80m, 100m, null, "soporte@example.test");

        Assert.Equal("soporte@example.test", to);
        Assert.Equal("Grupo Delta usó el 80% del tope del asistente", notice.Subject);
        Assert.Equal("Grupo Delta usó el 80% del tope del asistente", notice.Title);
        Assert.Equal(["Grupo Delta, que usa la clave de la plataforma, ya usó US$ 80,00 de los US$ 100,00 de tope del 01/10/2026 al 31/10/2026.",
            "Si llega al tope, su asistente deja de responder hasta el 01/11/2026."], notice.Paragraphs);
        AssertDetails(notice, ("Organización", "Grupo Delta"), ("Usado", "US$ 80,00 de US$ 100,00"), ("Mes", "01/10/2026 – 31/10/2026"));
        Assert.Equal(new EmailNoticeAction("Ver el uso", Url), notice.Action);
    }

    [Fact]
    public async Task At_the_platform_key_limit_tells_platform_support_in_one_paragraph()
    {
        var (_, notice) = await RenderAsync(AssistantKeyFunding.PlatformKey, 100, 100m, 100m, null, "soporte@example.test");

        Assert.Equal("Grupo Delta llegó al tope del asistente", notice.Subject);
        Assert.Equal("Grupo Delta llegó al tope del asistente", notice.Title);
        Assert.Equal(["Grupo Delta usó los US$ 100,00 de tope del 01/10/2026 al 31/10/2026 y su asistente dejó de responder hasta el 01/11/2026."],
            notice.Paragraphs);
        AssertDetails(notice, ("Organización", "Grupo Delta"), ("Usado", "US$ 100,00 de US$ 100,00"), ("Mes", "01/10/2026 – 31/10/2026"));
        Assert.Equal(new EmailNoticeAction("Ver el uso", Url), notice.Action);
    }

    [Fact]
    public async Task English_owner_gets_the_same_notice_in_english_formats()
    {
        var (_, near) = await RenderAsync(AssistantKeyFunding.OwnKey, 80, 160m, 200m, "Lucía", culture: "en-US");
        var reached = (await RenderAsync(AssistantKeyFunding.PlatformKey, 100, 100m, 100m, null, culture: "en-US")).Notice;

        Assert.Equal("Grupo Delta's assistant used 80% of the monthly limit", near.Subject);
        Assert.Equal("The assistant used 80% of the monthly limit", near.Title);
        Assert.Equal(["Hello, Lucía. Grupo Delta has already used $160.00 of its $200.00 limit from 10/01/2026 to 10/31/2026.",
            "If it reaches the limit, the assistant won't answer until 11/01/2026. You can raise the limit in the assistant settings."],
            near.Paragraphs);
        AssertDetails(near, ("Organization", "Grupo Delta"), ("Used", "$160.00 of $200.00"), ("Month", "10/01/2026 – 10/31/2026"));
        Assert.Equal(new EmailNoticeAction("View usage", Url), near.Action);
        Assert.Equal("Grupo Delta reached its assistant limit", reached.Subject);
        Assert.Equal(["Grupo Delta used its $100.00 limit from 10/01/2026 to 10/31/2026, and its assistant won't answer until 11/01/2026."],
            reached.Paragraphs);
    }

    [Fact]
    public async Task An_owner_without_a_name_gets_the_plain_greeting()
    {
        var (_, notice) = await RenderAsync(AssistantKeyFunding.OwnKey, 80, 160m, 200m, null);

        Assert.StartsWith("Hola. Grupo Delta ya usó", notice.Paragraphs[0], StringComparison.Ordinal);
    }

    private static void AssertDetails(EmailNotice notice, params (string Label, string Value)[] expected) =>
        Assert.Equal(expected.Select(row => new EmailNoticeDetail(row.Label, row.Value)), notice.Details);

    private static async Task<(string To, EmailNotice Notice)> RenderAsync(AssistantKeyFunding funding, int threshold, decimal used,
        decimal budget, string? name, string to = "lucia@example.test", string culture = "es-AR")
    {
        var catalog = new JsonReferenceDataCatalog();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 24, 15, 0, 0, TimeSpan.Zero));
        var services = new ServiceCollection();
        services.AddSingleton(new DisplayFormatter(catalog, catalog, catalog, catalog, catalog,
            new LibPhoneNumberDisplayFormatter(), new TimeZoneService(clock), clock));
        services.AddSingleton<ICultureCatalog>(catalog);
        var emails = new CapturingEmails();
        services.AddSingleton<IEmailTemplateRenderer>(emails);
        ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.AssistantModule.AddAssistantModule(services);
        using var provider = services.BuildServiceProvider();
        var usd = CurrencyCode.Create("USD").Value;
        // Una zona adelantada no corre el mes ni el día en que vuelve a responder: son fechas civiles del tope.
        var message = await provider.GetRequiredService<IAssistantBudgetEmailRenderer>().RenderAsync(
            new AssistantBudgetAlertNotice(to, name, funding, threshold, new Money(used, usd), new Money(budget, usd), "Grupo Delta",
                October, Url, culture, "Pacific/Kiritimati"), TestContext.Current.CancellationToken);
        var (notice, profile) = emails.Last ?? throw new InvalidOperationException("Nothing was rendered.");
        Assert.Equal(culture, profile.Entry.Code);
        Assert.Equal(notice.Subject, message.Subject);
        return (message.To, notice);
    }

    private sealed class CapturingEmails : IEmailTemplateRenderer
    {
        public (EmailNotice Notice, CultureProfile Culture)? Last { get; private set; }

        public Task<EmailMessage> RenderNoticeAsync(string to, EmailNotice notice, CultureProfile culture,
            CancellationToken cancellationToken)
        {
            Last = (notice, culture);
            return Task.FromResult(new EmailMessage(to, notice.Subject, "html", "text"));
        }

        public Task<EmailMessage> RenderLoginCodeAsync(string to, string code, int lifetimeMinutes, CultureProfile culture, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EmailMessage> RenderSignupCodeAsync(string to, string code, int lifetimeMinutes, CultureProfile culture, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EmailMessage> RenderVerifyEmailCodeAsync(string to, string code, int lifetimeMinutes, CultureProfile culture, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EmailMessage> RenderInvitationAsync(string to, InvitationNotice notice, CultureProfile culture, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<EmailMessage> RenderAccountNoticeAsync(string to, AccountNotice notice, CultureProfile culture, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
