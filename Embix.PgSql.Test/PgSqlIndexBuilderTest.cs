using Embix.Core;
using Embix.Core.Config;
using Embix.Core.Filters;
using Embix.Plugin.Greek;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Embix.PgSql.Test;

public sealed class PgSqlIndexBuilderTest(PgTestDatabase db)
    : IClassFixture<PgTestDatabase>
{
    private const int PLACE_COUNT = 2000;

    private static string GetProfile() =>
        "{ \"FilterChains\": [ { \"Id\": \"wsp-std\", \"Filters\": [ " +
        "{ \"Id\": \"text-filter.whitespace\" }, " +
        "{ \"Id\": \"text-filter.standard\" } ] } ], " +
        "\"Tokenizers\": [ { \"Id\": \"std\", " +
        "\"TypeId\": \"string-tokenizer.standard\" } ], " +
        "\"TokenMultipliers\": [ { \"Id\": \"rom\", " +
        "\"TypeId\": \"string-token-multiplier.grc-romanizer\" } ], " +
        "\"MetadataFields\": [\"rank\"], " +
        "\"Documents\": [ " +
        GetDocument("place", "SELECT title AS plttl, id AS m_target_id, " +
            "rank AS m_rank FROM place ORDER BY id LIMIT {1} OFFSET {0}") + ", " +
        GetDocument("grc", "SELECT greek AS plgrc, id AS m_target_id, " +
            "rank AS m_rank FROM place ORDER BY id LIMIT {1} OFFSET {0}") +
        "] }";

    private static string GetDocument(string id, string dataSql) =>
        $"{{ \"Id\": \"{id}\", \"CountSql\": \"SELECT COUNT(*) FROM place\", " +
        $"\"DataSql\": \"{dataSql}\", " +
        "\"TextFilterChains\": { \"*\": \"wsp-std\" }, " +
        "\"Tokenizers\": { \"*\": \"std\" }, " +
        "\"TokenMultipliers\": { \"plgrc\": \"rom\" } }";

    private void Init()
    {
        db.Reset();
        new PgSqlTableInitializer(db).Initialize(true);
        db.Execute("ALTER TABLE eix_occurrence ADD COLUMN rank integer;");

        StringBuilder sql = new("CREATE TABLE place (id integer PRIMARY KEY, " +
            "title text, greek text, rank integer);\n");
        sql.Append("INSERT INTO place SELECT n, 'Place W' || (n % 300) || ' ' || " +
            "'Common Area' || (n % 7), CASE WHEN n % 2 = 0 THEN 'μῆνιν ἄειδε' END, " +
            $"n % 5 FROM generate_series(1, {PLACE_COUNT}) AS n;");
        db.Execute(sql.ToString());
    }

    private async Task Build(int partitions, int bufferSize, bool bulk)
    {
        PgSqlTableInitializer initializer = new(db);
        if (bulk) initializer.DropIndexes();

        using (PgSqlIndexBuilderFactory factory = new(GetProfile(),
            db.ConnectionString, typeof(GrcRomanizerTextFilter).Assembly)
        {
            BufferSize = bufferSize
        })
        {
            foreach (DocumentDefinition doc in factory.Profile.GetDocuments())
            {
                using IndexBuilder builder = factory.GetBuilder(null, null);
                builder.PartitionCount = partitions;
                builder.MinPartitionSize = 10;
                await builder.BuildAsync(doc.Id!);
            }
        }

        if (bulk) initializer.CreateIndexes();
    }

    private void AssertIndex()
    {
        // plttl: 4 tokens per place; plgrc: 2 greek + 2 romanized tokens
        // for even places
        Assert.Equal(PLACE_COUNT * 4 + PLACE_COUNT / 2 * 4,
            db.Scalar("SELECT COUNT(*) FROM eix_occurrence"));
        // place, w0..w299, common, area0..area6, μηνιν, αειδε + romanized
        Assert.Equal(1 + 300 + 1 + 7 + 4,
            db.Scalar("SELECT COUNT(*) FROM eix_token"));
        Assert.Equal(0, db.Scalar("SELECT COUNT(*) FROM eix_occurrence o " +
            "WHERE NOT EXISTS (SELECT 1 FROM eix_token t WHERE t.id = o.token_id)"));
        // the romanized form of the filtered Greek word
        StringBuilder greek = new("μῆνιν");
        new StandardTextFilter().Apply(greek);
        string romanized = new GrcRomanizerStringTokenMultiplier()
            .Multiply(greek.ToString()).Last();
        Assert.NotEqual(greek.ToString(), romanized);
        Assert.Equal(PLACE_COUNT / 2, db.Scalar(
            "SELECT COUNT(DISTINCT o.target_id) FROM eix_occurrence o " +
            "INNER JOIN eix_token t ON o.token_id = t.id " +
            $"WHERE t.value = '{romanized}' AND o.field = 'plgrc'"));
        Assert.Equal(PLACE_COUNT / 5, db.Scalar(
            "SELECT COUNT(*) FROM eix_occurrence o " +
            "INNER JOIN eix_token t ON o.token_id = t.id " +
            "WHERE t.value = 'common' AND o.rank = 0"));
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(4, 13)]
    [InlineData(8, 500)]
    public async Task Build_BulkMode_Ok(int partitions, int bufferSize)
    {
        Init();
        await Build(partitions, bufferSize, true);
        AssertIndex();
        // indexes and FK recreated: the unique index proves no duplicates
        Assert.Equal(7, db.Scalar("SELECT COUNT(*) FROM pg_indexes " +
            "WHERE tablename IN ('eix_token', 'eix_occurrence')"));
    }

    [Fact]
    public async Task Build_WithConstraints_Ok()
    {
        // FK and unique index enforced during the load
        Init();
        await Build(4, 7, false);
        AssertIndex();
    }

    [Fact]
    public async Task Build_Rebuild_AfterClear_Ok()
    {
        Init();
        await Build(2, 50, true);
        new PgSqlTableInitializer(db).Initialize(true);
        await Build(3, 50, true);
        AssertIndex();
        // IDs restart after clearing (they may have gaps, as concurrent
        // workers racing for the same new token waste an ID)
        Assert.Equal(1, db.Scalar("SELECT MIN(id) FROM eix_token"));
    }
}
