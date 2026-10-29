using Fusi.Tools.Data;
using MySql.Data.MySqlClient;
using System.Data.Common;

namespace Embix.Search.MySql;

/// <summary>
/// Paged query builder base class for MySql.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <seealso cref="PagedQueryBuilder&lt;TRequest&gt;" />
public abstract class MySqlPagedQueryBuilder<TRequest>
    : PagedQueryBuilder<TRequest> where TRequest : PagingOptions
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="MySqlPagedQueryBuilder{TRequest}"/> class.
    /// </summary>
    /// <param name="connString">The connection string.</param>
    protected MySqlPagedQueryBuilder(string connString) : base(connString)
    {
    }

    /// <summary>
    /// Gets the connection.
    /// </summary>
    /// <param name="connString">The connection string.</param>
    /// <returns>The connection.</returns>
    protected override DbConnection GetConnection(string connString)
        => new MySqlConnection(connString);

    /// <summary>
    /// Creates the SQL dialect.
    /// </summary>
    /// <returns>The dialect.</returns>
    protected override SqlDialect CreateDialect()
        => new MySqlDialect();
}
