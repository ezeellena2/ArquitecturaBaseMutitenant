using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Application.Services.Platform;
using ArquitecturaBaseMultitenant.Domain.Common;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Platform;

/// <summary>Período y tipo de la lista cerrada del tablero; búsqueda acotada.</summary>
internal sealed class ListPlatformAuditRequestValidator : CursorRequestValidator<ListPlatformAuditRequest>
{
    private static readonly string[] Periods = ["0", "7", "30", "all"];

    public ListPlatformAuditRequestValidator()
    {
        RuleFor(request => request.Period)
            .Must(period => period is not null && Periods.Contains(period, StringComparer.Ordinal))
            .WithMessage(_ => ValidationTexts.FilterInvalid);
        RuleFor(request => request.Type)
            .Must(type => type is null || PlatformAuditCategories.Filterable.Contains(type, StringComparer.Ordinal))
            .WithMessage(_ => ValidationTexts.FilterInvalid);
        RuleFor(request => request.TenantId).Must(id => id != Guid.Empty).WithMessage(_ => ValidationTexts.FilterInvalid);
        RuleFor(request => request.AccountId).Must(id => id != Guid.Empty).WithMessage(_ => ValidationTexts.FilterInvalid);
        RuleFor(request => request.Search).MaximumLength(TextLimits.AuditSearch).WithMessage(_ => ValidationTexts.MaxLength);
    }
}
