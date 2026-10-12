using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Validation.OrganizationSettings;

internal sealed class ListOrganizationDomainsRequestValidator()
    : PagedRequestValidator<ListOrganizationDomainsRequest>(ListOrganizationDomainsRequest.SortableFields);
