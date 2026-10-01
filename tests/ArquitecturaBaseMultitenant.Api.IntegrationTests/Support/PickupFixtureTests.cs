using ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>
/// Comprueba que cada fixture use un pickup temporal propio y fuera del repositorio. Al terminar, solo debe
/// borrar el directorio que creó.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class PickupFixtureTests(ApiFactory factory)
{
    [Fact]
    public void Pickup_directory_is_unique_and_outside_the_source_tree()
    {
        using var client = factory.CreateClient();
        var directory = factory.Services.GetRequiredService<IOptions<EmailOptions>>()
            .Value.PickupDirectory;

        Assert.True(Path.IsPathFullyQualified(directory));
        Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(directory),
            StringComparison.OrdinalIgnoreCase);
        Assert.Matches(@"mt-tests-[0-9a-f]{32}$", directory);
    }

    [Fact]
    public async Task Disposing_fixture_removes_only_its_temporary_pickup()
    {
        string directory;
        await using (var isolated = new ApiFactory())
        {
            await isolated.InitializeAsync();
            using var client = isolated.CreateClient();
            directory = isolated.Services.GetRequiredService<IOptions<EmailOptions>>()
                .Value.PickupDirectory;
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "fixture-sentinel.eml"), "test",
                TestContext.Current.CancellationToken);
            Assert.True(File.Exists(Path.Combine(directory, "fixture-sentinel.eml")));
        }

        Assert.False(Directory.Exists(directory));
    }
}
