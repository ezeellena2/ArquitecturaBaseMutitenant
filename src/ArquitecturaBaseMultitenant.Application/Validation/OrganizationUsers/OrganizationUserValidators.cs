using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.OrganizationUsers;

internal sealed class ListOrganizationUsersRequestValidator : PagedRequestValidator<ListOrganizationUsersRequest>
{
    public ListOrganizationUsersRequestValidator() : base(ListOrganizationUsersRequest.SortableFields)
    {
        RuleFor(request => request.State).Must(state => state is null || Enum.IsDefined(state.Value))
            .WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.CompanyId).Must(id => id is null || id != Guid.Empty)
            .WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request).Must(request => !(request.WithoutCompany && request.CompanyId is not null))
            .WithMessage(_ => ValidationTexts.Required);
    }
}

internal sealed class InviteOrganizationUserRequestValidator : AbstractValidator<InviteOrganizationUserRequest>
{
    public InviteOrganizationUserRequestValidator()
    {
        RuleFor(request => request.Email).NotNull()
            .WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.RoleIds).NotNull().WithMessage(_ => ValidationTexts.Required);
        RuleForEach(request => request.RoleIds).NotEmpty().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Companies).NotNull().WithMessage(_ => ValidationTexts.Required);
        RuleForEach(request => request.Companies).ChildRules(company =>
        {
            company.RuleFor(item => item.CompanyId).NotEmpty().WithMessage(_ => ValidationTexts.Required);
            company.RuleFor(item => item.RoleIds).NotNull().WithMessage(_ => ValidationTexts.Required);
            company.RuleForEach(item => item.RoleIds).NotEmpty().WithMessage(_ => ValidationTexts.Required);
        });
        RuleFor(request => request.Companies).Must(companies => companies is null
            || companies.Select(item => item.CompanyId).Distinct().Count() == companies.Count)
            .WithMessage(_ => ValidationTexts.Required);
    }
}

internal sealed class UpdateOrganizationUserRequestValidator : AbstractValidator<UpdateOrganizationUserRequest>
{
    public UpdateOrganizationUserRequestValidator()
    {
        RuleFor(request => request.MemberId).NotEmpty().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Version).NotNull().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.RoleIds).NotNull().WithMessage(_ => ValidationTexts.Required);
        RuleForEach(request => request.RoleIds).NotEmpty().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Companies).NotNull().WithMessage(_ => ValidationTexts.Required);
        RuleForEach(request => request.Companies).ChildRules(company =>
        {
            company.RuleFor(item => item.CompanyId).NotEmpty().WithMessage(_ => ValidationTexts.Required);
            company.RuleFor(item => item.RoleIds).NotNull().WithMessage(_ => ValidationTexts.Required);
            company.RuleForEach(item => item.RoleIds).NotEmpty().WithMessage(_ => ValidationTexts.Required);
        });
        RuleFor(request => request.Companies).Must(companies => companies is null
            || companies.Select(item => item.CompanyId).Distinct().Count() == companies.Count)
            .WithMessage(_ => ValidationTexts.Required);
    }
}

internal sealed class ChangeOrganizationUserStatusRequestValidator : AbstractValidator<ChangeOrganizationUserStatusRequest>
{
    public ChangeOrganizationUserStatusRequestValidator()
    {
        RuleFor(request => request.MemberId).NotEmpty().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.IsActive).NotNull().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Version).NotNull().WithMessage(_ => ValidationTexts.Required);
    }
}
