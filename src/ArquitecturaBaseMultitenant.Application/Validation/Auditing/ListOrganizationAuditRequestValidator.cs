using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Models.Auditing;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Common;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Auditing;

internal sealed class ListOrganizationAuditRequestValidator : CursorRequestValidator<ListOrganizationAuditRequest>
{
    private static readonly string[] Periods = ["0", "7", "30", "all"];
    private static readonly string[] ActorKinds = Enum.GetNames<AuditActorKind>();

    public ListOrganizationAuditRequestValidator()
    {
        RuleFor(request => request).Must(request =>
            (request.FromUtc is null || request.FromUtc.Value.Kind == DateTimeKind.Utc)
            && (request.ToUtc is null || request.ToUtc.Value.Kind == DateTimeKind.Utc)
            && (request.FromUtc is null || request.ToUtc is null || request.FromUtc < request.ToUtc))
            .WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Period)
            .Must(period => period is not null && Periods.Contains(period, StringComparer.Ordinal))
            .WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Category)
            .Must(category => category is null || OrganizationAuditCategories.EntityTypes.ContainsKey(category))
            .WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.ActorId)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.ActorKind)
            .Must(kind => kind is null || ActorKinds.Contains(kind, StringComparer.Ordinal))
            .WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Search)
            .MaximumLength(TextLimits.AuditSearch)
            .WithMessage(_ => ValidationTexts.Required);
    }
}
