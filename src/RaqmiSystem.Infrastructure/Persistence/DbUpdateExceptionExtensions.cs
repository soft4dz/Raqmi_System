using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace RaqmiSystem.Infrastructure.Persistence;

/// <summary>
/// Provider-aware classification of <see cref="DbUpdateException"/> so services can
/// distinguish a unique-index/constraint violation (a legitimate concurrency race to be
/// surfaced as a 409 Conflict, or retried) from any other persistence failure (which must
/// keep propagating).
/// </summary>
public static class DbUpdateExceptionExtensions
{
    /// <summary>
    /// True when the exception was caused by a unique-index/unique-constraint violation.
    /// PostgreSQL (production) is detected via <see cref="PostgresException.SqlState"/> ==
    /// 23505; SQLite (integration-test provider) surfaces the same condition as a
    /// SqliteException whose message starts with "UNIQUE constraint failed" - matched on the
    /// message because this assembly does not reference Microsoft.Data.Sqlite.
    /// </summary>
    /// <param name="exception">The save failure to classify.</param>
    /// <param name="constraintOrIndexName">
    /// Optional: when provided, a PostgreSQL violation only matches if it was raised by this
    /// specific constraint/index. SQLite error messages carry column lists rather than index
    /// names, so the name filter is not applied for SQLite.
    /// </param>
    public static bool IsUniqueViolation(this DbUpdateException exception, string? constraintOrIndexName = null)
    {
        if (exception.GetBaseException() is PostgresException postgresException)
        {
            if (postgresException.SqlState != PostgresErrorCodes.UniqueViolation)
            {
                return false;
            }

            return constraintOrIndexName is null
                || string.Equals(postgresException.ConstraintName, constraintOrIndexName, StringComparison.OrdinalIgnoreCase);
        }

        // SQLite test provider: SQLITE_CONSTRAINT_UNIQUE ("UNIQUE constraint failed: table.col, ...").
        return exception.GetBaseException().Message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase);
    }
}
