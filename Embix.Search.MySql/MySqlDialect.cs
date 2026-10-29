namespace Embix.Search.MySql;

/// <summary>
/// MySql SQL dialect.
/// </summary>
/// <remarks>The fuzzy matching (<see cref="BuildSimilarity"/>) requires a
/// <c>SIMILARITY_STRING</c> user-defined function.</remarks>
/// <seealso cref="SqlDialect" />
public class MySqlDialect : SqlDialect
{
    /// <summary>
    /// Quotes a single part of an identifier with backticks.
    /// </summary>
    /// <param name="part">The part.</param>
    /// <returns>Quoted part.</returns>
    protected override string QuoteIdentifierPart(string part) =>
        "`" + part.Replace("`", "``") + "`";

    /// <summary>
    /// Builds a regular expression match condition.
    /// </summary>
    /// <param name="expression">The expression to match.</param>
    /// <param name="pattern">The pattern parameter placeholder.</param>
    /// <returns>SQL.</returns>
    public override string BuildRegexMatch(string expression, string pattern) =>
        $"{expression} REGEXP {pattern}";

    /// <summary>
    /// Builds an expression converting <paramref name="expression"/> into
    /// a number.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <returns>SQL.</returns>
    public override string BuildNumber(string expression) =>
        $"({expression} + 0.0)";

    /// <summary>
    /// Builds a fuzzy match condition using the <c>SIMILARITY_STRING</c> UDF.
    /// </summary>
    /// <param name="expression">The expression to match.</param>
    /// <param name="text">The text parameter placeholder.</param>
    /// <param name="thresholdParameter">The threshold parameter placeholder.
    /// </param>
    /// <param name="threshold">The threshold value.</param>
    /// <returns>SQL.</returns>
    public override string BuildSimilarity(string expression, string text,
        string thresholdParameter, double threshold) =>
        $"SIMILARITY_STRING({expression}, {text}) >= {thresholdParameter}";
}
