using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Platform;

internal sealed class ListPlatformAccountsRequestValidator : PagedRequestValidator<ListPlatformAccountsRequest>
{
    public ListPlatformAccountsRequestValidator() : base(ListPlatformAccountsRequest.SortableFields)
    {
        RuleFor(r => r.State).IsInEnum().When(r => r.State is not null).WithMessage(_ => ValidationTexts.FilterInvalid);
        RuleFor(r => r.Kind).IsInEnum().When(r => r.Kind is not null).WithMessage(_ => ValidationTexts.FilterInvalid);
    }
}

internal sealed class ListPlatformRecoveriesRequestValidator : PagedRequestValidator<ListPlatformRecoveriesRequest>
{
    public ListPlatformRecoveriesRequestValidator() : base(ListPlatformRecoveriesRequest.SortableFields) =>
        RuleFor(r => r.Status).IsInEnum().When(r => r.Status is not null).WithMessage(_ => ValidationTexts.FilterInvalid);
}

/// <summary>Motivo de una acción del operador sobre una cuenta: suspender, reactivar, cerrar sesiones y la baja.</summary>
internal sealed class PlatformAccountReasonRequestValidator : AbstractValidator<PlatformAccountReasonRequest>
{
    public PlatformAccountReasonRequestValidator() => RuleFor(r => r.Reason).Required().Description();
}

/// <summary>Versión y motivo de una acción del operador: pedidos de recuperación y retiro de operadores.</summary>
internal sealed class PlatformAccountActionRequestValidator : AbstractValidator<PlatformAccountActionRequest>
{
    public PlatformAccountActionRequestValidator()
    {
        RuleFor(r => r.Version).NotNull().WithMessage(_ => ValidationTexts.Required);
        RuleFor(r => r.Reason).Required().Description();
    }
}
