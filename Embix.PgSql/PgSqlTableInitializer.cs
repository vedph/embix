using Embix.Core;
using Npgsql;
using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace Embix.PgSql;

/// <summary>
/// PostgreSql table initializer. Tables are created in the first schema
/// of the connection's search path (usually <c>public</c>).
/// </summary>
/// <remarks>The schema requires PostgreSQL 15 or higher, and the
/// <c>pg_trgm</c> extension (shipped with the standard contrib modules).
/// If it is not yet installed in the target database, the user must have
/// the privilege to create it.</remarks>
/// <seealso cref="ITableInitializer" />
public class PgSqlTableInitializer : ITableInitializer
{
    private readonly IDbConnectionFactory _connFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PgSqlTableInitializer"/>
    /// class.
    /// </summary>
    /// <param name="factory">The connection factory.</param>
    /// <exception cref="ArgumentNullException">factory</exception>
    public PgSqlTableInitializer(IDbConnectionFactory factory)
    {
        _connFactory = factory
            ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Loads the SQL script with the specified resource name.
    /// </summary>
    /// <param name="name">The name (e.g. <c>Schema</c>).</param>
    /// <returns>SQL code.</returns>
    protected static string LoadScript(string name)
    {
        using StreamReader reader = new(
            Assembly.GetExecutingAssembly()
            .GetManifestResourceStream($"Embix.PgSql.Assets.{name}.pgsql")!,
            Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Gets the SQL code for tables definition.
    /// </summary>
    /// <returns>SQL code.</returns>
    protected virtual string GetSql() => LoadScript("Schema");

    /// <summary>
    /// Gets the SQL code for creating secondary indexes and constraints.
    /// </summary>
    /// <returns>SQL code.</returns>
    protected virtual string GetIndexesSql() => LoadScript("Indexes");

    /// <summary>
    /// Gets the SQL code for dropping secondary indexes and constraints.
    /// </summary>
    /// <returns>SQL code.</returns>
    protected virtual string GetDropIndexesSql() => LoadScript("DropIndexes");

    private void Execute(string sql)
    {
        using NpgsqlConnection connection =
            (NpgsqlConnection)_connFactory.GetConnection();
        connection.Open();
        using NpgsqlCommand cmd = new(sql, connection);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Initializes the database index tables by creating them with all their
    /// indexes if not present; if the tables are already present and
    /// <paramref name="clear"/> is true, they are truncated.
    /// </summary>
    /// <param name="clear">if set to <c>true</c>, truncate the index
    /// tables when present.</param>
    public void Initialize(bool clear)
    {
        StringBuilder sql = new(GetSql());
        sql.AppendLine().AppendLine(GetIndexesSql());
        // truncate both tables at once, so that the FK is respected;
        // no CASCADE, so that we never truncate tables outside the index
        if (clear) sql.AppendLine("TRUNCATE eix_occurrence, eix_token RESTART IDENTITY;");
        Execute(sql.ToString());
    }

    /// <summary>
    /// Drops the secondary indexes and the foreign key of the index tables.
    /// Call this before bulk loading a large index, and then call
    /// <see cref="CreateIndexes"/> after the load: building indexes once
    /// is much faster than updating them at each insert.
    /// </summary>
    public void DropIndexes() => Execute(GetDropIndexesSql());

    /// <summary>
    /// Creates the secondary indexes and the foreign key of the index tables
    /// when not present, synchronizes the token ID identity, and refreshes
    /// the tables statistics.
    /// </summary>
    public void CreateIndexes() => Execute(GetIndexesSql());
}
