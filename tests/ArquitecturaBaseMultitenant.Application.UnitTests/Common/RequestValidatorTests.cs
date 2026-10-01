using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Common;

/// <summary>
/// Comprueba que el puerto de validación ejecute todos los validadores registrados. Protege el uso de
/// nombres JSON al devolver errores por campo.
/// </summary>
public sealed class RequestValidatorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record Address(string? Street);

    private sealed record Request(Address Address, IReadOnlyList<string?> Permissions);

    private sealed record OtherRequest(string? Value);

    private sealed record TextRequest(string? Person, string? Organization, string? Short, string? Description, string? Long, string? Email);

    [Fact]
    public async Task One_request_validator_runs_every_registered_validator_and_uses_json_field_names()
    {
        var first = new InlineValidator<Request>();
        first.RuleFor(request => request.Address.Street).NotEmpty().WithMessage(_ => ValidationTexts.Required);
        var second = new InlineValidator<Request>();
        second.RuleForEach(request => request.Permissions).NotEmpty().WithMessage(_ => ValidationTexts.Required);
        var duplicate = new InlineValidator<Request>();
        duplicate.RuleFor(request => request.Address.Street).NotEmpty().WithMessage(_ => ValidationTexts.Required);

        using var fixture = new ServiceFixture<RequestValidatorTests>(first, second, duplicate);
        var error = Assert.IsType<ValidationError>(await fixture.Validator.ValidateAsync(
            new Request(new Address(null), [null]), Ct));

        Assert.Equal([ValidationTexts.Required], error.Errors["address.street"]);
        Assert.Equal([ValidationTexts.Required], error.Errors["permissions.0"]);
        Assert.NotNull(fixture.TimeProvider);
        Assert.NotNull(fixture.Logger);
    }

    [Fact]
    public async Task Valid_request_and_request_without_a_registered_validator_return_no_error()
    {
        var required = new InlineValidator<Request>();
        required.RuleFor(request => request.Address.Street).NotEmpty();
        using var fixture = new ServiceFixture<RequestValidatorTests>(required);

        Assert.Null(await fixture.Validator.ValidateAsync(new Request(new Address("Main Street"), []), Ct));
        Assert.Null(await fixture.Validator.ValidateAsync(new OtherRequest(null), Ct));
    }

    [Fact]
    public async Task Validator_dependencies_are_resolved_from_the_current_scope()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedDependency>();
        services.AddScoped<IValidator<Request>, ScopedDependentValidator>();
        services.AddScoped<IRequestValidator, RequestValidator>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<IRequestValidator>();

        Assert.IsType<ValidationError>(await validator.ValidateAsync(new Request(new Address(null), []), Ct));
    }

    [Fact]
    public void Application_registers_the_single_request_validator()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        var registration = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IRequestValidator));
        Assert.Equal(typeof(RequestValidator), registration.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, registration.Lifetime);
    }

    [Fact]
    public void Field_errors_preserve_the_business_code_and_attach_the_translated_message()
    {
        var cause = Error.Validation("Users.Email.Invalid", "Invalid email.");

        var error = FieldErrors.On(cause, "Email");

        Assert.Equal(cause.Code, error.Code);
        Assert.Equal([ErrorTexts.Find(cause.Code)], error.Errors["email"]);
        Assert.Equal([ValidationTexts.Required], FieldErrors.Validation("Permissions[0]", ValidationTexts.Required).Errors["permissions.0"]);
    }

    [Fact]
    public void Typed_text_rules_use_shared_limits_and_email_value_object()
    {
        var validator = new InlineValidator<TextRequest>();
        validator.RuleFor(request => request.Person).PersonName();
        validator.RuleFor(request => request.Organization).OrganizationName();
        validator.RuleFor(request => request.Short).ShortName();
        validator.RuleFor(request => request.Description).Description();
        validator.RuleFor(request => request.Long).LongText();
        validator.RuleFor(request => request.Email).ValidEmail();

        var valid = new TextRequest("Ana", "Empresa", "Nombre", null, null, "ana@ñandú.com.ar");
        Assert.True(validator.Validate(valid).IsValid);

        var invalid = new TextRequest(new string('p', TextLimits.PersonName + 1),
            new string('o', TextLimits.OrganizationName + 1),
            new string('s', TextLimits.ShortName + 1),
            new string('d', TextLimits.Description + 1),
            new string('l', TextLimits.LongText + 1), "sin-arroba");
        var failures = validator.Validate(invalid).Errors;

        Assert.Equal(6, failures.Count);
        Assert.Contains(failures, failure => failure.PropertyName == "Person");
        Assert.Contains(failures, failure => failure.PropertyName == "Organization");
        Assert.Contains(failures, failure => failure.PropertyName == "Short");
        Assert.Contains(failures, failure => failure.PropertyName == "Description");
        Assert.Contains(failures, failure => failure.PropertyName == "Long");
        Assert.Contains(failures, failure => failure.PropertyName == "Email" && failure.ErrorMessage == ValidationTexts.EmailInvalid);
    }

    private sealed class ScopedDependency;

    private sealed class ScopedDependentValidator : AbstractValidator<Request>
    {
        public ScopedDependentValidator(ScopedDependency dependency)
        {
            ArgumentNullException.ThrowIfNull(dependency);
            RuleFor(request => request.Address.Street).NotEmpty();
        }
    }
}
