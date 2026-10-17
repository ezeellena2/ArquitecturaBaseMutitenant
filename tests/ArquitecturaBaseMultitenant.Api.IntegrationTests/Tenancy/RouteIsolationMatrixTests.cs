using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// La matriz de aislamiento (E9). Toda ruta de la Api, también las de los módulos, pasa por ella: con un acceso que no
/// declara, ninguna llega a la acción; una ruta del sitio público responde sólo en el subdominio de una organización
/// publicada; y con identificadores de otra organización, persona u operador responde 404, igual que con uno inventado,
/// sin devolver sus datos ni tocarlos. Cada ruta con identificadores tiene su caso: las pruebas con los datos ajenos o
/// el motivo por el que el identificador no pertenece a una organización, una persona ni un operador. Los módulos suman
/// sus casos y sus datos en su carpeta, así quitar un módulo se lleva su parte.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed partial class RouteIsolationMatrixTests(ApiFactory factory)
{
    private const string PlatformWide = "Plataforma opera sobre todas las organizaciones y cuentas";
    private const string NotAnIdentifier = "El parámetro no identifica datos de nadie";
    private static readonly string[] Technical = ["OutboxMessages", "AssistantTurnWorks", "RateLimitCounters", "CacheTagVersions",
        "IdempotencyKeys", "DataProtectionKeys", "OpenIddictTokens", "OpenIddictAuthorizations"];
    private static readonly IReadOnlyDictionary<string, RouteCase> Cases = CreateCases();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Dictionary<string, RouteCase> CreateCases()
    {
        var cases = new Dictionary<string, RouteCase>(StringComparer.Ordinal)
        {
            ["GET /api/companies/{companyid:guid}"] = Foreign(w => Business($"/api/companies/{w.B("company")}")),
            ["PUT /api/companies/{companyid:guid}"] = Foreign(w => Business($"/api/companies/{w.B("company")}",
                new { name = "Matriz", taxIdNumber = (string?)null, timeZoneId = (string?)null, version = 1 })),
            ["PUT /api/companies/{companyid:guid}/status"] = Foreign(w => Business($"/api/companies/{w.B("company")}/status",
                new { status = nameof(CompanyStatus.Inactive), version = 1 })),
            ["GET /api/companies/{companyid:guid}/candidates/paged"] = Foreign(w => Business($"/api/companies/{w.B("company")}/candidates/paged")),
            ["GET /api/companies/{companyid:guid}/members/paged"] = Foreign(w => Business($"/api/companies/{w.B("company")}/members/paged")),
            ["GET /api/companies/{companyid:guid}/members/filter-counts"] = Foreign(w => Business($"/api/companies/{w.B("company")}/members/filter-counts")),
            ["GET /api/companies/{companyid:guid}/roles/paged"] = Foreign(w => Business($"/api/companies/{w.B("company")}/roles/paged")),
            ["POST /api/companies/{companyid:guid}/members"] = Foreign(
                w => Business($"/api/companies/{w.B("company")}/members", new { memberId = w.A("member"), roleIds = Array.Empty<Guid>() }),
                w => Business($"/api/companies/{w.A("company")}/members", new { memberId = w.B("member"), roleIds = Array.Empty<Guid>() })),
            ["PUT /api/companies/{companyid:guid}/members/{memberid:guid}/roles"] = Foreign(
                w => Business($"/api/companies/{w.B("company")}/members/{w.B("member")}/roles", new { roleIds = Array.Empty<Guid>(), version = 1 }),
                w => Business($"/api/companies/{w.A("company")}/members/{w.B("member")}/roles", new { roleIds = Array.Empty<Guid>(), version = 1 })),
            ["PUT /api/companies/{companyid:guid}/members/{memberid:guid}/status"] = Foreign(
                w => Business($"/api/companies/{w.B("company")}/members/{w.B("member")}/status", new { status = nameof(CompanyMembershipStatus.Inactive), version = 1 }),
                w => Business($"/api/companies/{w.A("company")}/members/{w.B("member")}/status", new { status = nameof(CompanyMembershipStatus.Inactive), version = 1 })),
            ["POST /api/companies/{companyid:guid}/invitations/{invitationid:guid}/resend"] = Foreign(
                w => Business($"/api/companies/{w.B("company")}/invitations/{w.B("invitation")}/resend"),
                w => Business($"/api/companies/{w.A("company")}/invitations/{w.B("invitation")}/resend")),
            ["DELETE /api/companies/{companyid:guid}/invitations/{invitationid:guid}"] = Foreign(
                w => Business($"/api/companies/{w.B("company")}/invitations/{w.B("invitation")}"),
                w => Business($"/api/companies/{w.A("company")}/invitations/{w.B("invitation")}")),
            ["GET /api/directory/{slug}/logo"] = Foreign(w => new Probe(Caller.Anonymous, $"/api/directory/{w.BText("slug")}/logo")),
            ["POST /api/me/login-methods/{methodid:guid}/code"] = Foreign(w => Consumer($"/api/me/login-methods/{w.B("login-method")}/code")),
            ["POST /api/me/login-methods/{methodid:guid}/verify"] = Foreign(w => Consumer($"/api/me/login-methods/{w.B("login-method")}/verify", new { code = "123456" })),
            ["PUT /api/me/login-methods/{methodid:guid}/primary"] = Foreign(w => Consumer($"/api/me/login-methods/{w.B("login-method")}/primary")),
            ["DELETE /api/me/login-methods/{methodid:guid}"] = Foreign(w => Consumer($"/api/me/login-methods/{w.B("login-method")}")),
            ["POST /api/organization/settings/domains/{domainid:guid}/verify"] = Foreign(w => Business($"/api/organization/settings/domains/{w.B("domain")}/verify")),
            ["DELETE /api/organization/settings/domains/{domainid:guid}"] = Foreign(w => Business($"/api/organization/settings/domains/{w.B("domain")}?version=1")),
            ["GET /api/roles/{id:guid}"] = Foreign(w => Business($"/api/roles/{w.B("role")}")),
            ["PUT /api/roles/{id:guid}"] = Foreign(w => Business($"/api/roles/{w.B("role")}", new
            {
                name = "Matriz",
                description = (string?)null,
                scope = nameof(RoleScope.Organization),
                companyId = (Guid?)null,
                permissions = new[] { "roles.read" },
                version = 1,
            })),
            ["DELETE /api/roles/{id:guid}"] = Foreign(w => Business($"/api/roles/{w.B("role")}", new { version = 1 })),
            ["POST /api/users/invitations/{invitationid:guid}/resend"] = Foreign(w => Business($"/api/users/invitations/{w.B("invitation")}/resend")),
            ["DELETE /api/users/invitations/{invitationid:guid}"] = Foreign(w => Business($"/api/users/invitations/{w.B("invitation")}")),
            ["GET /api/users/{memberid:guid}"] = Foreign(w => Business($"/api/users/{w.B("member")}")),
            ["PUT /api/users/{memberid:guid}"] = Foreign(w => Business($"/api/users/{w.B("member")}",
                new { roleIds = Array.Empty<Guid>(), companies = Array.Empty<object>(), version = 1 })),
            ["PUT /api/users/{memberid:guid}/status"] = Foreign(w => Business($"/api/users/{w.B("member")}/status", new { isActive = false, version = 1 })),
            ["DELETE /api/users/{memberid:guid}"] = Foreign(w => Business($"/api/users/{w.B("member")}?version=1")),
        };
        foreach (var route in new[]
        {
            "GET /api/platform/accounts/{id:guid}", "POST /api/platform/accounts/{id:guid}/authenticator/reset",
            "POST /api/platform/accounts/{id:guid}/deletion", "PUT /api/platform/accounts/{id:guid}/operator",
            "DELETE /api/platform/accounts/{id:guid}/operator", "POST /api/platform/accounts/{id:guid}/reactivate",
            "POST /api/platform/accounts/{id:guid}/revoke-sessions", "POST /api/platform/accounts/{id:guid}/suspend",
            "GET /api/platform/audit/{id:guid}", "GET /api/platform/legal-documents/{id:guid}",
            "DELETE /api/platform/operators/{id:guid}/invitation", "POST /api/platform/operators/{id:guid}/invitation/resend",
            "POST /api/platform/recoveries/{id:guid}/approve", "POST /api/platform/recoveries/{id:guid}/reject",
            "GET /api/platform/tenants/{id:guid}", "POST /api/platform/tenants/{id:guid}/approve", "POST /api/platform/tenants/{id:guid}/close",
            "GET /api/platform/tenants/{id:guid}/domains", "GET /api/platform/tenants/{id:guid}/domains/paged",
            "DELETE /api/platform/tenants/{id:guid}/domains/{domain}", "POST /api/platform/tenants/{id:guid}/domains/{domain}/verify",
            "GET /api/platform/tenants/{id:guid}/features", "PUT /api/platform/tenants/{id:guid}/features/{featurekey}",
            "GET /api/platform/tenants/{id:guid}/history", "GET /api/platform/tenants/{id:guid}/public-site",
            "POST /api/platform/tenants/{id:guid}/public-site/allow-publish", "POST /api/platform/tenants/{id:guid}/public-site/unpublish",
            "POST /api/platform/tenants/{id:guid}/reactivate", "POST /api/platform/tenants/{id:guid}/reject",
            "POST /api/platform/tenants/{id:guid}/retry-provisioning", "POST /api/platform/tenants/{id:guid}/suspend",
        })
            cases[route] = Shared(PlatformWide);
        AddAssistantCases(cases);
        AddWhatsAppCases(cases);
        return cases;
    }

    static partial void AddAssistantCases(Dictionary<string, RouteCase> cases);
    static partial void AddWhatsAppCases(Dictionary<string, RouteCase> cases);
    static partial void AddAssistantSeed(List<Func<World, Task>> seeds);
    static partial void AddWhatsAppSeed(List<Func<World, Task>> seeds);

    [Fact]
    public void Every_route_with_identifiers_has_an_isolation_case()
    {
        var routes = Endpoints().Select(route => route.Key).Where(key => key.Contains('{', StringComparison.Ordinal)).Distinct().ToArray();
        var missing = routes.Except(Cases.Keys).ToArray();
        var stale = Cases.Keys.Except(routes).ToArray();
        Assert.True(missing.Length == 0 && stale.Length == 0,
            "Sin caso:\n" + string.Join('\n', missing) + "\nSobran:\n" + string.Join('\n', stale));
    }

    [Fact]
    public async Task A_route_rejects_every_access_it_does_not_declare()
    {
        using var anonymous = Client("https://localhost");
        var consumer = await PersonAsync();
        var business = await BusinessAsync(await PersonAsync(), "Matriz accesos");
        var platform = await OperatorAsync();
        var failures = new List<string>();
        foreach (var route in Endpoints())
        {
            if (route.Endpoint.Metadata.GetMetadata<AccessAttribute>() is not { } access) continue;
            await ExpectAsync(anonymous, "anónimo", HttpStatusCode.Unauthorized);
            foreach (var (caller, client) in new[] { (Access.Consumer, consumer.Client), (Access.Business, business.Client), (Access.Platform, platform.Client) })
                if (!access.Allowed.Contains(caller)) await ExpectAsync(client, caller.ToString(), HttpStatusCode.Forbidden);

            async Task ExpectAsync(HttpClient client, string name, HttpStatusCode expected)
            {
                var (status, _) = await SendAsync(client, route.Method, Placeholder(route.Template), EmptyBody(route));
                if (status != expected) failures.Add($"{route.Key} como {name}: {(int)status}, se esperaba {(int)expected}");
            }
        }
        Assert.True(failures.Count == 0, string.Join('\n', failures));
    }

    [Fact]
    public async Task Public_site_routes_answer_only_on_a_published_subdomain()
    {
        var mark = Mark("site");
        var published = await SiteAsync(await PersonAsync(), "Matriz publicada " + Guid.NewGuid().ToString("N")[..8], true);
        var hidden = await SiteAsync(await PersonAsync(), "Organización " + mark, false);
        using var main = Client("https://localhost");
        using var publishedSite = Client($"https://{published.Slug}.localhost:5174");
        using var hiddenSite = Client($"https://{hidden.Slug}.localhost:5174");
        var failures = new List<string>();
        var routes = Endpoints().Where(route => route.Endpoint.Metadata.GetMetadata<PublicSiteAttribute>() is not null).ToArray();
        Assert.NotEmpty(routes);
        foreach (var route in routes)
        {
            var path = Placeholder(route.Template);
            var (_, hiddenBody) = await SendAsync(main, route.Method, path, EmptyBody(route));
            var notFound = Code(hiddenBody);
            foreach (var (name, client) in new[] { ("el dominio principal", main), ("un subdominio sin publicar", hiddenSite) })
            {
                var (status, body) = await SendAsync(client, route.Method, path, EmptyBody(route));
                if (status != HttpStatusCode.NotFound || Code(body) != notFound) failures.Add($"{route.Key} responde en {name}: {(int)status}");
            }
            var (publishedStatus, publishedBody) = await SendAsync(publishedSite, route.Method, path, EmptyBody(route));
            if ((publishedStatus == HttpStatusCode.NotFound && Code(publishedBody) == notFound) || (int)publishedStatus >= 500)
                failures.Add($"{route.Key} no responde en el subdominio publicado: {(int)publishedStatus}");
            if (publishedBody.Contains(mark, StringComparison.Ordinal)) failures.Add($"{route.Key} devuelve datos de otra organización");
        }
        Assert.True(failures.Count == 0, string.Join('\n', failures));
    }

    [Fact]
    public async Task Identifiers_of_another_organization_person_or_operator_answer_404_without_data_or_effects()
    {
        var world = await WorldAsync();
        try
        {
            await ProbeAsync(world);
        }
        finally
        {
            foreach (var cleanup in world.Cleanups) await cleanup();
        }
    }

    private async Task ProbeAsync(World world)
    {
        var seeds = new List<Func<World, Task>>();
        AddAssistantSeed(seeds);
        AddWhatsAppSeed(seeds);
        foreach (var seed in seeds) await seed(world);
        // Las puertas están abiertas: con datos propios cada lado responde, así un 404 de la matriz es por el dato ajeno.
        world.OpenDoors.AddRange([
            (Caller.Business, $"/api/companies/{world.A("company")}"),
            (Caller.Consumer, "/api/me/login-methods"),
            (Caller.Platform, $"/api/platform/tenants/{world.OrganizationA}"),
        ]);
        foreach (var (caller, path) in world.OpenDoors)
            Assert.True((await SendAsync(world.Client(caller), "GET", path, null)).Status == HttpStatusCode.OK, $"{caller} no abre {path}");
        var before = await ForeignRowsAsync(world);
        world.SeededTables.AddRange(["tenant.Companies", "tenant.Roles", "tenant.Invitations", "platform.TenantDomains", "identity.LoginMethods"]);
        foreach (var table in world.SeededTables)
            Assert.True(before.Any(row => row.Key.EndsWith(":" + table, StringComparison.Ordinal) && !row.Value.StartsWith("0:", StringComparison.Ordinal)),
                $"La foto de los datos ajenos no ve {table}");
        var failures = new List<string>();
        var control = world.Control();
        foreach (var (route, @case) in Cases.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var method = route[..route.IndexOf(' ', StringComparison.Ordinal)];
            foreach (var build in @case.Probes)
            {
                var probe = build(world);
                var invented = build(control);
                var (status, body) = await SendAsync(world.Client(probe.Caller), method, probe.Path, probe.Body);
                var expected = await SendAsync(world.Client(invented.Caller), method, invented.Path, invented.Body);
                if (status != HttpStatusCode.NotFound) failures.Add($"{route} ({probe.Path}): {(int)status} {Code(body)}, se esperaba 404");
                else if (Code(body) != Code(expected.Body)) failures.Add($"{route} ({probe.Path}): {Code(body)} distingue el dato ajeno de uno inventado ({Code(expected.Body)})");
                if (world.Leaks(body, probe.Caller) is { } leak) failures.Add($"{route} ({probe.Path}) devuelve {leak}");
            }
        }
        foreach (var route in Endpoints().Where(route => route.Method == "GET" && !route.Template.Contains('{', StringComparison.Ordinal)))
            foreach (var caller in Callers(route.Endpoint))
            {
                var (status, body) = await SendAsync(world.Client(caller), "GET", "/" + route.Template, null);
                if ((int)status >= 500) failures.Add($"{route.Key} como {caller}: {(int)status}");
                if (world.Leaks(body, caller) is { } leak) failures.Add($"{route.Key} como {caller} devuelve {leak}");
            }
        var after = await ForeignRowsAsync(world);
        failures.AddRange(before.Where(row => !after.TryGetValue(row.Key, out var value) || value != row.Value)
            .Select(row => $"Cambiaron datos ajenos en {row.Key}"));
        Assert.True(failures.Count == 0, string.Join('\n', failures));
    }

    private static IEnumerable<Caller> Callers(Endpoint endpoint)
    {
        if (endpoint.Metadata.GetMetadata<PublicSiteAttribute>() is not null) yield return Caller.PublishedSite;
        else if (endpoint.Metadata.GetMetadata<AccessAttribute>() is { } access)
        {
            if (access.Allowed.Contains(Access.Consumer)) yield return Caller.Consumer;
            if (access.Allowed.Contains(Access.Business)) yield return Caller.Business;
            if (access.Allowed.Contains(Access.Platform)) yield return Caller.Platform;
        }
        else yield return Caller.Anonymous;
    }

    /// <summary>Siembra A (quien pregunta) y B (los datos ajenos) en el núcleo; los módulos siembran lo suyo después.</summary>
    private async Task<World> WorldAsync()
    {
        var world = new World(factory);
        var ownerA = await PersonAsync();
        var siteA = await SiteAsync(ownerA, "Matriz A " + Guid.NewGuid().ToString("N")[..8], true);
        world.OrganizationA = siteA.TenantId;
        world.PersonalA = ownerA.Personal;
        world.Clients[Caller.Consumer] = ownerA.Client;
        world.Clients[Caller.Business] = await TokenAsync(ownerA, "business", siteA.TenantId);
        var operatorA = await ExclusiveOperatorAsync();
        world.OperatorA = operatorA.Account;
        world.Own["operator"] = operatorA.Account.UserId;
        world.Clients[Caller.Platform] = operatorA.Client;
        world.Clients[Caller.Anonymous] = Client("https://localhost");
        world.Clients[Caller.PublishedSite] = Client($"https://{siteA.Slug}.localhost:5174");
        world.OwnerA = ownerA.Account;

        var ownerB = await PersonAsync();
        var siteB = await SiteAsync(ownerB, "Organización " + world.OrganizationMark, false);
        world.OrganizationB = siteB.TenantId;
        world.PersonalB = ownerB.Personal;
        world.OwnerB = ownerB.Account;
        world.ForeignText["slug"] = siteB.Slug;
        world.ForeignClients[Caller.Consumer] = ownerB.Client;
        world.ForeignClients[Caller.Business] = await TokenAsync(ownerB, "business", siteB.TenantId);
        world.OperatorB = await OperatorAccountAsync();

        world.Own["company"] = await CompanyAsync(world.Clients[Caller.Business], "Matriz propia");
        world.Own["member"] = await OrganizationJourney.AddNamedMemberAsync(factory, world.OrganizationA, "Matriz miembro", Ct);
        var foreign = world.ForeignClients[Caller.Business];
        world.Foreign["company"] = await CompanyAsync(foreign, world.OrganizationMark + " empresa");
        world.Foreign["member"] = await OrganizationJourney.AddNamedMemberAsync(factory, world.OrganizationB, world.OrganizationMark + " miembro", Ct);
        using (var added = await AccountJourney.PostAsync(foreign, $"/api/companies/{world.B("company")}/members",
            new { memberId = world.B("member"), roleIds = Array.Empty<Guid>() }, Ct))
            Assert.True(added.IsSuccessStatusCode, await added.Content.ReadAsStringAsync(Ct));
        world.Foreign["role"] = await CreatedIdAsync(foreign, "/api/roles", new
        {
            name = world.OrganizationMark + " rol",
            description = (string?)null,
            scope = nameof(RoleScope.Organization),
            companyId = (Guid?)null,
            permissions = new[] { "roles.read" },
        }, "id");
        using (var roles = await foreign.GetAsync($"/api/roles/paged?companyId={world.B("company")}&pageSize=50", Ct))
        {
            using var json = JsonDocument.Parse(await roles.Content.ReadAsStringAsync(Ct));
            var companyRole = json.RootElement.GetProperty("items").EnumerateArray()
                .First(role => role.GetProperty("isSystemRole").GetBoolean()).GetProperty("id").GetGuid();
            world.Foreign["invitation"] = await CreatedIdAsync(foreign, "/api/users/invitations", new
            {
                email = world.OrganizationMark + "@example.test",
                roleIds = Array.Empty<Guid>(),
                companies = new[] { new { companyId = world.B("company"), roleIds = new[] { companyRole } } },
            }, "invitationId");
        }
        world.Foreign["domain"] = await CreatedIdAsync(foreign, "/api/organization/settings/domains",
            new { domain = world.OrganizationMark + ".example.test" }, "id");
        world.Foreign["login-method"] = await CreatedIdAsync(world.ForeignClients[Caller.Consumer], "/api/me/login-methods",
            new { email = world.PersonMark + "@example.test" }, "methodId");
        return world;
    }

    private async Task<Dictionary<string, string>> ForeignRowsAsync(World world)
    {
        var ids = world.ForeignIdentifiers();
        var rows = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tenant in new[] { world.OrganizationB, world.PersonalB })
        {
            await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
            await connection.OpenAsync(Ct);
            await using (var context = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenant, false)", connection))
            {
                context.Parameters.AddWithValue("tenant", tenant.ToString("D"));
                await context.ExecuteScalarAsync(Ct);
            }
            var tables = new List<(string Schema, string Table, List<string> Columns)>();
            await using (var columns = new NpgsqlCommand("""
                SELECT c.table_schema, c.table_name, c.column_name FROM information_schema.columns c
                JOIN information_schema.tables t ON t.table_schema = c.table_schema AND t.table_name = c.table_name AND t.table_type = 'BASE TABLE'
                WHERE c.table_schema IN ('tenant', 'public_site', 'engagement', 'platform', 'identity') AND c.data_type = 'uuid'
                    AND c.column_name IN ('Id', 'TenantId', 'BusinessTenantId', 'ConsumerTenantId', 'OrganizationId', 'OwnerUserId',
                        'UserId', 'AccountId', 'MemberUserId', 'ConversationId', 'ChatId', 'CustomerId', 'ChannelId', 'TemplateId', 'BroadcastId')
                ORDER BY 1, 2, 3
                """, connection))
            await using (var reader = await columns.ExecuteReaderAsync(Ct))
                while (await reader.ReadAsync(Ct))
                {
                    var (schema, table) = (reader.GetString(0), reader.GetString(1));
                    if (Technical.Contains(table)) continue;
                    if (tables.Count == 0 || tables[^1].Schema != schema || tables[^1].Table != table) tables.Add((schema, table, []));
                    tables[^1].Columns.Add(reader.GetString(2));
                }
            foreach (var (schema, table, columns) in tables)
            {
                var filter = string.Join(" OR ", columns.Select(column => $"t.\"{column}\" = ANY(@ids)"));
                await using var command = new NpgsqlCommand($"""
                    SELECT count(*)::text || ':' || coalesce(md5(string_agg(t::text, '|' ORDER BY t::text)), '')
                    FROM "{schema}"."{table}" t WHERE {filter}
                    """, connection);
                command.Parameters.AddWithValue("ids", ids);
                rows[$"{tenant:N}:{schema}.{table}"] = (string)(await command.ExecuteScalarAsync(Ct))!;
            }
        }
        return rows;
    }

    private async Task<Person> PersonAsync()
    {
        var client = Client("https://localhost");
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        using var me = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        return new Person(account, client, me!.RootElement.GetProperty("activeTenantId").GetGuid());
    }

    private async Task<Person> BusinessAsync(Person owner, string name)
    {
        var tenantId = await OrganizationJourney.StageAsync(factory, owner.Account, name + " " + Guid.NewGuid().ToString("N")[..8], Ct);
        return owner with { Client = await TokenAsync(owner, "business", tenantId) };
    }

    private async Task<Person> OperatorAsync()
    {
        var client = Client("https://localhost");
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, PlatformOperators.All, Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        return new Person(account, client, Guid.Empty);
    }

    /// <summary>
    /// Un operador exclusivo de Plataforma, sin espacio personal (ADR 0042), como el de AssistantExecutionActorSecurityTests:
    /// el único que ejecuta el chat de Plataforma.
    /// </summary>
    private async Task<Person> ExclusiveOperatorAsync()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var email = Email.Create("matriz-operador-" + Guid.NewGuid().ToString("N") + "@example.test").Value;
        Guid owner;
        using (var setup = factory.Services.CreateScope())
        {
            var services = setup.ServiceProvider;
            var created = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var accounts = services.GetRequiredService<IUserRepository>();
                var user = await accounts.CreateAsync("Operador de la matriz", "es-AR", "America/Argentina/Buenos_Aires", ct);
                await accounts.SetPrimaryEmailAsync(user.Id, email, ct);
                var method = LoginMethod.CreateEmail(user.Id, email);
                Assert.True(method.Verify(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime).IsSuccess);
                Assert.True(method.MakePrimary().IsSuccess);
                services.GetRequiredService<ILoginMethodRepository>().Add(method);
                return Result.Success(user.Id);
            }, CommitPolicy.OnSuccess, Ct);
            owner = created.Value;
        }
        await PlatformOperators.GrantAsync(factory.Services, owner, PlatformOperators.All, Ct);
        var client = Client("https://localhost");
        using (var requested = await AccountJourney.PostAsync(client, "/api/auth/login-code", new { email = email.Value }, Ct))
            Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(factory.Services, email.Value, Ct);
        using (var verified = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify",
            new { email = email.Value, code, returnUrl = "/connect/authorize?access=platform" }, Ct))
            Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        await PlatformFactorJourney.CompleteAsync(factory.Services, client, owner, Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        using var snapshot = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        var documents = snapshot!.RootElement.GetProperty("pendingLegalDocuments").EnumerateArray()
            .Select(row => new { id = row.GetProperty("id").GetGuid(), version = row.GetProperty("version").GetInt32() }).ToArray();
        using (var accepted = await AccountJourney.PostAsync(client, "/api/legal/accept", new { documents }, Ct))
            Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        return new Person(new AccountJourney(owner, email), client, Guid.Empty);
    }

    /// <summary>Un operador que no ingresa: sólo es dueño de sus datos personales de Plataforma.</summary>
    private async Task<AccountJourney> OperatorAccountAsync()
    {
        using var client = Client("https://localhost");
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, PlatformOperators.All, Ct);
        return account;
    }

    /// <summary>Un cliente aparte con la sesión de otro acceso de la misma cuenta: la Api sólo mira el bearer.</summary>
    private async Task<HttpClient> TokenAsync(Person person, string access, Guid tenantId)
    {
        var consumer = person.Client.DefaultRequestHeaders.Authorization;
        await AccountJourney.AuthorizeAsync(person.Client, access, Ct, tenantId);
        var client = Client("https://localhost");
        client.DefaultRequestHeaders.Authorization = person.Client.DefaultRequestHeaders.Authorization;
        person.Client.DefaultRequestHeaders.Authorization = consumer;
        return client;
    }

    private async Task<(Guid TenantId, string Slug)> SiteAsync(Person owner, string name, bool published)
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var tenant = Tenant.CreateBusiness(name, requiresApproval: false);
        Assert.True(tenant.SetSlug(TenantSlug.Create("matriz-" + Guid.NewGuid().ToString("N")[..12]).Value).IsSuccess);
        Assert.True(tenant.Activate().IsSuccess);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenant.Id, TenantKind.Business);
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [owner.Account.UserId], owner.Account.UserId);
            var page = PublicPage.Create();
            Assert.True(page.UpdateContent(name, null, null, null, null, null).IsSuccess);
            Assert.True(page.SetLogo([137, 80, 78, 71, 13, 10, 26, 10, 1], "image/png").IsSuccess);
            if (published) Assert.True(page.Publish(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime).IsSuccess);
            services.GetRequiredService<IPublicPageRepository>().Add(page);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess);
        return (tenant.Id, tenant.Slug!);
    }

    private static async Task<Guid> CompanyAsync(HttpClient client, string name) =>
        await CreatedIdAsync(client, "/api/companies", new { name, taxIdNumber = (string?)null, timeZoneId = (string?)null }, "companyId");

    internal static async Task<Guid> CreatedIdAsync(HttpClient client, string path, object body, string property)
    {
        using var response = await AccountJourney.PostAsync(client, path, body, Ct);
        var content = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.IsSuccessStatusCode, $"{path}: {(int)response.StatusCode} {content}");
        using var json = JsonDocument.Parse(content);
        return (json.RootElement.ValueKind == JsonValueKind.String ? json.RootElement : json.RootElement.GetProperty(property)).GetGuid();
    }

    internal static async Task<(HttpStatusCode Status, string Body)> SendAsync(HttpClient client, string method, string path, object? body)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Content = body switch
        {
            HttpContent content => content,
            null when method is "POST" or "PUT" => JsonContent.Create(new { }),
            null => null,
            _ => JsonContent.Create(body),
        };
        if (method == "POST") request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var response = await client.SendAsync(request, Ct);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(Ct));
    }

    /// <summary>El cuerpo vacío del tipo que declara la ruta: una subida espera un formulario multipart.</summary>
    private static MultipartFormDataContent? EmptyBody(RouteInfo route) =>
        route.Endpoint.Metadata.GetMetadata<IAcceptsMetadata>()?.ContentTypes
            .Any(type => type.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase)) == true ? new MultipartFormDataContent() : null;

    private static string? Code(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            return json.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
        }
        catch (JsonException) { return null; }
    }

    private static string Placeholder(string template) => "/" + System.Text.RegularExpressions.Regex.Replace(template,
        @"\{(?<name>[a-z]+)(:guid)?\}", match => match.Groups[0].Value.Contains(":guid", StringComparison.Ordinal)
            ? Guid.NewGuid().ToString("D") : "matriz");

    private HttpClient Client(string baseAddress) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri(baseAddress), AllowAutoRedirect = false });

    private static string Mark(string kind) => $"mt-matriz-{kind}-{Guid.NewGuid():N}"[..32];

    private IEnumerable<RouteInfo> Endpoints() =>
        factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() is { } action
                && action.ControllerTypeInfo.Assembly == typeof(Program).Assembly)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["<ANY>"])
                .Select(method => new RouteInfo(method.ToUpperInvariant(),
                    (endpoint.RoutePattern.RawText ?? "").Trim('/').ToLowerInvariant(), endpoint)))
            .OrderBy(route => route.Key, StringComparer.Ordinal);

    internal static RouteCase Foreign(params Func<World, Probe>[] probes) => new(probes, null);
    internal static RouteCase Shared(string reason) => new([], reason);
    internal static Probe Business(string path, object? body = null) => new(Caller.Business, path, body);
    internal static Probe Consumer(string path, object? body = null) => new(Caller.Consumer, path, body);
    internal static Probe Platform(string path, object? body = null) => new(Caller.Platform, path, body);

    private sealed record RouteInfo(string Method, string Template, Endpoint Endpoint)
    {
        public string Key => $"{Method} /{Template}";
    }

    private sealed record Person(AccountJourney Account, HttpClient Client, Guid Personal);

    internal enum Caller { Anonymous, Consumer, Business, Platform, PublishedSite }

    /// <summary>Una prueba con un dato ajeno: quién pregunta, la ruta concreta y el cuerpo, si lleva uno.</summary>
    internal sealed record Probe(Caller Caller, string Path, object? Body = null);

    /// <summary>
    /// El caso de una ruta con identificadores: sus pruebas con datos ajenos o el motivo por el que no lleva ninguna.
    /// </summary>
    internal sealed record RouteCase(IReadOnlyList<Func<World, Probe>> Probes, string? Reason);

    /// <summary>
    /// Lo sembrado: los clientes de A, que preguntan; los datos propios de A, para las rutas anidadas; y los de B, con sus
    /// marcas. La versión de control devuelve identificadores inventados en lugar de los de B.
    /// </summary>
    internal sealed class World
    {
        private readonly bool _control;

        internal World(ApiFactory factory) => Factory = factory;

        private World(World source)
        {
            Factory = source.Factory;
            Own = source.Own;
            Foreign = source.Foreign;
            ForeignText = source.ForeignText;
            Clients = source.Clients;
            ForeignClients = source.ForeignClients;
            OrganizationMark = source.OrganizationMark;
            PersonMark = source.PersonMark;
            OperatorMark = source.OperatorMark;
            _control = true;
        }

        internal ApiFactory Factory { get; }
        internal Dictionary<string, Guid> Own { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, Guid> Foreign { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, string> ForeignText { get; } = new(StringComparer.Ordinal);
        internal Dictionary<Caller, HttpClient> Clients { get; } = [];
        internal Dictionary<Caller, HttpClient> ForeignClients { get; } = [];
        internal string OrganizationMark { get; } = Mark("org");
        internal string PersonMark { get; } = Mark("persona");
        internal string OperatorMark { get; } = Mark("operador");
        internal Guid OrganizationA { get; set; }
        internal Guid PersonalA { get; set; }
        internal Guid OrganizationB { get; set; }
        internal Guid PersonalB { get; set; }
        internal AccountJourney OwnerA { get; set; } = null!;
        internal AccountJourney OwnerB { get; set; } = null!;
        internal AccountJourney OperatorA { get; set; } = null!;
        internal AccountJourney OperatorB { get; set; } = null!;

        /// <summary>Lo que un módulo cambió para todos y vuelve a dejar como estaba al terminar.</summary>
        internal List<Func<Task>> Cleanups { get; } = [];

        /// <summary>Rutas con datos propios que deben responder 200: prueban que el módulo está abierto para quien pregunta.</summary>
        internal List<(Caller Caller, string Path)> OpenDoors { get; } = [];

        /// <summary>Tablas donde la foto de los datos ajenos tiene que ver filas sembradas.</summary>
        internal List<string> SeededTables { get; } = [];

        internal Guid A(string kind) => Own[kind];
        internal Guid B(string kind) => _control ? Guid.NewGuid() : Foreign[kind];
        internal string BText(string kind) => _control ? "matriz-" + Guid.NewGuid().ToString("N")[..12] : ForeignText[kind];
        internal HttpClient Client(Caller caller) => Clients[caller];
        internal World Control() => new(this);

        internal Guid[] ForeignIdentifiers() =>
            [.. Foreign.Values, OrganizationB, PersonalB, OwnerB.UserId, OperatorB.UserId];

        /// <summary>
        /// La marca ajena que lleva una respuesta. Plataforma ve todas las organizaciones y cuentas: con ella sólo cuenta
        /// lo personal de otro operador.
        /// </summary>
        internal string? Leaks(string body, Caller caller)
        {
            if (body.Contains(OperatorMark, StringComparison.Ordinal)) return "datos de otro operador";
            if (caller == Caller.Platform) return null;
            if (body.Contains(OrganizationMark, StringComparison.Ordinal)) return "datos de otra organización";
            return body.Contains(PersonMark, StringComparison.Ordinal) ? "datos de otra persona" : null;
        }
    }
}
