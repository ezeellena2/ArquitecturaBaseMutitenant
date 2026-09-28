namespace ArquitecturaBaseMultitenant.Domain.Common;

/// <summary>Marca de borrado lógico completada por Infrastructure.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; }

    DateTime? DeletedAtUtc { get; }

    Guid? DeletedBy { get; }
}
