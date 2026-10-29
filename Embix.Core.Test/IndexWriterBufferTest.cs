using System.Collections.Concurrent;
using System.Linq;
using Xunit;

namespace Embix.Core.Test;

public sealed class IndexWriterBufferTest
{
    private static readonly IndexRecordNames _names =
        new(["id", "value", "language"], ["token_id", "field", "target_id"]);

    private static IndexRecord TokenRecord(int id, string value) => new(_names)
    {
        Token = [id, value, ""],
        Occurrence = [id, "f", "1"]
    };

    private static IndexRecord OccurrenceRecord(int tokenId) => new(_names)
    {
        Occurrence = [tokenId, "f", "1"]
    };

    [Fact]
    public void Flush_OccurrenceOfUnsavedToken_Deferred()
    {
        WriteLog log = new();
        ConcurrentDictionary<int, bool> saved = new();
        IndexWriterBuffer buffer = new(new RecordingRecordWriter(log), saved);

        buffer.Add(TokenRecord(1, "a"));
        // token 2 is buffered by another partition
        buffer.Add(OccurrenceRecord(2));
        buffer.Add(OccurrenceRecord(1));
        buffer.Flush();

        Assert.Equal(3, log.Entries.Count);
        Assert.DoesNotContain(log.Entries, e => e.Record.TokenId == 2);
        Assert.Equal(1, buffer.Count);
        Assert.Equal(0, buffer.PendingCount);
        Assert.True(saved.ContainsKey(1));

        // once token 2 is saved, its occurrence gets written
        saved[2] = true;
        buffer.Flush();
        Assert.Equal(0, buffer.Count);
        Assert.Contains(log.Entries, e => !e.IsToken && e.Record.TokenId == 2);
    }

    [Fact]
    public void Flush_OccurrenceBeforeItsTokenInBuffer_Written()
    {
        WriteLog log = new();
        IndexWriterBuffer buffer = new(new RecordingRecordWriter(log), new());

        buffer.Add(OccurrenceRecord(1));
        buffer.Add(TokenRecord(1, "a"));
        buffer.Flush();

        Assert.Equal(0, buffer.Count);
        Assert.Equal(3, log.Entries.Count);
        WriteLogValidator.Validate(log);
    }

    [Fact]
    public void Extract_ReturnsAllAndEmpties()
    {
        IndexWriterBuffer buffer = new(new RecordingRecordWriter(new()), new());
        buffer.Add(TokenRecord(1, "a"));
        buffer.Add(OccurrenceRecord(2));

        var records = buffer.Extract();

        Assert.Equal(2, records.Count);
        Assert.Equal(0, buffer.Count);
        Assert.Equal(0, buffer.PendingCount);
        Assert.Equal([1, 2], records.Select(r => r.TokenId));
    }

    [Fact]
    public void Dispose_DisposesWriter()
    {
        WriteLog log = new();
        IndexWriterBuffer buffer = new(new RecordingRecordWriter(log), new());
        buffer.Dispose();
        buffer.Dispose();
        Assert.Equal(1, log.DisposedWriters);
    }
}
