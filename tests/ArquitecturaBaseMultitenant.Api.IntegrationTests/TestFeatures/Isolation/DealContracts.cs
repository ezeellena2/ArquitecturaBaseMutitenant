using System.ComponentModel.DataAnnotations;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Common;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;

public sealed record CreateDealHttpRequest([param: MaxLength(TextLimits.Description)] string? Summary)
{
    public override string ToString() => nameof(CreateDealHttpRequest);
}

public sealed record TransitionDealHttpRequest([param: Required] uint? Version);
public sealed record CreateDealRequest(string? Summary);
public sealed record TransitionDealRequest(Guid Id, uint? Version, DealStatus Status);
public sealed record DealResponse(Guid Id, string Summary, string? ConsumerName, DealStatus Status, uint Version);
public sealed record PosterResponse(Guid Id);
public sealed record IsolationListHttpQuery(int? Page, int? PageSize);
public sealed record IsolationListRequest : PagedRequest;

internal sealed class IsolationListRequestValidator() : PagedRequestValidator<IsolationListRequest>([]);

internal sealed class CreateDealRequestValidator : AbstractValidator<CreateDealRequest>
{
    public CreateDealRequestValidator() => RuleFor(request => request.Summary).Required().Description();
}

internal sealed class TransitionDealRequestValidator : AbstractValidator<TransitionDealRequest>
{
    public TransitionDealRequestValidator() => RuleFor(request => request.Version)
        .NotNull().WithMessage(_ => ValidationTexts.Required);
}
