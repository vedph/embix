using System;
using Embix.Core;
using MySql.Data.MySqlClient;
using System.Data;

namespace Embix.MySql;

/// <summary>
/// A connection factory for MySql.
/// </summary>
/// <seealso cref="IDbConnectionFactory" />
public class MySqlDbConnectionFactory : IDbConnectionFactory
{
    /// <summary>
    /// The connection string.
    /// </summary>
    protected string ConnectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="MySqlDbConnectionFactory"/>
    /// class.
    /// </summary>
    /// <param name="connString">The connection string.</param>
    /// <exception cref="ArgumentNullException">connString</exception>
    public MySqlDbConnectionFactory(string connString)
    {
        ConnectionString = connString
            ?? throw new ArgumentNullException(nameof(connString));
    }

    /// <summary>
    /// Gets a new connection to the database.
    /// </summary>
    /// <returns>Connection.</returns>
    public IDbConnection GetConnection() => new MySqlConnection(ConnectionString);
}
