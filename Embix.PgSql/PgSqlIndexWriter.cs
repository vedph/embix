using Embix.Core;
using Embix.Core.Config;
using Npgsql;
using System;

namespace Embix.PgSql;

/// <summary>
/// PostgreSql index writer, using <see cref="PgSqlIndexRecordWriter"/>.
/// </summary>
public sealed class PgSqlIndexWriter : BufferedIndexWriter, IIndexWriter
{
    private readonly IDbConnectionFactory _connFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PgSqlIndexWriter"/> class.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <param name="connFactory">The connection factory, which must return
    /// <see cref="NpgsqlConnection"/>'s.</param>
    /// <exception cref="ArgumentNullException">profile or connFactory
    /// </exception>
    public PgSqlIndexWriter(EmbixProfile profile,
        IDbConnectionFactory connFactory) : base(profile)
    {
        _connFactory = connFactory
            ?? throw new ArgumentNullException(nameof(connFactory));
    }

    /// <summary>
    /// Gets a new record writer.
    /// </summary>
    /// <returns>Record writer.</returns>
    protected override IIndexRecordWriter GetRecordWriter() =>
        new PgSqlIndexRecordWriter((NpgsqlConnection)_connFactory.GetConnection());
}
