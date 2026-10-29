using Embix.Core.Config;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Embix.Core;

/// <summary>
/// Buffered index writer. This uses one <see cref="IndexWriterBuffer"/>
/// for each partition.
/// </summary>
/// <seealso cref="IndexWriter" />
public abstract class BufferedIndexWriter : IndexWriter, IDisposable
{
    private readonly ConcurrentDictionary<int, IndexWriterBuffer> _buffers;
    // there is no concurrent HashSet, so we use a dictionary to store
    // the IDs of all the tokens which have been saved (i.e. have been
    // flushed from buffers). This is required to avoid saving occurrences
    // when their token is still buffered.
    private readonly ConcurrentDictionary<int, bool> _savedTokens;
    private bool _disposed;

    /// <summary>
    /// Gets or sets the size of the token buffer (=the maximum count of
    /// tokens it should contain).
    /// </summary>
    public int BufferSize { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferedIndexWriter" />
    /// class.
    /// </summary>
    /// <param name="profile">The profile to be used.</param>
    /// <exception cref="ArgumentNullException">profile</exception>
    protected BufferedIndexWriter(EmbixProfile profile) : base(profile)
    {
        _buffers = new ConcurrentDictionary<int, IndexWriterBuffer>();
        _savedTokens = new ConcurrentDictionary<int, bool>();
        BufferSize = 10000;
    }

    /// <summary>
    /// Gets a new record writer. The caller owns the returned writer.
    /// </summary>
    /// <returns>Record writer.</returns>
    protected abstract IIndexRecordWriter GetRecordWriter();

    /// <summary>
    /// Enqueues the specified token and its occurrence.
    /// </summary>
    /// <param name="partitionNr">The partition number.</param>
    /// <param name="record">The record.</param>
    protected void EnqueueRecord(int partitionNr, IndexRecord record)
    {
        IndexWriterBuffer buffer = _buffers.GetOrAdd(partitionNr,
            _ => new IndexWriterBuffer(GetRecordWriter(), _savedTokens));
        buffer.Add(record);

        // flush when buffer size limit reached (not counting records
        // deferred by a previous flush, which would cause a flush
        // at each write)
        if (buffer.PendingCount >= BufferSize) buffer.Flush();
    }

    /// <summary>
    /// Writes the specified token from the specified field.
    /// </summary>
    /// <param name="documentId">The ID of the document data being written
    /// belong to.</param>
    /// <param name="partitionNr">The partition number of the caller.</param>
    /// <param name="field">The field the token comes from.</param>
    /// <param name="token">The token value.</param>
    /// <param name="metadata">The optional metadata for this token.
    /// Some tokens might be accompanied by some metadata; e.g. language,
    /// read from their source. In this case they are stored into this
    /// dictionary.</param>
    public void Write(string documentId, int partitionNr, string field,
        string token,
        IDictionary<string, object>? metadata = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (Logger?.IsEnabled(LogLevel.Debug) == true)
        {
            Logger.LogDebug("[#{PartitionNr:000}] Write: {DocumentId}.{Field}: {Token}",
                partitionNr, documentId, field, token);
        }
        IndexRecord record = GetTokenRecord(documentId, field, token, metadata);
        EnqueueRecord(partitionNr, record);
    }

    /// <summary>
    /// Flushes all the buffers. When flushing all the buffers at the end
    /// of a document, we must ensure that we first write ALL the tokens
    /// from ALL the buffers; and then their occurrences. This must not be
    /// called while other threads are writing.
    /// </summary>
    public void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Logger?.LogInformation("Flushing all buffers");

        List<IndexRecord> records = [];
        foreach (IndexWriterBuffer buffer in _buffers.Values)
            records.AddRange(buffer.Extract());
        if (records.Count == 0) return;

        using (IIndexRecordWriter writer = GetRecordWriter())
        {
            writer.Write(records);
        }

        foreach (IndexRecord record in records)
        {
            if (record.Token != null) _savedTokens[record.TokenId] = true;
        }

        Logger?.LogInformation("Flushed: {Count}", records.Count);
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and
    /// unmanaged resources; <c>false</c> to release only unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            try
            {
                Flush();
            }
            finally
            {
                foreach (IndexWriterBuffer buffer in _buffers.Values)
                    buffer.Dispose();
                _buffers.Clear();
            }
        }
        _disposed = true;
    }

    /// <summary>
    /// Flushes all the buffers and releases resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
