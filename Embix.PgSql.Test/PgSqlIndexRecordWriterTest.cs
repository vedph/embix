using Embix.Core;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Embix.PgSql.Test;

public sealed class PgSqlIndexRecordWriterAppendValueTest
{
    [Theory]
    [InlineData(null, @"\N")]
    [InlineData("", "")]
    [InlineData("abc", "abc")]
    [InlineData("a\tb\nc\rd", @"a\tb\nc\rd")]
    [InlineData(@"a\b", @"a\\b")]
    [InlineData(@"\N", @"\\N")]
    [InlineData(true, "t")]
    [InlineData(false, "f")]
    [InlineData(12, "12")]
    [InlineData(-1.5, "-1.5")]
    public void AppendValue_Ok(object? value, string expected)
    {
        StringBuilder sb = new();
        PgSqlIndexRecordWriter.AppendValue(value, sb);
        Assert.Equal(expected, sb.ToString());
    }

    [Fact]
    public void AppendValue_DbNull_Null()
    {
        StringBuilder sb = new();
        PgSqlIndexRecordWriter.AppendValue(DBNull.Value, sb);
        Assert.Equal(@"\N", sb.ToString());
    }

    [Fact]
    public void AppendValue_DateTime_Iso()
    {
        StringBuilder sb = new();
        PgSqlIndexRecordWriter.AppendValue(new DateTime(2020, 1, 2, 3, 4, 5), sb);
        Assert.Equal("2020-01-02 03:04:05", sb.ToString());
    }

    [Fact]
    public void AppendValue_Bytes_HexEscaped()
    {
        StringBuilder sb = new();
        PgSqlIndexRecordWriter.AppendValue(new byte[] { 0xAB, 0x01 }, sb);
        Assert.Equal(@"\\xAB01", sb.ToString());
    }
}

public sealed class PgSqlIndexRecordWriterTest(PgTestDatabase db)
    : IClassFixture<PgTestDatabase>
{
    private static readonly IndexRecordNames _names = new(
        ["id", "value", "language"],
        ["token_id", "field", "target_id", "rank", "isOk", "note", "day"]);

    private void Init()
    {
        db.Reset();
        new PgSqlTableInitializer(db).Initialize(false);
        // metadata of various types; "isOk" is mixed-case on purpose
        db.Execute("ALTER TABLE eix_occurrence ADD COLUMN rank smallint, " +
            "ADD COLUMN \"isOk\" boolean, ADD COLUMN note text, " +
            "ADD COLUMN day date;");
    }

    private PgSqlIndexRecordWriter GetWriter() =>
        new((NpgsqlConnection)db.GetConnection());

    [Fact]
    public void Write_SpecialChars_RoundTrip()
    {
        Init();
        string[] values = ["a\tb", "c\nd", @"e\f", @"\N", "ἄειδε", "o'k \"q\"", "x\r"];
        List<IndexRecord> records = [.. values.Select((v, i) => new IndexRecord(_names)
        {
            Token = [i + 1, v, i % 2 == 0 ? "" : "grc"],
            Occurrence = [i + 1, "fld", (i + 1).ToString(), null!, null!, v, null!]
        })];

        using (PgSqlIndexRecordWriter writer = GetWriter()) writer.Write(records);

        Assert.Equal(values, db.Strings("SELECT value FROM eix_token ORDER BY id"));
        Assert.Equal(values, db.Strings("SELECT note FROM eix_occurrence ORDER BY id"));
        Assert.Equal(3, db.Scalar("SELECT COUNT(*) FROM eix_token WHERE language = 'grc'"));
        Assert.Equal(4, db.Scalar("SELECT COUNT(*) FROM eix_token WHERE language = ''"));
    }

    [Fact]
    public void Write_TypedMetadata_ParsedByServer()
    {
        Init();
        using (PgSqlIndexRecordWriter writer = GetWriter())
        {
            writer.Write([new IndexRecord(_names)
            {
                Token = [1, "a", ""],
                // a long into a smallint, a date from DateTime
                Occurrence = [1, "fld", "x", 3L, true, null!,
                    new DateTime(2024, 5, 6)]
            }]);
        }

        Assert.Equal(["fld|x|3|True||2024-05-06"],
            db.Strings("SELECT field, target_id, rank, \"isOk\", note, " +
                "day::text FROM eix_occurrence"));
    }

    [Fact]
    public void Write_ManyBatches_SameConnection()
    {
        Init();
        using (PgSqlIndexRecordWriter writer = GetWriter())
        {
            for (int batch = 0; batch < 5; batch++)
            {
                writer.Write([.. Enumerable.Range(1, 1000).Select(n =>
                {
                    int id = batch * 1000 + n;
                    return new IndexRecord(_names)
                    {
                        Token = [id, "t" + id, ""],
                        Occurrence = [id, "f", "1", null!, null!, null!, null!]
                    };
                })]);
            }
        }
        Assert.Equal(5000, db.Scalar("SELECT COUNT(*) FROM eix_token"));
        Assert.Equal(5000, db.Scalar("SELECT COUNT(*) FROM eix_occurrence"));
    }

    [Fact]
    public void Write_OccurrenceWithoutToken_FkViolation()
    {
        Init();
        using PgSqlIndexRecordWriter writer = GetWriter();
        Assert.Throws<PostgresException>(() => writer.Write([new IndexRecord(_names)
        {
            Occurrence = [99, "f", "1", null!, null!, null!, null!]
        }]));
    }
}
