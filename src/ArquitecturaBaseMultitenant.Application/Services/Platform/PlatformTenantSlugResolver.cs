using System.Globalization;
using System.Text;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

internal sealed class PlatformTenantSlugResolver(ITenantSlugAvailabilityReader slugs)
{
    internal async Task<Result<string>> AllocateAsync(string name, Guid operationId, CancellationToken ct)
    {
        var normalized = new StringBuilder();
        foreach (var character in name.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9') normalized.Append(character);
            else if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark
                && normalized.Length > 0 && normalized[^1] != '-') normalized.Append('-');
        }
        var proposed = TenantSlug.Create(normalized.ToString().TrimEnd('-'));
        if (proposed.IsSuccess && !await slugs.IsReservedAsync(proposed.Value.Value, ct)
            && !await slugs.IsTakenAsync(proposed.Value.Value, ct)) return proposed.Value.Value;
        var fallback = "org-" + operationId.ToString("N");
        if (await slugs.IsReservedAsync(fallback, ct)) return TenantSlugErrors.Reserved;
        if (await slugs.IsTakenAsync(fallback, ct)) return TenantSlugErrors.Taken;
        return fallback;
    }
}
