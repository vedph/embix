using SqlKata.Compilers;
using SqlKata.Execution;
using System;
using System.Data;

namespace Embix.Search;

/// <summary>
/// Query builder base class.
/// </summary>
public abstract class QueryBuilder
{
    protected readonly string _connString;
    private QueryFactory _qf;

    /// <summary>
    /// Gets the query factory.
    /// </summary>
    public QueryFactory QueryFactory
    {
        get
        {
            if (_qf != null) return _qf;
            _qf = new QueryFactory(
                GetConnection(_connString),
                GetSqlCompiler());
            return _qf;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryBuilder"/> class.
    /// </summary>
    /// <param name="connString">The connection string.</param>
    /// <exception cref="ArgumentNullException">connString</exception>
    protected QueryBuilder(string connString)
    {
        _connString = connString
            ?? throw new ArgumentNullException(nameof(connString));
    }

    /// <summary>
    /// Gets the connection.
    /// </summary>
    /// <param name="connString">The connection string.</param>
    /// <returns>The connection.</returns>
    protected abstract IDbConnection GetConnection(string connString);

    /// <summary>
    /// Gets the SQL compiler.
    /// </summary>
    /// <returns>The SQL compiler.</returns>
    protected abstract Compiler GetSqlCompiler();
}
