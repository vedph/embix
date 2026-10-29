using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Embix.Core.Test;

public sealed class SqlIndexRecordWriterTest
{
    private static readonly IndexRecordNames _names = new(
        ["id", "value", "language"],
        ["token_id", "field", "target_id", "rank", "yearMin", "yearMax"]);

    private static List<IndexRecord> GetRecords(int count) =>
        [.. Enumerable.Range(1, count).Select(n => new IndexRecord(_names)
        {
            Token = [n, "v" + n, n % 2 == 0 ? "grc" : ""],
            Occurrence = [n, "fld", n.ToString(), n, null!, 1900 + n]
        })];

    [Theory]
    [InlineData(SqlIndexRecordWriter.DEFAULT_MAX_PARAMETERS)]
    [InlineData(7)]
    [InlineData(1)]
    public void Write_Chunked_AllWritten(int maxParameters)
    {
        using SqliteTestDb db = new();
        using (SqlIndexRecordWriter writer = new(db.GetConnection())
        {
            MaxParameters = maxParameters
        })
        {
            writer.Write(GetRecords(25));
        }

        Assert.Equal(25, db.Scalar("SELECT COUNT(*) FROM eix_token"));
        Assert.Equal(25, db.Scalar("SELECT COUNT(*) FROM eix_occurrence"));
        Assert.Equal(1, db.Scalar(
            "SELECT COUNT(*) FROM eix_occurrence WHERE token_id=4 AND " +
            "field='fld' AND target_id='4' AND rank=4 AND yearMin IS NULL " +
            "AND yearMax=1904"));
        Assert.Equal(12, db.Scalar(
            "SELECT COUNT(*) FROM eix_token WHERE language='grc'"));
    }

    [Fact]
    public void Write_Parts_OnlyRequestedWritten()
    {
        using SqliteTestDb db = new();
        using (SqlIndexRecordWriter writer = new(db.GetConnection()))
        {
            writer.Write(GetRecords(3), IndexRecordParts.Token);
        }

        Assert.Equal(3, db.Scalar("SELECT COUNT(*) FROM eix_token"));
        Assert.Equal(0, db.Scalar("SELECT COUNT(*) FROM eix_occurrence"));
    }

    [Fact]
    public void Write_Empty_NoOp()
    {
        using SqliteTestDb db = new();
        using SqlIndexRecordWriter writer = new(db.GetConnection());
        writer.Write([]);
        Assert.Equal(0, db.Scalar("SELECT COUNT(*) FROM eix_token"));
    }

    [Fact]
    public void Write_TextWithQuotes_Preserved()
    {
        using SqliteTestDb db = new();
        using (SqlIndexRecordWriter writer = new(db.GetConnection()))
        {
            writer.Write([new IndexRecord(_names)
            {
                Token = [1, "l'a\"b;--", ""],
                Occurrence = [1, "f", "1", null!, null!, null!]
            }]);
        }
        Assert.Equal(1, db.Scalar(
            "SELECT COUNT(*) FROM eix_token WHERE value='l''a\"b;--'"));
    }
}
