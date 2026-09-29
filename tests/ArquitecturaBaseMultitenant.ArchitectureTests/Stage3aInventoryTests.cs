namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>Las piezas de ingreso prometidas por la parte 3a existen antes de cerrar su puerta.</summary>
public sealed class Stage3aInventoryTests
{
    private static readonly string[] RequiredFiles =
    [
        "src/ArquitecturaBaseMultitenant.Api/Controllers/Auth/LoginCodeController.cs",
        "src/ArquitecturaBaseMultitenant.Api/Controllers/Auth/SignupController.cs",
        "src/ArquitecturaBaseMultitenant.Api/Controllers/Auth/ExternalLoginController.cs",
        "src/ArquitecturaBaseMultitenant.Api/Controllers/Auth/ConnectController.cs",
        "src/ArquitecturaBaseMultitenant.Api/Controllers/Account/MeController.cs",
        "src/ArquitecturaBaseMultitenant.Api/Controllers/Account/LegalController.cs",
        "src/ArquitecturaBaseMultitenant.Api/Authentication/OpenIdPrincipalFactory.cs",
        "src/ArquitecturaBaseMultitenant.Api/Tenancy/TenantResolutionMiddleware.cs",
        "src/ArquitecturaBaseMultitenant.Application/Services/Auth/AccountService.cs",
        "src/ArquitecturaBaseMultitenant.Application/Services/Auth/ConnectService.cs",
        "src/ArquitecturaBaseMultitenant.Application/Interfaces/Services/IConnectLogoutService.cs",
        "src/ArquitecturaBaseMultitenant.Application/Services/Auth/ConnectLogoutService.cs",
        "src/ArquitecturaBaseMultitenant.Application/Services/Profile/ProfileService.cs",
        "src/ArquitecturaBaseMultitenant.Application/Services/Legal/LegalService.cs",
        "src/ArquitecturaBaseMultitenant.Infrastructure/Identity/IdentityRegistration.cs",
        "src/ArquitecturaBaseMultitenant.Infrastructure/Persistence/Seed/DevelopmentSeeder.cs",
        "docs/contracts/openapi.json",
        "tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Auth/AuthPipelineTests.cs",
        "tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Auth/LogoutTests.cs",
        "tests/ArquitecturaBaseMultitenant.Application.UnitTests/Services/Auth/ConnectLogoutServiceTests.cs",
    ];

    [Fact]
    public void Stage3a_backend_inventory_is_present()
    {
        Assert.DoesNotContain(RequiredFiles, path => !File.Exists(Path.Combine(SolutionRoot.FullPath,
            path.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void Split_stage_keeps_harness_at_two_until_3c_closes() =>
        Assert.Equal(2, HarnessStage.Closed);

    [Fact]
    public void Identity_feature_and_first_area_pointers_are_present()
    {
        var root = SolutionRoot.FullPath;
        var feature = Path.Combine(root, "docs", "features", "identidad.md");

        Assert.True(File.Exists(feature), "The identity feature document is missing.");
        Assert.Contains("## 3a · Ingreso", File.ReadAllText(feature), StringComparison.Ordinal);

        foreach (var folder in new[]
        {
            "src/ArquitecturaBaseMultitenant.Domain/Users",
            "src/ArquitecturaBaseMultitenant.Domain/Authentication",
            "src/ArquitecturaBaseMultitenant.Infrastructure/Identity",
            "tests/ArquitecturaBaseMultitenant.Application.UnitTests/Identity",
            "tests/ArquitecturaBaseMultitenant.Domain.UnitTests/Authentication",
        })
        {
            var fullFolder = Path.Combine(root, folder.Replace('/', Path.DirectorySeparatorChar));
            var agents = Path.Combine(fullFolder, "AGENTS.md");
            var claude = Path.Combine(fullFolder, "CLAUDE.md");

            Assert.True(File.Exists(agents), $"{folder}/AGENTS.md is missing.");
            Assert.Contains("docs/features/identidad.md", File.ReadAllText(agents), StringComparison.Ordinal);
            Assert.True(File.Exists(claude), $"{folder}/CLAUDE.md is missing.");
            Assert.Equal("@AGENTS.md", File.ReadAllText(claude).Trim());
        }
    }
}
