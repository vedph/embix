using Npgsql;
using SqlKata.Compilers;
using System.Data;

namespace Embix.Search.PgSql
{
    /// <summary>
    /// Non-paged query builder base class for PostgreSql.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <seealso cref="NonPagedQueryBuilder&lt;TRequest&gt;" />
    public abstract class PgSqlNonPagedQueryBuilder<TRequest>
        : NonPagedQueryBuilder<TRequest>
    {
        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="PgSqlNonPagedQueryBuilder{TRequest}"/> class.
        /// </summary>
        /// <param name="connString">The connection string.</param>
        protected PgSqlNonPagedQueryBuilder(string connString) : base(connString)
        {
        }

        /// <summary>
        /// Gets the connection.
        /// </summary>
        /// <param name="connString">The connection string.</param>
        /// <returns>The connection.</returns>
        protected override IDbConnection GetConnection(string connString)
            => new NpgsqlConnection(connString);

        /// <summary>
        /// Gets the SQL compiler.
        /// </summary>
        /// <returns>The compiler.</returns>
        protected override Compiler GetSqlCompiler()
            => new PostgresCompiler();
    }
}
