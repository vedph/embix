using SqlKata;
using SqlKata.Compilers;
using Xunit;

namespace Embix.Search.Test;

public sealed class QueryTextClauseBuilderTest
{
    private readonly PostgresCompiler _compiler;

    public QueryTextClauseBuilderTest()
    {
        _compiler = new PostgresCompiler();
    }

    private string GetSql(Query query)
    {
        return _compiler.Compile(query).ToString();
    }

    [Fact]
    public void Build_Equal_Ok()
    {
        QueryTextClauseBuilder builder = new();
        Query query = new("x");
        builder.AddClause(query, "t", "=", "abc");
        string sql = GetSql(query);
        Assert.Equal("SELECT * FROM \"x\" WHERE \"t\" = 'abc'", sql);
    }

    [Fact]
    public void Build_NotEqual_Ok()
    {
        QueryTextClauseBuilder builder = new();
        Query query = new("x");
        builder.AddClause(query, "t", "<>", "abc");
        string sql = GetSql(query);
        Assert.Equal("SELECT * FROM \"x\" WHERE \"t\" <> 'abc'", sql);
    }

    [Fact]
    public void Build_Contains_Ok()
    {
        QueryTextClauseBuilder builder = new();
        Query query = new("x");
        builder.AddClause(query, "t", "*=", "abc");
        string sql = GetSql(query);
        Assert.Equal("SELECT * FROM \"x\" WHERE \"t\" ilike '%abc%'", sql);
    }

    [Fact]
    public void Build_StartsWith_Ok()
    {
        QueryTextClauseBuilder builder = new();
        Query query = new("x");
        builder.AddClause(query, "t", "^=", "abc");
        string sql = GetSql(query);
        Assert.Equal("SELECT * FROM \"x\" WHERE \"t\" ilike 'abc%'", sql);
    }

    [Fact]
    public void Build_EndsWith_Ok()
    {
        QueryTextClauseBuilder builder = new();
        Query query = new("x");
        builder.AddClause(query, "t", "$=", "abc");
        string sql = GetSql(query);
        Assert.Equal("SELECT * FROM \"x\" WHERE \"t\" ilike '%abc'", sql);
    }

    [Fact]
    public void Build_Wildcard_Question_Ok()
    {
        QueryTextClauseBuilder builder = new();
        Query query = new("x");
        builder.AddClause(query, "t", "?=", "a?c");
        string sql = GetSql(query);
        Assert.Equal("SELECT * FROM \"x\" WHERE \"t\" ilike 'a_c'", sql);
    }

    [Fact]
    public void Build_Wildcard_Asterisk_Ok()
    {
        QueryTextClauseBuilder builder = new();
        Query query = new("x");
        builder.AddClause(query, "t", "?=", "a*c");
        string sql = GetSql(query);
        Assert.Equal("SELECT * FROM \"x\" WHERE \"t\" ilike 'a%c'", sql);
    }

    [Fact]
    public void Build_Regex_Ok()
    {
        QueryTextClauseBuilder builder = new();
        Query query = new("x");
        builder.AddClause(query, "t", "~=", "rosa[em]?");
        string sql = GetSql(query);
        Assert.Equal("SELECT * FROM \"x\" WHERE \"t\" ~ 'rosa[em]?'", sql);
    }

    [Fact]
    public void Build_FuzzyNoTreshold_Ok()
    {
        QueryTextClauseBuilder builder = new();
        Query query = new("x");
        builder.AddClause(query, "t", "%=", "abc");
        string sql = GetSql(query);
        Assert.Equal("SELECT * FROM \"x\" WHERE " +
            "cast(levenshtein(t,'abc') as double precision) / " +
            "greatest(length('t'), length('abc')) >= 0.9", sql);
    }

    [Fact]
    public void Build_FuzzyTreshold_Ok()
    {
        QueryTextClauseBuilder builder = new();
        Query query = new("x");
        builder.AddClause(query, "t", "%=", "abc:0.8");
        string sql = GetSql(query);
        Assert.Equal("SELECT * FROM \"x\" WHERE " +
            "cast(levenshtein(t,'abc') as double precision) / " +
            "greatest(length('t'), length('abc')) >= 0.8", sql);
    }
}
