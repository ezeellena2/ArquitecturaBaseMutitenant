using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Regla de confirmación compartida por UnitOfWork y sus dobles de prueba.</summary>
public static class CommitPolicyExtensions
{
    public static bool Commits(this CommitPolicy policy, Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess || policy == CommitPolicy.OnAnyResult;
    }

    public static void ThrowIfUndefined(this CommitPolicy policy)
    {
        if (!Enum.IsDefined(policy))
        {
            throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown commit policy.");
        }
    }
}
