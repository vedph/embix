using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Embix.Search;

/// <summary>
/// A set of named SQL parameters being collected while building a query.
/// Each added value gets a unique name, and its placeholder is returned
/// for use in the SQL code.
/// </summary>
public sealed class SqlParameters : IReadOnlyDictionary<string, object?>
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);
    private readonly string _namePrefix;
    private int _next;

    /// <summary>
    /// Gets the SQL dialect.
    /// </summary>
    public SqlDialect Dialect { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlParameters"/> class.
    /// </summary>
    /// <param name="dialect">The SQL dialect.</param>
    /// <param name="namePrefix">The prefix for automatically generated
    /// parameter names (default <c>p</c>, giving <c>p0</c>, <c>p1</c>...).
    /// </param>
    /// <exception cref="ArgumentNullException">dialect or namePrefix</exception>
    public SqlParameters(SqlDialect dialect, string namePrefix = "p")
    {
        Dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
        _namePrefix = namePrefix ?? throw new ArgumentNullException(nameof(namePrefix));
    }

    /// <summary>
    /// Adds a parameter with an automatically generated name.
    /// </summary>
    /// <param name="value">The value (null for SQL NULL).</param>
    /// <returns>The parameter placeholder for SQL code (e.g. <c>@p0</c>).
    /// </returns>
    public string Add(object? value)
    {
        string name;
        do
        {
            name = _namePrefix + _next.ToString(CultureInfo.InvariantCulture);
            _next++;
        } while (_values.ContainsKey(name));
        return Add(name, value);
    }

    /// <summary>
    /// Adds or replaces the parameter with the specified name.
    /// </summary>
    /// <param name="name">The name, without prefix.</param>
    /// <param name="value">The value (null for SQL NULL).</param>
    /// <returns>The parameter placeholder for SQL code.</returns>
    /// <exception cref="ArgumentNullException">name</exception>
    public string Add(string name, object? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        _values[name] = value;
        return Dialect.ParameterPrefix + name;
    }

    /// <summary>
    /// Adds the parameters to the specified command.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <exception cref="ArgumentNullException">command</exception>
    public void ApplyTo(IDbCommand command) =>
        ApplyTo(this, command);

    /// <summary>
    /// Adds the specified parameters to the specified command.
    /// </summary>
    /// <param name="parameters">The parameters.</param>
    /// <param name="command">The command.</param>
    /// <exception cref="ArgumentNullException">parameters or command</exception>
    public static void ApplyTo(IReadOnlyDictionary<string, object?> parameters,
        IDbCommand command)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(command);
        foreach (var kv in parameters)
        {
            IDbDataParameter p = command.CreateParameter();
            p.ParameterName = kv.Key;
            p.Value = kv.Value ?? DBNull.Value;
            command.Parameters.Add(p);
        }
    }

    #region IReadOnlyDictionary
    /// <inheritdoc/>
    public object? this[string key] => _values[key];
    /// <inheritdoc/>
    public IEnumerable<string> Keys => _values.Keys;
    /// <inheritdoc/>
    public IEnumerable<object?> Values => _values.Values;
    /// <inheritdoc/>
    public int Count => _values.Count;
    /// <inheritdoc/>
    public bool ContainsKey(string key) => _values.ContainsKey(key);
    /// <inheritdoc/>
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out object? value) =>
        _values.TryGetValue(key, out value);
    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() =>
        _values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();
    #endregion
}
