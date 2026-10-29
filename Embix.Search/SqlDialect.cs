using System;
using System.Linq;

namespace Embix.Search;

/// <summary>
/// The SQL features which differ across RDBMS, as used by
/// <see cref="QueryTextClauseBuilder"/> and query builders. All the methods
/// receive SQL expressions (e.g. quoted column names) and parameter
/// placeholders, and return SQL code.
/// </summary>
public abstract class SqlDialect
{
    /// <summary>
    /// Gets the prefix for parameter placeholders in SQL code. Default is
    /// <c>@</c>.
    /// </summary>
    public virtual string ParameterPrefix => "@";

    /// <summary>
    /// Quotes a single part of an identifier.
    /// </summary>
    /// <param name="part">The part (e.g. a column name).</param>
    /// <returns>Quoted part.</returns>
    protected abstract string QuoteIdentifierPart(string part);

    /// <summary>
    /// Quotes the specified identifier, which can be qualified (e.g.
    /// <c>t.value</c>, quoted as <c>"t"."value"</c> in PostgreSQL). A
    /// <c>*</c> part is not quoted.
    /// </summary>
    /// <param name="name">The identifier.</param>
    /// <returns>Quoted identifier.</returns>
    /// <exception cref="ArgumentNullException">name</exception>
    public string QuoteIdentifier(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return string.Join(".", name.Split('.')
            .Select(p => p == "*" ? p : QuoteIdentifierPart(p)));
    }

    /// <summary>
    /// Builds a <c>LIKE</c> condition. The pattern uses the default escape
    /// character (<c>\</c>) in all the supported RDBMS.
    /// </summary>
    /// <param name="expression">The expression to match.</param>
    /// <param name="pattern">The pattern parameter placeholder.</param>
    /// <param name="caseInsensitive">True for a case-insensitive match.</param>
    /// <returns>SQL.</returns>
    public virtual string BuildLike(string expression, string pattern,
        bool caseInsensitive) => caseInsensitive
            ? $"LOWER({expression}) LIKE LOWER({pattern})"
            : $"{expression} LIKE {pattern}";

    /// <summary>
    /// Builds a regular expression match condition.
    /// </summary>
    /// <param name="expression">The expression to match.</param>
    /// <param name="pattern">The pattern parameter placeholder.</param>
    /// <returns>SQL.</returns>
    public abstract string BuildRegexMatch(string expression, string pattern);

    /// <summary>
    /// Builds an expression converting <paramref name="expression"/> into
    /// a number, for numeric comparisons.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <returns>SQL.</returns>
    public abstract string BuildNumber(string expression);

    /// <summary>
    /// Builds a fuzzy match condition, true when the similarity between
    /// <paramref name="expression"/> and <paramref name="text"/> is at least
    /// <paramref name="threshold"/>.
    /// </summary>
    /// <param name="expression">The expression to match.</param>
    /// <param name="text">The text parameter placeholder.</param>
    /// <param name="thresholdParameter">The threshold parameter placeholder.
    /// </param>
    /// <param name="threshold">The threshold value (0-1), which may be used
    /// to pick the best strategy.</param>
    /// <returns>SQL.</returns>
    public abstract string BuildSimilarity(string expression, string text,
        string thresholdParameter, double threshold);

    /// <summary>
    /// Builds the paging clause appended to a query.
    /// </summary>
    /// <param name="limit">The limit parameter placeholder.</param>
    /// <param name="offset">The offset parameter placeholder.</param>
    /// <returns>SQL.</returns>
    public virtual string BuildPaging(string limit, string offset) =>
        $"LIMIT {limit} OFFSET {offset}";
}
