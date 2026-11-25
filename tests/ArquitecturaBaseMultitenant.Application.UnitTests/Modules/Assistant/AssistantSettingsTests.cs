using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
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

    // B13 (0064 §6 y §7): la clave ilegible y el proveedor desconocido frenan antes de reservar, y cada uno ve lo que arregla.
    [Theory]
    [InlineData(AssistantMode.PlatformKey)]
    [InlineData(AssistantMode.OwnKey)]
    public async Task An_unreadable_saved_key_stops_the_turn_before_reserving_without_calling_the_provider(AssistantMode mode)
    {
        var ct = TestContext.Current.CancellationToken;
        Fixture Funded(AssistantMode? unreadable)
        {
            var fixture = new Fixture();
            if (mode == AssistantMode.OwnKey) fixture.Organization = new(AssistantMode.OwnKey, fixture.Model.Id, AssistantEffort.Low, 8, fixture.Price, 30, true, "1234", 1);
            if (unreadable is { } funding) fixture.Unreadable.Add(funding);
            return fixture;
        }
        Assert.True((await Funded(null).Policy.ResolveAsync(ct)).IsSuccess);
        Assert.Equal(AssistantErrors.ProviderKeyUnreadable, (await Funded(mode).Policy.ResolveAsync(ct)).Error);
        // The other funding's key is not probed: an organization's own key never decides the platform's.
        var other = mode == AssistantMode.OwnKey ? AssistantMode.PlatformKey : AssistantMode.OwnKey;
        Assert.True((await Funded(other).Policy.ResolveAsync(ct)).IsSuccess);
    }

    [Fact]
    public async Task A_provider_this_version_does_not_know_pauses_its_own_key_organization_and_every_platform_key_user()
    {
        var ct = TestContext.Current.CancellationToken;
        var own = new Fixture();
        own.Organization = new(AssistantMode.OwnKey, own.Model.Id, AssistantEffort.Low, 8, own.Price, 30, true, "1234", 1);
        own.Providers[own.Model.Id] = "FutureProvider";
        Assert.Equal(AssistantErrors.ProviderUnsupportedProvider, (await own.Policy.ResolveAsync(ct)).Error);
        Assert.True((await own.Builder.BuildAsync(ct)).UnsupportedProvider);

        // With the platform key the saved provider is the default model's: an organization that chose a known model also pauses.
        var platform = new Fixture();
        var future = Guid.CreateVersion7();
        platform.Platform = platform.Platform with { DefaultModelId = future, AllowedModelIds = [platform.Model.Id, future] };
        platform.Organization = new(AssistantMode.PlatformKey, platform.Model.Id, AssistantEffort.Low, 50, null, 30, false, null, 1);
        platform.Providers[future] = "FutureProvider";
        Assert.Equal(AssistantErrors.ProviderUnsupportedProvider, (await platform.Policy.ResolveAsync(ct)).Error);
        var organization = await platform.Builder.BuildAsync(ct);
        Assert.False(organization.UnsupportedProvider);
        Assert.Equal(AssistantErrors.ProviderPlatformUnavailableCode, organization.ProviderIssue);
        Assert.True((await platform.Builder.BuildPlatformAsync(ct)).UnsupportedProvider);
        Assert.False((await new Fixture().Builder.BuildPlatformAsync(ct)).UnsupportedProvider);
    }

    [Fact]
    public async Task Each_settings_screen_shows_the_issue_its_owner_fixes_according_to_who_pays()
    {
        var ct = TestContext.Current.CancellationToken;
        var fixture = new Fixture();
        fixture.Platform = fixture.Platform with { ProviderIssue = AssistantErrors.ProviderCredentialRejectedCode };
        // Platform key: the operator sees the rejected key; the organization only learns that the platform is on it.
        Assert.Equal(AssistantErrors.ProviderCredentialRejectedCode, (await fixture.Builder.BuildPlatformAsync(ct)).ProviderIssue);
        Assert.Equal(AssistantErrors.ProviderPlatformUnavailableCode, (await fixture.Builder.BuildAsync(ct)).ProviderIssue);
        // Own key: the owner sees the issue of their key, never the platform's.
        fixture.Organization = new(AssistantMode.OwnKey, fixture.Model.Id, AssistantEffort.Low, 8, fixture.Price, 30, true, "1234", 1, AssistantErrors.ProviderLimitedCode);
        Assert.Equal(AssistantErrors.ProviderLimitedCode, (await fixture.Builder.BuildAsync(ct)).ProviderIssue);
        fixture.Organization = fixture.Organization with { ProviderIssueCode = null };
        Assert.Null((await fixture.Builder.BuildAsync(ct)).ProviderIssue);
        // An unreadable key is probed when reading, for whoever configures it.
        var unreadable = new Fixture { Organization = fixture.Organization };
        unreadable.Unreadable.Add(AssistantMode.OwnKey);
        Assert.Equal(AssistantErrors.ProviderKeyUnreadableCode, (await unreadable.Builder.BuildAsync(ct)).ProviderIssue);
        var platformUnreadable = new Fixture();
        platformUnreadable.Unreadable.Add(AssistantMode.PlatformKey);
        Assert.Equal(AssistantErrors.ProviderKeyUnreadableCode, (await platformUnreadable.Builder.BuildPlatformAsync(ct)).ProviderIssue);
        Assert.Equal(AssistantErrors.ProviderPlatformUnavailableCode, (await platformUnreadable.Builder.BuildAsync(ct)).ProviderIssue);
        var healthy = new Fixture();
        Assert.Null((await healthy.Builder.BuildAsync(ct)).ProviderIssue);
        Assert.Null((await healthy.Builder.BuildPlatformAsync(ct)).ProviderIssue);
    }

    [Theory]
    [InlineData(AssistantErrors.ProviderCredentialRejectedCode, AssistantKeyFunding.PlatformKey, Access.Business, AssistantErrors.ProviderPlatformUnavailableCode)]
    [InlineData(AssistantErrors.ProviderLimitedCode, AssistantKeyFunding.PlatformKey, Access.Consumer, AssistantErrors.ProviderPlatformUnavailableCode)]
    [InlineData(AssistantErrors.ProviderKeyUnreadableCode, AssistantKeyFunding.PlatformKey, Access.Business, AssistantErrors.ProviderPlatformUnavailableCode)]
    [InlineData(AssistantErrors.ProviderUnsupportedProviderCode, AssistantKeyFunding.PlatformKey, Access.Consumer, AssistantErrors.ProviderPlatformUnavailableCode)]
    [InlineData(AssistantErrors.ProviderCredentialRejectedCode, AssistantKeyFunding.PlatformKey, Access.Platform, AssistantErrors.ProviderCredentialRejectedCode)]
    [InlineData(AssistantErrors.ProviderCredentialRejectedCode, AssistantKeyFunding.OwnKey, Access.Business, AssistantErrors.ProviderCredentialRejectedCode)]
    [InlineData(AssistantErrors.ProviderKeyUnreadableCode, AssistantKeyFunding.OwnKey, Access.Business, AssistantErrors.ProviderKeyUnreadableCode)]
    [InlineData(AssistantErrors.ProviderModelRejectedCode, AssistantKeyFunding.PlatformKey, Access.Business, AssistantErrors.ProviderModelRejectedCode)]
    [InlineData(AssistantErrors.ProviderRejectedCode, AssistantKeyFunding.PlatformKey, Access.Business, AssistantErrors.ProviderRejectedCode)]
    [InlineData(AssistantErrors.ProviderUncertainCode, AssistantKeyFunding.PlatformKey, Access.Consumer, AssistantErrors.ProviderUncertainCode)]
    [InlineData(AssistantErrors.ProviderUnavailableCode, AssistantKeyFunding.PlatformKey, Access.Business, AssistantErrors.ProviderUnavailableCode)]
    public void The_person_sees_the_failure_that_who_pays_lets_them_act_on(string code, AssistantKeyFunding funding, Access access, string shown) =>
        Assert.Equal(shown, AssistantProviderIssuePolicy.Shown(code, funding, access));

    [Fact]
    public void Only_what_the_configuration_owner_fixes_is_recorded_in_their_settings()
    {
        Assert.Equal([AssistantErrors.ProviderCredentialRejectedCode, AssistantErrors.ProviderLimitedCode, AssistantErrors.ProviderModelRejectedCode,
            AssistantErrors.ProviderUnavailableCode], AssistantErrors.ProviderFailureCodes.Where(AssistantProviderIssuePolicy.IsRecorded));
        Assert.Null(AssistantProviderIssuePolicy.Shown(null, AssistantKeyFunding.PlatformKey, Access.Business));
        var row = AssistantOrganizationProviderIssue.Create();
        var at = new DateTime(2026, 11, 25, 3, 0, 0, DateTimeKind.Utc);
        Assert.True(row.Record(AssistantErrors.ProviderLimitedCode, at));
        Assert.False(row.Record(AssistantErrors.ProviderLimitedCode, at.AddMinutes(1)));
        Assert.Equal(at, row.AtUtc);
        Assert.True(row.Record(null, at));
        Assert.Null(row.AtUtc);
        var platform = AssistantPlatformProviderIssue.Create();
        Assert.Equal(AssistantPlatformSettings.SingletonId, platform.Id);
        Assert.True(platform.Record(AssistantErrors.ProviderCredentialRejectedCode, at));
        Assert.True(platform.Record(null, at));
        Assert.False(platform.Record(null, at));
        Assert.Throws<ArgumentException>(() => platform.Record(AssistantErrors.ProviderLimitedCode, DateTime.SpecifyKind(at, DateTimeKind.Unspecified)));
    }

    internal static AssistantModelTechnicalRequest Technical() => new(true, true, true, true, true,
        [AssistantEffort.Low, AssistantEffort.Medium, AssistantEffort.High], 100000, 4096,
        new(0.125m, CurrencyCode.Create("USD").Value), new(0.2m, CurrencyCode.Create("USD").Value), new(0.01m, CurrencyCode.Create("USD").Value), null);

    private sealed class Fixture : IAssistantSettingsReader, IAssistantModelReader, ITenantAdminReader, ITenantContext, ICurrentUser, IAssistantKeyProtector
    {
        public HashSet<AssistantMode> Unreadable { get; } = [];
        public Dictionary<Guid, string> Providers { get; } = [];
        public AssistantSettingsBuilder Builder { get; }
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
            var connection = new AssistantConnectionPolicy(this, this, this);
            Policy = new(this, this, this, this, this, connection);
            Builder = new(this, this, this, this, connection);
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
        public Task<AssistantKeyMaterial> GetPlatformKeyAsync(CancellationToken ct) => Task.FromResult(new AssistantKeyMaterial([(byte)AssistantMode.PlatformKey], "0000", true));
        public Task<AssistantKeyMaterial> GetOrganizationKeyAsync(CancellationToken ct) => Task.FromResult(new AssistantKeyMaterial([(byte)AssistantMode.OwnKey], "1234", true));
        public Task<string?> FindProviderAsync(Guid modelId, CancellationToken ct) =>
            Task.FromResult(Providers.TryGetValue(modelId, out var provider) ? provider : modelId == Model.Id ? nameof(AssistantProvider.Anthropic) : null);
        public byte[] ProtectPlatform(string key) => throw new NotSupportedException();
        public string UnprotectPlatform(byte[] ciphertext) => throw new NotSupportedException();
        public byte[] ProtectOrganization(string key) => throw new NotSupportedException();
        public string UnprotectOrganization(byte[] ciphertext) => throw new NotSupportedException();
        public bool CanUnprotectPlatform(byte[] ciphertext) => !Unreadable.Contains((AssistantMode)ciphertext[0]);
        public bool CanUnprotectOrganization(byte[] ciphertext) => !Unreadable.Contains((AssistantMode)ciphertext[0]);
        public Task<IReadOnlyList<AssistantModelPriceResponse>> ListAllAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<AssistantModelPriceResponse>>([Model]);
        public Task<PagedResult<AssistantModelPriceResponse>> ListAsync(AssistantModelListRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ActiveTenantAdminEmail>> ListActiveVerifiedEmailRecipientsAsync(Guid tenantId, CancellationToken ct) => Task.FromResult<IReadOnlyList<ActiveTenantAdminEmail>>(Recipient ? [new(UserId!.Value, Email.Create("owner@example.test").Value)] : []);
        public Task<IReadOnlyList<Guid>> ListOwnedBusinessTenantIdsAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> ListOwnedOpenTenantIdsAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<int> CountOtherActiveOwnersAsync(Guid tenantId, Guid userId, CancellationToken ct) => throw new NotSupportedException();
    }
}
