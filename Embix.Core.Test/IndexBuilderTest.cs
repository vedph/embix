using Embix.Core.Config;
using Fusi.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Embix.Core.Test;

public sealed class IndexBuilderTest
{
    private const string PLACE_SCHEMA =
        "CREATE TABLE place (id INTEGER PRIMARY KEY, title TEXT, " +
        "description TEXT, rank INTEGER, yearMin INTEGER, yearMax INTEGER);";

    private const string DATA_SQL =
        "SELECT title AS plttl, description AS pldsc, id AS m_target_id, " +
        "rank AS m_rank, yearMin AS m_yearMin, yearMax AS m_yearMax " +
        "FROM place ORDER BY id LIMIT {1} OFFSET {0}";

    private static string GetProfileJson(string documents) =>
        "{ \"FilterChains\": [ { \"Id\": \"wsp-std\", \"Filters\": [ " +
        "{ \"Id\": \"text-filter.whitespace\" }, " +
        "{ \"Id\": \"text-filter.standard\" } ] } ], " +
        "\"Tokenizers\": [ { \"Id\": \"std\", " +
        "\"TypeId\": \"string-tokenizer.standard\" } ], " +
        "\"TokenMultipliers\": [ { \"Id\": \"rev\", " +
        "\"TypeId\": \"string-token-multiplier.reverse\" } ], " +
        "\"MetadataFields\": [\"rank\", \"yearMin\", \"yearMax\"], " +
        "\"Documents\": [" + documents + "] }";

    private static string GetPlaceDocument(string id = "place",
        string multipliers = "{ \"pldsc\": \"rev\" }",
        string where = "") =>
        $"{{ \"Id\": \"{id}\", " +
        $"\"CountSql\": \"SELECT COUNT(*) FROM place {where}\", " +
        $"\"DataSql\": \"{DATA_SQL.Replace("FROM place", "FROM place " + where)}\", " +
        "\"TextFilterChains\": { \"*\": \"wsp-std\" }, " +
        "\"Tokenizers\": { \"*\": \"std\" }, " +
        $"\"TokenMultipliers\": {multipliers} }}";

    private static SqliteTestDb CreateDb(IEnumerable<string> places)
    {
        SqliteTestDb db = new();
        StringBuilder sql = new(PLACE_SCHEMA);
        int id = 0;
        foreach (string place in places)
        {
            id++;
            string[] parts = place.Split('|');
            sql.Append("INSERT INTO place VALUES (").Append(id).Append(", ")
               .Append(parts[0].Length == 0 ? "NULL" : $"'{parts[0]}'")
               .Append(", ")
               .Append(parts.Length < 2 ? "NULL" : $"'{parts[1]}'")
               .Append($", {id % 3}, {1900 + id}, NULL);\n");
        }
        db.Execute(sql.ToString());
        return db;
    }

    private static IndexBuilder CreateBuilder(SqliteTestDb db, string profileJson,
        int bufferSize = 100)
    {
        EmbixProfile profile = TestHelper.CreateProfile(profileJson);
        SqlIndexWriter writer = new(profile, db) { BufferSize = bufferSize };
        return new IndexBuilder(profile, db, writer);
    }

    private static List<string> GetOccurrences(SqliteTestDb db) =>
        [.. db.Query("SELECT t.value, o.field, o.target_id, o.rank, " +
            "o.yearMin, o.yearMax FROM eix_occurrence o " +
            "INNER JOIN eix_token t ON o.token_id = t.id " +
            "ORDER BY CAST(o.target_id AS INTEGER), o.field, t.value")
            .Select(r => string.Join("|", r))];

    [Fact]
    public async Task Build_SinglePartition_Ok()
    {
        using SqliteTestDb db = CreateDb(["Alpha Beta|Gamma", "Beta"]);
        using (IndexBuilder builder = CreateBuilder(db,
            GetProfileJson(GetPlaceDocument())))
        {
            await builder.BuildAsync("place");
        }

        Assert.Equal(
        [
            // the reverse multiplier applies to pldsc only
            "ammag|pldsc|1|1|1901|",
            "gamma|pldsc|1|1|1901|",
            "alpha|plttl|1|1|1901|",
            "beta|plttl|1|1|1901|",
            "beta|plttl|2|2|1902|",
        ], GetOccurrences(db));
        Assert.Equal(4, db.Scalar("SELECT COUNT(*) FROM eix_token"));
    }

    [Fact]
    public async Task Build_DefaultMultiplier_AppliedToAllFields()
    {
        using SqliteTestDb db = CreateDb(["ab|cd"]);
        using (IndexBuilder builder = CreateBuilder(db, GetProfileJson(
            GetPlaceDocument(multipliers: "{ \"*\": \"rev\" }"))))
        {
            await builder.BuildAsync("place");
        }

        Assert.Equal(
        [
            "cd|pldsc|1|1|1901|",
            "dc|pldsc|1|1|1901|",
            "ab|plttl|1|1|1901|",
            "ba|plttl|1|1|1901|",
        ], GetOccurrences(db));
    }

    [Fact]
    public async Task Build_NoDefaultComponents_UsesNullComponents()
    {
        // no "*" entries: the field is not filtered and not tokenized
        const string doc = "{ \"Id\": \"place\", " +
            "\"CountSql\": \"SELECT COUNT(*) FROM place\", " +
            "\"DataSql\": \"SELECT title AS plttl, id AS m_target_id " +
            "FROM place ORDER BY id LIMIT {1} OFFSET {0}\" }";
        using SqliteTestDb db = CreateDb(["Alpha Beta"]);
        using (IndexBuilder builder = CreateBuilder(db, GetProfileJson(doc)))
        {
            await builder.BuildAsync("place");
        }

        Assert.Equal(["Alpha Beta|plttl|1|||"], GetOccurrences(db));
    }

    private static string[] GetRandomPlaces(int count, int seed)
    {
        string[] vocabulary = [.. Enumerable.Range(0, 150)
            .Select(n => "w" + n.ToString("000"))];
        Random random = new(seed);
        return [.. Enumerable.Range(0, count).Select(_ =>
            string.Join(" ", Enumerable.Range(0, random.Next(1, 6))
                .Select(_ => vocabulary[random.Next(vocabulary.Length)]))
            + "|" +
            string.Join(" ", Enumerable.Range(0, random.Next(0, 4))
                .Select(_ => vocabulary[random.Next(vocabulary.Length)])))];
    }

    private static int CountExpectedOccurrences(IEnumerable<string> places) =>
        places.Sum(p => p.Split('|').Sum(
            s => s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length));

    [Theory]
    [InlineData(1, 100)]
    [InlineData(4, 7)]
    [InlineData(7, 1)]
    public async Task Build_Partitions_AllWrittenWithIntegrity(int partitions,
        int bufferSize)
    {
        string[] places = GetRandomPlaces(503, partitions);
        using SqliteTestDb db = CreateDb(places);
        using (IndexBuilder builder = CreateBuilder(db,
            GetProfileJson(GetPlaceDocument(multipliers: "{}")), bufferSize))
        {
            builder.PartitionCount = partitions;
            builder.MinPartitionSize = 10;
            await builder.BuildAsync("place");
        }

        // FKs are enforced by SQLite, so occurrences were written after tokens
        Assert.Equal(CountExpectedOccurrences(places),
            db.Scalar("SELECT COUNT(*) FROM eix_occurrence"));
        Assert.Equal(
            db.Scalar("SELECT COUNT(DISTINCT t.value) FROM eix_occurrence o " +
                "INNER JOIN eix_token t ON o.token_id = t.id"),
            db.Scalar("SELECT COUNT(*) FROM eix_token"));
        Assert.Equal(places.Length,
            db.Scalar("SELECT COUNT(DISTINCT target_id) FROM eix_occurrence"));
    }

    [Theory]
    [InlineData("ORDER BY rank")]
    [InlineData("")]
    public async Task Build_NonUniqueOrNoOrder_EachRecordOnce(string orderBy)
    {
        // with paging, a non-unique order made partitions overlap
        string[] places = [.. Enumerable.Range(1, 400).Select(n => "w" + n)];
        using SqliteTestDb db = CreateDb(places);
        string doc = "{ \"Id\": \"place\", " +
            "\"CountSql\": \"SELECT COUNT(*) FROM place\", " +
            "\"DataSql\": \"SELECT title AS plttl, id AS m_target_id " +
            $"FROM place {orderBy}\", " +
            "\"TextFilterChains\": { \"*\": \"wsp-std\" }, " +
            "\"Tokenizers\": { \"*\": \"std\" } }";
        using (IndexBuilder builder = CreateBuilder(db, GetProfileJson(doc), 7))
        {
            builder.PartitionCount = 8;
            builder.MinPartitionSize = 1;
            await builder.BuildAsync("place");
        }

        Assert.Equal(400, db.Scalar("SELECT COUNT(*) FROM eix_occurrence"));
        Assert.Equal(400, db.Scalar(
            "SELECT COUNT(DISTINCT target_id) FROM eix_occurrence"));
    }

    [Fact]
    public async Task Build_InvalidDataSql_ThrowsOriginalError()
    {
        using SqliteTestDb db = CreateDb(
            Enumerable.Range(1, 300).Select(n => "w" + n));
        string doc = "{ \"Id\": \"place\", " +
            "\"CountSql\": \"SELECT COUNT(*) FROM place\", " +
            "\"DataSql\": \"SELECT nonexisting FROM place\" }";
        using IndexBuilder builder = CreateBuilder(db, GetProfileJson(doc));
        builder.PartitionCount = 4;
        builder.MinPartitionSize = 1;

        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(
            () => builder.BuildAsync("place"));
    }

    [Fact]
    public async Task Build_FailingWorker_ThrowsOriginalError()
    {
        using SqliteTestDb db = CreateDb(
            Enumerable.Range(1, 5000).Select(n => "w" + n));
        EmbixProfile profile = TestHelper.CreateProfile(
            GetProfileJson(GetPlaceDocument()));
        using IndexBuilder builder = new(profile, db, new FailingWriter());
        builder.PartitionCount = 4;
        builder.MinPartitionSize = 1;

        // the reader would block on the full channel if not cancelled
        Task build = builder.BuildAsync("place");
        Assert.Same(build, await Task.WhenAny(build, Task.Delay(30000)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => build);
    }

    private sealed class FailingWriter : IIndexWriter
    {
        public IMetadataSupplier? MetadataSupplier { get; set; }
        public Microsoft.Extensions.Logging.ILogger? Logger { get; set; }
        public void Write(string documentId, int partitionNr, string field,
            string token, IDictionary<string, object>? metadata = null) =>
            throw new InvalidOperationException("failing writer");
        public void Flush() { }
        public void Dispose() { }
    }

    [Fact]
    public async Task Build_RecordLimit_Respected()
    {
        string[] places = [.. Enumerable.Range(1, 100).Select(n => "w" + n)];
        using SqliteTestDb db = CreateDb(places);
        using (IndexBuilder builder = CreateBuilder(db,
            GetProfileJson(GetPlaceDocument())))
        {
            builder.PartitionCount = 4;
            builder.MinPartitionSize = 1;
            builder.RecordLimit = 15;
            await builder.BuildAsync("place");
        }

        Assert.Equal(15, db.Scalar("SELECT COUNT(*) FROM eix_occurrence"));
        Assert.Equal(15, db.Scalar(
            "SELECT MAX(CAST(target_id AS INTEGER)) FROM eix_occurrence"));
    }

    [Fact]
    public async Task Build_Empty_NothingWritten()
    {
        using SqliteTestDb db = CreateDb([]);
        using (IndexBuilder builder = CreateBuilder(db,
            GetProfileJson(GetPlaceDocument())))
        {
            await builder.BuildAsync("place");
        }
        Assert.Equal(0, db.Scalar("SELECT COUNT(*) FROM eix_occurrence"));
    }

    [Fact]
    public async Task Build_Cancelled_Throws()
    {
        using SqliteTestDb db = CreateDb(["a"]);
        using IndexBuilder builder = CreateBuilder(db,
            GetProfileJson(GetPlaceDocument()));
        using CancellationTokenSource cts = new();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => builder.BuildAsync("place", cts.Token));
    }

    [Fact]
    public async Task Build_UnknownDocument_Throws()
    {
        using SqliteTestDb db = CreateDb([]);
        using IndexBuilder builder = CreateBuilder(db,
            GetProfileJson(GetPlaceDocument()));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => builder.BuildAsync("not-existing"));
    }

    [Fact]
    public async Task Build_Progress_Reported()
    {
        using SqliteTestDb db = CreateDb(["a", "b"]);
        List<ProgressReport> reports = [];
        using (IndexBuilder builder = CreateBuilder(db,
            GetProfileJson(GetPlaceDocument())))
        {
            await builder.BuildAsync("place", default,
                new SyncProgress(r => { lock (reports) reports.Add(r); }));
        }
        lock (reports)
        {
            Assert.Contains(reports, r => r.Message == "Partitions: 1");
            Assert.Contains(reports, r => r.Percent == 100);
        }
    }

    [Fact]
    public void CalculatePartitionCount_Ok()
    {
        using SqliteTestDb db = CreateDb(
            Enumerable.Range(1, 50).Select(n => "w" + n));
        using IndexBuilder builder = CreateBuilder(db,
            GetProfileJson(GetPlaceDocument()));
        builder.PartitionCount = 4;

        builder.MinPartitionSize = 100;
        Assert.Equal(Tuple.Create(1, 50), builder.CalculatePartitionCount("place"));

        builder.MinPartitionSize = 10;
        Assert.Equal(Tuple.Create(4, 50), builder.CalculatePartitionCount("place"));
    }

    [Fact]
    public async Task Factory_SharedWriterAcrossDocuments_AllFlushed()
    {
        // two documents sharing tokens, built like the CLI does: this used
        // to lose the buffered tail of all the documents after the first one
        using SqliteTestDb db = CreateDb(
            Enumerable.Range(1, 30).Select(n => $"common w{n}|x{n % 4}"));
        string json = GetProfileJson(
            GetPlaceDocument("odd", "{}", "WHERE id % 2 = 1") + ", " +
            GetPlaceDocument("even", "{}", "WHERE id % 2 = 0"));

        using (TestIndexBuilderFactory factory = new(json, db)
        {
            BufferSize = 1000
        })
        {
            foreach (DocumentDefinition doc in factory.Profile.GetDocuments())
            {
                using IndexBuilder builder = factory.GetBuilder(null, null);
                await builder.BuildAsync(doc.Id!);
            }
        }

        // 30 places x (2 title tokens + 1 description token)
        Assert.Equal(90, db.Scalar("SELECT COUNT(*) FROM eix_occurrence"));
        // common, w1..w30, x0..x3
        Assert.Equal(35, db.Scalar("SELECT COUNT(*) FROM eix_token"));
    }

    private sealed class SyncProgress(Action<ProgressReport> handler)
        : IProgress<ProgressReport>
    {
        public void Report(ProgressReport value) => handler(value);
    }
}
