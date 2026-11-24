using ArquitecturaBaseMultitenant.Application.Services.Platform;
using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Platform;

/// <summary>Nadie da lo que no tiene, entre operadores de Plataforma (ADR 0061 §4), con lo efectivo (decisión 260).</summary>
public sealed class PlatformDelegationPolicyTests
{
    [Fact]
    public void An_operator_gives_and_takes_away_only_what_it_holds_with_what_it_includes()
    {
        var held = PlatformDelegationPolicy.Effective([PlatformPermissions.Operators.Manage, PlatformPermissions.Accounts.Manage]);

        Assert.True(PlatformDelegationPolicy.CanChangePermissions(held, [], [PlatformPermissions.Accounts.Read]));
        Assert.True(PlatformDelegationPolicy.CanChangePermissions(held, [], [PlatformPermissions.Accounts.Manage]));
        Assert.False(PlatformDelegationPolicy.CanChangePermissions(held, [], [PlatformPermissions.Tenants.Read]));
        Assert.False(PlatformDelegationPolicy.CanChangePermissions(held,
            [PlatformPermissions.Audit.Read, PlatformPermissions.Accounts.Read], [PlatformPermissions.Accounts.Read]));
        // Lo que no cambia no cuenta.
        Assert.True(PlatformDelegationPolicy.CanChangePermissions(held,
            [PlatformPermissions.Audit.Read], [PlatformPermissions.Audit.Read, PlatformPermissions.Accounts.Manage]));
        Assert.True(PlatformDelegationPolicy.CanGrant(held, PlatformPermissions.Accounts.Read));
        Assert.False(PlatformDelegationPolicy.CanGrant(held, PlatformPermissions.Settings.Manage));
    }

    [Fact]
    public void Acting_on_an_operator_asks_for_all_its_permissions_except_on_oneself()
    {
        var held = PlatformDelegationPolicy.Effective([PlatformPermissions.Operators.Manage, PlatformPermissions.Accounts.Manage]);

        Assert.True(PlatformDelegationPolicy.CanActOn(self: false, held, [PlatformPermissions.Accounts.Manage]));
        Assert.False(PlatformDelegationPolicy.CanActOn(self: false, held, [PlatformPermissions.Tenants.Read]));
        Assert.False(PlatformDelegationPolicy.CanActOn(self: false, held, PlatformPermissions.All));
        Assert.True(PlatformDelegationPolicy.CanActOn(self: true, held, PlatformPermissions.All));
        Assert.True(PlatformDelegationPolicy.CanActOn(self: false, PlatformDelegationPolicy.Effective(PlatformPermissions.All),
            PlatformPermissions.All));
        // Un código fuera del catálogo, como el de un módulo quitado, no pide nada.
        Assert.True(PlatformDelegationPolicy.CanActOn(self: false, held, ["platform.removed.manage"]));
    }
}
