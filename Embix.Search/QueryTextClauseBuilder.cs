using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Embix.Search;

/// <summary>
/// SQL query text clause builder. This builds a single WHERE clause
/// condition for a field, using a number of different match operators
/// for it: <c>=</c>=equal, <c>&lt;&gt;</c>=not equal, <c>*=</c>=contains,
/// <c>^=</c>=starts with, <c>$=</c>=ends with, <c>?=</c>=match wildcards,
/// <c>~=</c>=regular expression match, <c>%=</c>=fuzzy match (optionally
/// suffixing the value with <c>:minimumThreshold</c>); numeric comparisons
/// <c>==</c>, <c>!=</c>, <c>&lt;</c>, <c>&gt;</c>, <c>&lt;=</c>,
/// <c>&gt;=</c>; and bit tests <c>:</c>=has any of the flags,
/// <c>&amp;:</c>=has all the flags, <c>!:</c>=has none of the flags.
/// Text matches other than equality are case-insensitive. The SQL code
/// is built according to the <see cref="SqlDialect"/> of the parameters.
/// </summary>
public class QueryTextClauseBuilder
{
    private static readonly char[] _wildcards = ['*', '?'];
    private static readonly char[] _likeSpecials = ['\\', '%', '_'];
    private static readonly Regex _nrRegex = new("^[0-9a-fA-F]{1,8}$",
        RegexOptions.Compiled);

    /// <summary>
    /// Gets or sets the flags separators. Default is comma.
    /// </summary>
    public char[] FlagSeparators { get; set; }

    /// <summary>
    /// Gets the flags with their bit values. Names are case-insensitive.
    /// </summary>
    public Dictionary<string, int> Flags { get; }

    /// <summary>
    /// Gets or sets the default fuzzy matching minimum threshold (0-1).
    /// Default is 0.3, i.e. the default threshold of the PostgreSQL
    /// <c>pg_trgm</c> trigram similarity.
    /// </summary>
    public double DefaultFuzzyThreshold { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTextClauseBuilder"/>
    /// class.
    /// </summary>
    public QueryTextClauseBuilder()
    {
        DefaultFuzzyThreshold = 0.3D;
        FlagSeparators = [','];
        Flags = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the specified character is a wildcard.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>
    /// <c>true</c> if the specified character is a wildcard; otherwise,
    /// <c>false</c>.
    /// </returns>
    public static bool IsWildcard(char c) => Array.IndexOf(_wildcards, c) > -1;

    /// <summary>
    /// Escapes the LIKE wildcards (<c>%</c>, <c>_</c>) and the escape
    /// character (<c>\</c>, the default one in both PostgreSQL and MySql)
    /// in the specified value, so that they are matched literally.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>Escaped value.</returns>
    public static string EscapeLike(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.IndexOfAny(_likeSpecials) == -1) return value;

        StringBuilder sb = new(value.Length + 4);
        foreach (char c in value)
        {
            if (Array.IndexOf(_likeSpecials, c) > -1) sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Builds a numeric comparison clause.
    /// </summary>
    /// <param name="parameters">The parameters.</param>
    /// <param name="column">The quoted column.</param>
    /// <param name="op">The SQL comparison operator.</param>
    /// <param name="value">The value.</param>
    /// <returns>SQL condition.</returns>
    protected virtual string BuildNumericClause(SqlParameters parameters,
        string column, string op, string value)
    {
        // a non-numeric value cannot match anything
        if (!double.TryParse(value, NumberStyles.Float,
            CultureInfo.InvariantCulture, out double n))
        {
            return "1 = 0";
        }
        return $"{parameters.Dialect.BuildNumber(column)} {op} {parameters.Add(n)}";
    }

    /// <summary>
    /// Builds a case-insensitive LIKE clause.
    /// </summary>
    /// <param name="parameters">The parameters.</param>
    /// <param name="column">The quoted column.</param>
    /// <param name="pattern">The LIKE pattern.</param>
    /// <returns>SQL condition.</returns>
    protected virtual string BuildLikeClause(SqlParameters parameters,
        string column, string pattern) =>
        parameters.Dialect.BuildLike(column, parameters.Add(pattern), true);

    /// <summary>
    /// Builds a regular expression match clause.
    /// </summary>
    /// <param name="parameters">The parameters.</param>
    /// <param name="column">The quoted column.</param>
    /// <param name="value">The regular expression.</param>
    /// <returns>SQL condition.</returns>
    protected virtual string BuildRegexClause(SqlParameters parameters,
        string column, string value) =>
        parameters.Dialect.BuildRegexMatch(column, parameters.Add(value));

    /// <summary>
    /// Parses the value of a fuzzy clause, which is either a simple text
    /// to match, or this text followed by the minimum threshold for matching
    /// introduced by a colon. If no threshold is specified,
    /// <see cref="DefaultFuzzyThreshold"/> is used.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>Tuple where 1=text and 2=threshold.</returns>
    protected (string Text, double Threshold) ParseFuzzyValue(string value)
    {
        // extract threshold from value (N:T) or assume default if not specified
        int i = value.LastIndexOf(':');
        if (i > -1 && double.TryParse(value.AsSpan(i + 1), NumberStyles.Float,
            CultureInfo.InvariantCulture, out double threshold))
        {
            return (value[..i], threshold);
        }
        return (i > -1 ? value[..i] : value, DefaultFuzzyThreshold);
    }

    /// <summary>
    /// Builds a fuzzy match clause.
    /// </summary>
    /// <param name="parameters">The parameters.</param>
    /// <param name="column">The quoted column.</param>
    /// <param name="value">The value, optionally followed by colon and
    /// threshold.</param>
    /// <returns>SQL condition.</returns>
    protected virtual string BuildFuzzyClause(SqlParameters parameters,
        string column, string value)
    {
        var (text, threshold) = ParseFuzzyValue(value);
        return parameters.Dialect.BuildSimilarity(column,
            parameters.Add(text), parameters.Add(threshold), threshold);
    }

    private int ParseFlag(string flag)
    {
        // named flags first, as some names (e.g. "bad") are also hex numbers
        if (Flags.TryGetValue(flag, out int value)) return value;

        if (_nrRegex.IsMatch(flag))
        {
            return int.Parse(flag, NumberStyles.HexNumber,
                CultureInfo.InvariantCulture);
        }
        return 0;
    }

    /// <summary>
    /// Gets the numeric value resulting from ORing the values of all the
    /// flags found in <paramref name="flags"/>, as separated by
    /// <see cref="FlagSeparators"/>.
    /// </summary>
    /// <param name="flags">The flags.</param>
    /// <returns>The numeric value.</returns>
    protected int GetFlagsValue(string flags)
    {
        if (string.IsNullOrEmpty(flags)) return 0;

        int n = 0;
        foreach (string s in flags.Split(FlagSeparators,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            n |= ParseFlag(s);
        }
        return n;
    }

    /// <summary>
    /// Builds a bit test clause.
    /// </summary>
    /// <param name="parameters">The parameters.</param>
    /// <param name="column">The quoted column.</param>
    /// <param name="op">The operator.</param>
    /// <param name="value">The flags.</param>
    /// <returns>SQL condition.</returns>
    protected virtual string BuildBitTestClause(SqlParameters parameters,
        string column, string op, string value)
    {
        int n = GetFlagsValue(value);
        string p = parameters.Add(n);
        return op switch
        {
            "&:" => $"({column} & {p}) = {p}",
            "!:" => $"({column} & {p}) = 0",
            _ => $"({column} & {p}) <> 0",
        };
    }

    /// <summary>
    /// Builds the condition for a field name, an operator, and a value.
    /// </summary>
    /// <param name="parameters">The parameters to add values to.</param>
    /// <param name="name">The field name, optionally qualified (e.g.
    /// <c>t.value</c>). This is quoted according to the dialect.</param>
    /// <param name="op">The operator.</param>
    /// <param name="value">The value.</param>
    /// <returns>The SQL condition, or null if the operator is unknown.
    /// </returns>
    /// <exception cref="ArgumentNullException">any argument</exception>
    public string? BuildClause(SqlParameters parameters, string name,
        string op, string value)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(op);
        ArgumentNullException.ThrowIfNull(value);

        string column = parameters.Dialect.QuoteIdentifier(name);
        switch (op)
        {
            case "=":   // equal
                return $"{column} = {parameters.Add(value)}";
            case "<>":  // not equal
                return $"{column} <> {parameters.Add(value)}";
            case "*=":  // contains
                return BuildLikeClause(parameters, column,
                    "%" + EscapeLike(value) + "%");
            case "^=":  // starts-with
                return BuildLikeClause(parameters, column, EscapeLike(value) + "%");
            case "$=":  // ends-with
                return BuildLikeClause(parameters, column, "%" + EscapeLike(value));
            case "?=":  // wildcards (?=1 char, *=0-N chars)
                // if value has no wildcards, fallback to equals
                if (value.IndexOfAny(_wildcards) == -1)
                    goto case "=";
                // translate wildcards: * => %, ? => _
                return BuildLikeClause(parameters, column,
                    EscapeLike(value).Replace('*', '%').Replace('?', '_'));
            case "~=":  // regex
                return BuildRegexClause(parameters, column, value);
            case "%=":  // fuzzy (with optional threshold)
                return BuildFuzzyClause(parameters, column, value);
            case "==":  // equal (numeric)
                return BuildNumericClause(parameters, column, "=", value);
            case "!=":  // not equal (numeric)
                return BuildNumericClause(parameters, column, "<>", value);
            case "<":   // less-than (numeric)
                return BuildNumericClause(parameters, column, "<", value);
            case ">":   // greater-than (numeric)
                return BuildNumericClause(parameters, column, ">", value);
            case "<=":   // less-than or equal (numeric)
                return BuildNumericClause(parameters, column, "<=", value);
            case ">=":   // greater-than or equal (numeric)
                return BuildNumericClause(parameters, column, ">=", value);
            case ":":   // has any bitvalue of
            case "&:":  // has all bitvalues of
            case "!:":  // has not any bitvalue of
                return BuildBitTestClause(parameters, column, op, value);
            default:
                Debug.WriteLine("Unknown operator: " + op);
                return null;
        }
    }

    /// <summary>
    /// Adds to <paramref name="where"/> the condition specified by a field
    /// name, an operator, and a value. Unknown operators add nothing.
    /// </summary>
    /// <param name="where">The where builder.</param>
    /// <param name="name">The field name, optionally qualified.</param>
    /// <param name="op">The operator.</param>
    /// <param name="value">The value.</param>
    /// <returns>The where builder (for chaining).</returns>
    /// <exception cref="ArgumentNullException">any argument</exception>
    public SqlWhereBuilder AddClause(SqlWhereBuilder where, string name,
        string op, string value)
    {
        ArgumentNullException.ThrowIfNull(where);
        string? clause = BuildClause(where.Parameters, name, op, value);
        if (clause != null) where.Add(clause);
        return where;
    }
}
