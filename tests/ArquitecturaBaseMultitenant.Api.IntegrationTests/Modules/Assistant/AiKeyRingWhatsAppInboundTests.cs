using System.Collections.Concurrent;
using System.Reflection;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Inbound;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// Revisión adversarial de la B15 (spec: «no toma turnos ni entradas de WhatsApp»; 0064 §11: «no acepta nada por WhatsApp»).
/// La readiness saca de la red a una copia con otro anillo, pero la cola de entrada de WhatsApp la toma el worker de cualquier
/// copia, esté lista o no. Una copia cuyo anillo no coincide no puede descifrar lo que guardó el webhook de las demás (gasta
/// los intentos de la entrada hasta perderla) y lo que escribe lo cifra con su anillo, ilegible para las demás. Tiene que dejar la
/// entrada sin tocar, y la copia con el anillo de la instalación la procesa.
/// </summary>
public sealed class AiKeyRingWhatsAppInboundTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string Number = "keyring-fixture-number";

    [Fact]
    public async Task A_copy_whose_ring_does_not_match_takes_no_WhatsApp_entry()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        var consumed = new ConcurrentBag<Guid>();
        await using var good = Host(factory, consumed, otherRing: false);
        using (good.CreateClient()) { }
        await using (var scope = good.Services.CreateAsyncScope())
        {
            var checkedRing = await scope.ServiceProvider.GetRequiredService<IAiKeyRingControlService>().CheckAsync(Ct);
            Assert.True(checkedRing.IsSuccess);
            Assert.Empty(checkedRing.Value);
        }

        await using var other = Host(factory, consumed, otherRing: true);
        using (other.CreateClient()) { }
        Assert.False(await other.Services.GetRequiredService<AiKeyRingReadiness>().MatchesAsync(Ct));
        var id = await SeedAsync(good.Services);

        await RunIterationAsync(other.Services);

        Assert.DoesNotContain(id, consumed);
        await using (var read = good.Services.CreateAsyncScope())
        {
            var row = await read.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppMessage>().AsNoTracking()
                .SingleAsync(value => value.Id == id, Ct);
            Assert.Null(row.ProcessedAtUtc);
            Assert.Null(row.LeaseId);
            Assert.Equal(0, row.Attempts);
        }

        await RunIterationAsync(good.Services);
        Assert.Contains(id, consumed);
    }

    private static WebApplicationFactory<Program> Host(ApiFactory factory, ConcurrentBag<Guid> consumed, bool otherRing) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("WhatsApp:PhoneNumberId", Number);
            builder.UseSetting("WhatsApp:AccessToken", "keyring-fixture-access");
            if (otherRing)
                builder.ConfigureServices(services => services.AddDataProtection().SetApplicationName("OtraInstalacion"));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IWhatsAppInboundConsumer>();
                services.AddScoped<IWhatsAppInboundConsumer>(_ => new Consumer(consumed));
            });
        });

    /// <summary>La vuelta del worker, como la corre <c>PeriodicWorker</c>: donde vaya la guarda, la cubre.</summary>
    private static async Task RunIterationAsync(IServiceProvider services)
    {
        var processor = services.GetServices<IHostedService>().OfType<WhatsAppInboundProcessor>().Single();
        var iteration = typeof(WhatsAppInboundProcessor).GetMethod("RunIterationAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)iteration.Invoke(processor, [Ct])!;
    }

    private static async Task<Guid> SeedAsync(IServiceProvider services)
    {
        var id = Guid.Empty;
        await using var scope = services.CreateAsyncScope();
        var saved = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nowUtc = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            var candidate = WhatsAppContact.Create(PhoneNumber.Create("+5491134567891").Value, nowUtc);
            var contact = await context.Set<WhatsAppContact>().SingleOrDefaultAsync(value => value.WaId == candidate.WaId, ct);
            if (contact is null) { contact = candidate; context.Set<WhatsAppContact>().Add(contact); }
            var row = WhatsAppMessage.Create(Number, Guid.NewGuid().ToString("N"), "inbound", WhatsAppMessageDirection.Inbound,
                WhatsAppMessageKind.Text, WhatsAppMessageStatus.Pending, nowUtc, nowUtc, contact.Id);
            context.Set<WhatsAppMessage>().Add(row);
            id = row.Id;
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(saved.IsSuccess);
        return id;
    }

    private sealed class Consumer(ConcurrentBag<Guid> consumed) : IWhatsAppInboundConsumer
    {
        public Task ConsumeAsync(WhatsAppInboundContext context, CancellationToken ct)
        {
            consumed.Add(context.Message.Id);
            return Task.CompletedTask;
        }
    }
}
