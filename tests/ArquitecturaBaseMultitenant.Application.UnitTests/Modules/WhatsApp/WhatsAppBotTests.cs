using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
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
    [Fact]
    public async Task Closed_signup_buttons_have_only_public_action_ids_and_configured_platform_name()
    {
        var reply = await new WhatsAppBotReplyBuilder(new JsonReferenceDataCatalog()).BuildAsync("number", "+5491134567890", "Mi sitio", "es-AR",
            "Bot.Closed", new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc), Ct, choices: true);
        Assert.Contains("Mi sitio", reply.Body); Assert.Null(reply.LinkUrl);
        Assert.Equal(new[] { BotButtons.SignIn, BotButtons.Recover }, reply.ReplyButtons!.Select(value => value.Id));
        Assert.Equal(new[] { "Ingresar", "Recuperar mi cuenta" }, reply.ReplyButtons!.Select(value => value.Title));
    }
}
