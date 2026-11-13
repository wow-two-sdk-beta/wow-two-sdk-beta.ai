using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Shared;

/// <summary>Classifies the store's failures.</summary>
internal static class AiStore
{
    /// <summary>Gets whether a save failed on a unique index or primary key (SQLSTATE 23505).</summary>
    /// <param name="exception">The failed save.</param>
    public static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
