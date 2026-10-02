using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Resources;

/// <summary>Cada permiso organizacional y de plataforma tiene nombre y descripción en español e inglés.</summary>
public sealed class PermissionTextsTests
{
    public static TheoryData<string> Keys()
    {
        var permissions = Permissions.All.Concat(PlatformPermissions.All).ToArray();
        var data = new TheoryData<string>();
        foreach (var key in permissions
            .Select(permission => "Area." + permission[..permission.IndexOf('.', StringComparison.Ordinal)])
            .Concat(permissions.Select(permission => "Permission." + permission))
            .Concat(permissions.Select(permission => "PermissionDescription." + permission))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            data.Add(key);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Keys))]
    public void Permission_key_has_spanish_and_english_texts(string key)
    {
        Assert.False(string.IsNullOrWhiteSpace(Text(CultureInfo.InvariantCulture, key)), $"Missing Spanish text for {key}");
        Assert.False(string.IsNullOrWhiteSpace(Text(CultureInfo.GetCultureInfo("en"), key)), $"Missing English text for {key}");
    }

    private static string? Text(CultureInfo culture, string key) =>
        PermissionTexts.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)?.GetString(key);
}
