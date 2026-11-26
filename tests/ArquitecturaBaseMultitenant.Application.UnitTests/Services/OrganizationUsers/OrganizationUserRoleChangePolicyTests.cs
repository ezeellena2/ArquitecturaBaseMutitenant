using ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.OrganizationUsers;

/// <summary>
/// La regla de qué da, quita y vuelve a dar un cambio de roles (ADR 0061), una sola para «Guardar cambios» y la fuente del
/// asistente: lo de una empresa inactiva no se quita, vuelve si el cambio la reactiva, y a un invitado se le da todo.
/// </summary>
public sealed class OrganizationUserRoleChangePolicyTests
{
    private static readonly Guid Sales = Guid.CreateVersion7();
    private static readonly Guid Audit = Guid.CreateVersion7();
    private static readonly Guid Storage = Guid.CreateVersion7();
    private static readonly Guid Delta = Guid.CreateVersion7();
    private static readonly Guid Gamma = Guid.CreateVersion7();

    [Fact]
    public void What_is_not_asked_is_removed_and_what_is_new_is_added_and_granted()
    {
        var changes = Decide([(Sales, null), (Storage, Delta)], [(Audit, null), (Storage, Delta)], inactive: [], companies: [Delta]);

        Assert.Equal(Keys((Sales, null)), changes.Removed);
        Assert.Equal(Keys((Audit, null)), changes.Added);
        Assert.Equal(changes.Added, changes.Granted);
        Assert.Empty(changes.Revived);
    }

    [Fact]
    public void A_general_edit_keeps_the_roles_of_an_inactive_company_and_reactivating_it_gives_them_again()
    {
        (Guid, Guid?)[] saved = [(Sales, null), (Storage, Gamma)];

        var kept = Decide(saved, [(Sales, null)], inactive: [Gamma], companies: []);
        var reactivated = Decide(saved, [(Sales, null)], inactive: [Gamma], companies: [Gamma]);

        Assert.Empty(kept.Removed);
        Assert.Empty(kept.Revived);
        Assert.Empty(reactivated.Removed);
        Assert.Equal(Keys((Storage, Gamma)), reactivated.Revived);
    }

    [Fact]
    public void An_invited_person_is_given_everything_asked_even_what_is_already_saved()
    {
        var changes = Decide([(Sales, null)], [(Sales, null), (Audit, null)], inactive: [], companies: [], invited: true);

        Assert.Equal(Keys((Audit, null)), changes.Added);
        Assert.Equal(Keys((Sales, null), (Audit, null)), changes.Granted);
    }

    private static (Guid RoleId, Guid? CompanyId)[] Keys(params (Guid RoleId, Guid? CompanyId)[] keys) => keys;

    private static OrganizationUserRoleChanges<(Guid RoleId, Guid? CompanyId)> Decide((Guid, Guid?)[] saved, (Guid, Guid?)[] desired,
        HashSet<Guid> inactive, Guid[] companies, bool invited = false) =>
        OrganizationUserRoleChangePolicy.Decide<(Guid RoleId, Guid? CompanyId)>(saved, desired, key => key, inactive, companies, invited);
}
