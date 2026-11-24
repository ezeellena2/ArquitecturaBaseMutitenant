namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>
/// Lo que un helper que corre dentro de la transacción deja para después del commit (AGENTS.md, regla 2): un aviso en el log
/// que depende de lo que se guardó, como el de un tope de correo que llegó a su marca. Corre recién cuando la transacción de
/// la unidad de trabajo se confirma, y un rollback lo descarta. Solo para lo que no escribe ni falla.
/// </summary>
public interface IAfterCommit
{
    /// <summary>Deja <paramref name="action"/> para cuando se confirme la transacción en curso.</summary>
    void Add(Action action);
}
