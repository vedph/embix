using Embix.Core;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Embix.PgSql;

/// <summary>
/// PostgreSql index record writer. This uses <c>COPY ... FROM STDIN</c>,
/// the fastest way of bulk loading data into PostgreSql. The text format
/// is used, so that values are parsed by the server according to their
/// target column type: this way, metadata columns can have any type.
/// </summary>
/// <seealso cref="IIndexRecordWriter" />
public sealed class PgSqlIndexRecordWriter : IIndexRecordWriter
{
    private readonly NpgsqlConnection _connection;
    private readonly Dictionary<IList<string>, string> _copyCommands = [];
    private bool _disposed;

    /// <summary>
    /// Gets or sets the name of the token table.
    /// </summary>
    public string TokenTable { get; set; } = "eix_token";

    /// <summary>
    /// Gets or sets the name of the occurrence table.
    /// </summary>
    public string OccurrenceTable { get; set; } = "eix_occurrence";

    /// <summary>
    /// Initializes a new instance of the <see cref="PgSqlIndexRecordWriter"/>
    /// class.
    /// </summary>
    /// <param name="connection">The connection to use. This is owned by
    /// the writer, which opens it when required and disposes it when
    /// disposed.</param>
    /// <exception cref="ArgumentNullException">connection</exception>
    public PgSqlIndexRecordWriter(NpgsqlConnection connection)
    {
        _connection = connection
            ?? throw new ArgumentNullException(nameof(connection));
    }

    private static string QuoteIdentifier(string name) =>
        "\"" + name.Replace("\"", "\"\"") + "\"";

    private string GetCopyCommand(string table, IList<string> names)
    {
        if (!_copyCommands.TryGetValue(names, out string? sql))
        {
            sql = $"COPY {QuoteIdentifier(table)} (" +
                string.Join(", ", names.Select(QuoteIdentifier)) +
                ") FROM STDIN";
            _copyCommands[names] = sql;
        }
        return sql;
    }

    /// <summary>
    /// Appends the specified value to <paramref name="sb"/> in the COPY text
    /// format.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="sb">The target string builder.</param>
    internal static void AppendValue(object? value, StringBuilder sb)
    {
        switch (value)
        {
            case null:
            case DBNull:
                sb.Append(@"\N");
                return;
            case string s:
                AppendEscaped(s, sb);
                return;
            case bool b:
                sb.Append(b ? 't' : 'f');
                return;
            case DateTime dt:
                sb.Append(dt.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF",
                    CultureInfo.InvariantCulture));
                return;
            case DateTimeOffset dto:
                sb.Append(dto.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFFzzz",
                    CultureInfo.InvariantCulture));
                return;
            case byte[] bytes:
                // bytea hex format, with its backslash escaped for COPY
                sb.Append(@"\\x").Append(Convert.ToHexString(bytes));
                return;
            case IFormattable f:
                AppendEscaped(f.ToString(null, CultureInfo.InvariantCulture), sb);
                return;
            default:
                AppendEscaped(value.ToString() ?? "", sb);
                return;
        }
    }

    private static void AppendEscaped(string s, StringBuilder sb)
    {
        foreach (char c in s)
        {
            switch (c)
            {
                case '\\': sb.Append(@"\\"); break;
                case '\t': sb.Append(@"\t"); break;
                case '\n': sb.Append(@"\n"); break;
                case '\r': sb.Append(@"\r"); break;
                default: sb.Append(c); break;
            }
        }
    }

    private void CopyRows(string table, IList<string> names,
        IEnumerable<IList<object>> rows)
    {
        StringBuilder line = new();
        using TextWriter writer = _connection.BeginTextImport(
            GetCopyCommand(table, names));
        foreach (IList<object> row in rows)
        {
            line.Clear();
            for (int i = 0; i < row.Count; i++)
            {
                if (i > 0) line.Append('\t');
                AppendValue(row[i], line);
            }
            line.Append('\n');
            writer.Write(line);
        }
        // disposing the writer completes the COPY
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

        if (_connection.State != System.Data.ConnectionState.Open)
            _connection.Open();
        IndexRecordNames names = records[0].Names;

        if ((parts & IndexRecordParts.Token) != 0
            && records.Any(r => r.Token != null))
        {
            CopyRows(TokenTable, names.TokenNames,
                records.Where(r => r.Token != null).Select(r => r.Token!));
        }

        if ((parts & IndexRecordParts.Occurrence) != 0
            && records.Any(r => r.Occurrence != null))
        {
            CopyRows(OccurrenceTable, names.OccurrenceNames,
                records.Where(r => r.Occurrence != null)
                    .Select(r => r.Occurrence!));
        }
    }

    /// <summary>
    /// Disposes the connection.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _connection.Dispose();
        _disposed = true;
    }
}
