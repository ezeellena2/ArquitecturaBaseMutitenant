using System.Text.Json;
using ArquitecturaBaseMultitenant.Domain.Results;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.Common.Validation;

/// <summary>Resuelve todos los IValidator del pedido y agrupa sus mensajes por campo JSON.</summary>
internal sealed class RequestValidator(IServiceProvider serviceProvider) : IRequestValidator
{
    public Task<ValidationError?> ValidateAsync<TRequest>(TRequest request, CancellationToken cancellationToken) =>
        ValidateAsync(request, serviceProvider.GetServices<IValidator<TRequest>>(), cancellationToken);

    internal static async Task<ValidationError?> ValidateAsync<TRequest>(
        TRequest request,
        IEnumerable<IValidator<TRequest>> validators,
        CancellationToken cancellationToken)
    {
        var context = new ValidationContext<TRequest>(request);
        var failures = new List<ValidationFailure>();

        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count == 0)
        {
            return null;
        }

        var errors = failures
            .GroupBy(failure => ToFieldName(failure.PropertyName), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

        return new ValidationError(errors);
    }

    internal static string ToFieldName(string propertyName) =>
        string.Join('.', propertyName.Replace('[', '.').Replace("]", string.Empty, StringComparison.Ordinal)
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(JsonNamingPolicy.CamelCase.ConvertName));
}
