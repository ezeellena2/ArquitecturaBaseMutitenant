namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Localiza la raíz de la solución recorriendo los directorios desde el ensamblado de tests. Permite leer
/// archivos del repositorio sin depender del directorio de ejecución.
/// </summary>
internal static class SolutionRoot
{
    public static string FullPath { get; } = Find();

    private static string Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArquitecturaBaseMultitenant.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("ArquitecturaBaseMultitenant.slnx was not found.");
    }
}
