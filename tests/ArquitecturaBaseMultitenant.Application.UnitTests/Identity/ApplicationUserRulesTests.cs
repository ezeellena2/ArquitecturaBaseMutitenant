using System.Reflection;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Identity;

/// <summary>
/// Comprueba las reglas puras de la cuenta Identity: nombre, estado y preferencias iniciales. Evita
/// recortar silenciosamente un nombre demasiado largo.
/// </summary>
public sealed class ApplicationUserRulesTests
{
    [Fact]
    public void Create_rejects_a_name_above_the_person_limit_without_truncating_it()
    {
        var result = ApplicationUser.Create(new string('N', TextLimits.PersonName + 1), "es-AR", "America/Argentina/Buenos_Aires");

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.InvalidDisplayName, result.Error);
    }

    [Fact]
    public void Create_sets_active_identity_and_preferences_from_the_caller()
    {
        var result = ApplicationUser.Create(new string('N', TextLimits.PersonName), "en-US", "America/New_York");

        Assert.True(result.IsSuccess);
        var user = result.Value;
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(new string('N', TextLimits.PersonName), user.DisplayName);
        Assert.Equal("en-US", user.Culture);
        Assert.Equal("America/New_York", user.TimeZoneId);
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal(user.Id.ToString("D", System.Globalization.CultureInfo.InvariantCulture), user.UserName);
        Assert.Null(user.LastBusinessTenantId);
        Assert.Null(user.DeletionScheduledForUtc);
    }

    [Fact]
    public void Rename_rejects_long_name_without_changing_the_previous_value()
    {
        var user = ApplicationUser.Create("Ana", "es-AR", "America/Argentina/Buenos_Aires").Value;

        var result = user.Rename(new string('N', TextLimits.PersonName + 1));

        Assert.Equal(UserErrors.InvalidDisplayName, result.Error);
        Assert.Equal("Ana", user.DisplayName);
    }

    [Fact]
    public void Account_properties_do_not_expose_public_setters()
    {
        var names = new[]
        {
            nameof(ApplicationUser.DisplayName), nameof(ApplicationUser.Culture), nameof(ApplicationUser.TimeZoneId),
            nameof(ApplicationUser.Status), nameof(ApplicationUser.IsPlatformOperator),
            nameof(ApplicationUser.LastBusinessTenantId), nameof(ApplicationUser.DeletionRequestedAtUtc),
            nameof(ApplicationUser.DeletionScheduledForUtc), nameof(ApplicationUser.DeletionReason),
            nameof(ApplicationUser.DeletionRequestedByOperatorId), nameof(ApplicationUser.DeletedAtUtc),
        };

        foreach (var name in names)
        {
            var property = typeof(ApplicationUser).GetProperty(name, BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(property);
            Assert.False(property.SetMethod?.IsPublic == true, $"{name} has a public setter.");
        }
    }

    [Fact]
    public void Suspension_and_business_memory_do_not_change_the_access_side()
    {
        var user = ApplicationUser.Create("Ana", "es-AR", "America/Argentina/Buenos_Aires").Value;
        var businessId = Guid.CreateVersion7();

        user.RememberBusinessTenant(businessId);
        Assert.Equal(businessId, user.LastBusinessTenantId);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.True(user.Suspend().IsSuccess);
        Assert.Equal(UserStatus.Suspended, user.Status);
        Assert.True(user.Reactivate().IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
    }
}
