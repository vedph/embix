using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Embix.Search;

/// <summary>
/// Base class for a non-paged query builder.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <seealso cref="QueryBuilder" />
public abstract class NonPagedQueryBuilder<TRequest> : QueryBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NonPagedQueryBuilder{TRequest}"/>
    /// class.
    /// </summary>
    /// <param name="connString">The connection string.</param>
    protected NonPagedQueryBuilder(string connString) : base(connString)
    {
    }

    /// <summary>
    /// Builds the query from the specified request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The query.</returns>
    public abstract SqlQuery Build(TRequest request);

    /// <summary>
    /// Builds and executes the query for the specified request.
    /// </summary>
    /// <typeparam name="T">The type of the mapped items.</typeparam>
    /// <param name="request">The request.</param>
    /// <param name="map">The row mapping function.</param>
    /// <returns>Items.</returns>
    public List<T> Get<T>(TRequest request, Func<IDataRecord, T> map) =>
        Query(Build(request), map);

    /// <summary>
    /// Builds and executes the query for the specified request.
    /// </summary>
    /// <typeparam name="T">The type of the mapped items.</typeparam>
    /// <param name="request">The request.</param>
    /// <param name="map">The row mapping function.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Items.</returns>
    public Task<List<T>> GetAsync<T>(TRequest request, Func<IDataRecord, T> map,
        CancellationToken cancel = default) =>
        QueryAsync(Build(request), map, cancel);
}
