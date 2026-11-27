using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Validation;

/// <summary>La revisión: el archivo o lo pegado, el modo y la versión de la pantalla; las cuatro listas van sin orden elegido.</summary>
internal sealed class FormCatalogImportPreviewRequestValidator : PagedRequestValidator<FormCatalogImportPreviewRequest>
{
    public FormCatalogImportPreviewRequestValidator() : base([])
    {
        RuleFor(request => request.Content).NotNull().OverridePropertyName("file").WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Mode).NotNull().WithMessage(_ => ValidationTexts.Required)
            .IsInEnum().WithMessage(_ => ValidationTexts.FilterInvalid);
        RuleFor(request => request.Version).NotNull().WithMessage(_ => ValidationTexts.Required);
    }
}

/// <summary>Aplicar: lo mismo que la revisión, con el SHA-256 de lo revisado y las confirmaciones (C7).</summary>
internal sealed class ApplyFormCatalogImportRequestValidator : AbstractValidator<ApplyFormCatalogImportRequest>
{
    public ApplyFormCatalogImportRequestValidator()
    {
        RuleFor(request => request.Content).NotNull().OverridePropertyName("file").WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Mode).NotNull().WithMessage(_ => ValidationTexts.Required)
            .IsInEnum().WithMessage(_ => ValidationTexts.FilterInvalid);
        RuleFor(request => request.Version).NotNull().WithMessage(_ => ValidationTexts.Required);
        // Un SHA-256 que no es el de estos bytes da FileChanged al aplicar, con el formato que tenga.
        RuleFor(request => request.Sha256).NotEmpty().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.ConfirmedDeactivations).GreaterThanOrEqualTo(0).When(request => request.ConfirmedDeactivations is not null)
            .WithMessage(_ => ValidationTexts.NumberRange);
    }
}

/// <summary>La plantilla: un tipo de valor conocido, o ninguno.</summary>
internal sealed class FormCatalogTemplateRequestValidator : AbstractValidator<FormCatalogTemplateRequest>
{
    public FormCatalogTemplateRequestValidator()
    {
        RuleFor(request => request.ValueType).IsInEnum().When(request => request.ValueType is not null)
            .WithMessage(_ => ValidationTexts.FilterInvalid);
    }
}
