using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence;

/// <summary>Traduce el choque de un índice único de PostgreSQL sin registrar valores sensibles.</summary>
internal static class UniqueViolations
{
    public static UniqueConstraintViolationException? Translate(Exception exception) =>
        exception is DbUpdateException
        {
            InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } unique,
        }
            ? new UniqueConstraintViolationException(
                $"Another request saved a row with the same unique key first ({unique.ConstraintName}).", exception)
            {
                ConstraintName = unique.ConstraintName,
            }
            : null;
}
