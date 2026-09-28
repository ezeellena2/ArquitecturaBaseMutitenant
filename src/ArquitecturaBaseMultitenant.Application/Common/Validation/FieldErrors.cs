using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Common.Validation;

/// <summary>Errores de negocio que se muestran bajo un campo del formulario.</summary>
internal static class FieldErrors
{
    public static ValidationError On(Error error, string field)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentException.ThrowIfNullOrWhiteSpace(field);

        return new ValidationError(
            error.Code,
            error.Description,
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [RequestValidator.ToFieldName(field)] = [ErrorTexts.Find(error.Code) ?? error.Description],
            });
    }

    public static ValidationError Validation(string field, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);

        return new ValidationError(new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [RequestValidator.ToFieldName(field)] = [message],
        });
    }
}
