using Embix.Core;
using Npgsql;
using System;
using System.Data;

namespace Embix.PgSql
{
    /// <summary>
    /// PostgreSql connection factory.
    /// </summary>
    /// <seealso cref="IDbConnectionFactory" />
    public class PgSqlDbConnectionFactory : IDbConnectionFactory
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
        public PgSqlDbConnectionFactory(string connString)
        {
            ConnectionString = connString
                ?? throw new ArgumentNullException(nameof(connString));
        }

        /// <summary>
        /// Gets a new connection to the database.
        /// </summary>
        /// <returns>Connection.</returns>
        public IDbConnection GetConnection() => new NpgsqlConnection(ConnectionString);
    }
}
