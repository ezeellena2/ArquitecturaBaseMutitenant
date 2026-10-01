using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Tenancy;

/// <summary>
/// Comprueba que una acción de datos compartidos la ejecute la parte requerida desde su espacio. Evita
/// confundir el acceso de la persona con el de la empresa.
/// </summary>
public sealed class PartyPolicyTests
{
    private readonly Guid _consumerTenantId = Guid.CreateVersion7();
    private readonly Guid _businessTenantId = Guid.CreateVersion7();

    [Fact]
    public void Required_consumer_may_act_only_from_its_personal_space()
    {
        var shared = new SharedRow(_consumerTenantId, _businessTenantId);

        Assert.True(PartyPolicy.Require(shared, Party.Consumer, _consumerTenantId, Party.Consumer));
        Assert.False(PartyPolicy.Require(shared, Party.Business, _businessTenantId, Party.Consumer));
        Assert.False(PartyPolicy.Require(shared, Party.Consumer, Guid.CreateVersion7(), Party.Consumer));
    }

    [Fact]
    public void Required_business_may_act_only_from_its_organization()
    {
        var shared = new SharedRow(_consumerTenantId, _businessTenantId);

        Assert.True(PartyPolicy.Require(shared, Party.Business, _businessTenantId, Party.Business));
        Assert.False(PartyPolicy.Require(shared, Party.Consumer, _consumerTenantId, Party.Business));
        Assert.False(PartyPolicy.Require(shared, Party.Business, Guid.CreateVersion7(), Party.Business));
    }

    private sealed record SharedRow(Guid ConsumerTenantId, Guid BusinessTenantId) : IConsumerBusinessShared;
}
