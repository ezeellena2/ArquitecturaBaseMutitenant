using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Validation;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Assistant;

public sealed class AssistantContractValidationTests
{
    [Fact]
    public void Quick_accepts_a_new_conversation_or_an_existing_id_and_version_together()
    {
        var validator = new AssistantQuickRequestValidator();
        Assert.True(validator.Validate(new AssistantQuickRequest(null, null)).IsValid);
        Assert.True(validator.Validate(new AssistantQuickRequest(Guid.CreateVersion7(), 0)).IsValid);
        Assert.False(validator.Validate(new AssistantQuickRequest(Guid.CreateVersion7(), null)).IsValid);
        Assert.False(validator.Validate(new AssistantQuickRequest(null, 1)).IsValid);
        Assert.False(validator.Validate(new AssistantQuickRequest(Guid.Empty, 1)).IsValid);
    }

    [Fact]
    public void Settings_validate_write_only_key_action_and_usd_prices_by_field()
    {
        var validator = new UpdateOrganizationAssistantSettingsRequestValidator();
        var request = new UpdateOrganizationAssistantSettingsRequest(AssistantMode.OwnKey, Guid.CreateVersion7(), AssistantEffort.Medium,
            50, new Money(1, CurrencyCode.Create("USD").Value), AssistantKeyAction.Replace, "test-only-secret", 0);
        Assert.True(validator.Validate(request).IsValid);
        Assert.Contains(validator.Validate(request with { KeyAction = AssistantKeyAction.Keep }).Errors, error => error.PropertyName == "ApiKey");
        Assert.Contains(validator.Validate(request with { ApiKey = null }).Errors, error => error.PropertyName == "ApiKey");
        Assert.Contains(validator.Validate(request with { MonthlyBudget = new Money(-1, CurrencyCode.Create("USD").Value) }).Errors,
            error => error.PropertyName == "MonthlyBudget");
        Assert.Contains(validator.Validate(request with { MonthlyBudget = new Money(1, CurrencyCode.Create("ARS").Value) }).Errors,
            error => error.PropertyName == "MonthlyBudget");
        Assert.DoesNotContain("test-only-secret", request.ToString());
    }

    [Fact]
    public void Message_requires_text_version_and_unique_at_most_five_attachment_ids()
    {
        var validator = new SendAssistantMessageRequestValidator();
        Assert.True(validator.Validate(new SendAssistantMessageRequest("Consulta", [], 0)).IsValid);
        Assert.False(validator.Validate(new SendAssistantMessageRequest(" ", [], 1)).IsValid);
        Assert.False(validator.Validate(new SendAssistantMessageRequest("Consulta", [], null)).IsValid);
        var id = Guid.CreateVersion7();
        Assert.False(validator.Validate(new SendAssistantMessageRequest("Consulta", [id, id], 1)).IsValid);
        Assert.False(validator.Validate(new SendAssistantMessageRequest("Consulta", Enumerable.Range(0, 6).Select(_ => Guid.CreateVersion7()).ToArray(), 1)).IsValid);
    }

    [Theory]
    [InlineData("2026-10", true)]
    [InlineData("0001-01", true)]
    [InlineData("9999-12", true)]
    [InlineData("0000-01", false)]
    [InlineData("2026-+1", false)]
    [InlineData("2026- 1", false)]
    [InlineData("2026-1 ", false)]
    [InlineData("2026-\u0661\u0662", false)]
    [InlineData("2026-00", false)]
    [InlineData("2026-13", false)]
    [InlineData("2026-1", false)]
    public void Usage_month_is_a_closed_wire_month(string month, bool valid)
    {
        Assert.Equal(valid, new PlatformAssistantUsageRequestValidator().Validate(new PlatformAssistantUsageRequest(month, 1, 20)).IsValid);
        Assert.Equal(valid, new OrganizationAssistantUsageRequestValidator().Validate(new OrganizationAssistantUsageRequest(month, null, 1, 20)).IsValid);
    }
}
