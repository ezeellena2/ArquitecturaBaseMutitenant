using System.Globalization;
using System.Resources;
using ArquitecturaBaseMultitenant.Application.Services.Auth;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.WhatsApp;

public sealed class WhatsAppOperatorReplyTests
{
    [Fact]
    public void Operator_reply_contains_no_link_or_platform_disclosure()
    {
        var resources = new ResourceManager("ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources.WhatsAppTexts", typeof(LoginLinkIssuer).Assembly);
        var reply = resources.GetString("Bot.OperatorNeutralReply", CultureInfo.GetCultureInfo("es"));
        Assert.Equal("Para entrar, usá la web.", reply);
        Assert.DoesNotContain("plataforma", reply!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("operador", reply!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http", reply!, StringComparison.OrdinalIgnoreCase);
    }
}
