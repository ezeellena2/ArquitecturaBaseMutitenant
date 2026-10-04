using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence;

/// <summary>
/// Traduce el choque de un índice único o de una clave foránea de PostgreSQL, sin registrar valores sensibles. El
/// servicio compara el nombre con <c>UniqueKeys</c> o <c>ForeignKeys</c> y responde su error.
/// </summary>
internal static class ConstraintViolations
{
    public static Exception? Translate(Exception exception) =>
        exception is DbUpdateException { InnerException: PostgresException postgres }
            ? postgres.SqlState switch
            {
                PostgresErrorCodes.UniqueViolation => new UniqueConstraintViolationException(
                    $"Another request saved a row with the same unique key first ({postgres.ConstraintName}).",
                    exception)
                {
                    ConstraintName = postgres.ConstraintName,
                },
                // 23503 al guardar una fila que apunta a otra que ya no está; 23001 al borrar una fila que otra
                // apunta con ON DELETE RESTRICT.
                PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.RestrictViolation =>
                    new ForeignKeyViolationException(
                        $"Another request changed a row on the other side of a foreign key first ({postgres.ConstraintName}).",
                        exception)
                    {
                        ConstraintName = postgres.ConstraintName,
                    },
                _ => null,
            }
            : null;
}
