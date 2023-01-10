using SqlKata;
using SqlKata.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Embix.Search;

/// <summary>
/// SQL query text clause builder. This builds a single WHERE clause
/// condition for a text field, using a number of different match operators
/// for it: <c>=</c>=equal, <c>&lt;&gt;</c>=not equal, <c>*=</c>=contains,
/// <c>^=</c>=starts with, <c>$=</c>=ends with, <c>?=</c>=match wildcards,
/// <c>~=</c>=regular expression match, <c>%=</c>=fuzzy match (optionally
/// suffixing the value with <c>:minimumTreshold</c>).
/// </summary>
/// <remarks>Currently this supports only MySql and PostgreSQL.</remarks>
public class QueryTextClauseBuilder
{
    private static readonly char[] _wildcards = new[] { '*', '?' };
    private static readonly Regex _nrRegex = new("^[0-9a-fA-F]{1,8}$",
        RegexOptions.Compiled);

    /// <summary>
    /// Gets or sets the flags separators. Default is comma.
    /// </summary>
    public char[] FlagSeparators { get; set; }

    /// <summary>
    /// Gets the flags with their bit values.
    /// </summary>
    public Dictionary<string, int> Flags { get; }

    /// <summary>
    /// Gets or sets the default fuzzy matching minimum treshold.
    /// Default is 0.9.
    /// </summary>
    public double DefaultFuzzyTreshold { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTextClauseBuilder"/>
    /// class.
    /// </summary>
    public QueryTextClauseBuilder()
    {
        DefaultFuzzyTreshold = 0.9D;
        FlagSeparators = new[] { ',' };
        Flags = new Dictionary<string, int>();
    }

    /// <summary>
    /// Determines whether the specified character is a wildcard.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>
    /// <c>true</c> if the specified character is a wildcard; otherwise,
    /// <c>false</c>.
    /// </returns>
    public static bool IsWildcard(char c) => _wildcards.Any(w => w == c);

    /// <summary>
    /// Adds a cast-numeric comparison clause.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="name">The field name.</param>
    /// <param name="op">The operator.</param>
    /// <param name="value">The value.</param>
    protected virtual void AddNumericClause(Query query, string name, string op,
        string value)
    {
        query.ForSqlServer(q => q.WhereRaw(
            $"CAST(? AS DECIMAL(9,2)) {op} ?", name, value));

        query.ForMySql(q => q.WhereRaw($"(? + 0.0) {op} ?", name, value));

        query.ForPostgreSql(q => q.WhereRaw(
            $"CAST(? AS double precision) {op} ?", name, value));
    }

    /// <summary>
    /// Adds a regular expression match clause.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="name">The field name.</param>
    /// <param name="value">The value.</param>
    protected virtual void AddRegexClause(Query query, string name,
        string value)
    {
        // Sql Server requires a UDF

        query.ForMySql(q => q.WhereRaw($"[{name}] REGEXP ?", value));

        query.ForPostgreSql(q => q.WhereRaw($"[{name}] ~ ?", value));
    }

    /// <summary>
    /// Parses the value of a fuzzy clause, which is either a simple text
    /// to match, or this text followed by the minimum treshold for matching
    /// introduced by a colon. If no treshold is specified,
    /// <see cref="DefaultFuzzyTreshold"/> is used.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>Tuple where 1=text and 2=treshold.</returns>
    protected Tuple<string, double> ParseFuzzyValue(string value)
    {
        // extract treshold from value (N:T) or assume default if not specified
        string text = value;
        double treshold = DefaultFuzzyTreshold;

        int i = value.LastIndexOf(':');
        if (i > -1)
        {
            text = value[..i];
            if (!double.TryParse(value.AsSpan(i + 1), NumberStyles.Float,
                CultureInfo.InvariantCulture, out treshold))
            {
                treshold = DefaultFuzzyTreshold;
            }
        }
        return Tuple.Create(text, treshold);
    }

    /// <summary>
    /// Adds a fuzzy match comparison clause.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="name">The field name.</param>
    /// <param name="value">The value.</param>
    protected virtual void AddFuzzyMatchClause(Query query, string name,
        string value)
    {
        var t = ParseFuzzyValue(value);

        // Sql Server requires a UDF

        query.ForMySql(q => q.WhereRaw($"SIMILARITY_STRING([{name}],?) >= ?",
            t.Item1, t.Item2));

        query.ForPostgreSql(q => q.WhereRaw(
            $"cast(levenshtein({name},?) as double precision) / " +
            "greatest(length(?), length(?)) >= ?",
            t.Item1, name, t.Item1, t.Item2));
    }

    private int ParseFlag(string flag)
    {
        if (_nrRegex.IsMatch(flag))
        {
            return int.Parse(flag, NumberStyles.HexNumber,
                CultureInfo.InvariantCulture);
        }
        return Flags.ContainsKey(flag) ? Flags[flag] : 0;
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

        // parse all the values and OR them into n
        int[] values = (from s in flags.ToLowerInvariant().Split(
            FlagSeparators, StringSplitOptions.RemoveEmptyEntries)
                        select ParseFlag(s)).ToArray();
        int n = 0;
        foreach (int v in values) n |= v;

        return n;
    }

    /// <summary>
    /// Adds the bit test clause.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="name">The field name.</param>
    /// <param name="op">The op.</param>
    /// <param name="value">The value.</param>
    protected virtual void AddBitTestClause(Query query, string name,
        string op, string value)
    {
        int n = GetFlagsValue(value);
        string tail = op switch
        {
            "&:" => " = " + n,  // & n = n
            "!:" => " = 0",     // & n = 0
            _ => " <> 0",       // & n <> 0
        };
        query.WhereRaw("(? & ?)" + tail, name, n);
    }

    /// <summary>
    /// Adds to <paramref name="query"/> the clause for the condition
    /// specified by a field name, an operator, and a value.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="name">The field name.</param>
    /// <param name="op">The operator.</param>
    /// <param name="value">The value.</param>
    /// <returns>The input query (for chaining).</returns>
    public Query AddClause(Query query, string name, string op, string value)
    {
        switch (op)
        {
            case "=":   // equal
                query.Where(name, "=", value);
                break;
            case "<>":  // not equal
                query.Where(name, "<>", value);
                break;
            case "*=":  // contains
                query.WhereContains(name, value);
                break;
            case "^=":  // starts-with
                query.WhereStarts(name, value);
                break;
            case "$=":  // ends-with
                query.WhereEnds(name, value);
                break;
            case "?=":  // wildcards (?=1 char, *=0-N chars)
                // if value has no wildcards, fallback to equals
                if (value.IndexOfAny(_wildcards) == -1)
                    goto case "=";
                // translate wildcards: * => %, ? => _
                string wild = value.Replace('*', '%').Replace('?', '_');
                query.WhereLike(name, wild);
                break;
            case "~=":  // regex
                AddRegexClause(query, name, value);
                break;
            case "%=":  // fuzzy (with optional treshold)
                AddFuzzyMatchClause(query, name, value);
                break;
            case "==":  // equal (numeric)
                AddNumericClause(query, name, "=", value);
                break;
            case "!=":  // not equal (numeric)
                AddNumericClause(query, name, "<>", value);
                break;
            case "<":   // less-than (numeric)
                AddNumericClause(query, name, "<=", value);
                break;
            case ">":   // greater-than (numeric)
                AddNumericClause(query, name, ">", value);
                break;
            case "<=":   // less-than or equal (numeric)
                AddNumericClause(query, name, "<=", value);
                break;
            case ">=":   // greater-than or equal (numeric)
                AddNumericClause(query, name, ">=", value);
                break;
            case ":":   // has any bitvalue of
            case "&:":  // has all bitvalues of
            case "!:":  // has not any bitvalue of
                AddBitTestClause(query, name, op, value);
                break;
            default:
                Debug.WriteLine("Unknown operator: " + op);
                break;
        }

        return query;
    }
}
