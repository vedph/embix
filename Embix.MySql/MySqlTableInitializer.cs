using Embix.Core;
using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Reflection;
using System.Text;

namespace Embix.MySql;

/// <summary>
/// MySql table initializer.
/// </summary>
/// <seealso cref="ITableInitializer" />
public class MySqlTableInitializer : ITableInitializer
{
    private readonly IDbConnectionFactory _connFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MySqlTableInitializer"/>
    /// class.
    /// </summary>
    /// <param name="factory">The factory.</param>
    /// <exception cref="ArgumentNullException">factory</exception>
    public MySqlTableInitializer(IDbConnectionFactory factory)
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
            .GetManifestResourceStream("Embix.MySql.Assets.Schema.mysql")!,
            Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static bool TableExists(string table, IDbConnection connection)
    {
        DbConnectionStringBuilder builder = new()
        {
            ConnectionString = connection.ConnectionString
        };
        string? databaseName = builder["Database"] as string;
        if (databaseName == null) return false;

        IDbCommand cmd = connection.CreateCommand();
        // https://stackoverflow.com/questions/464474/check-if-a-sql-table-exists
        cmd.CommandText = "SELECT CASE WHEN EXISTS(" +
            "(SELECT * FROM information_schema.tables " +
            $"WHERE table_name = '{table}' AND table_schema='{databaseName}')" +
            ") THEN 1 ELSE 0 END;";
        long? n = (long?)cmd.ExecuteScalar();
        return n != null && n == 1;
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
        using MySqlConnection connection =
            (MySqlConnection)_connFactory.GetConnection();
        connection.Open();

        if (!TableExists("eix_token", connection))
        {
            // https://stackoverflow.com/questions/1324693/c-mysql-ado-net-delimiter-causing-syntax-error
            MySqlScript script = new(connection, GetSql());
            script.Execute();
        }
        else if (clear)
        {
            MySqlScript script = new(connection,
                "SET FOREIGN_KEY_CHECKS=0;\n" +
                "TRUNCATE TABLE eix_token;\n" +
                "TRUNCATE TABLE eix_occurrence;\n" +
                "SET FOREIGN_KEY_CHECKS=1;\n");
            script.Execute();
        }
        connection.Close();
    }
}
