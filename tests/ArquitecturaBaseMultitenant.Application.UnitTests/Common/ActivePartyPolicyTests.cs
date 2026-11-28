using ArquitecturaBaseMultitenant.Application.Common.Tenancy;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Common;

/// <summary>
/// Comprueba que <see cref="ActivePartyPolicy"/> derive la parte activa desde <c>ITenantContext</c>
/// sin permitir que el llamador declare otra parte ni operar sin clase de espacio.
/// </summary>
public sealed class ActivePartyPolicyTests
{
    private readonly Guid _consumerTenantId = Guid.CreateVersion7();
    private readonly Guid _businessTenantId = Guid.CreateVersion7();

    [Fact]
    public void Active_consumer_tenant_context_passes_only_for_consumer_operations()
    {
        var shared = new SharedRow(_consumerTenantId, _businessTenantId);
        var context = new FakeTenantContext { TenantId = _consumerTenantId, TenantKind = TenantKind.Personal };

        Assert.True(ActivePartyPolicy.Require(context, shared, Party.Consumer).IsSuccess);
        Assert.Equal(PartyErrors.NotAllowed, ActivePartyPolicy.Require(context, shared, Party.Business).Error);
    }

    [Fact]
    public void Active_business_tenant_context_passes_only_for_business_operations()
    {
        var shared = new SharedRow(_consumerTenantId, _businessTenantId);
        var context = new FakeTenantContext { TenantId = _businessTenantId, TenantKind = TenantKind.Business };

        Assert.True(ActivePartyPolicy.Require(context, shared, Party.Business).IsSuccess);
        Assert.Equal(PartyErrors.NotAllowed, ActivePartyPolicy.Require(context, shared, Party.Consumer).Error);
    }

    [Fact]
    public void Unclassified_or_empty_tenant_context_is_rejected()
    {
        var shared = new SharedRow(_consumerTenantId, _businessTenantId);

        var unclassifiedConsumer = new FakeTenantContext { TenantId = _consumerTenantId, TenantKind = null };
        var unclassifiedBusiness = new FakeTenantContext { TenantId = _businessTenantId, TenantKind = null };
        var emptyTenant = new FakeTenantContext { TenantId = null, TenantKind = TenantKind.Personal };
        var unrelatedTenant = new FakeTenantContext { TenantId = Guid.CreateVersion7(), TenantKind = TenantKind.Personal };

        foreach (var context in new[] { unclassifiedConsumer, unclassifiedBusiness, emptyTenant, unrelatedTenant })
        {
            var consumerResult = ActivePartyPolicy.Require(context, shared, Party.Consumer);
            var businessResult = ActivePartyPolicy.Require(context, shared, Party.Business);
            Assert.True(consumerResult.IsFailure);
            Assert.Equal(PartyErrors.NotAllowedCode, consumerResult.Error.Code);
            Assert.Equal(ErrorType.Forbidden, consumerResult.Error.Type);
            Assert.True(businessResult.IsFailure);
            Assert.Equal(PartyErrors.NotAllowedCode, businessResult.Error.Code);
            Assert.Equal(ErrorType.Forbidden, businessResult.Error.Type);
        }
    }

    [Fact]
    public void Business_context_cannot_act_as_consumer_even_when_ids_match()
    {
        var sameId = Guid.CreateVersion7();
        var shared = new SharedRow(sameId, sameId);
        var businessContext = new FakeTenantContext { TenantId = sameId, TenantKind = TenantKind.Business };

        Assert.Equal(PartyErrors.NotAllowed, ActivePartyPolicy.Require(businessContext, shared, Party.Consumer).Error);
        Assert.True(ActivePartyPolicy.Require(businessContext, shared, Party.Business).IsSuccess);
    }

    private sealed record SharedRow(Guid ConsumerTenantId, Guid BusinessTenantId) : IConsumerBusinessShared
    {
        public void ReplaceConsumerCopy() { }
    }
}
