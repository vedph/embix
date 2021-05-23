using System;
using Embix.Core;
using MySql.Data.MySqlClient;
using System.Data;

namespace Embix.MySql
{
    /// <summary>
    /// A connection factory for MySql.
    /// </summary>
    /// <seealso cref="IDbConnectionFactory" />
    public class MySqlDbConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connString;

        /// <summary>
        /// Initializes a new instance of the <see cref="MySqlDbConnectionFactory"/>
        /// class.
        /// </summary>
        /// <param name="connString">The connection string.</param>
        /// <exception cref="ArgumentNullException">connString</exception>
        public MySqlDbConnectionFactory(string connString)
        {
            _connString = connString
                ?? throw new System.ArgumentNullException(nameof(connString));
        }

        /// <summary>
        /// Gets a new connection to the database.
        /// </summary>
        /// <returns>Connection.</returns>
        public IDbConnection GetConnection() => new MySqlConnection(_connString);
    }
}
