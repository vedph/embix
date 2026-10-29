using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Embix.Core;

/// <summary>
/// A buffer for a single thread, used by <see cref="BufferedIndexWriter"/>.
/// The buffer owns its record writer, and disposes it when disposed.
/// </summary>
public sealed class IndexWriterBuffer : IDisposable
{
    private readonly object _locker = new();
    private readonly IIndexRecordWriter _writer;
    private readonly Queue<IndexRecord> _queue;
    private readonly ConcurrentDictionary<int, bool> _savedTokens;
    private bool _disposed;

    /// <summary>
    /// Gets the count of buffered records, including those deferred by
    /// a previous <see cref="Flush"/> because their token was not yet saved.
    /// </summary>
    public int Count => _queue.Count;

    /// <summary>
    /// Gets the count of records added since the last flush.
    /// </summary>
    public int PendingCount { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexWriterBuffer"/> class.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="savedTokens">The IDs of the tokens already saved, shared
    /// among all the buffers.</param>
    /// <exception cref="ArgumentNullException">writer or savedTokens</exception>
    public IndexWriterBuffer(IIndexRecordWriter writer,
        ConcurrentDictionary<int, bool> savedTokens)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _savedTokens = savedTokens
            ?? throw new ArgumentNullException(nameof(savedTokens));
        _queue = new Queue<IndexRecord>();
    }

    /// <summary>
    /// Adds the specified record to the buffer.
    /// </summary>
    /// <param name="record">The record.</param>
    public void Add(IndexRecord record)
    {
        lock (_locker)
        {
            _queue.Enqueue(record);
            PendingCount++;
        }
    }

    /// <summary>
    /// Extracts all the buffered records, thus emptying this buffer.
    /// </summary>
    /// <returns>The records.</returns>
    public IList<IndexRecord> Extract()
    {
        lock (_locker)
        {
            List<IndexRecord> records = [.. _queue];
            _queue.Clear();
            PendingCount = 0;
            return records;
        }
    }

    /// <summary>
    /// Flushes this buffer. Records whose token is neither saved nor
    /// included in this buffer (because it is still buffered by another
    /// partition) are kept in the buffer for a later flush.
    /// </summary>
    public void Flush()
    {
        lock (_locker)
        {
            PendingCount = 0;
            if (_queue.Count == 0) return;

            // collect the tokens carried by this buffer
            HashSet<int> bufferedTokens = [];
            foreach (IndexRecord record in _queue)
            {
                if (record.Token != null) bufferedTokens.Add(record.TokenId);
            }

            // pick records which can be written, defer the others
            List<IndexRecord> records = new(_queue.Count);
            int count = _queue.Count;
            for (int i = 0; i < count; i++)
            {
                IndexRecord record = _queue.Dequeue();
                int tokenId = record.TokenId;
                if (bufferedTokens.Contains(tokenId)
                    || _savedTokens.ContainsKey(tokenId))
                {
                    records.Add(record);
                }
                else
                {
                    _queue.Enqueue(record);
                }
            }
            if (records.Count == 0) return;

            _writer.Write(records);

            // update the saved tokens list
            foreach (int id in bufferedTokens) _savedTokens[id] = true;
        }
    }

    /// <summary>
    /// Disposes the record writer.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _writer.Dispose();
        _disposed = true;
    }
}
