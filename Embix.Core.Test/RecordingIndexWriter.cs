using Embix.Core.Config;
using System;
using System.Collections.Generic;

namespace Embix.Core.Test;

/// <summary>
/// The log of all the writes performed by a set of
/// <see cref="RecordingRecordWriter"/>'s, in their global order.
/// </summary>
internal sealed class WriteLog
{
    private readonly List<(bool IsToken, IndexRecord Record)> _entries = [];

    public int CreatedWriters { get; set; }
    public int DisposedWriters { get; set; }

    public void Add(bool isToken, IndexRecord record)
    {
        lock (_entries) _entries.Add((isToken, record));
    }

    public IReadOnlyList<(bool IsToken, IndexRecord Record)> Entries
    {
        get
        {
            lock (_entries) return [.. _entries];
        }
    }
}

/// <summary>
/// Record writer which just records what it writes.
/// </summary>
internal sealed class RecordingRecordWriter(WriteLog log) : IIndexRecordWriter
{
    public void Write(IList<IndexRecord> records,
        IndexRecordParts parts = IndexRecordParts.All)
    {
        // as required by the contract: all tokens first, then occurrences
        lock (log)
        {
            foreach (IndexRecord r in records)
                if (r.Token != null) log.Add(true, r);
            foreach (IndexRecord r in records)
                log.Add(false, r);
        }
    }

    public void Dispose()
    {
        lock (log) log.DisposedWriters++;
    }
}

/// <summary>
/// Buffered index writer using <see cref="RecordingRecordWriter"/>.
/// </summary>
internal sealed class RecordingIndexWriter(EmbixProfile profile, WriteLog log)
    : BufferedIndexWriter(profile), IIndexWriter
{
    protected override IIndexRecordWriter GetRecordWriter()
    {
        lock (log) log.CreatedWriters++;
        return new RecordingRecordWriter(log);
    }
}

/// <summary>
/// Validates a write log against the ordering and uniqueness constraints
/// of the index tables.
/// </summary>
internal static class WriteLogValidator
{
    public static void Validate(WriteLog log)
    {
        HashSet<int> tokenIds = [];
        HashSet<(string, string)> tokenKeys = [];

        foreach (var (isToken, record) in log.Entries)
        {
            if (isToken)
            {
                int id = (int)record.Token![0];
                if (!tokenIds.Add(id))
                    throw new InvalidOperationException($"Token {id} written twice");
                var key = ((string)record.Token[2], (string)record.Token[1]);
                if (!tokenKeys.Add(key))
                    throw new InvalidOperationException($"Token {key} duplicated");
            }
            else if (!tokenIds.Contains(record.TokenId))
            {
                throw new InvalidOperationException(
                    $"Occurrence of token {record.TokenId} written before it");
            }
        }
    }
}
