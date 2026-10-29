using Embix.Search.MySql;
using Embix.Search.PgSql;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Embix.Search.Test;

public sealed class QueryTextClauseBuilderTest
{
    private static (string? Sql, Dictionary<string, object?> Parameters) Build(
        string name, string op, string value,
        QueryTextClauseBuilder? builder = null, SqlDialect? dialect = null)
    {
        SqlParameters parameters = new(dialect ?? new PgSqlDialect());
        string? sql = (builder ?? new QueryTextClauseBuilder())
            .BuildClause(parameters, name, op, value);
        return (sql, parameters.ToDictionary(p => p.Key, p => p.Value));
    }

    private static void AssertClause(string expectedSql,
        object?[] expectedValues, string name, string op, string value,
        QueryTextClauseBuilder? builder = null, SqlDialect? dialect = null)
    {
        var (sql, parameters) = Build(name, op, value, builder, dialect);
        Assert.Equal(expectedSql, sql);
        Assert.Equal(expectedValues,
            Enumerable.Range(0, expectedValues.Length)
                .Select(i => parameters["p" + i]));
        Assert.Equal(expectedValues.Length, parameters.Count);
    }

    [Fact]
    public void Build_Equal_Ok() =>
        AssertClause("\"t\" = @p0", ["abc"], "t", "=", "abc");

    [Fact]
    public void Build_NotEqual_Ok() =>
        AssertClause("\"t\" <> @p0", ["abc"], "t", "<>", "abc");

    [Fact]
    public void Build_QualifiedName_Quoted() =>
        AssertClause("\"t\".\"value\" = @p0", ["abc"], "t.value", "=", "abc");

    [Fact]
    public void Build_Contains_Ok() =>
        AssertClause("\"t\" ILIKE @p0", ["%abc%"], "t", "*=", "abc");

    [Fact]
    public void Build_StartsWith_Ok() =>
        AssertClause("\"t\" ILIKE @p0", ["abc%"], "t", "^=", "abc");

    [Fact]
    public void Build_EndsWith_Ok() =>
        AssertClause("\"t\" ILIKE @p0", ["%abc"], "t", "$=", "abc");

    [Fact]
    public void Build_Wildcard_Question_Ok() =>
        AssertClause("\"t\" ILIKE @p0", ["a_c"], "t", "?=", "a?c");

    [Fact]
    public void Build_Wildcard_Asterisk_Ok() =>
        AssertClause("\"t\" ILIKE @p0", ["a%c"], "t", "?=", "a*c");

    [Fact]
    public void Build_WildcardWithoutWildcards_IsEqual() =>
        AssertClause("\"t\" = @p0", ["abc"], "t", "?=", "abc");

    [Fact]
    public void Build_ContainsWithLikeSpecials_Escaped() =>
        AssertClause("\"t\" ILIKE @p0", [@"%50\%\_%"], "t", "*=", "50%_");

    [Fact]
    public void Build_WildcardWithLikeSpecials_Escaped() =>
        AssertClause("\"t\" ILIKE @p0", [@"a\_b%"], "t", "?=", "a_b*");

    [Theory]
    [InlineData("abc", "abc")]
    [InlineData("50%", @"50\%")]
    [InlineData("a_b", @"a\_b")]
    [InlineData(@"a\b", @"a\\b")]
    public void EscapeLike_Ok(string value, string expected) =>
        Assert.Equal(expected, QueryTextClauseBuilder.EscapeLike(value));

    [Fact]
    public void Build_Regex_Ok() =>
        AssertClause("\"t\" ~ @p0", ["rosa[em]?"], "t", "~=", "rosa[em]?");

    [Fact]
    public void Build_FuzzyNoThreshold_IndexedSimilarity() =>
        AssertClause("(\"t\" % @p0 AND similarity(\"t\", @p0) >= @p1)",
            ["abc", 0.3], "t", "%=", "abc");

    [Fact]
    public void Build_FuzzyThreshold_IndexedSimilarity() =>
        AssertClause("(\"t\" % @p0 AND similarity(\"t\", @p0) >= @p1)",
            ["abc", 0.8], "t", "%=", "abc:0.8");

    [Fact]
    public void Build_FuzzyThresholdBelowIndex_PlainSimilarity() =>
        AssertClause("similarity(\"t\", @p0) >= @p1",
            ["abc", 0.2], "t", "%=", "abc:0.2");

    [Fact]
    public void Build_FuzzyInvalidThreshold_Default() =>
        AssertClause("(\"t\" % @p0 AND similarity(\"t\", @p0) >= @p1)",
            ["abc", 0.3], "t", "%=", "abc:x");

    [Fact]
    public void Build_FuzzyCustomDefault_Ok() =>
        AssertClause("(\"t\" % @p0 AND similarity(\"t\", @p0) >= @p1)",
            ["abc", 0.6], "t", "%=", "abc",
            new QueryTextClauseBuilder { DefaultFuzzyThreshold = 0.6 });

    [Theory]
    [InlineData("==", "=")]
    [InlineData("!=", "<>")]
    [InlineData("<", "<")]
    [InlineData(">", ">")]
    [InlineData("<=", "<=")]
    [InlineData(">=", ">=")]
    public void Build_Numeric_Ok(string op, string sqlOp) =>
        AssertClause($"CAST(\"n\" AS double precision) {sqlOp} @p0", [12.5],
            "n", op, "12.5");

    [Fact]
    public void Build_NumericNotANumber_MatchesNothing() =>
        AssertClause("1 = 0", [], "n", "==", "abc");

    [Theory]
    [InlineData(":", "(\"f\" & @p0) <> 0")]
    [InlineData("&:", "(\"f\" & @p0) = @p0")]
    [InlineData("!:", "(\"f\" & @p0) = 0")]
    public void Build_BitTest_Ok(string op, string sql) =>
        AssertClause(sql, [5], "f", op, "1,4");

    [Fact]
    public void Build_BitTestNamedFlags_Ok()
    {
        QueryTextClauseBuilder builder = new();
        // "bad" is also a valid hex number: names must win
        builder.Flags["Bad"] = 2;
        builder.Flags["good"] = 8;
        AssertClause("(\"f\" & @p0) <> 0", [10], "f", ":", "bad, GOOD", builder);
    }

    [Fact]
    public void Build_UnknownOperator_Null()
    {
        var (sql, parameters) = Build("t", "@@", "abc");
        Assert.Null(sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void AddClause_AddsToWhere()
    {
        QueryTextClauseBuilder builder = new();
        SqlWhereBuilder where = new(new PgSqlDialect());
        builder.AddClause(where, "t", "=", "a");
        builder.AddClause(where, "t", "@@", "ignored");
        builder.AddClause(where, "n", ">", "3");

        Assert.Equal("WHERE (\"t\" = @p0) AND " +
            "(CAST(\"n\" AS double precision) > @p1)", where.ToSql());
        Assert.Equal(2, where.Parameters.Count);
    }

    [Fact]
    public void Build_MySql_Ok()
    {
        MySqlDialect dialect = new();
        AssertClause("`t`.`v` = @p0", ["a"], "t.v", "=", "a", dialect: dialect);
        AssertClause("LOWER(`t`) LIKE LOWER(@p0)", ["%a%"], "t", "*=", "a",
            dialect: dialect);
        AssertClause("`t` REGEXP @p0", ["^a"], "t", "~=", "^a", dialect: dialect);
        AssertClause("(`n` + 0.0) < @p0", [1.0], "n", "<", "1", dialect: dialect);
        AssertClause("SIMILARITY_STRING(`t`, @p0) >= @p1", ["a", 0.5],
            "t", "%=", "a:0.5", dialect: dialect);
    }
}
