using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Embix.Core
{
    /// <summary>
    /// A buffer for a single thread, used by <see cref="BufferedIndexWriter"/>.
    /// </summary>
    public sealed class IndexWriterBuffer
    {
        private readonly object _locker = new object();
        private readonly IIndexRecordWriter _writer;
        private readonly Queue<IndexRecord> _queue;
        private readonly ConcurrentDictionary<int, bool> _savedTokens;

        /// <summary>
        /// Gets the count of buffered records.
        /// </summary>
        public int Count => _queue.Count;

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexWriterBuffer"/> class.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <exception cref="ArgumentNullException">writer</exception>
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
            _queue.Enqueue(record);
        }

        /// <summary>
        /// Extracts all the buffered records, thus emptying this buffer.
        /// </summary>
        /// <returns>The records.</returns>
        public IList<IndexRecord> Extract()
        {
            List<IndexRecord> records = new List<IndexRecord>();
            while (_queue.TryDequeue(out IndexRecord record))
                records.Add(record);
            return records;
        }

        /// <summary>
        /// Flushes this buffer.
        /// </summary>
        public void Flush()
        {
            lock(_locker)
            {
                List<IndexRecord> records = new List<IndexRecord>();
                int limit = _queue.Count;

                while (records.Count <= limit
                    && _queue.TryDequeue(out IndexRecord record))
                {
                    int tokenId = (int)record.Occurrence[0];

                    // write only if occurrence's token has already been written,
                    // or if the occurrence has the token with itself; else re-enqueue
                    if ((record.Token != null && (int)record.Token[0] == tokenId)
                        || _savedTokens.ContainsKey(tokenId)
                        || records.Any(r => r.Token != null && (int)r.Token[0] == tokenId))
                    {
                        records.Add(record);
                    }
                    else
                    {
                        _queue.Enqueue(record);
                        limit--;
                    }
                }
                _writer.Write(records);

                // update the saved tokens list
                foreach (var record in records.Where(r => r.Token != null))
                    _savedTokens[(int)record.Token[0]] = true;
            }
        }
    }
}
