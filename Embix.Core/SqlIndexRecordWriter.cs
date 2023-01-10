using SqlKata;
using SqlKata.Execution;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Embix.Core;

/// <summary>
/// SQL-based index record writer.
/// </summary>
/// <seealso cref="IIndexRecordWriter" />
public sealed class SqlIndexRecordWriter : IIndexRecordWriter
{
    private readonly QueryFactory _queryFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlIndexRecordWriter"/>
    /// class.
    /// </summary>
    /// <param name="queryFactory">The query factory to use to build and
    /// execute SQL queries.</param>
    /// <exception cref="ArgumentNullException">queryFactory</exception>
    public SqlIndexRecordWriter(QueryFactory queryFactory)
    {
        _queryFactory = queryFactory
            ?? throw new ArgumentNullException(nameof(queryFactory));
    }

    /// <summary>
    /// Writes the specified record.
    /// </summary>
    /// <param name="record">The record, whose token can be null if
    /// already written.</param>
    /// <param name="parts">The parts of the record to be written.</param>
    /// <exception cref="ArgumentNullException">record</exception>
    public void Write(IndexRecord record,
        IndexRecordParts parts = IndexRecordParts.All)
    {
        if (record == null)
            throw new ArgumentNullException(nameof(record));

        // write token if any
        if (record.Token != null && ((parts & IndexRecordParts.Token) != 0))
        {
            Query query = _queryFactory.Query("eix_token")
                .AsInsert(record.Names.TokenNames, record.Token);
            _queryFactory.Execute(query);
        }

        // write occurrence if any
        if (record.Occurrence != null &&
            ((parts & IndexRecordParts.Occurrence) != 0))
        {
            Query query = _queryFactory.Query("eix_occurrence")
                .AsInsert(record.Names.OccurrenceNames, record.Occurrence);
            _queryFactory.Execute(query);
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
        if (records == null)
            throw new ArgumentNullException(nameof(records));
        if (records.Count == 0) return;

        // write tokens
        if ((parts & IndexRecordParts.Token) != 0)
        {
            var tokens = records
                .Where(r => r.Token != null)
                .Select(r => r.Token).ToArray();
            if (tokens.Length > 0)
            {
                Query query = _queryFactory.Query("eix_token")
                    .AsInsert(records[0].Names.TokenNames, tokens);
                _queryFactory.Execute(query);
            }
        }

        // write occurences
        if ((parts & IndexRecordParts.Occurrence) != 0)
        {
            var occurrences = records
                .Where(r => r.Occurrence != null)
                .Select(r => r.Occurrence).ToArray();
            if (occurrences.Length > 0)
            {
                Query query = _queryFactory.Query("eix_occurrence")
                    .AsInsert(records[0].Names.OccurrenceNames, occurrences);
                _queryFactory.Execute(query);
            }
        }
    }
}
