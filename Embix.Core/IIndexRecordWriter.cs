using System;
using System.Collections.Generic;

namespace Embix.Core;

/// <summary>
/// A generic record writer for <see cref="IIndexWriter"/>'s. Implementors
/// usually own a database connection, released when disposed.
/// </summary>
public interface IIndexRecordWriter : IDisposable
{
    /// <summary>
    /// Writes all the specified records at once. Implementors must write
    /// all the tokens before all the occurrences, so that no occurrence
    /// in <paramref name="records"/> is written before its token.
    /// </summary>
    /// <param name="records">The records.</param>
    /// <param name="parts">The parts of the record to be written.</param>
    void Write(IList<IndexRecord> records,
        IndexRecordParts parts = IndexRecordParts.All);
}

/// <summary>
/// The parts of an <see cref="IndexRecord"/>.
/// </summary>
[Flags]
public enum IndexRecordParts
{
    /// <summary>The token.</summary>
    Token = 1,
    /// <summary>The occurrence.</summary>
    Occurrence = 2,
    /// <summary>Both token and occurrence.</summary>
    All = Token | Occurrence
}
