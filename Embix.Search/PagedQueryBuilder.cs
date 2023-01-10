using Fusi.Tools.Data;
using SqlKata;
using System;

namespace Embix.Search;

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
    /// <returns>Tuple where 1=query and 2=count query.</returns>
    public abstract Tuple<Query, Query> Build(TRequest request);
}
