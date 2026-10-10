using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

public sealed partial class HomeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Installed_assistant_preserves_company_only_home(bool assistantEnabled) =>
        AssertCompanyHomeAsync(async (tenantId, adminClient) =>
        {
        if (assistantEnabled)
        {
            await AssistantTestData.EnableAsync(factory, tenantId);
            using var beforeNotice = await adminClient.GetAsync("/api/organization/assistant/conversations", Ct);
            Assert.Equal(HttpStatusCode.BadRequest, beforeNotice.StatusCode);
            using var problem = await beforeNotice.Content.ReadFromJsonAsync<JsonDocument>(Ct);
            Assert.Equal(ArquitecturaBaseMultitenant.Domain.Assistant.AssistantErrors.NoticeAcceptanceRequiredCode,
                problem!.RootElement.GetProperty("code").GetString());
            await AssistantExecutionActorTests.AcceptNoticeAsync(adminClient, "/api/organization/assistant/notice");
        }
        using var assistant = await adminClient.GetAsync("/api/organization/assistant/conversations", Ct);
        Assert.Equal(assistantEnabled ? HttpStatusCode.OK : HttpStatusCode.NotFound, assistant.StatusCode);
        using var profile = await adminClient.GetAsync("/api/me", Ct);
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        using var profileJson = JsonDocument.Parse(await profile.Content.ReadAsStringAsync(Ct));
        Assert.Contains(Permissions.Assistant.Use, profileJson.RootElement.GetProperty("effectivePermissions")
            .GetProperty("organization").EnumerateArray().Select(permission => permission.GetString()));

        });

    [Fact]
    public async Task An_explicit_organization_role_with_only_assistant_use_keeps_organization_home()
    {
        using var ownerClient = NewClient();
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Inicio asignado", Ct);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct);
        var companyId = await OrganizationJourney.CreateCompanyAsync(ownerClient, "Delta Norte S.A.", Ct);
        using var memberClient = NewClient();
        var member = await AccountJourney.RegisterAsync(factory, memberClient, Ct);
        var memberId = await OrganizationJourney.AddActiveMemberAsync(factory, tenantId, member.UserId, Ct);
        using var role = await AccountJourney.PostAsync(ownerClient, "/api/roles", new
        {
            name = "Solo usar asistente",
            description = (string?)null,
            scope = "Organization",
            companyId = (Guid?)null,
            permissions = new[] { Permissions.Assistant.Use },
        }, Ct);
        Assert.True(role.StatusCode == HttpStatusCode.Created, await role.Content.ReadAsStringAsync(Ct));
        var roleId = await role.Content.ReadFromJsonAsync<Guid>(Ct);
        await PutAsync(ownerClient, $"/api/users/{memberId:D}", new
        {
            roleIds = new[] { roleId },
            companies = new[] { new { companyId, roleIds = new[] { await CompanyAdminRoleIdAsync(ownerClient) } } },
            version = await VersionAsync(ownerClient, $"/api/users/{memberId:D}"),
        });
        await AccountJourney.AuthorizeAsync(memberClient, "business", Ct);

        var home = await OrganizationJourney.HomeAsync(memberClient, Ct);

        var organization = home.GetProperty("organization");
        Assert.Equal(JsonValueKind.Object, organization.ValueKind);
        foreach (var area in new[] { "users", "roles", "companies", "publicPage", "settings", "audit" })
            Assert.Equal(JsonValueKind.Null, organization.GetProperty(area).ValueKind);
        Assert.Empty(home.GetProperty("companies").EnumerateArray());
        Assert.Empty(Pending(home));
    }


}
