using Embix.Core.Config;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Embix.Core
{
    /// <summary>
    /// Buffered index writer. This uses one <see cref="IndexWriterBuffer"/>
    /// for each partition.
    /// </summary>
    /// <seealso cref="IndexWriter" />
    public abstract class BufferedIndexWriter : IndexWriter
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
        /// <exception cref="ArgumentNullException">profile or getConnection or
        /// compiler</exception>
        protected BufferedIndexWriter(EmbixProfile profile) : base(profile)
        {
            _buffers = new ConcurrentDictionary<int, IndexWriterBuffer>();
            _savedTokens = new ConcurrentDictionary<int, bool>();
            BufferSize = 100;
        }

        /// <summary>
        /// Gets a record writer.
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
            // create the partition's buffer if it does not exist
            if (!_buffers.ContainsKey(partitionNr))
            {
                _buffers[partitionNr] = new IndexWriterBuffer(
                    GetRecordWriter(), _savedTokens);
            }
            IndexWriterBuffer buffer = _buffers[partitionNr];
            buffer.Add(record);

            // flush when buffer size limit reached
            if (buffer.Count >= BufferSize) buffer.Flush();
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
            IDictionary<string, object> metadata = null)
        {
            Logger?.LogDebug($"[#{partitionNr:000}] Write: {documentId}.{field}: {token}");
            IndexRecord record = GetTokenRecord(documentId, field, token, metadata);
            EnqueueRecord(partitionNr, record);
        }

        private void FlushAllBuffers()
        {
            // when flushing all the buffers at the end of the document,
            // we must ensure that we first write ALL the tokens from ALL
            // the buffers; and then their occurrences.
            Logger?.LogInformation("Flushing all buffers");

            List<IndexRecord> records = new List<IndexRecord>();
            foreach (IndexWriterBuffer buffer in _buffers.Values)
                records.AddRange(buffer.Extract());
            IIndexRecordWriter writer = GetRecordWriter();

            writer.Write(records);

            Logger?.LogInformation("Flushed: " + records.Count);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and
        /// unmanaged resources; <c>false</c> to release only unmanaged resources.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing) FlushAllBuffers();
                _disposed = true;
            }
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)'
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
