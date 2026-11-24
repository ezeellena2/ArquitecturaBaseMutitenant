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
        // Plazos (272) y nombre de los correos (273): si faltan se conserva lo guardado; si llegan, dentro de sus cotas.
        RuleFor(request => request.LoginAuditRetentionMonths)
            .InclusiveBetween(PlatformSettingsLimits.MinLoginAuditRetentionMonths, PlatformSettingsLimits.MaxLoginAuditRetentionMonths)
            .WithMessage(_ => ValidationTexts.NumberRange);
        RuleFor(request => request.LoginCodeRetentionDays)
            .InclusiveBetween(PlatformSettingsLimits.MinLoginCodeRetentionDays, PlatformSettingsLimits.MaxLoginCodeRetentionDays)
            .WithMessage(_ => ValidationTexts.NumberRange);
        RuleFor(request => request.ClosedInvitationRetentionDays)
            .InclusiveBetween(PlatformSettingsLimits.MinClosedInvitationRetentionDays, PlatformSettingsLimits.MaxClosedInvitationRetentionDays)
            .WithMessage(_ => ValidationTexts.NumberRange);
        When(request => request.AppName is not null, () => RuleFor(request => request.AppName).Required().MaxLength(PlatformSettingsLimits.NameMaxLength));
        When(request => request.SenderName is not null, () => RuleFor(request => request.SenderName).Required().MaxLength(PlatformSettingsLimits.NameMaxLength));
        RuleFor(request => request.EmailLogoUrl)
            .Must(url => string.IsNullOrEmpty(url) || Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            .WithMessage(_ => ValidationTexts.SecureUrlInvalid);
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
