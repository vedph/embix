using MySql.Data.MySqlClient;
using SqlKata.Compilers;
using System.Data;

namespace Embix.Search.MySql;

/// <summary>
/// Non-paged query builder base class for MySql.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <seealso cref="NonPagedQueryBuilder&lt;TRequest&gt;" />
public abstract class MySqlNonPagedQueryBuilder<TRequest>
    : NonPagedQueryBuilder<TRequest>
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="MySqlNonPagedQueryBuilder{TRequest}"/> class.
    /// </summary>
    /// <param name="connString">The connection string.</param>
    protected MySqlNonPagedQueryBuilder(string connString) : base(connString)
    {
    }

    /// <summary>
    /// Gets the connection.
    /// </summary>
    /// <param name="connString">The connection string.</param>
    /// <returns>The connection.</returns>
    protected override IDbConnection GetConnection(string connString)
        => new MySqlConnection(connString);

    /// <summary>
    /// Gets the SQL compiler.
    /// </summary>
    /// <returns>The compiler.</returns>
    protected override Compiler GetSqlCompiler()
        => new MySqlCompiler();
}
