using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Users;

/// <summary>
/// Centraliza los errores de una cuenta inexistente, un nombre demasiado largo o un cambio de estado
/// inválido. Los servicios los devuelven como Result para que la API informe un motivo estable.
/// </summary>
public static class UserErrors
{
    public const string NotFoundCode = "Users.User.NotFound";
    public const string InvalidDisplayNameCode = "Users.User.InvalidDisplayName";
    public const string InvalidTransitionCode = "Users.User.InvalidTransition";

    public static readonly Error NotFound = Error.NotFound(NotFoundCode, "The user was not found.");
    public static readonly Error InvalidDisplayName = Error.Validation(InvalidDisplayNameCode, "The display name is too long.");
    public static readonly Error InvalidTransition = Error.Conflict(InvalidTransitionCode, "The account cannot change to that state.");
}
