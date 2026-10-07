using System.ComponentModel.DataAnnotations;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Common;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;

public sealed record CreatePersonalWidgetHttpRequest([param: MaxLength(TextLimits.ShortName)] string? Name)
{
    public override string ToString() => nameof(CreatePersonalWidgetHttpRequest);
}

public sealed record UpdatePersonalWidgetHttpRequest(
    [param: MaxLength(TextLimits.ShortName)] string? Name, [param: Required] uint? Version)
{
    public override string ToString() => nameof(UpdatePersonalWidgetHttpRequest);
}

public sealed record DeletePersonalWidgetHttpRequest([param: Required] uint? Version);
public sealed record PersonalWidgetListHttpQuery(int? Page, int? PageSize, string? Sort, string? Search)
{
    public override string ToString() => nameof(PersonalWidgetListHttpQuery);
}

public sealed record CreatePersonalWidgetRequest(string? Name);
public sealed record UpdatePersonalWidgetRequest(Guid Id, string? Name, uint? Version);
public sealed record DeletePersonalWidgetRequest(Guid Id, uint? Version);
public sealed record PersonalWidgetResponse(Guid Id, string Name, uint Version);

public sealed record ListPersonalWidgetsRequest : PagedRequest
{
    public static IReadOnlyCollection<string> SortableFields { get; } = ["name", "createdAtUtc"];
}

internal sealed class ListPersonalWidgetsRequestValidator()
    : PagedRequestValidator<ListPersonalWidgetsRequest>(ListPersonalWidgetsRequest.SortableFields);

internal sealed class CreatePersonalWidgetRequestValidator : AbstractValidator<CreatePersonalWidgetRequest>
{
    public CreatePersonalWidgetRequestValidator() => RuleFor(request => request.Name).ShortName();
}

internal sealed class UpdatePersonalWidgetRequestValidator : AbstractValidator<UpdatePersonalWidgetRequest>
{
    public UpdatePersonalWidgetRequestValidator()
    {
        RuleFor(request => request.Name).ShortName();
        RuleFor(request => request.Version).NotNull().WithMessage(_ => ValidationTexts.Required);
    }
}

internal sealed class DeletePersonalWidgetRequestValidator : AbstractValidator<DeletePersonalWidgetRequest>
{
    public DeletePersonalWidgetRequestValidator() => RuleFor(request => request.Version)
        .NotNull().WithMessage(_ => ValidationTexts.Required);
}
