using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Inbound;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

public sealed class WhatsAppInboundProcessorTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
    private readonly Probe _probe = new();
    private WebApplicationFactory<Program> Host(bool consumer = true) => factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("WhatsApp:PhoneNumberId", "fixture-number");
        builder.UseSetting("WhatsApp:AccessToken", "fixture-access");
        builder.UseSetting("WhatsApp:ProcessInboundInBackground", "false");
        builder.UseSetting("WhatsApp:InboundMaxAttempts", "2");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(_clock);
            services.RemoveAll<IWhatsAppInboundConsumer>();
            services.RemoveAll<IExternalLoginLock>();
            services.AddScoped<IExternalLoginLock>(provider => new ObservedPhoneLock(new ExternalLoginLock(provider.GetRequiredService<ApplicationDbContext>()), _probe));
            if (consumer) services.AddScoped<IWhatsAppInboundConsumer>(provider => new Consumer(provider.GetRequiredService<ApplicationDbContext>(), _probe, _clock));
        });
    });

    [Fact]
    public async Task Missing_consumer_never_claims_or_consumes_pending_input()
    {
        await using var app = Host(false); using var client = app.CreateClient();
        await ResetQueueAsync(app.Services);
        var id = await SeedAsync(app.Services);
        await using var scope = app.Services.CreateAsyncScope();
        Assert.Null((await Service(scope.ServiceProvider).ClaimAsync(Ct)).Value);
        var row = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppMessage>().AsNoTracking().SingleAsync(value => value.Id == id, Ct);
        Assert.Equal(0, row.Attempts); Assert.Null(row.LeaseId); Assert.Null(row.ProcessedAtUtc);
    }

    [Fact]
    public async Task Lease_expired_while_waiting_for_identity_lock_never_invokes_consumer()
    {
        await using var app = Host(); using var client = app.CreateClient();
        await ResetQueueAsync(app.Services);
        var id = await SeedAsync(app.Services);
        await using var claiming = app.Services.CreateAsyncScope();
        var claim = (await Service(claiming.ServiceProvider).ClaimAsync(Ct)).Value!;
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holding = TransactionAsync(app.Services, async (provider, ct) =>
        {
            await provider.GetRequiredService<IExternalLoginLock>().LockPhoneAsync(PhoneNumber.Create("+5491134567890").Value, ct);
            locked.SetResult(); await release.Task.WaitAsync(ct);
        });
        await locked.Task.WaitAsync(Ct);
        _probe.WaitingPhone = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var processing = app.Services.CreateAsyncScope();
        var work = Service(processing.ServiceProvider).ProcessAsync(claim, Ct);
        await _probe.WaitingPhone.Task.WaitAsync(Ct);
        Assert.False(work.IsCompleted);
        _clock.Advance(TimeSpan.FromMinutes(3)); release.SetResult();
        await holding; Assert.True((await work).IsSuccess);
        Assert.Empty(_probe.Contexts);
        await using var read = app.Services.CreateAsyncScope();
        var context = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Null((await context.Set<WhatsAppMessage>().AsNoTracking().SingleAsync(value => value.Id == id, Ct)).ProcessedAtUtc);
        Assert.Equal(0, await context.OutboxMessages.CountAsync(value => value.Channel == _probe.Channel, Ct));
    }

    [Fact]
    public async Task Lease_expired_during_consumer_rolls_back_effect_and_waits_for_backoff()
    {
        await using var app = Host(); using var client = app.CreateClient();
        await ResetQueueAsync(app.Services);
        var id = await SeedAsync(app.Services); _probe.ExpireDuringConsumer = id;
        Assert.Equal(0, await Worker(app.Services).ProcessPendingAsync(Ct));
        await using var read = app.Services.CreateAsyncScope();
        var context = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await context.Set<WhatsAppMessage>().AsNoTracking().SingleAsync(value => value.Id == id, Ct);
        Assert.Null(row.ProcessedAtUtc); Assert.Null(row.LeaseId); Assert.Equal(1, row.Attempts);
        Assert.True(row.NextAttemptAtUtc > _clock.GetUtcNow().UtcDateTime);
        Assert.Equal(0, await context.OutboxMessages.CountAsync(value => value.Channel == _probe.Channel, Ct));
        Assert.Null((await Service(read.ServiceProvider).ClaimAsync(Ct)).Value);
    }

    [Fact]
    public async Task Concurrent_claims_restart_expiry_and_stale_workers_keep_one_atomic_effect()
    {
        await using var app = Host(); using var client = app.CreateClient();
        await ResetQueueAsync(app.Services);
        var firstId = await SeedAsync(app.Services); var secondId = await SeedAsync(app.Services);
        await using var first = app.Services.CreateAsyncScope(); await using var second = app.Services.CreateAsyncScope();
        var claims = await Task.WhenAll(Service(first.ServiceProvider).ClaimAsync(Ct), Service(second.ServiceProvider).ClaimAsync(Ct));
        Assert.NotNull(claims[0].Value); Assert.NotNull(claims[1].Value);
        Assert.NotEqual(claims[0].Value!.MessageId, claims[1].Value!.MessageId);
        await Service(second.ServiceProvider).ProcessAsync(claims[1].Value!, Ct);
        await using var restart = app.Services.CreateAsyncScope();
        Assert.Null((await Service(restart.ServiceProvider).ClaimAsync(Ct)).Value);
        _clock.Advance(TimeSpan.FromMinutes(3));
        var recovered = (await Service(restart.ServiceProvider).ClaimAsync(Ct)).Value!;
        Assert.Equal(claims[0].Value!.MessageId, recovered.MessageId);
        Assert.NotEqual(claims[0].Value!.LeaseId, recovered.LeaseId);
        await Service(first.ServiceProvider).ProcessAsync(claims[0].Value!, Ct);
        await Service(restart.ServiceProvider).ProcessAsync(recovered, Ct);
        await Service(first.ServiceProvider).ProcessAsync(claims[0].Value!, Ct);
        await using var read = app.Services.CreateAsyncScope();
        var context = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rows = await context.Set<WhatsAppMessage>().AsNoTracking().Where(value => value.Id == firstId || value.Id == secondId).ToArrayAsync(Ct);
        Assert.All(rows, value => Assert.NotNull(value.ProcessedAtUtc));
        Assert.Equal(2, await context.OutboxMessages.CountAsync(value => value.Channel == _probe.Channel, Ct));
    }

    [Fact]
    public async Task Poison_rolls_back_effects_has_backoff_and_exhaustion_does_not_block_next_message()
    {
        await using var app = Host(); using var client = app.CreateClient();
        await ResetQueueAsync(app.Services);
        var poison = await SeedAsync(app.Services); _probe.Poison = poison;
        var healthy = await SeedAsync(app.Services);
        var worker = Worker(app.Services);
        Assert.Equal(1, await worker.ProcessPendingAsync(Ct));
        await using (var read = app.Services.CreateAsyncScope())
        {
            var context = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var failed = await context.Set<WhatsAppMessage>().AsNoTracking().SingleAsync(value => value.Id == poison, Ct);
            Assert.Equal(1, failed.Attempts); Assert.Equal(WhatsAppMessageStatus.Failed, failed.Status); Assert.NotNull(failed.NextAttemptAtUtc);
            Assert.NotNull((await context.Set<WhatsAppMessage>().AsNoTracking().SingleAsync(value => value.Id == healthy, Ct)).ProcessedAtUtc);
            Assert.Equal(1, await context.OutboxMessages.CountAsync(value => value.Channel == _probe.Channel, Ct));
        }
        _clock.Advance(TimeSpan.FromSeconds(7));
        Assert.Equal(0, await worker.ProcessPendingAsync(Ct));
        var next = await SeedAsync(app.Services);
        Assert.Equal(1, await Worker(app.Services).ProcessPendingAsync(Ct));
        await using var final = app.Services.CreateAsyncScope();
        var finalContext = final.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var exhausted = await finalContext.Set<WhatsAppMessage>().AsNoTracking().SingleAsync(value => value.Id == poison, Ct);
        Assert.Equal(2, exhausted.Attempts); Assert.Null(exhausted.NextAttemptAtUtc);
        Assert.NotNull((await finalContext.Set<WhatsAppMessage>().AsNoTracking().SingleAsync(value => value.Id == next, Ct)).ProcessedAtUtc);
        Assert.Equal(2, await finalContext.OutboxMessages.CountAsync(value => value.Channel == _probe.Channel, Ct));
    }

    [Fact]
    public async Task Reassigned_contact_never_transfers_original_or_anonymous_history()
    {
        await using var app = Host(); using var client = app.CreateClient();
        await ResetQueueAsync(app.Services);
        Guid original = Guid.Empty; Guid methodId = Guid.Empty; Guid current = Guid.Empty;
        await TransactionAsync(app.Services, async (provider, ct) =>
        {
            var users = provider.GetRequiredService<IUserRepository>();
            original = (await users.CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
            current = (await users.CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
            var method = LoginMethod.CreatePhone(original, PhoneNumber.Create("+5491134567890").Value);
            method.Verify(_clock.GetUtcNow().UtcDateTime); methodId = method.Id;
            provider.GetRequiredService<ILoginMethodRepository>().Add(method);
        });
        var owned = await SeedAsync(app.Services, original, methodId);
        var anonymous = await SeedAsync(app.Services);
        await TransactionAsync(app.Services, async (provider, ct) =>
        {
            var context = provider.GetRequiredService<ApplicationDbContext>();
            context.LoginMethods.Remove(await context.LoginMethods.SingleAsync(value => value.Id == methodId, ct));
            var nextMethod = LoginMethod.CreatePhone(current, PhoneNumber.Create("+5491134567890").Value);
            nextMethod.Verify(_clock.GetUtcNow().UtcDateTime); context.LoginMethods.Add(nextMethod);
            foreach (var contact in await context.Set<WhatsAppContact>().ToArrayAsync(ct)) contact.Link(nextMethod.Id, current);
        });
        await Worker(app.Services).ProcessPendingAsync(Ct);
        var prior = Assert.Single(_probe.Contexts, value => value.Message.Id == owned);
        Assert.Equal(original, prior.Message.OriginalUserId);
        Assert.Equal(original, prior.OriginalAccount?.Id);
        Assert.Null(prior.OriginalMethod); Assert.False(prior.OriginalMethodAvailable);
        var unknown = Assert.Single(_probe.Contexts, value => value.Message.Id == anonymous);
        Assert.Null(unknown.Message.OriginalUserId); Assert.Null(unknown.OriginalAccount); Assert.False(unknown.OriginalMethodAvailable);
        Assert.Equal(current, unknown.Contact?.UserId);
        await using var read = app.Services.CreateAsyncScope();
        var entity = read.ServiceProvider.GetRequiredService<ApplicationDbContext>().Model.FindEntityType(typeof(WhatsAppMessage))!;
        Assert.Equal(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Throw, entity.FindProperty(nameof(WhatsAppMessage.OriginalUserId))!.GetAfterSaveBehavior());
        Assert.DoesNotContain(entity.GetForeignKeys(), key => key.Properties.Any(property => property.Name == nameof(WhatsAppMessage.OriginalMethodId)));
    }

    private WhatsAppInboundProcessor Worker(IServiceProvider provider) => new(provider.GetRequiredService<WhatsAppInboundSignal>(),
        provider.GetRequiredService<IServiceScopeFactory>(), provider.GetRequiredService<ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs.TenantJobRunner>(),
        Options.Create(new WhatsAppOptions { PhoneNumberId = "fixture-number", InboundMaxAttempts = 2 }), _clock, NullLogger<WhatsAppInboundProcessor>.Instance);
    private static IWhatsAppInboundService Service(IServiceProvider provider) => provider.GetRequiredService<IWhatsAppInboundService>();
    private static Task ResetQueueAsync(IServiceProvider services) => TransactionAsync(services, async (provider, ct) =>
    {
        var context = provider.GetRequiredService<ApplicationDbContext>();
        // Cada caso de esta clase comparte el contenedor, pero conserva su propia cola y efectos.
        context.Set<WhatsAppMessage>().RemoveRange(await context.Set<WhatsAppMessage>().Where(value => value.PhoneNumberId == "fixture-number").ToArrayAsync(ct));
    });
    private async Task<Guid> SeedAsync(IServiceProvider provider, Guid? owner = null, Guid? method = null)
    {
        var id = Guid.Empty;
        await TransactionAsync(provider, (scope, _) =>
        {
            var context = scope.GetRequiredService<ApplicationDbContext>();
            var contact = WhatsAppContact.Create(PhoneNumber.Create("+5491134567890").Value, _clock.GetUtcNow().UtcDateTime);
            // Todos usan el único contacto de este número: creado en una transacción previa si hace falta.
            return SeedCoreAsync(context, contact, owner, method);
        });
        return id;
        async Task SeedCoreAsync(ApplicationDbContext context, WhatsAppContact candidate, Guid? original, Guid? originalMethod)
        {
            var contact = await context.Set<WhatsAppContact>().SingleOrDefaultAsync(value => value.WaId == candidate.WaId, Ct);
            if (contact is null) { contact = candidate; context.Set<WhatsAppContact>().Add(contact); }
            var row = WhatsAppMessage.Create("fixture-number", Guid.NewGuid().ToString("N"), "inbound", WhatsAppMessageDirection.Inbound,
                WhatsAppMessageKind.Text, WhatsAppMessageStatus.Pending, _clock.GetUtcNow().UtcDateTime, _clock.GetUtcNow().UtcDateTime,
                contact.Id, originalUserId: original, originalMethodId: originalMethod);
            context.Set<WhatsAppMessage>().Add(row); id = row.Id;
        }
    }
    private static async Task TransactionAsync(IServiceProvider services, Func<IServiceProvider, CancellationToken, Task> work)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        { await work(scope.ServiceProvider, ct); return Result.Success(); }, CommitPolicy.OnSuccess, Ct);
    }
    private sealed class Probe
    {
        internal string Channel { get; } = "inbound-proof-" + Guid.NewGuid().ToString("N");
        internal Guid? Poison { get; set; }
        internal Guid? ExpireDuringConsumer { get; set; }
        internal TaskCompletionSource? WaitingPhone { get; set; }
        internal List<WhatsAppInboundContext> Contexts { get; } = [];
    }
    private sealed class Consumer(ApplicationDbContext context, Probe probe, TimeProvider clock) : IWhatsAppInboundConsumer
    {
        public Task ConsumeAsync(WhatsAppInboundContext value, CancellationToken ct)
        {
            context.OutboxMessages.Add(OutboxMessage.Enqueue(probe.Channel, "encrypted-test-effect", clock.GetUtcNow().UtcDateTime));
            if (value.Message.Id == probe.Poison) throw new InvalidOperationException("Fixture poison.");
            if (value.Message.Id == probe.ExpireDuringConsumer) ((FakeTimeProvider)clock).Advance(TimeSpan.FromMinutes(3));
            probe.Contexts.Add(value); return Task.CompletedTask;
        }
    }
    private sealed class ObservedPhoneLock(IExternalLoginLock inner, Probe probe) : IExternalLoginLock
    {
        public Task LockPhoneAsync(PhoneNumber phone, CancellationToken cancellationToken)
        { probe.WaitingPhone?.TrySetResult(); return inner.LockPhoneAsync(phone, cancellationToken); }
        public Task LockEmailAsync(Email email, CancellationToken cancellationToken) => inner.LockEmailAsync(email, cancellationToken);
        public Task AcquireAsync(string providerSubject, Email? email, CancellationToken cancellationToken) => inner.AcquireAsync(providerSubject, email, cancellationToken);
    }
}
