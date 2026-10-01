using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

/// <summary>
/// Reúne los errores que se devuelven cuando el acceso elegido no permite una operación
/// o la persona no pertenece a la organización. Sus códigos permiten identificar y traducir
/// el motivo del rechazo; la API convierte estos errores en respuestas HTTP 403.
/// </summary>
public static class AccessErrors
{
    // Códigos estables: los usan las traducciones, las respuestas de la API y los tests.
    public const string WrongCode = "Tenancy.Access.Wrong";
    public const string NotMemberCode = "Tenancy.Access.NotMember";
    public const string ConsumerCannotCreateBusinessCode = "Tenancy.Access.ConsumerCannotCreateBusiness";

    // El acceso actual (persona, empresa o plataforma) no está habilitado para esta operación.
    public static readonly Error Wrong = Error.Forbidden(WrongCode, "The selected access cannot perform this operation.");
    // La persona no tiene una membresía que le permita entrar en la organización elegida.
    public static readonly Error NotMember = Error.Forbidden(NotMemberCode, "The user is not a member of this organization.");
    // El acceso como persona no permite crear una organización de empresa.
    public static readonly Error ConsumerCannotCreateBusiness = Error.Forbidden(ConsumerCannotCreateBusinessCode, "The consumer access cannot create a business.");
}
