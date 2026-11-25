using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;

/// <summary>0064 §11: el valor de control de cada clave del anillo de la instalación, por su id. Nunca entrega la clave.</summary>
public interface IAiKeyRingControl
{
    /// <summary>Las claves del anillo de esta copia; si todavía no hay ninguna, crea la primera, como cualquier cifrado.</summary>
    AiKeyRingKeys ReadKeys();
    /// <summary>El control de una clave que esta copia puede usar.</summary>
    byte[] CreateControl(Guid keyId);
    /// <summary>Si el control guardado es el de la clave de este anillo con ese id; false también si no lo puede comprobar.</summary>
    bool Matches(Guid keyId, byte[] control);
}
