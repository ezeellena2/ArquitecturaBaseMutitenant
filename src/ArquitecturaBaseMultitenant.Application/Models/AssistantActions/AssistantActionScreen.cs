namespace ArquitecturaBaseMultitenant.Application.Models.AssistantActions;

/// <summary>
/// Las pantallas del núcleo donde se confirma una acción (lista cerrada, ADR 0068 §2). El módulo que propone traduce cada una
/// a su ruta; una pantalla nueva suma su valor y su traducción.
/// </summary>
public enum AssistantActionScreen
{
    /// <summary>El diálogo «Invitar usuario», sobre el Inicio de la organización.</summary>
    InviteUser,
    /// <summary>La ficha de una persona de la organización, con su confirmación de quitar.</summary>
    UserDetail,
    /// <summary>El editor de roles: un rol nuevo o uno que ya existe.</summary>
    RoleEditor,
    /// <summary>El listado de roles, con su confirmación de dar de baja.</summary>
    Roles,
    /// <summary>El editor de un catálogo a mano, con sus opciones (Formularios).</summary>
    CatalogEditor,
}
