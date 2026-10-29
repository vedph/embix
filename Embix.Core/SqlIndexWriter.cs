using Embix.Core.Config;
using System;

namespace Embix.Core;

/// <summary>
/// Generic SQL-based index writer, using <see cref="SqlIndexRecordWriter"/>.
/// </summary>
public class SqlIndexWriter : BufferedIndexWriter, IIndexWriter
{
    private readonly string _openQuote;
    private readonly string _closeQuote;

    /// <summary>
    /// Gets the connection factory.
    /// </summary>
    protected IDbConnectionFactory ConnectionFactory { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlIndexWriter" /> class.
    /// </summary>
    /// <param name="profile">The profile to be used.</param>
    /// <param name="connFactory">The connection factory.</param>
    /// <param name="openQuote">The opening quote for SQL identifiers.</param>
    /// <param name="closeQuote">The closing quote for SQL identifiers.</param>
    /// <exception cref="ArgumentNullException">profile or connFactory
    /// </exception>
    public SqlIndexWriter(
        EmbixProfile profile,
        IDbConnectionFactory connFactory,
        string openQuote = "\"",
        string closeQuote = "\"") : base(profile)
    {
        ConnectionFactory = connFactory
            ?? throw new ArgumentNullException(nameof(connFactory));
        _openQuote = openQuote;
        _closeQuote = closeQuote;
    }

    /// <summary>
    /// Gets a new record writer.
    /// </summary>
    /// <returns>Record writer.</returns>
    protected override IIndexRecordWriter GetRecordWriter() =>
        new SqlIndexRecordWriter(ConnectionFactory.GetConnection(),
            _openQuote, _closeQuote);
}
