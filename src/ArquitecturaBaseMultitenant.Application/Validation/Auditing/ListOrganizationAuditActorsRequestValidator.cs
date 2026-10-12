using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Models.Auditing;

namespace ArquitecturaBaseMultitenant.Application.Validation.Auditing;

internal sealed class ListOrganizationAuditActorsRequestValidator()
    : PagedRequestValidator<ListOrganizationAuditActorsRequest>(ListOrganizationAuditActorsRequest.SortableFields);
