using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.OrganizationSettings;

internal sealed class ListOrganizationDomainsRequestValidator
    : PagedRequestValidator<ListOrganizationDomainsRequest>
{
    public ListOrganizationDomainsRequestValidator() : base(ListOrganizationDomainsRequest.SortableFields)
    {
        RuleFor(request => request.Status).Must(status => status is null or TenantDomainStatus.Pending or TenantDomainStatus.Verified)
            .WithMessage(_ => ValidationTexts.FilterInvalid);
    }
}
