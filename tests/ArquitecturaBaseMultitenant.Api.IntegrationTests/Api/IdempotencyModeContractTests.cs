using System.Text;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Api;

public sealed class IdempotencyModeContractTests
{
    [Theory]
    [InlineData(IdempotencyMode.DurableReference)]
    [InlineData(IdempotencyMode.DurableStream)]
    public async Task Private_protocol_stubs_delegate_without_reading_or_capturing_in_the_legacy_store(IdempotencyMode mode)
    {
        var store = new Store();
        var (context, stream) = Context(mode, key: true);
        await using (stream)
        {
            var response = context.HttpContext.Response.Body;
            var called = false;
            await new IdempotencyFilter(store, new User(), new Tenant(), new Site()).OnResourceExecutionAsync(context, () =>
            {
                called = true;
                Assert.Same(response, context.HttpContext.Response.Body);
                context.HttpContext.Response.StatusCode = 501;
                return Task.FromResult(new ResourceExecutedContext(context, []));
            });
            Assert.True(called);
            Assert.Equal(0, stream.Reads);
            Assert.Equal(0, store.Reserves);
            Assert.Equal(0, store.Completions);
            Assert.Equal(0, store.Releases);
        }
    }
    [Fact]
    public async Task Buffered_json_keeps_its_existing_hash_reservation_and_response_capture()
    {
        var store = new Store();
        var (context, stream) = Context(IdempotencyMode.BufferedJson, key: true);
        await using (stream)
        {
            await new IdempotencyFilter(store, new User(), new Tenant(), new Site()).OnResourceExecutionAsync(context, async () =>
            {
                context.HttpContext.Response.StatusCode = 201;
                await context.HttpContext.Response.WriteAsync("{}", TestContext.Current.CancellationToken);
                return new ResourceExecutedContext(context, []);
            });
            Assert.True(stream.Reads > 0);
            Assert.Equal(1, store.Reserves);
            Assert.Equal(1, store.Completions);
            Assert.Equal("{}", store.Body);
        }
    }
    [Theory]
    [InlineData(IdempotencyMode.BufferedJson)]
    [InlineData(IdempotencyMode.DurableReference)]
    [InlineData(IdempotencyMode.DurableStream)]
    public async Task Every_mode_rejects_a_missing_key_before_delegating_or_reading(IdempotencyMode mode)
    {
        var store = new Store();
        var (context, stream) = Context(mode, key: false);
        await using (stream)
        {
            await new IdempotencyFilter(store, new User(), new Tenant(), new Site()).OnResourceExecutionAsync(context,
                () => throw new InvalidOperationException("Must not delegate without a key."));
            Assert.Equal(400, Assert.IsType<ObjectResult>(context.Result).StatusCode);
            Assert.Equal(0, stream.Reads);
            Assert.Equal(0, store.Reserves);
        }
    }
    private static (ResourceExecutingContext, ReadProbe) Context(IdempotencyMode mode, bool key)
    {
        var http = new DefaultHttpContext();
        var stream = new ReadProbe(Encoding.UTF8.GetBytes("{\"text\":\"private\"}"));
        http.Request.Body = stream;
        http.Response.Body = new MemoryStream();
        if (key) http.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor { EndpointMetadata = [new IdempotentAttribute { Mode = mode }] }, new ModelStateDictionary());
        return (new ResourceExecutingContext(action, [], []), stream);
    }
    private sealed class ReadProbe(byte[] bytes) : MemoryStream(bytes)
    {
        public int Reads { get; private set; }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { Reads++; return base.ReadAsync(buffer, cancellationToken); }
    }
    private sealed class Store : IIdempotencyStore
    {
        public int Reserves { get; private set; }
        public int Completions { get; private set; }
        public int Releases { get; private set; }
        public string? Body { get; private set; }
        public Task<IdempotencyReservation> ReserveAsync(Guid? tenantId, Guid userId, Guid key, string bodyHash, string route, CancellationToken cancellationToken)
        { Reserves++; return Task.FromResult(new IdempotencyReservation(Guid.CreateVersion7(), IdempotencyReservationState.Reserved)); }
        public Task CompleteAsync(Guid id, int statusCode, string responseBody, CancellationToken cancellationToken)
        { Completions++; Body = responseBody; return Task.CompletedTask; }
        public Task ReleaseAsync(Guid id, CancellationToken cancellationToken) { Releases++; return Task.CompletedTask; }
        public Task<int> CleanupExpiredAsync(CancellationToken cancellationToken) => Task.FromResult(0);
    }
    private sealed class User : ICurrentUser { public Guid? UserId => Guid.Empty; public Access? Access => Domain.Users.Access.Consumer; }
    private sealed class Tenant : ITenantContext { public Guid? TenantId => Guid.Empty; public TenantKind? TenantKind => Domain.Tenancy.TenantKind.Personal; public Guid RequiredTenantId => Guid.Empty; }
    private sealed class Site : IPublicSiteContext { public Guid? BusinessTenantId => null; public string? Slug => null; }
}
