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

    [Theory]
    [InlineData(LoginMethodType.Email)]
    [InlineData(LoginMethodType.Phone)]
    public async Task Successful_proof_claims_all_matching_foreign_pending_and_preserves_other_methods(LoginMethodType type)
    {
        var store = new IdentityTestStore();
        var id = Guid.CreateVersion7();
        store.Users.Add(id, new UserAccountRow(id, null, "es-AR", "UTC", UserStatus.Active, false, null, null));
        var primary = LoginMethod.CreateEmail(id, Email.Create("primary@example.test").Value);
        primary.Verify(NowUtc);
        primary.MakePrimary();
        store.Methods.Add(primary);
        LoginMethod Create(Guid owner) => type == LoginMethodType.Phone
            ? LoginMethod.CreatePhone(owner, PhoneNumber.Create("+5491134567890").Value)
            : LoginMethod.CreateEmail(owner, Email.Create("pending@example.test").Value);
        var own = Create(id);
        var b = Create(Guid.CreateVersion7());
        var c = Create(Guid.CreateVersion7());
        var otherValue = LoginMethod.CreateEmail(b.UserId, Email.Create("other@example.test").Value);
        var otherType = LoginMethod.CreateGoogle(c.UserId, own.Value);
        var verified = LoginMethod.CreateEmail(c.UserId, Email.Create("verified@example.test").Value);
        verified.Verify(NowUtc);
        store.Methods.AddRange([own, b, c, otherValue, otherType, verified]);

        var result = await Linker(store).VerifyAsync(own, NowUtc, Ct);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(b, store.Methods);
        Assert.DoesNotContain(c, store.Methods);
        Assert.Contains(own, store.Methods);
        Assert.Contains(primary, store.Methods);
        Assert.Contains(otherValue, store.Methods);
        Assert.Contains(otherType, store.Methods);
        Assert.Contains(verified, store.Methods);
        Assert.False(own.IsPrimary);
        Assert.True(primary.IsPrimary);
    }

    [Theory]
    [InlineData(LoginMethodType.Email)]
    [InlineData(LoginMethodType.Phone)]
    public async Task Verified_foreign_owner_rejects_proof_without_removing_any_pending(LoginMethodType type)
    {
        var store = new IdentityTestStore();
        LoginMethod Create(Guid owner) => type == LoginMethodType.Phone
            ? LoginMethod.CreatePhone(owner, PhoneNumber.Create("+5491134567890").Value)
            : LoginMethod.CreateEmail(owner, Email.Create("pending@example.test").Value);
        var own = Create(Guid.CreateVersion7());
        var pending = Create(Guid.CreateVersion7());
        var verified = Create(Guid.CreateVersion7());
        verified.Verify(NowUtc);
        store.Methods.AddRange([own, pending, verified]);
        var observer = new PhoneObserver(own);
        var linker = new LoginMethodLinker(store, store, store, store, new LoginMethodDomainLinker(store), [observer]);

        var result = await linker.VerifyAsync(own, NowUtc, Ct);

        Assert.True(result.IsFailure);
        Assert.Equal(type == LoginMethodType.Phone ? LoginMethodErrors.PhoneAlreadyUsed.Code : LoginMethodErrors.AlreadyUsed.Code, result.Error.Code);
        Assert.Equal(3, store.Methods.Count);
        Assert.Null(own.VerifiedAtUtc);
        Assert.False(own.IsPrimary);
        Assert.Equal(0, observer.Verifications);
    }

    [Fact]
    public async Task Claim_revalidates_stale_candidates_and_tolerates_missing_methods()
    {
        var store = new IdentityTestStore();
        var id = Guid.CreateVersion7();
        var otherId = Guid.CreateVersion7();
        store.Users.Add(id, new UserAccountRow(id, null, "es-AR", "UTC", UserStatus.Active, false, null, null));
        var own = LoginMethod.CreateEmail(id, Email.Create("pending@example.test").Value);
        var foreign = LoginMethod.CreateEmail(otherId, Email.Create(own.Value).Value);
        var changedValue = LoginMethod.CreateEmail(otherId, Email.Create("changed@example.test").Value);
        var changedType = LoginMethod.CreateGoogle(otherId, own.Value);
        var verified = LoginMethod.CreateEmail(otherId, Email.Create("verified@example.test").Value);
        verified.Verify(NowUtc);
        store.Methods.AddRange([own, foreign, changedValue, changedType, verified]);
        // Proyecciones previas obsoletas: la entidad tracked es la autoridad para decidir el retiro.
        store.PendingLookupOverride = [.. store.Methods.Select(method => new LoginMethodLookup(method.Id, otherId, null)),
            new LoginMethodLookup(Guid.CreateVersion7(), otherId, null)];

        Assert.True((await Linker(store).VerifyAsync(own, NowUtc, Ct)).IsSuccess);

        Assert.Equal(new[] { own.Id, changedValue.Id, changedType.Id, verified.Id }.Order(), store.Methods.Select(method => method.Id).Order());
        Assert.NotNull(verified.VerifiedAtUtc);
        Assert.NotNull(own.VerifiedAtUtc);
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
