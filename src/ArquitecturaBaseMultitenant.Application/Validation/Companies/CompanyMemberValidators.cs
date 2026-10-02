using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Companies;

internal sealed class ListCompanyMembersRequestValidator : AbstractValidator<ListCompanyMembersRequest>
{
    public ListCompanyMembersRequestValidator()
    {
        Include(new CompanyMemberPagedRequestValidator());
        RuleFor(request => request.State).Must(state => state is null || Enum.IsDefined(state.Value))
            .WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.RoleId).Must(roleId => roleId is null || roleId != Guid.Empty)
            .WithMessage(_ => ValidationTexts.Required);
    }
}

internal sealed class CompanyMemberPagedRequestValidator : PagedRequestValidator<ListCompanyMembersRequest>
{
    public CompanyMemberPagedRequestValidator() : base(ListCompanyMembersRequest.SortableFields) { }
}

internal sealed class AddCompanyMemberRequestValidator : AbstractValidator<AddCompanyMemberRequest>
{
    public AddCompanyMemberRequestValidator()
    {
        RuleFor(request => request.MemberId).NotNull().WithMessage(_ => ValidationTexts.Required)
            .Must(memberId => memberId is not null && memberId != Guid.Empty)
            .WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.RoleIds).NotNull().WithMessage(_ => ValidationTexts.Required);
        RuleForEach(request => request.RoleIds).NotEmpty().WithMessage(_ => ValidationTexts.Required);
    }
}

internal sealed class UpdateCompanyMemberRolesRequestValidator : AbstractValidator<UpdateCompanyMemberRolesRequest>
{
    public UpdateCompanyMemberRolesRequestValidator()
    {
        RuleFor(request => request.MemberId).NotEmpty().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.RoleIds).NotNull().WithMessage(_ => ValidationTexts.Required);
        RuleForEach(request => request.RoleIds).NotEmpty().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Version).NotNull().WithMessage(_ => ValidationTexts.Required);
    }
}

internal sealed class ChangeCompanyMemberStatusRequestValidator : AbstractValidator<ChangeCompanyMemberStatusRequest>
{
    public ChangeCompanyMemberStatusRequestValidator()
    {
        RuleFor(request => request.MemberId).NotEmpty().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Status).NotNull().WithMessage(_ => ValidationTexts.Required)
            .Must(status => status is not null && Enum.IsDefined(status.Value))
            .WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Version).NotNull().WithMessage(_ => ValidationTexts.Required);
    }
}
