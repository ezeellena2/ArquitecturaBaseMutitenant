using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.Controllers.Account;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Contracts;

/// <summary>
/// Contrato de la base de la 12 (K1): la campana de los tres accesos, sin permiso propio, y el retiro del canje del enlace
/// de un solo uso (decisión 179). El consentimiento de «Cargar teléfonos» lo prueba el módulo de WhatsApp.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class Base12ContractTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void The_bell_answers_the_three_accesses_without_a_permission_of_its_own()
    {
        var access = typeof(NotificationsController).GetCustomAttribute<AccessAttribute>();
        Assert.Equal([Access.Consumer, Access.Business, Access.Platform], access!.Allowed);
        var actions = typeof(NotificationsController).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        Assert.Equal(2, actions.Length);
        Assert.All(actions.Append<MemberInfo>(typeof(NotificationsController)), member => Assert.DoesNotContain(member.GetCustomAttributes(),
            attribute => attribute is HasPermissionAttribute or HasCompanyPermissionAttribute or HasPlatformPermissionAttribute));
    }

    [Fact]
    public void OpenApi_publishes_the_bell_list_and_mark_as_read()
    {
        var document = Contract();
        var schemas = document["components"]!["schemas"]!;
        var list = document["paths"]!["/api/notifications"]!["get"]!;
        Assert.Equal("#/components/schemas/NotificationListResponse",
            list["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>());
        var response = schemas["NotificationListResponse"]!;
        Assert.Equal(["items", "unread"], Required(response));
        Assert.Equal(NotificationListResponse.Limit, response["properties"]!["items"]!["maxItems"]!.GetValue<int>());
        Assert.Equal("#/components/schemas/NotificationItem", response["properties"]!["items"]!["items"]!["$ref"]!.GetValue<string>());

        var item = schemas["NotificationItem"]!;
        Assert.Equal(["id", "kind", "link", "occurredAtUtc", "params", "read"], Required(item));
        var properties = item["properties"]!;
        Assert.Equal(NotificationItem.IdMaxLength, properties["id"]!["maxLength"]!.GetValue<int>());
        Assert.Equal("string", properties["kind"]!["type"]!.GetValue<string>());
        Assert.Equal("string", properties["params"]!["additionalProperties"]!["type"]!.GetValue<string>());
        Assert.Equal("date-time", properties["occurredAtUtc"]!["format"]!.GetValue<string>());
        Assert.Equal("string", properties["link"]!["type"]!.GetValue<string>());
        Assert.Equal("boolean", properties["read"]!["type"]!.GetValue<string>());

        // Marcar leído: sin cuerpo, sin idempotencia y 204 también para un id que no es de un aviso visible.
        var read = document["paths"]!["/api/notifications/{id}/read"]!["post"]!;
        Assert.NotNull(read["responses"]!["204"]);
        Assert.Null(read["requestBody"]);
        Assert.Equal(["id"], read["parameters"]!.AsArray().Select(parameter => parameter!["name"]!.GetValue<string>()));
        foreach (var operation in new[] { list, read })
        {
            Assert.Null(operation["responses"]!["501"]);
            Assert.DoesNotContain(operation["x-error-codes"]!.AsArray(), error => error!["code"]!.GetValue<string>() == "General.Contract.NotImplemented");
        }
    }

    [Fact]
    public async Task Login_link_redemption_is_retired_from_the_contract_and_the_api()
    {
        var document = Contract();
        Assert.DoesNotContain(document["paths"]!.AsObject(), path => path.Key.StartsWith("/api/auth/login-link", StringComparison.Ordinal));
        Assert.DoesNotContain(document["components"]!["schemas"]!.AsObject(), schema => schema.Key.Contains("LoginLink", StringComparison.Ordinal));

        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        foreach (var action in new[] { "preview", "redeem" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login-link/" + action)
            {
                Content = JsonContent.Create(new { token = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", access = "consumer" }),
            };
            request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
            using var response = await client.SendAsync(request, Ct);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.False(response.Headers.Contains("Set-Cookie"));
        }
    }

    private static string[] Required(JsonNode schema) =>
        [.. schema["required"]!.AsArray().Select(value => value!.GetValue<string>()).Order(StringComparer.Ordinal)];

    private static JsonNode Contract()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArquitecturaBaseMultitenant.slnx")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
        return JsonNode.Parse(File.ReadAllText(Path.Combine(root, "docs", "contracts", "openapi.json")))!;
    }
}
