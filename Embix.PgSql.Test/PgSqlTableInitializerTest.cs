using System.Linq;
using Xunit;

namespace Embix.PgSql.Test;

public sealed class PgSqlTableInitializerTest(PgTestDatabase db)
    : IClassFixture<PgTestDatabase>
{
    private static readonly string[] _secondaryIndexes =
    [
        "eix_occurrence_target_field_idx",
        "eix_occurrence_token_field_target_idx",
        "eix_token_value_language_uix",
        "eix_token_value_pattern_idx",
        "eix_token_value_trgm_idx",
    ];

    private string[] GetIndexes() =>
        [.. db.Strings("SELECT indexname FROM pg_indexes " +
            "WHERE schemaname = 'public' ORDER BY indexname")];

    private string[] GetForeignKeys() =>
        [.. db.Strings("SELECT conname FROM pg_constraint " +
            "WHERE conrelid = 'eix_occurrence'::regclass AND contype = 'f'")];

    private PgSqlTableInitializer GetInitializer()
    {
        db.Reset();
        return new PgSqlTableInitializer(db);
    }

    [Fact]
    public void Initialize_CreatesTablesIndexesAndFk()
    {
        PgSqlTableInitializer initializer = GetInitializer();

        initializer.Initialize(false);

        Assert.Equal(
            [.. _secondaryIndexes.Append("eix_occurrence_pk")
                .Append("eix_token_pk").Order()],
            GetIndexes());
        Assert.Equal(["eix_occurrence_token_fk"], GetForeignKeys());
        Assert.Equal(1, db.Scalar("SELECT COUNT(*) FROM pg_extension " +
            "WHERE extname = 'pg_trgm'"));
    }

    [Fact]
    public void Initialize_Twice_Idempotent()
    {
        PgSqlTableInitializer initializer = GetInitializer();
        initializer.Initialize(false);
        db.Execute("INSERT INTO eix_token (id, value) VALUES (1, 'a')");

        initializer.Initialize(false);

        Assert.Equal(1, db.Scalar("SELECT COUNT(*) FROM eix_token"));
        Assert.Single(GetForeignKeys());
    }

    [Fact]
    public void Initialize_Clear_TruncatesAndRestartsIdentity()
    {
        PgSqlTableInitializer initializer = GetInitializer();
        initializer.Initialize(false);
        db.Execute("INSERT INTO eix_token (id, value) VALUES (1, 'a');" +
            "INSERT INTO eix_occurrence (token_id, field, target_id) " +
            "VALUES (1, 'f', '1'), (1, 'f', '2');");

        initializer.Initialize(true);

        Assert.Equal(0, db.Scalar("SELECT COUNT(*) FROM eix_token"));
        Assert.Equal(0, db.Scalar("SELECT COUNT(*) FROM eix_occurrence"));
        db.Execute("INSERT INTO eix_token (id, value) VALUES (1, 'a');" +
            "INSERT INTO eix_occurrence (token_id, field, target_id) " +
            "VALUES (1, 'f', '1');");
        Assert.Equal(1, db.Scalar("SELECT MAX(id) FROM eix_occurrence"));
    }

    [Fact]
    public void DropIndexes_KeepsPrimaryKeys_CreateIndexesRestores()
    {
        PgSqlTableInitializer initializer = GetInitializer();
        initializer.Initialize(false);

        initializer.DropIndexes();
        Assert.Equal(["eix_occurrence_pk", "eix_token_pk"], GetIndexes());
        Assert.Empty(GetForeignKeys());

        initializer.CreateIndexes();
        Assert.Equal(7, GetIndexes().Length);
        Assert.Single(GetForeignKeys());
    }

    [Fact]
    public void CreateIndexes_SyncsTokenIdentity()
    {
        PgSqlTableInitializer initializer = GetInitializer();
        initializer.Initialize(false);
        db.Execute("INSERT INTO eix_token (id, value) VALUES (1, 'a'), (41, 'b')");

        initializer.CreateIndexes();

        db.Execute("INSERT INTO eix_token (value) VALUES ('c')");
        Assert.Equal(42, db.Scalar("SELECT id FROM eix_token WHERE value = 'c'"));
    }

    [Fact]
    public void CreateIndexes_DuplicateTokens_Fails()
    {
        // the unique index is the integrity check of a bulk load
        PgSqlTableInitializer initializer = GetInitializer();
        initializer.Initialize(false);
        initializer.DropIndexes();
        db.Execute("INSERT INTO eix_token (id, value) VALUES (1, 'a'), (2, 'a')");

        Assert.Throws<Npgsql.PostgresException>(initializer.CreateIndexes);
    }

    [Fact]
    public void Initialize_LegacySchema_Upgraded()
    {
        db.Reset();
        // Embix 4 schema
        db.Execute(
            "CREATE TABLE eix_token (id serial NOT NULL, value varchar(100) NOT NULL, " +
            "\"language\" varchar(5) NULL, CONSTRAINT token_pk PRIMARY KEY (id));" +
            "CREATE INDEX eix_token_value_idx ON eix_token (value);" +
            "CREATE TABLE eix_occurrence (id serial NOT NULL, token_id integer NOT NULL, " +
            "field char(5) NOT NULL, target_id varchar(50) NOT NULL, " +
            "CONSTRAINT eix_occurrence_pk PRIMARY KEY (id), " +
            "CONSTRAINT eix_occurrence_fk FOREIGN KEY (token_id) " +
            "REFERENCES eix_token(id) ON DELETE CASCADE ON UPDATE CASCADE);" +
            "CREATE INDEX eix_occurrence_field_idx ON eix_occurrence (field);" +
            "INSERT INTO eix_token (id, value) VALUES (7, 'a');");

        new PgSqlTableInitializer(db).Initialize(false);

        string[] indexes = GetIndexes();
        Assert.DoesNotContain("eix_token_value_idx", indexes);
        Assert.DoesNotContain("eix_occurrence_field_idx", indexes);
        Assert.All(_secondaryIndexes, i => Assert.Contains(i, indexes));
        // the legacy FK is kept, and no duplicate FK is added
        Assert.Equal(["eix_occurrence_fk"], GetForeignKeys());
        // serial sequence synced too
        db.Execute("INSERT INTO eix_token (value) VALUES ('b')");
        Assert.Equal(8, db.Scalar("SELECT id FROM eix_token WHERE value = 'b'"));
    }
}
