using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Common.Validation;

/// <summary>Errores de negocio que se muestran bajo un campo del formulario.</summary>
internal static class FieldErrors
{
    public static ValidationError On(Error error, string field)
    {
        ArgumentNullException.ThrowIfNull(error);
        return On(error, field, ErrorTexts.Find(error.Code) ?? error.Code);
    }

    /// <summary>
    /// El mismo error de negocio, con su código, y bajo el campo un texto ya armado con el dato que solo conoce el servicio (por
    /// ejemplo, el máximo que fija la plataforma al guardar).
    /// </summary>
    public static ValidationError On(Error error, string field, string message)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new ValidationError(
            error.Code,
            error.Description,
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [RequestValidator.ToFieldName(field)] = [message],
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
