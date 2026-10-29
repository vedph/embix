using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;

namespace Embix.Search;

/// <summary>
/// A parameterized SQL query: SQL code with its parameters values.
/// </summary>
/// <param name="Sql">The SQL code.</param>
/// <param name="Parameters">The parameters, keyed by name (without the
/// placeholder prefix).</param>
public sealed record SqlQuery(string Sql,
    IReadOnlyDictionary<string, object?> Parameters)
{
    private static readonly IReadOnlyDictionary<string, object?> _noParameters =
        new Dictionary<string, object?>();

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlQuery"/> class
    /// without parameters.
    /// </summary>
    /// <param name="sql">The SQL code.</param>
    public SqlQuery(string sql) : this(sql, _noParameters)
    {
    }

    /// <summary>
    /// Creates a command for this query on the specified connection.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <returns>The command, to be disposed by the caller.</returns>
    /// <exception cref="ArgumentNullException">connection</exception>
    public DbCommand CreateCommand(DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        DbCommand command = connection.CreateCommand();
        command.CommandText = Sql;
        SqlParameters.ApplyTo(Parameters, command);
        return command;
    }

    /// <summary>
    /// Converts to string, for diagnostic purposes.
    /// </summary>
    /// <returns>SQL followed by parameters.</returns>
    public override string ToString() => Parameters.Count == 0
        ? Sql
        : Sql + " -- " + string.Join(", ",
            Parameters.Select(p => $"{p.Key}={p.Value ?? "NULL"}"));
}
