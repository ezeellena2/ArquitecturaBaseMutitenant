using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Settings;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Platform;

internal sealed class UpdatePlatformSettingsRequestValidator : AbstractValidator<UpdatePlatformSettingsRequest>
{
    public UpdatePlatformSettingsRequestValidator()
    {
        RuleFor(request => request.ConsumerSignup).NotNull().WithMessage(_ => ValidationTexts.Required)
            .Must(mode => mode is null || Enum.IsDefined(mode.Value)).WithMessage(_ => ValidationTexts.RegistrationModeInvalid);
        RuleFor(request => request.BusinessSignup).NotNull().WithMessage(_ => ValidationTexts.Required)
            .Must(mode => mode is null || Enum.IsDefined(mode.Value)).WithMessage(_ => ValidationTexts.RegistrationModeInvalid);
        RuleFor(request => request.MaxOwnedOrganizations).NotNull().WithMessage(_ => ValidationTexts.Required)
            .InclusiveBetween(1, 50).WithMessage(_ => ValidationTexts.OwnedOrganizationsRange);
        RuleFor(request => request.SalesEmail).NotNull().WithMessage(_ => ValidationTexts.EmailInvalid);
        RuleFor(request => request.SupportEmail).NotNull().WithMessage(_ => ValidationTexts.EmailInvalid);
        RuleFor(request => request.Version).NotNull().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Reason).Required().Description();
        // Sin rateLimits se conservan las cuotas; con ellas, van los doce perfiles completos (E9).
        When(request => request.RateLimits is not null, () =>
        {
            var quota = new RateLimitQuotaInputValidator();
            RuleFor(request => request.RateLimits!.Ip).Quota(quota);
            RuleFor(request => request.RateLimits!.PublicIp).Quota(quota);
            RuleFor(request => request.RateLimits!.Identity).Quota(quota);
            RuleFor(request => request.RateLimits!.Tenant).Quota(quota);
            RuleFor(request => request.RateLimits!.LoginCode).Quota(quota);
            RuleFor(request => request.RateLimits!.LoginVerify).Quota(quota);
            RuleFor(request => request.RateLimits!.InvitationPreview).Quota(quota);
            RuleFor(request => request.RateLimits!.InvitationAccept).Quota(quota);
            RuleFor(request => request.RateLimits!.AssistantQuickQuery).Quota(quota);
            RuleFor(request => request.RateLimits!.AssistantConversation).Quota(quota);
            RuleFor(request => request.RateLimits!.AssistantNewKey).Quota(quota);
            RuleFor(request => request.RateLimits!.WhatsAppWebhook).Quota(quota);
        });
    }
}

/// <summary>Una cuota editada: los dos valores, dentro de sus cotas y sin cero para apagarla.</summary>
internal sealed class RateLimitQuotaInputValidator : AbstractValidator<RateLimitQuotaInput>
{
    public RateLimitQuotaInputValidator()
    {
        RuleFor(quota => quota.PermitLimit).NotNull().WithMessage(_ => ValidationTexts.Required)
            .InclusiveBetween(RateLimitQuota.MinPermitLimit, RateLimitQuota.MaxPermitLimit)
            .WithMessage(_ => ValidationTexts.RateLimitPermitRange);
        RuleFor(quota => quota.WindowSeconds).NotNull().WithMessage(_ => ValidationTexts.Required)
            .InclusiveBetween(RateLimitQuota.MinWindowSeconds, RateLimitQuota.MaxWindowSeconds)
            .WithMessage(_ => ValidationTexts.RateLimitWindowRange);
    }
}

internal static class RateLimitQuotaRules
{
    public static void Quota<T>(this IRuleBuilderInitial<T, RateLimitQuotaInput?> rule, RateLimitQuotaInputValidator quota) =>
        rule.NotNull().WithMessage(_ => ValidationTexts.Required).SetValidator(quota!);
}
