using System.Reflection;
using System.Xml.Linq;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// capas-y-flujo.md: sin MediatR (handlers ni CQRS), sin AutoMapper y sin Scrutor, en ningún proyecto. Se leen todos los
/// <c>.csproj</c>, <c>.props</c> y <c>.targets</c> del repo, con <c>Directory.Packages.props</c>, y las referencias de los
/// ensamblados de producción, por si una dependencia transitiva lo trae y el código lo usa.
/// </summary>
public sealed class ForbiddenPackagesTests
{
    private static readonly string[] Forbidden = ["AutoMapper", "MediatR", "Scrutor"];

    private static readonly string[] Ignored = ["bin", "obj", ".local", "node_modules", "TestResults", ".git"];

    private static readonly Assembly[] Production =
    [
        Assembly.Load("ArquitecturaBaseMultitenant.Domain"),
        Assembly.Load("ArquitecturaBaseMultitenant.Application"),
        Assembly.Load("ArquitecturaBaseMultitenant.Infrastructure"),
        Assembly.Load("ArquitecturaBaseMultitenant.Api"),
    ];

    [Fact]
    public void No_project_references_or_pins_a_forbidden_package()
    {
        var packages = PackageEntries().ToArray();

        Assert.Contains(packages, entry => entry.File == "Directory.Packages.props" && entry.Package == "FluentValidation");
        Assert.Contains(packages, entry => entry.File.EndsWith("ArquitecturaBaseMultitenant.Infrastructure.csproj", StringComparison.Ordinal));
        Assert.Empty(packages.Where(entry => IsForbidden(entry.Package)).Select(entry => $"{entry.File}: {entry.Package}"));
    }

    [Fact]
    public void No_production_assembly_references_a_forbidden_library()
    {
        var references = Production.SelectMany(assembly => assembly.GetReferencedAssemblies()
                .Select(reference => (Assembly: assembly.GetName().Name, Reference: reference.Name ?? string.Empty)))
            .ToArray();

        Assert.Contains(references, entry => entry.Reference == "FluentValidation");
        Assert.Empty(references.Where(entry => IsForbidden(entry.Reference)).Select(entry => $"{entry.Assembly}: {entry.Reference}"));
    }

    [Theory]
    [InlineData("AutoMapper", true)]
    [InlineData("AutoMapper.Extensions.Microsoft.DependencyInjection", true)]
    [InlineData("MediatR", true)]
    [InlineData("mediatr.contracts", true)]
    [InlineData("Scrutor", true)]
    [InlineData("FluentValidation", false)]
    [InlineData("AutoMapperish", false)]
    [InlineData("Microsoft.Extensions.Caching.Hybrid", false)]
    public void Detector_finds_the_forbidden_packages_and_their_extensions(string package, bool forbidden) =>
        Assert.Equal(forbidden, IsForbidden(package));

    private static bool IsForbidden(string package) =>
        Forbidden.Any(name => package.Equals(name, StringComparison.OrdinalIgnoreCase)
            || package.StartsWith(name + ".", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<(string File, string Package)> PackageEntries() =>
        MsBuildFiles(SolutionRoot.FullPath)
            .SelectMany(file => XDocument.Load(file).Descendants()
                .Where(element => element.Name.LocalName is "PackageReference" or "PackageVersion" or "GlobalPackageReference")
                .Select(element => (File: Path.GetRelativePath(SolutionRoot.FullPath, file).Replace('\\', '/'),
                    Package: (element.Attribute("Include") ?? element.Attribute("Update"))?.Value ?? string.Empty)));

    private static IEnumerable<string> MsBuildFiles(string folder) =>
        Directory.EnumerateFiles(folder)
            .Where(file => Path.GetExtension(file) is ".csproj" or ".props" or ".targets")
            .Concat(Directory.EnumerateDirectories(folder)
                .Where(child => !Ignored.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase))
                .SelectMany(MsBuildFiles));
}
