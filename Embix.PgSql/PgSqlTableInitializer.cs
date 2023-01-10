using Embix.Core;
using Npgsql;
using System;
using System.Data;
using System.IO;
using System.Reflection;
using System.Text;

namespace Embix.PgSql;

/// <summary>
/// PostgreSql table initializer.
/// </summary>
/// <seealso cref="ITableInitializer" />
public class PgSqlTableInitializer : ITableInitializer
{
    private readonly IDbConnectionFactory _connFactory;

    public PgSqlTableInitializer(IDbConnectionFactory factory)
    {
        _connFactory = factory
            ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Gets the SQL code for tables definition.
    /// </summary>
    /// <returns>SQL code.</returns>
    protected virtual string GetSql()
    {
        using StreamReader reader = new(
            Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Embix.PgSql.Assets.Schema.pgsql")!,
            Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static bool TableExists(string table, IDbConnection connection)
    {
        IDbCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT EXISTS(" +
            "SELECT FROM pg_tables " +
            $"WHERE tablename = '{table}' AND schemaname='public'" +
            ");";
        return (bool)cmd.ExecuteScalar();
    }

    /// <summary>
    /// Initializes the database index tables by creating them if not
    /// present; if the tables are already present and <paramref name="clear" />
    /// is true, they are truncated.
    /// </summary>
    /// <param name="clear">if set to <c>true</c>, truncate the index
    /// tables when present.</param>
    public void Initialize(bool clear)
    {
        using NpgsqlConnection connection =
            (NpgsqlConnection)_connFactory.GetConnection();
        connection.Open();

        if (!TableExists("eix_token", connection))
        {
            NpgsqlCommand cmd = new(GetSql(), connection);
            cmd.ExecuteNonQuery();
        }
        else if (clear)
        {
            NpgsqlCommand cmd = new(
                "TRUNCATE TABLE eix_occurrence RESTART IDENTITY CASCADE;\n" +
                "TRUNCATE TABLE eix_token RESTART IDENTITY CASCADE;\n", connection);
            cmd.ExecuteNonQuery();
        }
        connection.Close();
    }
}
