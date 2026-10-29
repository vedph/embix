using Embix.Core;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using Xunit;

namespace Embix.PgSql.Test;

/// <summary>
/// A temporary PostgreSql database, created on first use and dropped when
/// disposed. The server is specified by the <c>EMBIX_TEST_PG</c> environment
/// variable (a connection string without database), defaulting to a local
/// server with user/password postgres/postgres. When the server is not
/// reachable, tests using this database are skipped.
/// </summary>
public sealed class PgTestDatabase : IDbConnectionFactory, IDisposable
{
    private const string DEFAULT_SERVER =
        "Host=localhost;Port=5432;Username=postgres;Password=postgres;" +
        "Timeout=5";

    private readonly string _server;
    private readonly string _name;
    private bool _created;
    private string? _unavailable;

    public string ConnectionString { get; }

    public PgTestDatabase()
    {
        _server = Environment.GetEnvironmentVariable("EMBIX_TEST_PG")
            ?? DEFAULT_SERVER;
        _name = "embix_test_" + Guid.NewGuid().ToString("N");
        ConnectionString = new NpgsqlConnectionStringBuilder(_server)
        {
            Database = _name,
            // let tests drop the database without waiting for idle pooled
            // connections
            Pooling = false
        }.ToString();
    }

    private NpgsqlConnection GetAdminConnection() =>
        new(new NpgsqlConnectionStringBuilder(_server)
        {
            Database = "postgres",
            Pooling = false
        }.ToString());

    /// <summary>
    /// Ensures that the database exists, or skips the current test.
    /// </summary>
    public void EnsureCreated()
    {
        if (_unavailable != null) Assert.Skip(_unavailable);
        if (_created) return;
        try
        {
            using NpgsqlConnection connection = GetAdminConnection();
            connection.Open();
            using NpgsqlCommand cmd = new($"CREATE DATABASE \"{_name}\"",
                connection);
            cmd.ExecuteNonQuery();
            _created = true;
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
        {
            _unavailable = "PostgreSql not available: " + ex.Message;
            Assert.Skip(_unavailable);
        }
    }

    /// <summary>
    /// Drops and recreates all the tables in the public schema.
    /// </summary>
    public void Reset()
    {
        EnsureCreated();
        Execute("DROP SCHEMA public CASCADE; CREATE SCHEMA public;");
    }

    public IDbConnection GetConnection() => new NpgsqlConnection(ConnectionString);

    public void Execute(string sql)
    {
        using NpgsqlConnection connection = new(ConnectionString);
        connection.Open();
        using NpgsqlCommand cmd = new(sql, connection);
        cmd.ExecuteNonQuery();
    }

    public long Scalar(string sql)
    {
        using NpgsqlConnection connection = new(ConnectionString);
        connection.Open();
        using NpgsqlCommand cmd = new(sql, connection);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    public List<string> Strings(string sql)
    {
        using NpgsqlConnection connection = new(ConnectionString);
        connection.Open();
        using NpgsqlCommand cmd = new(sql, connection);
        using NpgsqlDataReader reader = cmd.ExecuteReader();
        List<string> results = [];
        // all the result sets (e.g. "SET ...; EXPLAIN ...")
        do
        {
            while (reader.Read())
            {
                object?[] values = new object?[reader.FieldCount];
                for (int i = 0; i < values.Length; i++)
                    values[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                results.Add(string.Join("|", values));
            }
        } while (reader.NextResult());
        return results;
    }

    public void Dispose()
    {
        if (!_created) return;
        using NpgsqlConnection connection = GetAdminConnection();
        connection.Open();
        using NpgsqlCommand cmd = new(
            $"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)", connection);
        cmd.ExecuteNonQuery();
    }
}
