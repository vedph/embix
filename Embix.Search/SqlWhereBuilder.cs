using System;
using System.Collections.Generic;
using System.Text;

namespace Embix.Search;

/// <summary>
/// A builder for the conditions of a <c>WHERE</c> clause, combined with
/// <c>AND</c>, and their parameters.
/// </summary>
public sealed class SqlWhereBuilder
{
    /// <summary>
    /// Gets the parameters.
    /// </summary>
    public SqlParameters Parameters { get; }

    /// <summary>
    /// Gets the SQL dialect.
    /// </summary>
    public SqlDialect Dialect => Parameters.Dialect;

    /// <summary>
    /// Gets the conditions.
    /// </summary>
    public IList<string> Conditions { get; } = [];

    /// <summary>
    /// Gets a value indicating whether this builder has no conditions.
    /// </summary>
    public bool IsEmpty => Conditions.Count == 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlWhereBuilder"/> class.
    /// </summary>
    /// <param name="dialect">The SQL dialect.</param>
    public SqlWhereBuilder(SqlDialect dialect) : this(new SqlParameters(dialect))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlWhereBuilder"/> class,
    /// sharing the specified parameters (e.g. with other parts of the query).
    /// </summary>
    /// <param name="parameters">The parameters.</param>
    /// <exception cref="ArgumentNullException">parameters</exception>
    public SqlWhereBuilder(SqlParameters parameters)
    {
        Parameters = parameters
            ?? throw new ArgumentNullException(nameof(parameters));
    }

    /// <summary>
    /// Adds the specified condition.
    /// </summary>
    /// <param name="condition">The SQL condition.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException">condition</exception>
    public SqlWhereBuilder Add(string condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        Conditions.Add(condition);
        return this;
    }

    /// <summary>
    /// Adds a parameter.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The parameter placeholder.</returns>
    public string AddParameter(object? value) => Parameters.Add(value);

    /// <summary>
    /// Quotes the specified identifier.
    /// </summary>
    /// <param name="identifier">The identifier, optionally qualified.</param>
    /// <returns>Quoted identifier.</returns>
    public string Quote(string identifier) => Dialect.QuoteIdentifier(identifier);

    /// <summary>
    /// Builds the SQL code for the conditions, preceded by
    /// <paramref name="keyword"/>. Each condition is parenthesized when
    /// there are more than one. If there are no conditions, an empty string
    /// is returned.
    /// </summary>
    /// <param name="keyword">The keyword introducing the conditions
    /// (e.g. <c>WHERE</c>, <c>AND</c>, or an empty string).</param>
    /// <returns>SQL code.</returns>
    public string ToSql(string keyword = "WHERE")
    {
        if (Conditions.Count == 0) return "";

        StringBuilder sb = new();
        if (!string.IsNullOrEmpty(keyword)) sb.Append(keyword).Append(' ');
        if (Conditions.Count == 1) return sb.Append(Conditions[0]).ToString();

        for (int i = 0; i < Conditions.Count; i++)
        {
            if (i > 0) sb.Append(" AND ");
            sb.Append('(').Append(Conditions[i]).Append(')');
        }
        return sb.ToString();
    }
}
