using System.Reflection;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using FluentValidation;
using FluentValidation.Validators;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Exige que cada regla de cada validador de Application tenga su propio mensaje de <c>ValidationTexts</c>. Una regla
/// sin <c>WithMessage</c> le mostraría a la persona el texto genérico de FluentValidation, en inglés o con el nombre
/// de la propiedad (B5). Recorre <c>CreateDescriptor()</c>, también dentro de <c>Include</c> y <c>ChildRules</c>.
/// </summary>
public sealed class ValidationMessageTests
{
    private static readonly Assembly ApplicationAssembly = typeof(ValidationRules).Assembly;

    [Fact]
    public void Every_validation_rule_reports_its_own_message()
    {
        var validators = ApplicationAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false }
                && typeof(IValidator).IsAssignableFrom(type))
            .ToArray();

        Assert.NotEmpty(validators);
        var missing = validators.SelectMany(type => MissingMessages(type.Name, Create(type))).Distinct().ToArray();
        Assert.True(missing.Length == 0, "Reglas sin mensaje propio:" + Environment.NewLine
            + string.Join(Environment.NewLine, missing));
    }

    [Fact]
    public void Detector_finds_a_rule_without_its_own_message_also_inside_child_rules()
    {
        Assert.Equal(["ProbeValidator.Name: NotEmptyValidator", "ProbeValidator.Code: MaximumLengthValidator"],
            MissingMessages(nameof(ProbeValidator), new ProbeValidator()));
        Assert.Empty(MissingMessages(nameof(CompleteProbeValidator), new CompleteProbeValidator()));
    }

    private static IEnumerable<string> MissingMessages(string owner, IValidator validator)
    {
        foreach (var rule in validator.CreateDescriptor().Rules)
        {
            foreach (var component in rule.Components)
            {
                if (component.Validator is IChildValidatorAdaptor)
                {
                    foreach (var missing in MissingMessages(owner, ChildValidator(component.Validator)))
                        yield return missing;
                    continue;
                }

                var defaultMessage = component.Validator.GetDefaultMessageTemplate(component.ErrorCode);
                if (component.GetUnformattedErrorMessage() == defaultMessage)
                    yield return $"{owner}.{rule.PropertyName}: {component.Validator.Name}";
            }
        }
    }

    /// <summary>El validador de un Include o de ChildRules: el adaptador lo devuelve con un contexto vacío.</summary>
    private static IValidator ChildValidator(IPropertyValidator adaptor)
    {
        var parent = adaptor.GetType().GenericTypeArguments[0];
        var context = Activator.CreateInstance(typeof(ValidationContext<>).MakeGenericType(parent), [null]);
        var method = adaptor.GetType().GetMethod("GetValidator", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("The child validator adaptor changed its shape.");
        return (IValidator)(method.Invoke(adaptor, [context, null])
            ?? throw new InvalidOperationException("The child validator depends on the value being validated."));
    }

    /// <summary>Los validadores reciben catálogos de datos de referencia u opciones; los demás tipos no se esperan.</summary>
    private static IValidator Create(Type type)
    {
        var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .OrderBy(candidate => candidate.GetParameters().Length).First();
        var catalog = new JsonReferenceDataCatalog();
        var arguments = constructor.GetParameters().Select(parameter => parameter.ParameterType switch
        {
            { IsGenericType: true } options when options.GetGenericTypeDefinition() == typeof(IOptions<>) =>
                typeof(Options).GetMethod(nameof(Options.Create))!.MakeGenericMethod(options.GenericTypeArguments[0])
                    .Invoke(null, [Activator.CreateInstance(options.GenericTypeArguments[0])]),
            var other when other.IsInstanceOfType(catalog) => catalog,
            var other => throw new InvalidOperationException($"{type.Name} needs an unexpected {other.Name}."),
        }).ToArray();
        return (IValidator)constructor.Invoke(arguments);
    }

    private sealed record ProbeRequest(string? Name, IReadOnlyList<ProbeItem> Items);

    private sealed record ProbeItem(string? Code);

    private sealed class ProbeValidator : AbstractValidator<ProbeRequest>
    {
        public ProbeValidator()
        {
            RuleFor(request => request.Name).NotEmpty();
            RuleForEach(request => request.Items).ChildRules(item => item.RuleFor(value => value.Code).MaximumLength(3));
        }
    }

    private sealed class CompleteProbeValidator : AbstractValidator<ProbeRequest>
    {
        public CompleteProbeValidator()
        {
            RuleFor(request => request.Name).NotEmpty().WithMessage(_ => "Mensaje propio");
            RuleForEach(request => request.Items).ChildRules(item =>
                item.RuleFor(value => value.Code).MaximumLength(3).WithMessage(_ => "Mensaje propio"));
        }
    }
}
