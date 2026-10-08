using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Identity;

public sealed class AccountRegistrarTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static PersonalSpaceDraft Draft => new(Tenant.CreatePersonal("Personal"),
        TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"),
        "es-AR", "America/Argentina/Buenos_Aires");

    [Fact]
    public async Task Creating_an_account_does_not_implicitly_add_methods_personal_space_or_consent()
    {
        var store = new IdentityTestStore();
        var spaces = new Spaces();
        var registrar = new AccountRegistrar(store, spaces);
        var account = await registrar.CreateAsync(Draft.Culture, Draft.TimeZoneId, Ct);
        Assert.Single(store.Users);
        Assert.Empty(store.Methods);
        Assert.Equal(0, spaces.Staged);
        await registrar.StagePersonalAsync(account.Id, Draft, Ct);
        Assert.Equal(1, spaces.Staged);
    }

    [Fact]
    public async Task An_invited_operator_never_gets_a_personal_space()
    {
        var store = new IdentityTestStore();
        var spaces = new Spaces();
        var registrar = new AccountRegistrar(store, spaces);
        var expiry = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        var account = await registrar.CreateAsync(Draft.Culture, Draft.TimeZoneId, Ct, expiry);
        Assert.True(account.IsPlatformOperator);
        Assert.Equal(expiry, account.OperatorInvitationExpiresAtUtc);
        await Assert.ThrowsAsync<InvalidOperationException>(() => registrar.StagePersonalAsync(account.Id, Draft, Ct));
        Assert.Equal(0, spaces.Staged);
    }

    private sealed class Spaces : IPersonalSpaceProvisioner
    {
        internal int Staged { get; private set; }
        public Task<PersonalSpaceDraft> PrepareAsync(string? cultureCode, string? browserTimeZoneId, CancellationToken ct) =>
            throw new NotSupportedException();
        public void Stage(PersonalSpaceDraft draft, Guid userId) => Staged++;
    }
}
