using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Roles;

internal sealed class ListRolesRequestValidator : PagedRequestValidator<ListRolesRequest>
{
    public ListRolesRequestValidator() : base(ListRolesRequest.SortableFields)
    {
        RuleFor(request => request.SystemRoleKey).Must(key => key is null || SystemRoles.All.Contains(key, StringComparer.Ordinal))
            .WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.CompanyId).NotNull().When(request => request.OnlyOwn)
            .WithMessage(_ => ValidationTexts.Required);
    }
}
