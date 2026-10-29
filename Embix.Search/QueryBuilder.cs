using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Embix.Search;

/// <summary>
/// Query builder base class. This provides the SQL dialect for building
/// queries, and helpers to execute them. Each execution uses a new
/// connection, which is closed once done: connections are pooled by the
/// ADO.NET providers, and this makes the builder thread-safe.
/// </summary>
public abstract class QueryBuilder
{
    private SqlDialect? _dialect;

    /// <summary>
    /// Gets the connection string.
    /// </summary>
    protected string ConnectionString { get; }

    /// <summary>
    /// Gets the SQL dialect.
    /// </summary>
    public SqlDialect Dialect => _dialect ??= CreateDialect();

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryBuilder"/> class.
    /// </summary>
    /// <param name="connString">The connection string.</param>
    /// <exception cref="ArgumentNullException">connString</exception>
    protected QueryBuilder(string connString)
    {
        ConnectionString = connString
            ?? throw new ArgumentNullException(nameof(connString));
    }

    /// <summary>
    /// Gets a new connection.
    /// </summary>
    /// <param name="connString">The connection string.</param>
    /// <returns>The connection, not yet opened.</returns>
    protected abstract DbConnection GetConnection(string connString);

    /// <summary>
    /// Creates the SQL dialect.
    /// </summary>
    /// <returns>Dialect.</returns>
    protected abstract SqlDialect CreateDialect();

    /// <summary>
    /// Creates a new set of parameters for the <see cref="Dialect"/>.
    /// </summary>
    /// <returns>Parameters.</returns>
    public SqlParameters CreateParameters() => new(Dialect);

    /// <summary>
    /// Creates a new WHERE builder for the <see cref="Dialect"/>.
    /// </summary>
    /// <param name="parameters">The parameters to use, or null to create
    /// a new set.</param>
    /// <returns>Builder.</returns>
    public SqlWhereBuilder CreateWhere(SqlParameters? parameters = null) =>
        new(parameters ?? CreateParameters());

    /// <summary>
    /// Executes the specified query, mapping each of its rows with
    /// <paramref name="map"/>.
    /// </summary>
    /// <typeparam name="T">The type of the mapped items.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="map">The row mapping function.</param>
    /// <returns>Items.</returns>
    /// <exception cref="ArgumentNullException">query or map</exception>
    public List<T> Query<T>(SqlQuery query, Func<IDataRecord, T> map)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(map);

        using DbConnection connection = GetConnection(ConnectionString);
        connection.Open();
        using DbCommand command = query.CreateCommand(connection);
        using DbDataReader reader = command.ExecuteReader();
        List<T> items = [];
        while (reader.Read()) items.Add(map(reader));
        return items;
    }

    /// <summary>
    /// Executes the specified query, mapping each of its rows with
    /// <paramref name="map"/>.
    /// </summary>
    /// <typeparam name="T">The type of the mapped items.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="map">The row mapping function.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Items.</returns>
    /// <exception cref="ArgumentNullException">query or map</exception>
    public async Task<List<T>> QueryAsync<T>(SqlQuery query,
        Func<IDataRecord, T> map, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(map);

        await using DbConnection connection = GetConnection(ConnectionString);
        await connection.OpenAsync(cancel).ConfigureAwait(false);
        await using DbCommand command = query.CreateCommand(connection);
        await using DbDataReader reader = await command.ExecuteReaderAsync(cancel)
            .ConfigureAwait(false);
        List<T> items = [];
        while (await reader.ReadAsync(cancel).ConfigureAwait(false))
            items.Add(map(reader));
        return items;
    }

    private static T? ConvertScalar<T>(object? value)
    {
        if (value is null or DBNull) return default;
        if (value is T t) return t;
        Type type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T)Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Executes the specified query returning a single value (e.g. a count).
    /// </summary>
    /// <typeparam name="T">The type of the value, which is converted when
    /// required (e.g. from the <c>long</c> returned by a SQL <c>COUNT</c>
    /// to <c>int</c>).</typeparam>
    /// <param name="query">The query.</param>
    /// <returns>The value, or default if null.</returns>
    /// <exception cref="ArgumentNullException">query</exception>
    public T? Scalar<T>(SqlQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        using DbConnection connection = GetConnection(ConnectionString);
        connection.Open();
        using DbCommand command = query.CreateCommand(connection);
        return ConvertScalar<T>(command.ExecuteScalar());
    }

    /// <summary>
    /// Executes the specified query returning a single value (e.g. a count).
    /// </summary>
    /// <typeparam name="T">The type of the value, which is converted when
    /// required.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>The value, or default if null.</returns>
    /// <exception cref="ArgumentNullException">query</exception>
    public async Task<T?> ScalarAsync<T>(SqlQuery query,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using DbConnection connection = GetConnection(ConnectionString);
        await connection.OpenAsync(cancel).ConfigureAwait(false);
        await using DbCommand command = query.CreateCommand(connection);
        return ConvertScalar<T>(await command.ExecuteScalarAsync(cancel)
            .ConfigureAwait(false));
    }
}
