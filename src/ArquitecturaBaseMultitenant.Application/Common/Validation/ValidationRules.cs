using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Common.Validation;

/// <summary>Reglas reutilizables con mensajes traducidos y catálogos inyectados en cada validador.</summary>
public static class ValidationRules
{
    public static IRuleBuilderOptions<T, TProperty> Required<T, TProperty>(this IRuleBuilder<T, TProperty> ruleBuilder) =>
        ruleBuilder.NotEmpty().WithMessage(_ => ValidationTexts.Required);

    public static IRuleBuilderOptions<T, string?> MaxLength<T>(this IRuleBuilder<T, string?> ruleBuilder, int maxLength) =>
        ruleBuilder.MaximumLength(maxLength).WithMessage(_ => ValidationTexts.MaxLength);

    public static IRuleBuilderOptions<T, string?> PersonName<T>(this IRuleBuilderInitial<T, string?> ruleBuilder) =>
        ruleBuilder.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(_ => ValidationTexts.Required)
            .MaximumLength(TextLimits.PersonName).WithMessage(_ => ValidationTexts.MaxLength);

    public static IRuleBuilderOptions<T, string?> OrganizationName<T>(this IRuleBuilderInitial<T, string?> ruleBuilder) =>
        ruleBuilder.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(_ => ValidationTexts.Required)
            .MaximumLength(TextLimits.OrganizationName).WithMessage(_ => ValidationTexts.MaxLength);

    public static IRuleBuilderOptions<T, string?> ShortName<T>(this IRuleBuilderInitial<T, string?> ruleBuilder) =>
        ruleBuilder.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(_ => ValidationTexts.Required)
            .MaximumLength(TextLimits.ShortName).WithMessage(_ => ValidationTexts.MaxLength);

    public static IRuleBuilderOptions<T, string?> Description<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder.MaximumLength(TextLimits.Description).WithMessage(_ => ValidationTexts.MaxLength);

    public static IRuleBuilderOptions<T, string?> LongText<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder.MaximumLength(TextLimits.LongText).WithMessage(_ => ValidationTexts.MaxLength);

    public static IRuleBuilderOptions<T, string?> ValidEmail<T>(this IRuleBuilderInitial<T, string?> ruleBuilder) =>
        ruleBuilder.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(_ => ValidationTexts.Required)
            .Must(email => Email.Create(email).IsSuccess).WithMessage(_ => ValidationTexts.EmailInvalid);

    public static IRuleBuilderOptions<T, string?> ValidCurrency<T>(
        this IRuleBuilder<T, string?> ruleBuilder, ICurrencyCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return ruleBuilder.MustAsync(async (code, ct) =>
            code is null || (await catalog.FindAsync(code, ct))?.IsEnabled == true)
            .WithMessage(_ => ValidationTexts.CurrencyInvalid);
    }

    public static IRuleBuilderOptions<T, string?> ValidCountry<T>(
        this IRuleBuilder<T, string?> ruleBuilder, ICountryCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return ruleBuilder.MustAsync(async (code, ct) =>
            code is null || (await catalog.FindAsync(code, ct))?.IsEnabled == true)
            .WithMessage(_ => ValidationTexts.CountryInvalid);
    }

    public static IRuleBuilderOptions<T, string?> ValidTimeZone<T>(
        this IRuleBuilder<T, string?> ruleBuilder, ITimeZoneCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return ruleBuilder.MustAsync(async (id, ct) =>
            id is null || (await catalog.FindAsync(id, ct))?.IsEnabled == true)
            .WithMessage(_ => ValidationTexts.TimeZoneInvalid);
    }

    public static IRuleBuilderOptions<T, string?> ValidCulture<T>(
        this IRuleBuilder<T, string?> ruleBuilder, ICultureCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return ruleBuilder.MustAsync(async (code, ct) =>
            code is null || (await catalog.FindAsync(code, ct))?.IsEnabled == true)
            .WithMessage(_ => ValidationTexts.CultureInvalid);
    }

    public static IRuleBuilderOptions<T, string?> ValidTaxIdType<T>(
        this IRuleBuilder<T, string?> ruleBuilder, ITaxIdTypeCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return ruleBuilder.MustAsync(async (code, ct) =>
            code is null || (await catalog.FindAsync(code, ct))?.IsEnabled == true)
            .WithMessage(_ => ValidationTexts.TaxIdTypeInvalid);
    }
}
