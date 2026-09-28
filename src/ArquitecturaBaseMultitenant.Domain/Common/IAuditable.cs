namespace ArquitecturaBaseMultitenant.Domain.Common;

/// <summary>Marcas completadas por Infrastructure al guardar.</summary>
public interface IAuditable
{
    DateTime CreatedAtUtc { get; }

    Guid? CreatedBy { get; }

    DateTime? ModifiedAtUtc { get; }

    Guid? ModifiedBy { get; }
}
