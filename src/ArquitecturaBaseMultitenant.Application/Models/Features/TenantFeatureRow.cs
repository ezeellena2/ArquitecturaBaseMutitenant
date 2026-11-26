using ArquitecturaBaseMultitenant.Domain.Features;

namespace ArquitecturaBaseMultitenant.Application.Models.Features;

/// <summary>Datos persistidos sin cachear el resultado temporal de la habilitación.</summary>
public sealed record TenantFeatureRow(string Key, bool Enabled, TenantFeatureSource Source,
    DateTime? ExpiresAtUtc, uint Version, DateOnly? ExpiresOn = null)
{
    public bool IsEnabledAt(DateTime nowUtc) => Enabled && (ExpiresAtUtc is null || nowUtc < ExpiresAtUtc);

    /// <summary>
    /// La regla de un módulo con una fila por origen (ADR 0069), en un solo lugar: sin filas del módulo vale su valor de
    /// entrada; con filas, está prendido si alguna está vigente. Una prueba vencida no apaga una habilitación manual.
    /// </summary>
    public static bool IsModuleEnabled(IEnumerable<TenantFeatureRow> rows, string key, bool defaultEnabled, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var hasRows = false;
        foreach (var row in rows)
        {
            if (!string.Equals(row.Key, key, StringComparison.Ordinal)) continue;
            if (row.IsEnabledAt(nowUtc)) return true;
            hasRows = true;
        }
        return !hasRows && defaultEnabled;
    }
}
