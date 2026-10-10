namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Recorrido;

/// <summary>Valida el workspace efímero creado y eliminado exclusivamente por el runner.</summary>
public static class RecorridoPaths
{
    public static void Validate(string readyFile)
    {
        if (!Path.IsPathFullyQualified(readyFile)) throw new InvalidOperationException("Absolute isolated readiness path required.");
        var directory = Path.GetDirectoryName(Path.GetFullPath(readyFile));
        var parent = directory is null ? null : Path.GetDirectoryName(directory);
        var name = directory is null ? "" : Path.GetFileName(directory);
        if (!string.Equals(parent?.TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            || !(name.StartsWith("mt-e2e-", StringComparison.Ordinal)
                || name.StartsWith("arquitecturabase-recorrido-", StringComparison.Ordinal)
                || name.StartsWith("arquitecturabase-e2e-", StringComparison.Ordinal))
            || !Directory.Exists(directory) || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0
            || !Path.GetFileName(readyFile).StartsWith("mt-e2e-", StringComparison.Ordinal)
            || Path.GetExtension(readyFile) != ".ready")
            throw new InvalidOperationException("Private bridge requires its owned temporary workspace.");
    }
}
