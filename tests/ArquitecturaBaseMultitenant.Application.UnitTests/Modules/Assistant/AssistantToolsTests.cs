using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Assistant;

public sealed class AssistantToolsTests
{
    [Theory]
    [InlineData("""{"page":1,"pageSize":20,"search":" person "}""", true)]
    [InlineData("""{"page":0,"pageSize":20,"search":"person"}""", false)]
    [InlineData("""{"page":1,"pageSize":21,"search":"person"}""", false)]
    [InlineData("""{"page":1,"pageSize":2,"search":"person"}""", false)]
    [InlineData("""{"page":"1","pageSize":20,"search":"person"}""", false)]
    [InlineData("""{"page":1,"pageSize":20,"search":"person","tenantId":"00000000-0000-0000-0000-000000000001"}""", false)]
    [InlineData("""{"page":1,"page":2,"pageSize":20,"search":"person"}""", false)]
    [InlineData("""{"page":1,"pageSize":20,"search":null}""", false)]
    [InlineData("""{"page":1,"pageSize":20,"search":" "}""", false)]
    public void Tool_arguments_are_closed_typed_and_bounded(string json, bool valid)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Equal(valid, AssistantToolArguments.IsValid(document.RootElement,
            [new("page", "integer", true), new("pageSize", "integer", true), new("search", "string", true)]));
    }

    [Theory]
    [InlineData("""{"roleId":"00000000-0000-0000-0000-000000000000"}""", false)]
    [InlineData("""{"roleId":"00000000-0000-0000-0000-000000000001"}""", true)]
    [InlineData("""{"fromUtc":"2026-10-10T00:00:00","toUtc":"2026-10-11T00:00:00Z"}""", false)]
    [InlineData("""{"fromUtc":"2026-10-11T00:00:00Z","toUtc":"2026-10-10T00:00:00Z"}""", false)]
    [InlineData("""{"fromUtc":"2026-10-10T00:00:00Z","toUtc":"2026-10-11T00:00:00Z"}""", true)]
    [InlineData("""{"status":"Unknown"}""", false)]
    [InlineData("""{"status":"Pending"}""", true)]
    public void UUID_enums_and_utc_ranges_are_validated(string json, bool valid)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Equal(valid, AssistantToolArguments.IsValid(document.RootElement,
            [new("roleId", "uuid"), new("fromUtc", "utc"), new("toUtc", "utc"), new("status", "string", Values: ["Pending", "Expired"])]));
    }
}
