using System.Xml.Linq;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Comprueba que cada proyecto referencie únicamente los proyectos permitidos. Protege la dirección de
/// dependencias de la solución.
/// </summary>
public sealed class ProjectReferencesTests
{
    public static TheoryData<string, string[]> AllowedReferences => new()
    {
        { "ArquitecturaBaseMultitenant.Domain", [] },
        { "ArquitecturaBaseMultitenant.Application", ["ArquitecturaBaseMultitenant.Domain"] },
        { "ArquitecturaBaseMultitenant.Infrastructure", ["ArquitecturaBaseMultitenant.Application", "ArquitecturaBaseMultitenant.Domain"] },
        { "ArquitecturaBaseMultitenant.Api", ["ArquitecturaBaseMultitenant.Application", "ArquitecturaBaseMultitenant.Infrastructure", "ArquitecturaBaseMultitenant.ServiceDefaults"] },
        { "ArquitecturaBaseMultitenant.AppHost", ["ArquitecturaBaseMultitenant.Api"] },
        { "ArquitecturaBaseMultitenant.ServiceDefaults", [] },
    };

    [Theory]
    [MemberData(nameof(AllowedReferences))]
    public void Project_only_references_allowed_projects(string project, string[] allowed)
    {
        var forbidden = ReadProjectReferences(project).Except(allowed, StringComparer.Ordinal);

        Assert.Empty(forbidden);
    }

    private static string[] ReadProjectReferences(string project)
    {
        var path = Path.Combine(SolutionRoot.FullPath, "src", project, project + ".csproj");

        return XDocument.Load(path)
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")!.Value.Replace('\\', '/'))
            .Select(Path.GetFileNameWithoutExtension)
            .Select(name => name!)
            .ToArray();
    }
}
