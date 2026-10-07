using System.Text;
using ArquitecturaBaseMultitenant.Application.Common.Text;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

/// <summary>Normaliza las dos claims Google, ajusta su límite Unicode e importa sólo en la cuenta autorizada.</summary>
internal sealed class GoogleProfileWriter(IUserRepository users)
{
    internal Task FillMissingNamesAsync(Guid userId, ExternalLogin login, CancellationToken ct) =>
        login.EmailVerified && login.Email is not null
            ? users.FillMissingProfileNamesAsync(userId, FitName(login.FirstName, TextLimits.FirstName),
                FitName(login.LastName, TextLimits.LastName), ct)
            : Task.CompletedTask;

    private static string? FitName(string? externalName, int limit)
    {
        var clean = TextNormalizer.CleanName(externalName);
        if (clean is null || clean.Length <= limit) return clean;
        var result = new StringBuilder(limit);
        foreach (var rune in clean.EnumerateRunes())
        {
            if (result.Length + rune.Utf16SequenceLength > limit) break;
            result.Append(rune);
        }
        return TextNormalizer.CleanName(result.ToString());
    }
}
