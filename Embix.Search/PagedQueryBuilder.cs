using Fusi.Tools.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Embix.Search;

/// <summary>
/// A paged query: the query for a page of data, and the query for the
/// total count of the items matching the same request.
/// </summary>
/// <param name="Data">The data page query.</param>
/// <param name="Count">The total count query.</param>
public sealed record PagedSqlQuery(SqlQuery Data, SqlQuery Count);

/// <summary>
/// Base class for paged query builders.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <seealso cref="QueryBuilder" />
public abstract class PagedQueryBuilder<TRequest>
    : QueryBuilder where TRequest : PagingOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PagedQueryBuilder{TRequest}"/>
    /// class.
    /// </summary>
    /// <param name="connString">The connection string.</param>
    protected PagedQueryBuilder(string connString) : base(connString)
    {
    }

    /// <summary>
    /// Builds the queries from the specified request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The data and count queries.</returns>
    public abstract PagedSqlQuery Build(TRequest request);

    /// <summary>
    /// Builds the paging clause (e.g. <c>LIMIT @p1 OFFSET @p2</c>) for the
    /// specified request, adding its values to <paramref name="parameters"/>.
    /// When the request's page size is 0, all the items are requested, and
    /// an empty string is returned.
    /// </summary>
    /// <param name="parameters">The parameters.</param>
    /// <param name="request">The request.</param>
    /// <returns>SQL code.</returns>
    /// <exception cref="ArgumentNullException">parameters or request
    /// </exception>
    protected static string BuildPaging(SqlParameters parameters,
        TRequest request)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(request);

        if (request.PageSize == 0) return "";
        return parameters.Dialect.BuildPaging(
            parameters.Add(request.PageSize),
            parameters.Add(request.GetSkipCount()));
    }

    /// <summary>
    /// Gets the specified page of items.
    /// </summary>
    /// <typeparam name="T">The type of the mapped items.</typeparam>
    /// <param name="request">The request.</param>
    /// <param name="map">The row mapping function.</param>
    /// <returns>Page.</returns>
    public DataPage<T> GetPage<T>(TRequest request, Func<IDataRecord, T> map)
    {
        ArgumentNullException.ThrowIfNull(request);

        PagedSqlQuery query = Build(request);
        int total = Scalar<int>(query.Count);
        List<T> items = total == 0 ? [] : Query(query.Data, map);
        return new DataPage<T>(request.PageNumber, request.PageSize, total,
            items);
    }

    /// <summary>
    /// Gets the specified page of items.
    /// </summary>
    /// <typeparam name="T">The type of the mapped items.</typeparam>
    /// <param name="request">The request.</param>
    /// <param name="map">The row mapping function.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Page.</returns>
    public async Task<DataPage<T>> GetPageAsync<T>(TRequest request,
        Func<IDataRecord, T> map, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        PagedSqlQuery query = Build(request);
        int total = await ScalarAsync<int>(query.Count, cancel)
            .ConfigureAwait(false);
        List<T> items = total == 0
            ? []
            : await QueryAsync(query.Data, map, cancel).ConfigureAwait(false);
        return new DataPage<T>(request.PageNumber, request.PageSize, total,
            items);
    }
}
