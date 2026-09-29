using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

public sealed class TenantStampTests
{
    [Fact]
    public void Added_private_data_receives_the_active_tenant_even_if_constructed_with_another_id()
    {
        var activeTenant = Guid.NewGuid();
        using var context = CreateContext(activeTenant, TenantKind.Personal);
        var widget = new Widget(Guid.NewGuid(), "Widget");
        context.Add(widget);

        ApplyStamp(context, activeTenant, TenantKind.Personal);

        Assert.Equal(activeTenant, widget.TenantId);
    }

    [Fact]
    public async Task Async_save_interception_stamps_the_active_tenant()
    {
        var activeTenant = Guid.NewGuid();
        using var context = CreateContext(activeTenant, TenantKind.Personal);
        var widget = new Widget(Guid.NewGuid(), "Widget");
        context.Add(widget);
        var interceptor = new TenantStampInterceptor(new TestTenantContext(activeTenant, TenantKind.Personal));
        var eventData = new DbContextEventData(null!, (_, _) => string.Empty, context);

        await interceptor.SavingChangesAsync(eventData, default, TestContext.Current.CancellationToken);

        Assert.Equal(activeTenant, widget.TenantId);
    }

    [Fact]
    public void Added_public_data_receives_the_active_business_tenant()
    {
        var activeTenant = Guid.NewGuid();
        using var context = CreateContext(activeTenant, TenantKind.Business);
        var poster = new Poster(Guid.NewGuid(), false);
        context.Add(poster);

        ApplyStamp(context, activeTenant, TenantKind.Business);

        Assert.Equal(activeTenant, poster.BusinessTenantId);
    }

    [Theory]
    [InlineData(TenantKind.Personal)]
    [InlineData(TenantKind.Business)]
    public void Added_shared_data_stamps_only_the_active_partys_column(TenantKind activeKind)
    {
        var activeTenant = Guid.NewGuid();
        var consumerTenant = Guid.NewGuid();
        var businessTenant = Guid.NewGuid();
        using var context = CreateContext(activeTenant, activeKind);
        var deal = new Deal(consumerTenant, businessTenant);
        context.Add(deal);

        ApplyStamp(context, activeTenant, activeKind);

        Assert.Equal(activeKind == TenantKind.Personal ? activeTenant : consumerTenant, deal.ConsumerTenantId);
        Assert.Equal(activeKind == TenantKind.Business ? activeTenant : businessTenant, deal.BusinessTenantId);
    }

    [Fact]
    public void Added_public_or_shared_data_requires_the_active_partys_kind()
    {
        var tenantId = Guid.NewGuid();
        using var publicContext = CreateContext(tenantId, TenantKind.Personal);
        publicContext.Add(new Poster(Guid.NewGuid(), false));
        Assert.Throws<InvalidOperationException>(() => ApplyStamp(publicContext, tenantId, TenantKind.Personal));

        using var technicalContext = CreateContext(tenantId, null);
        technicalContext.Add(new Deal(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => ApplyStamp(technicalContext, tenantId, null));
    }

    [Fact]
    public void Tracked_entities_cannot_change_a_tenant_column_that_is_part_of_their_key()
    {
        var tenantId = Guid.NewGuid();
        using var privateContext = CreateContext(tenantId, TenantKind.Personal);
        var widget = new Widget(tenantId, "Widget");
        privateContext.Attach(widget);
        Assert.Throws<InvalidOperationException>(() =>
            privateContext.Entry(widget).Property(nameof(Widget.TenantId)).CurrentValue = Guid.NewGuid());

        using var publicContext = CreateContext(tenantId, TenantKind.Business);
        var poster = new Poster(tenantId, false);
        publicContext.Attach(poster);
        Assert.Throws<InvalidOperationException>(() =>
            publicContext.Entry(poster).Property(nameof(Poster.BusinessTenantId)).CurrentValue = Guid.NewGuid());

        using var sharedContext = CreateContext(tenantId, TenantKind.Personal);
        var deal = new Deal(tenantId, Guid.NewGuid());
        sharedContext.Attach(deal);
        Assert.Throws<InvalidOperationException>(() =>
            sharedContext.Entry(deal).Property(nameof(Deal.BusinessTenantId)).CurrentValue = Guid.NewGuid());
    }

    private static void ApplyStamp(DbContext context, Guid? tenantId, TenantKind? tenantKind)
    {
        var interceptor = new TenantStampInterceptor(new TestTenantContext(tenantId, tenantKind));
        var eventData = new DbContextEventData(null!, (_, _) => string.Empty, context);
        interceptor.SavingChanges(eventData, default);
    }

    private static IsolationApplicationDbContext CreateContext(Guid? tenantId, TenantKind? tenantKind)
    {
        var options = new DbContextOptionsBuilder<IsolationApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=stamp_model_test")
            .ReplaceService<IModelCustomizer, IsolationModelCustomizer>()
            .Options;
        return new IsolationApplicationDbContext(options, new TestTenantContext(tenantId, tenantKind));
    }

    private sealed class TestTenantContext(Guid? tenantId, TenantKind? tenantKind) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public TenantKind? TenantKind => tenantKind;
        public Guid RequiredTenantId => tenantId ?? throw new InvalidOperationException("No active tenant.");
    }
}
