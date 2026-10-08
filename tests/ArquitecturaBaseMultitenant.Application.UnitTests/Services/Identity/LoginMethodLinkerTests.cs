using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
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

    [Fact]
    public async Task Phone_observer_sees_verified_proof_once_and_primary_changes_do_not_repeat_it()
    {
        var store = new IdentityTestStore();
        var id = Guid.CreateVersion7();
        store.Users.Add(id, new UserAccountRow(id, null, "es-AR", "America/Argentina/Buenos_Aires", UserStatus.Active, false, null, null));
        var phone = LoginMethod.CreatePhone(id, PhoneNumber.Create("+5491134567890").Value);
        var observer = new PhoneObserver(phone);
        var linker = new LoginMethodLinker(store, store, store, store, new LoginMethodDomainLinker(store), [observer]);
        Assert.True((await linker.VerifyAsync(phone, NowUtc, Ct)).IsSuccess);
        store.Methods.Add(phone);
        Assert.True((await linker.VerifyAsync(phone, NowUtc, Ct)).IsSuccess);
        await linker.SynchronizePrimaryAsync(phone, Ct);
        Assert.Equal(1, observer.Verifications);
        await linker.RemovedAsync(phone, Ct);
        Assert.Equal(1, observer.Removals);
        Assert.Null(store.Users[id].PrimaryEmail);
    }

    private sealed class PhoneObserver(LoginMethod method) : IPhoneLinkObserver
    {
        internal int Verifications { get; private set; }
        internal int Removals { get; private set; }
        public Task VerifiedAsync(Guid userId, Guid methodId, PhoneNumber phone, CancellationToken ct)
        {
            Assert.NotNull(method.VerifiedAtUtc);
            Assert.Equal(method.UserId, userId);
            Assert.Equal(method.Id, methodId);
            Assert.Equal(method.Value, phone.Value);
            Verifications++;
            return Task.CompletedTask;
        }
        public Task RemovedAsync(Guid userId, Guid methodId, PhoneNumber phone, CancellationToken ct) { Removals++; return Task.CompletedTask; }
        public Task AccountDeletedAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();
    }

    private static LoginMethodLinker Linker(IdentityTestStore store) =>
        new(store, store, store, store, new LoginMethodDomainLinker(store));
}
