using Embix.Core.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Embix.Core.Test;

public sealed class BufferedIndexWriterTest
{
    private static EmbixProfile GetProfile() => TestHelper.CreateProfile(
        "{ \"MetadataFields\": [\"rank\", \"note\"], " +
        "\"TokenFieldLengths\": { \"value\": 5 }, " +
        "\"OccurrenceFieldLengths\": { \"note\": 3 } }");

    [Fact]
    public void Write_BelowBufferSize_WritesNothingUntilFlush()
    {
        WriteLog log = new();
        using RecordingIndexWriter writer = new(GetProfile(), log)
        {
            BufferSize = 10
        };

        writer.Write("d", 1, "f", "alpha");
        writer.Write("d", 1, "f", "beta");
        Assert.Empty(log.Entries);

        writer.Flush();
        Assert.Equal(4, log.Entries.Count);
        WriteLogValidator.Validate(log);
    }

    [Fact]
    public void Write_RepeatedToken_TokenWrittenOnce()
    {
        WriteLog log = new();
        using (RecordingIndexWriter writer = new(GetProfile(), log)
        {
            BufferSize = 2
        })
        {
            for (int i = 0; i < 5; i++) writer.Write("d", 1, "f", "alpha");
        }

        Assert.Single(log.Entries, e => e.IsToken);
        Assert.Equal(5, log.Entries.Count(e => !e.IsToken));
        WriteLogValidator.Validate(log);
    }

    [Fact]
    public void Write_Metadata_MappedAndTruncated()
    {
        WriteLog log = new();
        using (RecordingIndexWriter writer = new(GetProfile(), log))
        {
            writer.Write("d", 1, "fld", "abcdefgh", new Dictionary<string, object>
            {
                [IndexWriter.META_TARGET_ID] = 42,
                [IndexWriter.META_LANGUAGE] = "grc",
                ["rank"] = 3,
                ["note"] = "abcdef"
            });
        }

        IndexRecord record = log.Entries.Single(e => e.IsToken).Record;
        // token: id, value (truncated to 5), language
        Assert.Equal("abcde", record.Token![1]);
        Assert.Equal("grc", record.Token[2]);
        // occurrence: token_id, field, target_id, rank, note (truncated to 3)
        Assert.Equal(record.Token[0], record.Occurrence![0]);
        Assert.Equal("fld", record.Occurrence[1]);
        Assert.Equal("42", record.Occurrence[2]);
        Assert.Equal(3, record.Occurrence[3]);
        Assert.Equal("abc", record.Occurrence[4]);
    }

    [Fact]
    public void Write_SameValueDifferentLanguage_DistinctTokens()
    {
        WriteLog log = new();
        using (RecordingIndexWriter writer = new(GetProfile(), log))
        {
            writer.Write("d", 1, "f", "a", new Dictionary<string, object>
            {
                [IndexWriter.META_LANGUAGE] = "eng"
            });
            writer.Write("d", 1, "f", "a", new Dictionary<string, object>
            {
                [IndexWriter.META_LANGUAGE] = "ita"
            });
            writer.Write("d", 1, "f", "a");
        }

        Assert.Equal(3, log.Entries.Count(e => e.IsToken));
        WriteLogValidator.Validate(log);
    }

    [Fact]
    public async Task Write_ConcurrentPartitions_OrderAndUniquenessOk()
    {
        const int partitions = 6;
        const int tokensPerPartition = 3000;
        string[] vocabulary = [.. Enumerable.Range(0, 200).Select(n => "w" + n)];

        WriteLog log = new();
        using (RecordingIndexWriter writer = new(GetProfile(), log)
        {
            BufferSize = 7
        })
        {
            Task[] tasks = [.. Enumerable.Range(1, partitions).Select(p =>
                Task.Run(() =>
                {
                    Random random = new(p);
                    for (int i = 0; i < tokensPerPartition; i++)
                    {
                        writer.Write("d", p, "f",
                            vocabulary[random.Next(vocabulary.Length)]);
                    }
                }))];
            await Task.WhenAll(tasks);
            writer.Flush();

            // nothing left to write
            int count = log.Entries.Count;
            writer.Flush();
            Assert.Equal(count, log.Entries.Count);
        }

        Assert.Equal(partitions * tokensPerPartition,
            log.Entries.Count(e => !e.IsToken));
        Assert.Equal(vocabulary.Length, log.Entries.Count(e => e.IsToken));
        WriteLogValidator.Validate(log);
    }

    [Fact]
    public void Dispose_FlushesAndDisposesRecordWriters()
    {
        WriteLog log = new();
        RecordingIndexWriter writer = new(GetProfile(), log);
        writer.Write("d", 1, "f", "alpha");
        writer.Write("d", 2, "f", "beta");

        writer.Dispose();

        Assert.Equal(4, log.Entries.Count);
        Assert.Equal(log.CreatedWriters, log.DisposedWriters);
        Assert.Throws<ObjectDisposedException>(
            () => writer.Write("d", 1, "f", "gamma"));
    }

    [Fact]
    public void Flush_AfterFlush_WriterStillUsable()
    {
        WriteLog log = new();
        using (RecordingIndexWriter writer = new(GetProfile(), log))
        {
            writer.Write("d", 1, "f", "alpha");
            writer.Flush();
            writer.Write("d", 1, "f", "alpha");
            writer.Write("d", 1, "f", "beta");
        }

        Assert.Equal(2, log.Entries.Count(e => e.IsToken));
        Assert.Equal(3, log.Entries.Count(e => !e.IsToken));
        WriteLogValidator.Validate(log);
    }
}
