using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Localization;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.WhatsApp;

/// <summary>
/// Los ingresos por WhatsApp de Plataforma › Auditoría los nombra el módulo (N22): el núcleo no tiene esos textos y,
/// sin el módulo, muestra el genérico.
/// </summary>
public sealed class WhatsAppAuditTextTests
{
    [Theory]
    [InlineData("es-AR", LoginAuditMethod.WhatsAppCode, "Ingresó con un código por WhatsApp")]
    [InlineData("es-AR", LoginAuditMethod.WhatsAppLink, "Ingresó con un enlace de WhatsApp")]
    [InlineData("en-US", LoginAuditMethod.WhatsAppCode, "Signed in with a WhatsApp code")]
    [InlineData("en-US", LoginAuditMethod.Code, "Signed in with an email code")]
    public void The_sign_in_summary_of_a_module_method_comes_from_the_module(string culture, LoginAuditMethod method,
        string expected)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            Assert.Equal(expected, AuditTexts.SignIn(method, [new WhatsAppAuditTextSource()]));
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [Fact]
    public void Without_the_module_a_WhatsApp_sign_in_shows_the_generic_text()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("es-AR");
            var generic = AuditTexts.SignIn(LoginAuditMethod.WhatsAppCode, Array.Empty<IAuditTextSource>());
            Assert.DoesNotContain("WhatsApp", generic, StringComparison.OrdinalIgnoreCase);
            Assert.False(string.IsNullOrWhiteSpace(generic));
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }
}
