using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Validation;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Assistant;

public sealed class AssistantSettingsTests
{
    [Fact]
    public async Task Own_key_uses_live_platform_retention_without_changing_its_own_limits()
    {
        var fixture = new Fixture();
        fixture.Organization = new(AssistantMode.OwnKey, fixture.Model.Id, AssistantEffort.Low, 8, fixture.Price, 30, true, "1234", 1);
        fixture.Platform = fixture.Platform with { RetentionDays = 45, DailyMessageLimit = 70 };
        var resolved = await fixture.Policy.ResolveAsync(TestContext.Current.CancellationToken);
        Assert.True(resolved.IsSuccess);
        Assert.Equal(45, resolved.Value.RetentionDays);
        Assert.Equal(8, resolved.Value.DailyMessageLimit);
        Assert.Equal(fixture.Price, resolved.Value.MonthlyBudget);
    }

    [Fact]
    public void Budget_above_storage_capacity_is_rejected_by_both_validators()
    {
        var amount = new Money(1_000_000_000_000m, CurrencyCode.Create("USD").Value);
        var modelId = Guid.CreateVersion7();
        var normal = new Money(100m, amount.Currency);
        var organization = new UpdateOrganizationAssistantSettingsRequest(AssistantMode.OwnKey, modelId, AssistantEffort.Low, 50, normal, AssistantKeyAction.Keep, null, 1);
        var platform = new UpdatePlatformAssistantSettingsRequest(modelId, [modelId], AssistantEffort.Low, 50, normal, false, 30, AssistantKeyAction.Keep, null, 1);
        var organizationValidator = new UpdateOrganizationAssistantSettingsRequestValidator();
        var platformValidator = new UpdatePlatformAssistantSettingsRequestValidator();
        Assert.True(organizationValidator.Validate(organization).IsValid);
        Assert.True(platformValidator.Validate(platform).IsValid);
        Assert.Contains(organizationValidator.Validate(organization with { MonthlyBudget = amount }).Errors, error => error.PropertyName == "MonthlyBudget");
        Assert.Contains(platformValidator.Validate(platform with { MonthlyOrganizationBudget = amount }).Errors, error => error.PropertyName == "MonthlyOrganizationBudget");
    }

    [Fact]
    public void Provider_tariffs_preserve_subcent_values_but_reject_loss_of_six_digit_precision()
    {
        var currency = CurrencyCode.Create("USD").Value;
        var valid = new CreateAssistantModelRequest("Prueba", AssistantProvider.Anthropic, "fixture-model", new(0.125m, currency), new(2m, currency), true, Technical());
        var create = new CreateAssistantModelRequestValidator();
        Assert.True(create.Validate(valid).IsValid);
        Assert.False(create.Validate(valid with { InputPricePerMillion = new(0.1234567m, currency) }).IsValid);
        var update = new UpdateAssistantModelRequestValidator();
        Assert.False(update.Validate(new UpdateAssistantModelRequest("Prueba", AssistantProvider.Anthropic, "fixture-model", new(1m, currency), new(0.1234567m, currency), true, 1, Technical())).IsValid);
    }
    [Fact]
    public async Task Missing_key_returns_not_configured_and_later_live_settings_are_revalidated()
    {
        var fixture = new Fixture();
        fixture.Platform = fixture.Platform with { HasKey = false };
        Assert.Equal(AssistantErrors.SettingsNotConfigured, (await fixture.Policy.ResolveAsync(TestContext.Current.CancellationToken)).Error);
        fixture.Platform = fixture.Platform with { HasKey = true };
        Assert.True((await fixture.Policy.ResolveAsync(TestContext.Current.CancellationToken)).IsSuccess);
        fixture.Platform = fixture.Platform with { AllowedModelIds = [] };
        Assert.Equal(AssistantErrors.ModelNotAllowed, (await fixture.Policy.ResolveAsync(TestContext.Current.CancellationToken)).Error);
    }

    [Fact]
    public async Task Own_key_revocation_recipient_loss_and_key_loss_never_fall_back_to_platform_funding()
    {
        var fixture = new Fixture();
        fixture.Organization = new(AssistantMode.OwnKey, fixture.Model.Id, AssistantEffort.Low, 8, fixture.Price, 30, true, "1234", 1);
        var initial = await fixture.Policy.ResolveAsync(TestContext.Current.CancellationToken);
        Assert.Equal(AssistantMode.OwnKey, initial.Value.Mode);
        Assert.Equal(8, initial.Value.DailyMessageLimit);
        fixture.AllowOwn = false;
        Assert.Equal(AssistantErrors.SettingsOwnKeyNotAllowed, (await fixture.Policy.ResolveAsync(TestContext.Current.CancellationToken)).Error);
        fixture.AllowOwn = true;
        fixture.Recipient = false;
        Assert.Equal(AssistantErrors.SettingsAlertRecipientRequired, (await fixture.Policy.ResolveAsync(TestContext.Current.CancellationToken)).Error);
        fixture.Recipient = true;
        fixture.Organization = fixture.Organization with { HasKey = false };
        Assert.Equal(AssistantErrors.SettingsNotConfigured, (await fixture.Policy.ResolveAsync(TestContext.Current.CancellationToken)).Error);
    }

    [Fact]
    public async Task Active_compatible_is_independent_of_platform_allow_list_and_revoked_model_is_blocked()
    {
        var fixture = new Fixture();
        fixture.Platform = fixture.Platform with { AllowedModelIds = [] };
        fixture.Organization = new(AssistantMode.OwnKey, fixture.Model.Id, AssistantEffort.Low, 50, fixture.Price, 30, true, "1234", 1);
        Assert.True((await fixture.Policy.ResolveAsync(TestContext.Current.CancellationToken)).IsSuccess);
        fixture.Model = fixture.Model with { IsActive = false };
        Assert.Equal(AssistantErrors.ModelNotAllowed, (await fixture.Policy.ResolveAsync(TestContext.Current.CancellationToken)).Error);
        fixture.Model = fixture.Model with { IsActive = true, ProviderModelId = "unknown-reference", Technical = null };
        Assert.Equal(AssistantErrors.ModelNotAllowed, (await fixture.Policy.ResolveAsync(TestContext.Current.CancellationToken)).Error);
    }

    internal static AssistantModelTechnicalRequest Technical() => new(true, true, true, true, true,
        [AssistantEffort.Low, AssistantEffort.Medium, AssistantEffort.High], 100000, 4096,
        new(0.125m, CurrencyCode.Create("USD").Value), new(0.2m, CurrencyCode.Create("USD").Value), new(0.01m, CurrencyCode.Create("USD").Value), null);

    private sealed class Fixture : IAssistantSettingsReader, IAssistantModelReader, ITenantAdminReader, ITenantContext, ICurrentUser
    {
        public Money Price { get; } = new(100m, CurrencyCode.Create("USD").Value);
        public AssistantModelPriceResponse Model { get; set; }
        public PlatformAssistantSettingsResponse Platform { get; set; }
        public AssistantOrganizationSettingsRow? Organization { get; set; }
        public bool AllowOwn { get; set; } = true;
        public bool Recipient { get; set; } = true;
        public AssistantConfigurationPolicy Policy { get; }
        public Fixture()
        {
            Model = new(Guid.CreateVersion7(), "Fixture", AssistantProvider.Anthropic, "fixture-model", Price, Price, true, 1, AssistantModelReferenceBuilder.ToResponse(AssistantModelReferenceBuilder.ToDomain(Technical())));
            Platform = new(Model.Id, [Model.Id], AssistantEffort.Low, 50, Price, false, 30, true, "0000", 1);
            Policy = new(this, this, this, this, this);
        }
        public Guid? UserId { get; } = Guid.CreateVersion7();
        public Access? Access => Domain.Users.Access.Business;
        public Guid? TenantId { get; } = Guid.CreateVersion7();
        public TenantKind? TenantKind => Domain.Tenancy.TenantKind.Business;
        public Guid RequiredTenantId => TenantId!.Value;
        public Task<bool> IsPersonalEnabledAsync(CancellationToken ct) => Task.FromResult(Platform.PersonalEnabled);
        public Task<PlatformAssistantSettingsResponse> FindPlatformAsync(CancellationToken ct) => Task.FromResult(Platform);
        public Task<AssistantOrganizationSettingsRow?> FindOrganizationAsync(CancellationToken ct) => Task.FromResult(Organization);
        public Task<AssistantPolicyResponse> FindPolicyAsync(Guid tenantId, CancellationToken ct) => Task.FromResult(new AssistantPolicyResponse(AllowOwn, 1));
        public Task<AssistantKeyMaterial> GetPlatformKeyAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<AssistantKeyMaterial> GetOrganizationKeyAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<AssistantModelPriceResponse>> ListAllAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<AssistantModelPriceResponse>>([Model]);
        public Task<PagedResult<AssistantModelPriceResponse>> ListAsync(AssistantModelListRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ActiveTenantAdminEmail>> ListActiveVerifiedEmailRecipientsAsync(Guid tenantId, CancellationToken ct) => Task.FromResult<IReadOnlyList<ActiveTenantAdminEmail>>(Recipient ? [new(UserId!.Value, "owner@example.test")] : []);
        public Task<IReadOnlyList<Guid>> ListOwnedBusinessTenantIdsAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> ListOwnedOpenTenantIdsAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<int> CountOtherActiveOwnersAsync(Guid tenantId, Guid userId, CancellationToken ct) => throw new NotSupportedException();
    }
}
