using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Common.Validation;

/// <summary>Valida página, tamaño, lista blanca de orden y largo de búsqueda.</summary>
public abstract class PagedRequestValidator<TRequest> : AbstractValidator<TRequest>
    where TRequest : PagedRequest
{
    protected PagedRequestValidator(IReadOnlyCollection<string> sortableFields)
    {
        ArgumentNullException.ThrowIfNull(sortableFields);

        RuleFor(request => request.Page)
            .InclusiveBetween(1, PagedRequest.MaxPage).WithMessage(_ => ValidationTexts.PageInvalid);

        RuleFor(request => request.PageSize)
            .Must(size => PagedRequest.AllowedPageSizes.Contains(size)).WithMessage(_ => ValidationTexts.PageSizeInvalid);

        RuleFor(request => request.Sort)
            .Must(sort => IsSortable(sort, sortableFields)).WithMessage(_ => ValidationTexts.SortNotAllowed);

        RuleFor(request => request.Search)
            .MaximumLength(PagedRequest.MaxSearchLength).WithMessage(_ => ValidationTexts.MaxLength);
    }

    private static bool IsSortable(string? sort, IReadOnlyCollection<string> sortableFields)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return true;
        }

        var descriptor = SortDescriptor.Parse(sort);
        return descriptor is not null && sortableFields.Contains(descriptor.Field, StringComparer.OrdinalIgnoreCase);
    }
}
