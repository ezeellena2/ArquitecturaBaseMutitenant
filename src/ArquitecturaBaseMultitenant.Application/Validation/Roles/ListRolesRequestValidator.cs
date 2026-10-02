using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Models.Roles;

namespace ArquitecturaBaseMultitenant.Application.Validation.Roles;

internal sealed class ListRolesRequestValidator() : PagedRequestValidator<ListRolesRequest>(ListRolesRequest.SortableFields);
