using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

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

        Assert.True(PartyPolicy.Require(shared, Party.Consumer, _consumerTenantId, Party.Consumer).IsSuccess);
        Assert.False(PartyPolicy.Require(shared, Party.Business, _businessTenantId, Party.Consumer).IsSuccess);
        Assert.False(PartyPolicy.Require(shared, Party.Consumer, Guid.CreateVersion7(), Party.Consumer).IsSuccess);
    }

    [Fact]
    public void Required_business_may_act_only_from_its_organization()
    {
        var shared = new SharedRow(_consumerTenantId, _businessTenantId);

        Assert.True(PartyPolicy.Require(shared, Party.Business, _businessTenantId, Party.Business).IsSuccess);
        Assert.False(PartyPolicy.Require(shared, Party.Consumer, _consumerTenantId, Party.Business).IsSuccess);
        Assert.False(PartyPolicy.Require(shared, Party.Business, Guid.CreateVersion7(), Party.Business).IsSuccess);
    }

    [Theory]
    [InlineData(Party.Consumer, Party.Business)]
    [InlineData(Party.Business, Party.Consumer)]
    [InlineData((Party)99, Party.Business)]
    [InlineData(Party.Consumer, (Party)99)]
    public void The_wrong_party_receives_the_shared_forbidden_error(Party active, Party required)
    {
        var shared = new SharedRow(_consumerTenantId, _businessTenantId);
        var result = PartyPolicy.Require(shared, active, _consumerTenantId, required);
        Assert.True(result.IsFailure);
        Assert.Equal(PartyErrors.NotAllowedCode, result.Error.Code);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
    }

    [Fact]
    public void An_empty_or_unrelated_space_cannot_act_even_with_the_required_party()
    {
        var shared = new SharedRow(_consumerTenantId, _businessTenantId);
        foreach (var id in new[] { Guid.Empty, Guid.CreateVersion7() })
        {
            Assert.Equal(PartyErrors.NotAllowed, PartyPolicy.Require(shared, Party.Consumer, id, Party.Consumer).Error);
            Assert.Equal(PartyErrors.NotAllowed, PartyPolicy.Require(shared, Party.Business, id, Party.Business).Error);
        }
    }

    private sealed record SharedRow(Guid ConsumerTenantId, Guid BusinessTenantId) : IConsumerBusinessShared;
}
