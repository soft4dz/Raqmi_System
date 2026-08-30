using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace RaqmiSystem.Infrastructure.Persistence;

/// <summary>
/// Provider-aware classification of persistence failures so services can distinguish an
/// expected concurrency outcome - a unique-index/constraint violation, or a transaction the
/// server refused to serialize - which is to be surfaced as a 409 Conflict or retried, from any
/// other failure (which must keep propagating).
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

    /// <summary>
    /// True when PostgreSQL refused to serialize concurrent transactions: SQLSTATE 40001, raised
    /// when a Serializable transaction's reads no longer hold at commit time, or 40P01 for a
    /// detected deadlock. Both mean the transaction was rolled back whole - nothing was written -
    /// so a caller that opened it deliberately (see
    /// <c>UserAdministrationService.RunGuardedMutationAsync</c>) can answer with a retryable
    /// conflict instead of letting a 500 escape for what is a normal outcome under contention.
    /// Typed as an <see cref="Exception"/> extension because the abort can surface either as a
    /// <see cref="DbUpdateException"/> (from SaveChanges) or as the raw provider exception (from
    /// ExecuteUpdate or from the commit itself).
    ///
    /// SQLite (integration-test provider) has no serialization abort - it serializes writers with
    /// locks rather than aborting them - but it refuses the very same situation in its own way:
    /// a connection trying to write while another one already holds the reserved lock is turned
    /// away with SQLITE_BUSY/SQLITE_LOCKED ("database is locked", "database table is locked")
    /// instead of being made to wait, because waiting while holding a read lock would deadlock the
    /// two connections. The outcome is the one this method is about - the transaction wrote
    /// nothing and the caller may retry - so it is classified the same way, matched on the message
    /// because this assembly does not reference Microsoft.Data.Sqlite.
    /// </summary>
    public static bool IsSerializationFailure(this Exception exception)
    {
        var baseException = exception.GetBaseException();

        if (baseException is PostgresException postgresException)
        {
            return postgresException.SqlState is PostgresErrorCodes.SerializationFailure
                or PostgresErrorCodes.DeadlockDetected;
        }

        return baseException.Message.Contains("database is locked", StringComparison.OrdinalIgnoreCase)
            || baseException.Message.Contains("database table is locked", StringComparison.OrdinalIgnoreCase);
    }
}
