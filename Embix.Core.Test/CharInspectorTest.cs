using Embix.Core.Config;
using System.IO;
using System.Threading;
using Xunit;

namespace Embix.Core.Test;

public sealed class CharInspectorTest
{
    private static SqliteTestDb CreateDb()
    {
        SqliteTestDb db = new(false);
        db.Execute("CREATE TABLE t (a TEXT, b TEXT, n INTEGER);" +
            "INSERT INTO t VALUES ('aab', NULL, 12);" +
            "INSERT INTO t VALUES ('α,', 'b', NULL);");
        return db;
    }

    [Fact]
    public void Inspect_NoProgress_CountsAllChars()
    {
        using SqliteTestDb db = CreateDb();
        CharInspector inspector = new(db);

        inspector.Inspect(
        [
            new DocumentDefinition
            {
                CountSql = "SELECT COUNT(*) FROM t",
                DataSql = "SELECT a, b, n FROM t"
            }
        ], CancellationToken.None);

        Assert.Equal(2, inspector.Counts['a']);
        Assert.Equal(2, inspector.Counts['b']);
        Assert.Equal(1, inspector.Counts['α']);
        Assert.Equal(1, inspector.Counts[',']);
        // non-text values are inspected as text
        Assert.Equal(1, inspector.Counts['1']);
        Assert.Equal(1, inspector.Counts['2']);
    }

    [Fact]
    public void Inspect_Json_Ok()
    {
        using SqliteTestDb db = CreateDb();
        CharInspector inspector = new(db);

        inspector.Inspect("[ { \"CountSql\": \"SELECT COUNT(*) FROM t\", " +
            "\"DataSql\": \"SELECT a FROM t\" }, ]", CancellationToken.None);

        Assert.Equal(4, inspector.Counts.Count);
        inspector.Reset();
        Assert.Empty(inspector.Counts);
    }

    [Fact]
    public void SaveCsv_Ok()
    {
        using SqliteTestDb db = CreateDb();
        CharInspector inspector = new(db);
        inspector.Inspect("[ { \"CountSql\": \"SELECT COUNT(*) FROM t\", " +
            "\"DataSql\": \"SELECT a FROM t\" } ]", CancellationToken.None);

        StringWriter writer = new();
        inspector.SaveCsv(writer);

        Assert.Equal(
            "hex,dec,cat,glyph,freq\n" +
            "\"002C\",44,Po,\",\",1\n" +
            "\"0061\",97,Ll,a,2\n" +
            "\"0062\",98,Ll,b,1\n" +
            "\"03B1\",945,Ll,α,1\n",
            writer.ToString().ReplaceLineEndings("\n"));
    }
}
