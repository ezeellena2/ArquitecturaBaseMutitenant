namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>Una prueba de posesión autoriza una sola acción concreta.</summary>
public enum ReauthAction
{
    RemoveMethod,
    MakePrimary,
    DeleteAccount,
    CancelDeletion,
    AddEmail,
    LinkGoogle,
}
