using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Identity;

/// <summary>Conserva los nombres compuestos al guardar preferencias y borra sus partes al anonimizar.</summary>
public sealed class GoogleNameRulesTests
{
    [Fact]
    public void Import_changes_profile_version_once_and_legacy_rename_keeps_compound_parts()
    {
        var user = ApplicationUser.Create(null, "es-AR", "America/Argentina/Buenos_Aires").Value;
        Fill(user, "María José", "de la Cruz");
        Assert.Equal("María José de la Cruz", user.DisplayName);
        Assert.Equal(1u, user.ProfileVersion);
        Fill(user, "Otra", "Persona");
        Assert.Equal(1u, user.ProfileVersion);
        Assert.True(user.Rename(user.DisplayName).IsSuccess);
        Assert.Equal("María José", user.FirstName);
        Assert.Equal("de la Cruz", user.LastName);
    }

    [Fact]
    public void Each_part_keeps_its_full_limit_and_legacy_rename_preserves_the_combined_value()
    {
        var user = ApplicationUser.Create(null, "es-AR", "America/Argentina/Buenos_Aires").Value;
        Fill(user, new string('N', TextLimits.PersonName), new string('A', TextLimits.PersonName));
        Assert.Equal(TextLimits.PersonName * 2 + 1, user.DisplayName?.Length);
        Assert.True(user.Rename(user.DisplayName).IsSuccess);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Deletion_and_discarded_operator_invitation_remove_both_personal_name_parts(bool invitation)
    {
        var user = ApplicationUser.Create(null, "es-AR", "America/Argentina/Buenos_Aires").Value;
        Fill(user, "María José", "de la Cruz");
        Assert.Equal("María José de la Cruz", user.DisplayName);
        var nowUtc = new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);
        if (invitation)
        {
            user.InvitePlatformOperator(nowUtc.AddDays(7));
            Assert.True(user.DiscardOperatorInvitation(nowUtc, "Cuenta eliminada").IsSuccess);
        }
        else
        {
            Assert.True(user.RequestDeletion("Prueba", nowUtc, 30).IsSuccess);
            Assert.True(user.CompleteDeletion(nowUtc.AddDays(30), "Cuenta eliminada").IsSuccess);
        }
        Assert.Null(user.FirstName);
        Assert.Null(user.LastName);
        Assert.Equal("Cuenta eliminada", user.DisplayName);
    }

    private static void Fill(ApplicationUser user, string first, string last) =>
        user.FillMissingNames(first, last);
}
