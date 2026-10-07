using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>Ejercita el módulo sintético de persona a empresa en HTTP, EF, RLS y la UoW productivos.</summary>
public sealed class EngagementTests(EngagementFactory factory) : IClassFixture<EngagementFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter<DealStatus>() },
    };

    [Fact]
    public async Task A_person_requests_from_the_public_page_and_the_business_answers_from_its_inbox()
    {
        using var journey = await EngagementJourney.CreateAsync(factory);
        var id = await RequestAsync(journey);
        var requested = await GetAsync(journey.Client, id);
        Assert.Equal(DealStatus.Requested, requested.Status);
        await journey.BusinessAsync();
        var inbox = await ReadPageAsync<DealResponse>(journey.Client, "/test/deals");
        Assert.Equal(id, Assert.Single(inbox.Items).Id);
        using var confirmed = await TransitionAsync(journey.Client, id, "confirm", requested.Version);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        using var saved = await confirmed.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        var businessView = await GetAsync(journey.Client, id);
        Assert.Equal(saved!.RootElement.GetProperty("version").GetUInt32(), businessView.Version);
        await journey.ConsumerAsync();
        var personal = await GetAsync(journey.Client, id);
        Assert.Equal(DealStatus.Confirmed, personal.Status);
        var list = await ReadPageAsync<DealResponse>(journey.Client, "/test/deals");
        Assert.Equal(id, Assert.Single(list.Items).Id);
        Assert.Equal(journey.PersonalId, await ActiveTenantAsync(journey.Client));
    }

    [Fact]
    public async Task Each_transition_belongs_to_one_party()
    {
        using var journey = await EngagementJourney.CreateAsync(factory);
        var id = await RequestAsync(journey);
        var deal = await GetAsync(journey.Client, id);
        using var consumerConfirm = await TransitionAsync(journey.Client, id, "confirm", deal.Version);
        await AssertProblemAsync(consumerConfirm, HttpStatusCode.Forbidden, PartyErrors.NotAllowedCode);
        using var consumerReject = await TransitionAsync(journey.Client, id, "reject", deal.Version);
        await AssertProblemAsync(consumerReject, HttpStatusCode.Forbidden, PartyErrors.NotAllowedCode);
        await journey.BusinessAsync();
        using var businessCancel = await TransitionAsync(journey.Client, id, "cancel", deal.Version);
        await AssertProblemAsync(businessCancel, HttpStatusCode.Forbidden, PartyErrors.NotAllowedCode);
        Assert.Equal(DealStatus.Requested, (await GetAsync(journey.Client, id)).Status);
        await journey.ConsumerAsync();
        using var cancelled = await TransitionAsync(journey.Client, id, "cancel", deal.Version);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal(DealStatus.Cancelled, (await GetAsync(journey.Client, id)).Status);
    }

    [Fact]
    public async Task Only_the_two_parties_reach_a_deal()
    {
        using var journey = await EngagementJourney.CreateAsync(factory);
        var id = await RequestAsync(journey);
        var deal = await GetAsync(journey.Client, id);
        await journey.BusinessAsync(journey.BusinessB);
        using var businessGet = await journey.Client.GetAsync($"/test/deals/{id}", Ct);
        await AssertProblemAsync(businessGet, HttpStatusCode.NotFound, "Http.NotFound");
        using var businessChange = await TransitionAsync(journey.Client, id, "confirm", deal.Version);
        await AssertProblemAsync(businessChange, HttpStatusCode.NotFound, "Http.NotFound");
        Assert.Empty((await ReadPageAsync<DealResponse>(journey.Client, "/test/deals")).Items);
        using var carla = factory.Client();
        await AccountJourney.RegisterAsync(factory.Host, carla, Ct);
        await journey.SetModuleAsync(await ActiveTenantAsync(carla), true);
        using var consumerGet = await carla.GetAsync($"/test/deals/{id}", Ct);
        await AssertProblemAsync(consumerGet, HttpStatusCode.NotFound, "Http.NotFound");
        using var consumerChange = await TransitionAsync(carla, id, "cancel", deal.Version);
        await AssertProblemAsync(consumerChange, HttpStatusCode.NotFound, "Http.NotFound");
        Assert.Empty((await ReadPageAsync<DealResponse>(carla, "/test/deals")).Items);
    }

    [Fact]
    public async Task Personal_lists_requests_across_businesses_while_each_inbox_keeps_its_own()
    {
        using var journey = await EngagementJourney.CreateAsync(factory);
        var idA = await RequestAsync(journey);
        var idB = await RequestAsync(journey, journey.BusinessB);
        var personal = await ReadPageAsync<DealResponse>(journey.Client, "/test/deals");
        Assert.Equal(2, personal.TotalCount);
        Assert.Equal(new[] { idA, idB }.Order(), personal.Items.Select(row => row.Id).Order());
        var outsidePage = await ReadPageAsync<DealResponse>(journey.Client, "/test/deals?page=2&pageSize=10");
        Assert.Empty(outsidePage.Items);
        Assert.Equal(2, outsidePage.TotalCount);
        await journey.BusinessAsync();
        Assert.Equal(idA, Assert.Single((await ReadPageAsync<DealResponse>(journey.Client, "/test/deals")).Items).Id);
        await journey.BusinessAsync(journey.BusinessB);
        Assert.Equal(idB, Assert.Single((await ReadPageAsync<DealResponse>(journey.Client, "/test/deals")).Items).Id);
    }

    [Fact]
    public async Task Business_sees_only_what_was_shared()
    {
        using var journey = await EngagementJourney.CreateAsync(factory);
        var id = await RequestAsync(journey);
        await journey.BusinessAsync();
        using var response = await journey.Client.GetAsync("/test/deals", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        var row = Assert.Single(body!.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(id, row.GetProperty("id").GetGuid());
        Assert.Equal(EngagementJourney.CopiedName, row.GetProperty("consumerName").GetString());
        Assert.Equal(["consumerName", "id", "status", "summary", "version"],
            row.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        var json = body.RootElement.GetRawText();
        Assert.DoesNotContain(journey.Account.Email.Value, json, StringComparison.Ordinal);
        Assert.DoesNotContain(journey.Account.UserId.ToString("D"), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(journey.PersonalId.ToString("D"), json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_request_needs_a_published_subdomain()
    {
        using var journey = await EngagementJourney.CreateAsync(factory);
        var draft = await EngagementJourney.CreateSiteAsync(factory, journey.Account.UserId, published: false);
        var suspended = await EngagementJourney.CreateSiteAsync(factory, journey.Account.UserId, suspended: true);
        foreach (var url in new[] { "/test/deals", EngagementJourney.PublicUrl(draft), EngagementJourney.PublicUrl(suspended) })
        {
            using var unavailable = await AccountJourney.PostAsync(journey.Client, url, new { summary = "Pedido" }, Ct);
            Assert.Equal(HttpStatusCode.NotFound, unavailable.StatusCode);
        }
        using var anonymous = factory.Client();
        using var unauthenticated = await AccountJourney.PostAsync(anonymous,
            EngagementJourney.PublicUrl(journey.BusinessA), new { summary = "Pedido" }, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        await journey.BusinessAsync();
        using var wrongAccess = await AccountJourney.PostAsync(journey.Client,
            EngagementJourney.PublicUrl(journey.BusinessA), new { summary = "Pedido" }, Ct);
        await AssertProblemAsync(wrongAccess, HttpStatusCode.Forbidden, AccessErrors.WrongCode);
    }

    [Fact]
    public async Task A_request_needs_the_module_on_in_that_organization()
    {
        using var journey = await EngagementJourney.CreateAsync(factory);
        await journey.SetModuleAsync(journey.BusinessA.Id, false);
        using var disabled = await AccountJourney.PostAsync(journey.Client,
            EngagementJourney.PublicUrl(journey.BusinessA), new { summary = "Pedido" }, Ct);
        await AssertProblemAsync(disabled, HttpStatusCode.NotFound, "Http.NotFound");
        await journey.SetModuleAsync(journey.PersonalId, false);
        var id = await RequestAsync(journey, journey.BusinessB);
        Assert.Equal(journey.PersonalId, await ActiveTenantAsync(journey.Client));
        await journey.BusinessAsync(journey.BusinessB);
        Assert.Equal(id, (await GetAsync(journey.Client, id)).Id);
        using var disabledInbox = await journey.Client.GetAsync(
            EngagementJourney.PublicUrl(journey.BusinessA, "/test/deals"), Ct);
        // La ruta privada no declara PublicSite: incluso con el host A sigue consultando la bandeja B firmada.
        Assert.Equal(HttpStatusCode.OK, disabledInbox.StatusCode);
    }

    [Fact]
    public async Task Repeating_a_request_with_the_same_key_creates_one_deal()
    {
        using var journey = await EngagementJourney.CreateAsync(factory);
        var key = Guid.NewGuid();
        using var first = await SendRequestAsync(journey, journey.BusinessA, key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var id = await first.Content.ReadFromJsonAsync<Guid>(Ct);
        using var repeated = await SendRequestAsync(journey, journey.BusinessA, key);
        Assert.Equal(HttpStatusCode.Created, repeated.StatusCode);
        Assert.Equal(id, await repeated.Content.ReadFromJsonAsync<Guid>(Ct));
        Assert.Equal("true", Assert.Single(repeated.Headers.GetValues("Idempotent-Replayed")));
        Assert.Equal(id, Assert.Single((await ReadPageAsync<DealResponse>(journey.Client, "/test/deals")).Items).Id);
    }

    [Fact]
    public async Task The_same_key_and_body_cannot_replay_a_request_from_another_public_site()
    {
        using var journey = await EngagementJourney.CreateAsync(factory);
        var key = Guid.NewGuid();
        using var first = await SendRequestAsync(journey, journey.BusinessA, key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var id = await first.Content.ReadFromJsonAsync<Guid>(Ct);
        using var otherSite = await SendRequestAsync(journey, journey.BusinessB, key);
        await AssertProblemAsync(otherSite, HttpStatusCode.UnprocessableEntity, "Request.IdempotencyKeyReused");
        Assert.False(otherSite.Headers.Contains("Idempotent-Replayed"));
        await journey.BusinessAsync(journey.BusinessB);
        Assert.Empty((await ReadPageAsync<DealResponse>(journey.Client, "/test/deals")).Items);
        await journey.ConsumerAsync();
        Assert.Equal(id, Assert.Single((await ReadPageAsync<DealResponse>(journey.Client, "/test/deals")).Items).Id);
    }

    [Fact]
    public async Task Disabling_the_module_blocks_even_a_completed_idempotent_replay()
    {
        using var journey = await EngagementJourney.CreateAsync(factory);
        var key = Guid.NewGuid();
        using var first = await SendRequestAsync(journey, journey.BusinessA, key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        await journey.SetModuleAsync(journey.BusinessA.Id, false);
        using var disabled = await SendRequestAsync(journey, journey.BusinessA, key);
        await AssertProblemAsync(disabled, HttpStatusCode.NotFound, "Http.NotFound");
        Assert.False(disabled.Headers.Contains("Idempotent-Replayed"));
    }

    [Fact]
    public async Task Unpublishing_the_site_blocks_even_a_completed_idempotent_replay()
    {
        using var journey = await EngagementJourney.CreateAsync(factory);
        var key = Guid.NewGuid();
        using var first = await SendRequestAsync(journey, journey.BusinessA, key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        await journey.UnpublishAsync(journey.BusinessA);
        using var unpublished = await SendRequestAsync(journey, journey.BusinessA, key);
        await AssertProblemAsync(unpublished, HttpStatusCode.NotFound, "PublicSite.PublicPage.NotFound");
        Assert.False(unpublished.Headers.Contains("Idempotent-Replayed"));
    }

    [Fact]
    public async Task The_emergency_configuration_blocks_a_public_request_despite_the_business_grant()
    {
        using var journey = await EngagementJourney.CreateAsync(factory);
        await using var emergencyHost = factory.Host.WithWebHostBuilder(builder =>
            builder.UseSetting("FeatureManagement:test-deals", "false"));
        using var client = emergencyHost.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await AccountJourney.RegisterAsync(emergencyHost, client, Ct);
        using var disabled = await AccountJourney.PostAsync(client, EngagementJourney.PublicUrl(journey.BusinessA),
            new { summary = "Pedido" }, Ct);
        await AssertProblemAsync(disabled, HttpStatusCode.NotFound, "Http.NotFound");
    }

    [Fact]
    public async Task Expired_trials_are_hidden_in_the_public_business_even_when_personal_is_enabled()
    {
        using var journey = await EngagementJourney.CreateAsync(factory);
        await journey.SetModuleAsync(journey.BusinessA.Id, true,
            factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddMinutes(-1));
        using var expired = await SendRequestAsync(journey, journey.BusinessA, Guid.NewGuid());
        await AssertProblemAsync(expired, HttpStatusCode.NotFound, "Http.NotFound");
        Assert.Equal(journey.PersonalId, await ActiveTenantAsync(journey.Client));
    }

    [Fact]
    public async Task A_stale_version_does_not_change_the_state()
    {
        using var journey = await EngagementJourney.CreateAsync(factory);
        var id = await RequestAsync(journey);
        var original = await GetAsync(journey.Client, id);
        Assert.NotEqual(0u, original.Version);
        await journey.BusinessAsync();
        using var stale = await TransitionAsync(journey.Client, id, "confirm", 0);
        await AssertProblemAsync(stale, HttpStatusCode.Conflict, "General.ConcurrencyConflict");
        Assert.Equal(DealStatus.Requested, (await GetAsync(journey.Client, id)).Status);
        using var changed = await TransitionAsync(journey.Client, id, "reject", original.Version);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(DealStatus.Rejected, (await GetAsync(journey.Client, id)).Status);
        using var terminal = await TransitionAsync(journey.Client, id, "confirm", original.Version);
        Assert.Equal(HttpStatusCode.Conflict, terminal.StatusCode);
        Assert.Equal(DealStatus.Rejected, (await GetAsync(journey.Client, id)).Status);
    }

    [Fact]
    public async Task Each_change_is_audited_for_both_parties_without_the_copied_name()
    {
        using var journey = await EngagementJourney.CreateAsync(factory);
        var id = await RequestAsync(journey);
        var original = await GetAsync(journey.Client, id);
        await journey.BusinessAsync();
        using var confirmed = await TransitionAsync(journey.Client, id, "confirm", original.Version);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        var consumerEntries = await journey.AuditAsync(journey.PersonalId, id);
        var businessEntries = await journey.AuditAsync(journey.BusinessA.Id, id);
        foreach (var entries in new[] { consumerEntries, businessEntries })
        {
            Assert.Equal(2, entries.Count);
            Assert.Contains(entries, row => row.Action == AuditAction.Created);
            Assert.Contains(entries, row => row.Action == AuditAction.Updated);
            foreach (var entry in entries)
            {
                Assert.DoesNotContain(nameof(Deal.ConsumerName), entry.Changes, StringComparison.Ordinal);
                Assert.DoesNotContain(EngagementJourney.CopiedName, entry.Changes, StringComparison.Ordinal);
                Assert.Equal(journey.Account.UserId, entry.ActorId);
            }
        }
        Assert.Equal(consumerEntries.Select(row => row.Changes), businessEntries.Select(row => row.Changes));
    }

    [Fact]
    public async Task Published_posters_reach_the_public_page_and_drafts_do_not()
    {
        using var journey = await EngagementJourney.CreateAsync(factory);
        var published = await journey.AddPosterAsync(journey.BusinessA, true);
        var draft = await journey.AddPosterAsync(journey.BusinessA, false);
        var other = await journey.AddPosterAsync(journey.BusinessB, true);
        using var visitor = factory.Client();
        var listed = await ReadPageAsync<PosterResponse>(visitor,
            EngagementJourney.PublicUrl(journey.BusinessA, "/test/posters"));
        Assert.Equal(published.Id, Assert.Single(listed.Items).Id);
        foreach (var id in new[] { draft.Id, other.Id })
        {
            using var hidden = await visitor.GetAsync(
                EngagementJourney.PublicUrl(journey.BusinessA, $"/test/posters/{id}"), Ct);
            await AssertProblemAsync(hidden, HttpStatusCode.NotFound, "Http.NotFound");
        }
        await journey.BusinessAsync();
        using var ownDraft = await journey.Client.GetAsync(
            EngagementJourney.PublicUrl(journey.BusinessA, $"/test/posters/{draft.Id}"), Ct);
        Assert.Equal(HttpStatusCode.NotFound, ownDraft.StatusCode);
    }

    [Fact]
    public void Each_test_module_route_has_an_explicit_inventory()
    {
        string[] expected = ["GET /test/deals", "GET /test/deals/{id:guid}", "POST /test/deals",
            "POST /test/deals/{id:guid}/cancel", "POST /test/deals/{id:guid}/confirm",
            "POST /test/deals/{id:guid}/reject", "GET /test/posters", "GET /test/posters/{id:guid}"];
        var actual = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() is { } action
                && (action.ControllerTypeInfo.AsType() == typeof(DealsController)
                    || action.ControllerTypeInfo.AsType() == typeof(PostersController)))
            .SelectMany(endpoint => endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods
                .Select(method => $"{method} /{endpoint.RoutePattern.RawText!.Trim('/')}")).Order(StringComparer.Ordinal);
        Assert.Equal(expected.Order(StringComparer.Ordinal), actual);
    }

    private static async Task<Guid> RequestAsync(EngagementJourney journey, Tenant? business = null)
    {
        using var response = await SendRequestAsync(journey, business ?? journey.BusinessA, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        return await response.Content.ReadFromJsonAsync<Guid>(Ct);
    }

    private static async Task<HttpResponseMessage> SendRequestAsync(EngagementJourney journey, Tenant business, Guid key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, EngagementJourney.PublicUrl(business))
        {
            // Los ids y el nombre enviados de contrabando no existen en el contrato; el contexto y la cuenta mandan.
            Content = JsonContent.Create(new { summary = "Lo que se pidió", businessTenantId = journey.BusinessB.Id,
                consumerTenantId = Guid.Empty, consumerName = "Nombre falsificado" }),
        };
        request.Headers.Add("Idempotency-Key", key.ToString("D"));
        return await journey.Client.SendAsync(request, Ct);
    }

    private static async Task<DealResponse> GetAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<DealResponse>($"/test/deals/{id}", ResponseJsonOptions, Ct))!;

    private static async Task<PagedResult<T>> ReadPageAsync<T>(HttpClient client, string path) =>
        (await client.GetFromJsonAsync<PagedResult<T>>(path, ResponseJsonOptions, Ct))!;

    private static Task<HttpResponseMessage> TransitionAsync(HttpClient client, Guid id, string action, uint version) =>
        AccountJourney.PostAsync(client, $"/test/deals/{id}/{action}", new { version }, Ct);

    private static async Task<Guid> ActiveTenantAsync(HttpClient client)
    {
        using var me = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        return me!.RootElement.GetProperty("activeTenantId").GetGuid();
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.ToString(), StringComparison.OrdinalIgnoreCase);
        using var problem = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(code, problem!.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
    }
}
