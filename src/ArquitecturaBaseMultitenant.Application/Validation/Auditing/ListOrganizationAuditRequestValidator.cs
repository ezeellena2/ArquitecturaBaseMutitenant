using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Models.Auditing;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Auditing;

internal sealed class ListOrganizationAuditRequestValidator : CursorRequestValidator<ListOrganizationAuditRequest>
{
    private static readonly string[] Categories = ["users", "roles", "companies", "settings", "organization"];
    private static readonly string[] Periods = ["0", "7", "30", "all"];

    public ListOrganizationAuditRequestValidator()
    {
        RuleFor(request => request.Period)
            .Must(period => period is not null && Periods.Contains(period, StringComparer.Ordinal))
            .WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Category)
            .Must(category => category is null || Categories.Contains(category, StringComparer.Ordinal))
            .WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.ActorId)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Search)
            .MaximumLength(100)
            .WithMessage(_ => ValidationTexts.Required);
    }
}
