using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Roles;

internal sealed class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(request => request.Name).ShortName();
        RuleFor(request => request.Description).Description();
        RuleFor(request => request.Scope).NotNull().WithMessage(_ => ValidationTexts.Required)
            .Must(scope => scope is not null && Enum.IsDefined(scope.Value))
            .WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Permissions).ValidPermissions();
    }
}
