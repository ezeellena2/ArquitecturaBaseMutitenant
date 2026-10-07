using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Models.PublicSite;

namespace ArquitecturaBaseMultitenant.Application.Validation.PublicSite;

internal sealed class ListDirectoryRequestValidator() : PagedRequestValidator<ListDirectoryRequest>(ListDirectoryRequest.SortableFields);
