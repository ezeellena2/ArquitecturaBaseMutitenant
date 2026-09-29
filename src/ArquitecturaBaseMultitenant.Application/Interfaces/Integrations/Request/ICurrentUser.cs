namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;

/// <summary>Lado activo de una sesión autenticada.</summary>
public enum AccessKind
{
    Consumer,
    Business,
    Platform,
}

/// <summary>Identidad de la petición actual; un worker sin sesión expone valores nulos.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    AccessKind? Access { get; }
}
