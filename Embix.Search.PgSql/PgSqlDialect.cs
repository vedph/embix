namespace Embix.Search.PgSql;

/// <summary>
/// PostgreSql SQL dialect.
/// </summary>
/// <remarks>The fuzzy matching (<see cref="BuildSimilarity"/>) requires the
/// <c>pg_trgm</c> extension, and is served by the trigram index of the
/// Embix PostgreSql schema.</remarks>
/// <seealso cref="SqlDialect" />
public class PgSqlDialect : SqlDialect
{
    /// <summary>
    /// Gets or sets the similarity threshold used by the <c>%</c> operator
    /// of <c>pg_trgm</c>, i.e. the value of its
    /// <c>pg_trgm.similarity_threshold</c> setting. Default is 0.3, the
    /// <c>pg_trgm</c> default. Fuzzy matches with a threshold not lower than
    /// this value add the <c>%</c> operator to their condition, so that
    /// the trigram index can be used.
    /// </summary>
    public double IndexSimilarityThreshold { get; set; } = 0.3;

    /// <summary>
    /// Quotes a single part of an identifier with double quotes.
    /// </summary>
    /// <param name="part">The part.</param>
    /// <returns>Quoted part.</returns>
    protected override string QuoteIdentifierPart(string part) =>
        "\"" + part.Replace("\"", "\"\"") + "\"";

    /// <summary>
    /// Builds a <c>LIKE</c> or <c>ILIKE</c> condition. Both can be served
    /// by the trigram index.
    /// </summary>
    /// <param name="expression">The expression to match.</param>
    /// <param name="pattern">The pattern parameter placeholder.</param>
    /// <param name="caseInsensitive">True for a case-insensitive match.</param>
    /// <returns>SQL.</returns>
    public override string BuildLike(string expression, string pattern,
        bool caseInsensitive) =>
        $"{expression} {(caseInsensitive ? "ILIKE" : "LIKE")} {pattern}";

    /// <summary>
    /// Builds a regular expression match condition.
    /// </summary>
    /// <param name="expression">The expression to match.</param>
    /// <param name="pattern">The pattern parameter placeholder.</param>
    /// <returns>SQL.</returns>
    public override string BuildRegexMatch(string expression, string pattern) =>
        $"{expression} ~ {pattern}";

    /// <summary>
    /// Builds an expression converting <paramref name="expression"/> into
    /// a double precision number.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <returns>SQL.</returns>
    public override string BuildNumber(string expression) =>
        $"CAST({expression} AS double precision)";

    /// <summary>
    /// Builds a fuzzy match condition based on <c>pg_trgm</c> trigram
    /// similarity. When <paramref name="threshold"/> is not lower than
    /// <see cref="IndexSimilarityThreshold"/>, the condition also includes
    /// the <c>%</c> operator, which can use the trigram index.
    /// </summary>
    /// <param name="expression">The expression to match.</param>
    /// <param name="text">The text parameter placeholder.</param>
    /// <param name="thresholdParameter">The threshold parameter placeholder.
    /// </param>
    /// <param name="threshold">The threshold value.</param>
    /// <returns>SQL.</returns>
    public override string BuildSimilarity(string expression, string text,
        string thresholdParameter, double threshold)
    {
        string similarity =
            $"similarity({expression}, {text}) >= {thresholdParameter}";
        return threshold >= IndexSimilarityThreshold
            ? $"({expression} % {text} AND {similarity})"
            : similarity;
    }
}
