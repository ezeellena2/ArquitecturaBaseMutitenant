using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Auth;

public sealed class LoginMethodsCapabilitiesTests
{
    [Fact]
    public async Task Capabilities_come_from_available_registered_channels_without_a_specific_phone_key()
    {
        var service = new LoginMethodsService([
            new Channel("email", LoginMethodType.Email, true, [], null),
            new Channel("phone-test", LoginMethodType.Phone, true, ["AR"], "+5491123456789"),
            new Channel("unavailable", LoginMethodType.Phone, false, ["UY"], "+59899123456"),
        ], new Google(), TimeProvider.System, NullLogger<LoginMethodsService>.Instance);
        var result = await service.GetLoginMethodsAsync(TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Channels.Count);
        var phone = Assert.Single(result.Value.Channels, value => value.Key == "phone-test");
        Assert.Equal(["AR"], phone.Countries);
        Assert.Equal("+5491123456789", phone.ContactPhone);
        Assert.Null(Assert.Single(result.Value.Channels, value => value.Key == "email").ContactPhone);
    }

    private sealed class Google : IGoogleAvailability { public bool IsEnabled => false; }
    private sealed record Channel(string Key, LoginMethodType SupportedMethodType, bool IsAvailable,
        IReadOnlyList<string> Countries, string? ContactPhone) : ILoginCodeChannel
    {
        public Result ValidateNewDestination(LoginCodeDestination destination, string? country) => Result.Success();
        public string RenderLoginCode(string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw new NotSupportedException();
        public void EnqueueRenderedLoginCode(string payload, Guid? userId = null) => throw new NotSupportedException();
        public void EnqueueSignup(string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw new NotSupportedException();
        public void EnqueueVerification(Guid? userId, string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw new NotSupportedException();
    }
}
