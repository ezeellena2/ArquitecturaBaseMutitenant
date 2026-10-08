using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Application.Services.OrganizationSettings;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Identity;

public sealed class LoginMethodLinkerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTime NowUtc = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Email_cannot_take_the_verified_contact_of_another_google_account()
    {
        var store = new IdentityTestStore();
        var email = Email.Create("owner@example.test").Value;
        var google = LoginMethod.CreateGoogle(Guid.CreateVersion7(), "subject", email);
        google.Verify(NowUtc);
        store.Methods.Add(google);
        var pending = LoginMethod.CreateEmail(Guid.CreateVersion7(), email);
        var result = await Linker(store).VerifyAsync(pending, NowUtc, Ct);
        Assert.Equal(LoginMethodErrors.AlreadyUsed.Code, result.Error.Code);
        Assert.Null(pending.VerifiedAtUtc);
        Assert.False(pending.IsPrimary);
    }

    [Fact]
    public async Task Verification_preserves_primary_unless_recovery_explicitly_replaces_it()
    {
        var store = new IdentityTestStore();
        var userId = Guid.CreateVersion7();
        store.Users.Add(userId, new UserAccountRow(userId, null, "es-AR", "America/Argentina/Buenos_Aires",
            UserStatus.Active, false, null, null));
        var first = LoginMethod.CreateEmail(userId, Email.Create("first@example.test").Value);
        Assert.True((await Linker(store).VerifyAsync(first, NowUtc, Ct)).IsSuccess);
        store.Methods.Add(first);
        var next = LoginMethod.CreateEmail(userId, Email.Create("next@example.test").Value);
        Assert.True((await Linker(store).VerifyAsync(next, NowUtc, Ct)).IsSuccess);
        Assert.False(next.IsPrimary);
        Assert.Equal(first.Value, store.Users[userId].PrimaryEmail!.Value);
        Assert.True((await Linker(store).VerifyAsync(next, NowUtc, Ct, replacePrimary: true)).IsSuccess);
        Assert.False(first.IsPrimary);
        Assert.True(next.IsPrimary);
        Assert.Equal(next.Value, store.Users[userId].PrimaryEmail!.Value);
    }

    private static LoginMethodLinker Linker(IdentityTestStore store) =>
        new(store, store, store, store, new LoginMethodDomainLinker(store));
}
