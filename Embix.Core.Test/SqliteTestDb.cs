using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;

namespace Embix.Core.Test;

/// <summary>
/// A temporary SQLite database used as a real ADO.NET backend for tests.
/// A file (rather than an in-memory) database in WAL mode is used, so that
/// several connections can be used concurrently, as the indexer does.
/// Foreign keys are enforced.
/// </summary>
internal sealed class SqliteTestDb : IDbConnectionFactory, IDisposable
{
    public const string INDEX_SCHEMA =
        "CREATE TABLE eix_token (id INTEGER PRIMARY KEY, " +
        "value TEXT NOT NULL, language TEXT, UNIQUE (value, language));\n" +
        "CREATE TABLE eix_occurrence (" +
        "id INTEGER PRIMARY KEY AUTOINCREMENT, " +
        "token_id INTEGER NOT NULL REFERENCES eix_token(id), " +
        "field TEXT NOT NULL, target_id TEXT NOT NULL, " +
        "\"rank\" INTEGER, \"yearMin\" INTEGER, \"yearMax\" INTEGER);\n";

    private readonly string _path;

    public string ConnectionString { get; }

    public SqliteTestDb(bool createIndexSchema = true)
    {
        _path = Path.Combine(Path.GetTempPath(),
            $"embix-test-{Guid.NewGuid():N}.db");
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _path,
            ForeignKeys = true,
            Pooling = false,
            DefaultTimeout = 60
        }.ToString();

        Execute("PRAGMA journal_mode=WAL;");
        if (createIndexSchema) Execute(INDEX_SCHEMA);
    }

    public IDbConnection GetConnection() => new SqliteConnection(ConnectionString);

    public void Execute(string sql)
    {
        using SqliteConnection connection = new(ConnectionString);
        connection.Open();
        using SqliteCommand cmd = new(sql, connection);
        cmd.ExecuteNonQuery();
    }

    public long Scalar(string sql)
    {
        using SqliteConnection connection = new(ConnectionString);
        connection.Open();
        using SqliteCommand cmd = new(sql, connection);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    public List<object?[]> Query(string sql)
    {
        using SqliteConnection connection = new(ConnectionString);
        connection.Open();
        using SqliteCommand cmd = new(sql, connection);
        using SqliteDataReader reader = cmd.ExecuteReader();
        List<object?[]> rows = [];
        while (reader.Read())
        {
            object?[] row = new object?[reader.FieldCount];
            for (int i = 0; i < row.Length; i++)
                row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (string suffix in new[] { "", "-wal", "-shm" })
        {
            try
            {
                File.Delete(_path + suffix);
            }
            catch (IOException)
            {
                // best effort: it's a temp file
            }
        }
    }
}
