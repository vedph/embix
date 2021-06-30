using Fusi.Tools.Data;
using Npgsql;
using SqlKata.Compilers;
using System.Data;

namespace Embix.Search.PgSql
{
    /// <summary>
    /// Paged query builder base class for PgSql.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <seealso cref="PagedQueryBuilder&lt;TRequest&gt;" />
    public abstract class PgSqlPagedQueryBuilder<TRequest>
        : PagedQueryBuilder<TRequest> where TRequest : PagingOptions
    {
        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="PgSqlPagedQueryBuilder{TRequest}"/> class.
        /// </summary>
        /// <param name="connString">The connection string.</param>
        protected PgSqlPagedQueryBuilder(string connString) : base(connString)
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
