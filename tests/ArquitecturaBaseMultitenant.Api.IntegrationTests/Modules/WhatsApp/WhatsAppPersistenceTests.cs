using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ArquitecturaBaseMultitenant.TestSupport;
using Microsoft.Extensions.DependencyInjection;
namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;
public sealed class WhatsAppPersistenceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public void Runtime_design_time_and_bootstrap_have_the_same_model_even_when_disabled()
    {
        using var scope = factory.Services.CreateScope();
        var runtime = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        using var design = new DesignTimeApplicationDbContextFactory().CreateDbContext([]);
        using var bootstrap = Context();
        PersistenceModelComparison.AssertEqual(runtime, design);
        PersistenceModelComparison.AssertEqual(runtime, bootstrap);
        Assert.Equal("identity", runtime.Model.FindEntityType(typeof(WhatsAppContact))!.GetSchema());
        Assert.Equal("identity", runtime.Model.FindEntityType(typeof(WhatsAppMessage))!.GetSchema());
    }
    [Fact]
    public void Without_contributors_the_core_model_contains_no_module_entities()
    {
        using var context = Context([]);
        Assert.Null(context.Model.FindEntityType(typeof(WhatsAppContact)));
        Assert.Null(context.Model.FindEntityType(typeof(WhatsAppMessage)));
    }
    [Fact]
    public async Task Messages_survive_context_restart_and_delivery_events_have_independent_keys()
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var wamid = "wamid.fixture." + Guid.NewGuid().ToString("N");
        Guid contactId;
        using (var scope = factory.Services.CreateScope())
        {
            var contacts = scope.ServiceProvider.GetRequiredService<IWhatsAppContactRepository>();
            var messages = scope.ServiceProvider.GetRequiredService<IWhatsAppMessageRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var contact = WhatsAppContact.Create(PhoneNumber.Create("+5491134567890").Value, now);
            contactId = contact.Id;
            var result = await uow.ExecuteInTransactionAsync(ct =>
            {
                contacts.Add(contact);
                messages.Add(WhatsAppMessage.Create("fixture-number", wamid, "sent", WhatsAppMessageDirection.Outbound,
                    WhatsAppMessageKind.Delivery, WhatsAppMessageStatus.Sent, now, now, contactId));
                messages.Add(WhatsAppMessage.Create("fixture-number", wamid, "delivered", WhatsAppMessageDirection.Outbound,
                    WhatsAppMessageKind.Delivery, WhatsAppMessageStatus.Delivered, now, now, contactId));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, TestContext.Current.CancellationToken);
            Assert.True(result.IsSuccess);
        }
        using var restart = factory.Services.CreateScope();
        var context = restart.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2, await context.Set<WhatsAppMessage>().CountAsync(value => value.Wamid == wamid, TestContext.Current.CancellationToken));
        Assert.Equal(contactId, (await context.Set<WhatsAppContact>().SingleAsync(value => value.Id == contactId,
            TestContext.Current.CancellationToken)).Id);
    }
    [Fact]
    public void Global_declarations_reject_unknown_or_incorrect_schema_and_duplicate_declarations()
    {
        var builder = new ModelBuilder();
        builder.Entity<Undeclared>().ToTable("Undeclared", "identity").HasKey(value => value.Id);
        Assert.Throws<InvalidOperationException>(() =>
            ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls.TenantIsolationModelValidator.Validate(builder.Model));
        Assert.Throws<InvalidOperationException>(() =>
            ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls.TenantIsolationModelValidator.Validate(builder.Model,
                new Dictionary<Type, string> { [typeof(Undeclared)] = "platform" }));
        var contributor = Assert.Single(PersistenceModelContributors.Discover());
        Assert.Throws<InvalidOperationException>(() => PersistenceModelContributors.GlobalEntities([contributor, contributor]));
    }
    private static ApplicationDbContext Context(IEnumerable<IPersistenceModelContributor>? contributors = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql("Host=localhost;Database=fixture-model");
        options.UseOpenIddict<Guid>();
        return new ApplicationDbContext(options.Options, new TenantContext(), contributors);
    }
    private sealed class Undeclared { public Guid Id { get; set; } }
}
