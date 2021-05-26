using System;
using System.Collections.Generic;

namespace Embix.Core
{
    /// <summary>
    /// A generic record writer for <see cref="IIndexWriter"/>'s.
    /// </summary>
    public interface IIndexRecordWriter
    {
        /// <summary>
        /// Writes the specified record.
        /// </summary>
        /// <param name="record">The record, whose token can be null if
        /// already written.</param>
        /// <param name="parts">The parts of the record to be written.</param>
        void Write(IndexRecord record, IndexRecordParts parts = IndexRecordParts.All);

        /// <summary>
        /// Writes all the specified records at once.
        /// </summary>
        /// <param name="records">The records.</param>
        /// <param name="parts">The parts of the record to be written.</param>
        void Write(IList<IndexRecord> records,
            IndexRecordParts parts = IndexRecordParts.All);
    }

    [Flags]
    public enum IndexRecordParts
    {
        Token = 1,
        Occurrence = 2,
        All = Token | Occurrence
    }
}
