using SqlKata;

namespace Embix.Search
{
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
        public abstract Query Build(TRequest request);
    }
}
