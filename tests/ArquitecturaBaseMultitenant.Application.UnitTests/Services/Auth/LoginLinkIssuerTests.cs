using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using System.Globalization;
using System.Resources;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Auth;

public sealed class LoginLinkIssuerTests
{
    [Fact]
    public async Task An_operator_never_receives_a_link_even_with_a_verified_destination()
    {
        var store = new IdentityTestStore();
        var id = Guid.CreateVersion7();
        store.Users[id] = new UserAccountRow(id, "Operador", "es-AR", "UTC", UserStatus.Active, true, null, null);
        var phone = LoginMethod.CreatePhone(id, PhoneNumber.Create("+5491123456789").Value);
        phone.Verify(new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc));
        store.Methods.Add(phone);
        var issuer = new LoginLinkIssuer(store, store, null!, null!, null!, null!, TimeProvider.System);
        var result = await issuer.IssueAsync(id, phone.Id, TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("Auth.LoginLink.Invalid", result.Error.Code);
        Assert.DoesNotContain("operator", result.Error.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("platform", result.Error.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Single(store.Methods);
        Assert.Equal(["account"], store.Locks);
        var resources = new ResourceManager("ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources.WhatsAppTexts", typeof(LoginLinkIssuer).Assembly);
        var reply = resources.GetString("Bot.OperatorNeutralReply", CultureInfo.GetCultureInfo("es"));
        Assert.Equal("Para entrar, usá la web.", reply);
        Assert.DoesNotContain("plataforma", reply!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("operador", reply!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http", reply!, StringComparison.OrdinalIgnoreCase);
    }
}
