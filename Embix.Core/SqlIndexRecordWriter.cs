using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Embix.Core;

/// <summary>
/// Generic ADO.NET-based index record writer. This writes records using
/// parameterized multi-row <c>INSERT</c> statements, which are supported by
/// all the major RDBMS. Database-specific writers may provide faster
/// strategies (e.g. <c>COPY</c> in PostgreSQL).
/// </summary>
/// <seealso cref="IIndexRecordWriter" />
public class SqlIndexRecordWriter : IIndexRecordWriter
{
    /// <summary>
    /// The default maximum number of parameters in a single command. This
    /// is safely below the limits of all the major RDBMS (SQL Server: 2100,
    /// MySql and PostgreSQL: 65535).
    /// </summary>
    public const int DEFAULT_MAX_PARAMETERS = 2000;

    private readonly IDbConnection _connection;
    private readonly string _openQuote;
    private readonly string _closeQuote;
    private bool _disposed;

    /// <summary>
    /// Gets or sets the maximum number of parameters in a single command.
    /// </summary>
    public int MaxParameters { get; set; } = DEFAULT_MAX_PARAMETERS;

    /// <summary>
    /// Gets or sets the name of the token table.
    /// </summary>
    public string TokenTable { get; set; } = "eix_token";

    /// <summary>
    /// Gets or sets the name of the occurrence table.
    /// </summary>
    public string OccurrenceTable { get; set; } = "eix_occurrence";

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlIndexRecordWriter"/>
    /// class.
    /// </summary>
    /// <param name="connection">The connection to use. This is owned by
    /// the writer, which opens it when required and disposes it when
    /// disposed.</param>
    /// <param name="openQuote">The opening quote for SQL identifiers.</param>
    /// <param name="closeQuote">The closing quote for SQL identifiers.</param>
    /// <exception cref="ArgumentNullException">connection</exception>
    public SqlIndexRecordWriter(IDbConnection connection,
        string openQuote = "\"", string closeQuote = "\"")
    {
        _connection = connection
            ?? throw new ArgumentNullException(nameof(connection));
        _openQuote = openQuote ?? throw new ArgumentNullException(nameof(openQuote));
        _closeQuote = closeQuote ?? throw new ArgumentNullException(nameof(closeQuote));
    }

    /// <summary>
    /// Quotes the specified SQL identifier.
    /// </summary>
    /// <param name="name">The identifier.</param>
    /// <returns>Quoted identifier.</returns>
    protected string QuoteIdentifier(string name) =>
        _openQuote + name.Replace(_closeQuote, _closeQuote + _closeQuote)
        + _closeQuote;

    private void EnsureOpen()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
    }

    /// <summary>
    /// Inserts the specified rows into <paramref name="table"/>, using as
    /// many multi-row <c>INSERT</c> commands as required to respect
    /// <see cref="MaxParameters"/>.
    /// </summary>
    /// <param name="table">The table name.</param>
    /// <param name="names">The column names.</param>
    /// <param name="rows">The rows.</param>
    protected virtual void InsertRows(string table, IList<string> names,
        IList<IList<object>> rows)
    {
        if (rows.Count == 0) return;
        EnsureOpen();

        string head = $"INSERT INTO {QuoteIdentifier(table)} (" +
            string.Join(", ", names.Select(QuoteIdentifier)) + ") VALUES ";
        int rowsPerCommand = Math.Max(1, MaxParameters / names.Count);
        StringBuilder sql = new();

        for (int start = 0; start < rows.Count; start += rowsPerCommand)
        {
            int end = Math.Min(rows.Count, start + rowsPerCommand);
            using IDbCommand cmd = _connection.CreateCommand();
            sql.Clear().Append(head);

            int p = 0;
            for (int r = start; r < end; r++)
            {
                if (r > start) sql.Append(", ");
                sql.Append('(');
                for (int c = 0; c < names.Count; c++)
                {
                    string paramName = "@p" + p.ToString(CultureInfo.InvariantCulture);
                    p++;
                    if (c > 0) sql.Append(", ");
                    sql.Append(paramName);

                    IDbDataParameter param = cmd.CreateParameter();
                    param.ParameterName = paramName;
                    param.Value = rows[r][c] ?? DBNull.Value;
                    cmd.Parameters.Add(param);
                }
                sql.Append(')');
            }
            cmd.CommandText = sql.ToString();
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Writes all the specified records at once.
    /// </summary>
    /// <param name="records">The records.</param>
    /// <param name="parts">The parts of the record to be written.</param>
    /// <exception cref="ArgumentNullException">records</exception>
    public void Write(IList<IndexRecord> records,
        IndexRecordParts parts = IndexRecordParts.All)
    {
        ArgumentNullException.ThrowIfNull(records);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (records.Count == 0) return;

        IndexRecordNames names = records[0].Names;

        // write tokens
        if ((parts & IndexRecordParts.Token) != 0)
        {
            IList<object>[] tokens = [.. records
                .Where(r => r.Token != null)
                .Select(r => r.Token!)];
            InsertRows(TokenTable, names.TokenNames, tokens);
        }

        // write occurrences
        if ((parts & IndexRecordParts.Occurrence) != 0)
        {
            IList<object>[] occurrences = [.. records
                .Where(r => r.Occurrence != null)
                .Select(r => r.Occurrence!)];
            InsertRows(OccurrenceTable, names.OccurrenceNames, occurrences);
        }
    }

    /// <summary>
    /// Releases resources.
    /// </summary>
    /// <param name="disposing">True to release managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing) _connection.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// Disposes the connection.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
