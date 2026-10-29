using Embix.Search.MySql;
using Embix.Search.PgSql;
using System.Collections.Generic;
using Xunit;

namespace Embix.Search.Test;

public sealed class SqlDialectTest
{
    [Theory]
    [InlineData("value", "\"value\"")]
    [InlineData("t.value", "\"t\".\"value\"")]
    [InlineData("t.*", "\"t\".*")]
    [InlineData("yearMin", "\"yearMin\"")]
    [InlineData("a\"b", "\"a\"\"b\"")]
    public void PgSql_QuoteIdentifier_Ok(string name, string expected) =>
        Assert.Equal(expected, new PgSqlDialect().QuoteIdentifier(name));

    [Theory]
    [InlineData("t.value", "`t`.`value`")]
    [InlineData("a`b", "`a``b`")]
    public void MySql_QuoteIdentifier_Ok(string name, string expected) =>
        Assert.Equal(expected, new MySqlDialect().QuoteIdentifier(name));

    [Fact]
    public void BuildPaging_Ok()
    {
        Assert.Equal("LIMIT @a OFFSET @b", new PgSqlDialect().BuildPaging("@a", "@b"));
        Assert.Equal("LIMIT @a OFFSET @b", new MySqlDialect().BuildPaging("@a", "@b"));
    }

    [Fact]
    public void PgSql_Like_CaseSensitivity()
    {
        PgSqlDialect dialect = new();
        Assert.Equal("x ILIKE @p", dialect.BuildLike("x", "@p", true));
        Assert.Equal("x LIKE @p", dialect.BuildLike("x", "@p", false));
    }

    [Fact]
    public void PgSql_Similarity_IndexThreshold()
    {
        PgSqlDialect dialect = new() { IndexSimilarityThreshold = 0.5 };
        Assert.Equal("similarity(x, @t) >= @h",
            dialect.BuildSimilarity("x", "@t", "@h", 0.4));
        Assert.Equal("(x % @t AND similarity(x, @t) >= @h)",
            dialect.BuildSimilarity("x", "@t", "@h", 0.5));
    }
}

public sealed class SqlParametersTest
{
    [Fact]
    public void Add_GeneratesUniqueNames()
    {
        SqlParameters parameters = new(new PgSqlDialect());
        Assert.Equal("@p1", parameters.Add("p1", "explicit"));
        Assert.Equal("@p0", parameters.Add(1));
        // p1 is taken
        Assert.Equal("@p2", parameters.Add(2));
        Assert.Equal(3, parameters.Count);
        Assert.Equal("explicit", parameters["p1"]);
    }

    [Fact]
    public void Add_CustomPrefix()
    {
        SqlParameters parameters = new(new PgSqlDialect(), "q");
        Assert.Equal("@q0", parameters.Add(null));
        Assert.Null(parameters["q0"]);
    }
}

public sealed class SqlWhereBuilderTest
{
    [Fact]
    public void ToSql_Empty_Empty() =>
        Assert.Equal("", new SqlWhereBuilder(new PgSqlDialect()).ToSql());

    [Fact]
    public void ToSql_Single_NotParenthesized()
    {
        SqlWhereBuilder where = new(new PgSqlDialect());
        where.Add($"{where.Quote("a")} = {where.AddParameter(1)}");
        Assert.Equal("WHERE \"a\" = @p0", where.ToSql());
        Assert.Equal("AND \"a\" = @p0", where.ToSql("AND"));
        Assert.Equal("\"a\" = @p0", where.ToSql(""));
    }

    [Fact]
    public void ToSql_Many_Parenthesized()
    {
        SqlWhereBuilder where = new(new PgSqlDialect());
        where.Add("a = 1 OR b = 2").Add("c = 3");
        Assert.Equal("WHERE (a = 1 OR b = 2) AND (c = 3)", where.ToSql());
    }

    [Fact]
    public void SharedParameters_UniqueNames()
    {
        SqlParameters parameters = new(new PgSqlDialect());
        string paging = parameters.Add(10);
        SqlWhereBuilder where = new(parameters);
        Assert.Equal("@p1", where.AddParameter("x"));
        Assert.Equal("@p0", paging);
    }
}

public sealed class SqlQueryTest
{
    [Fact]
    public void ToString_WithParameters()
    {
        SqlQuery query = new("SELECT @p0, @p1", new Dictionary<string, object?>
        {
            ["p0"] = 1,
            ["p1"] = null
        });
        Assert.Equal("SELECT @p0, @p1 -- p0=1, p1=NULL", query.ToString());
        Assert.Equal("SELECT 1", new SqlQuery("SELECT 1").ToString());
    }
}
