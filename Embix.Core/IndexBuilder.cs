using Embix.Core.Config;
using Embix.Core.Filters;
using Fusi.Tools;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Embix.Core;

/// <summary>
/// Embedded index builder. This is the main class in the assembly.
/// Once you create it with an <see cref="EmbixProfile"/>, a connection
/// provider, and an <see cref="IIndexWriter"/>, it calculates a number
/// of partitions for the source records, and spawns a number of parallel
/// threads to index each partition.
/// </summary>
public sealed class IndexBuilder : IDisposable
{
    private readonly EmbixProfile _profile;
    private readonly IDbConnectionFactory _connFactory;
    private readonly IIndexWriter _writer;
    private readonly bool _ownsWriter;
    private int _partitionCount;
    private bool _disposed;

    /// <summary>
    /// The prefix assigned to field names when they must be consumed
    /// as metadata by the builder.
    /// </summary>
    public const string META_FIELD_PREFIX = "m_";

    /// <summary>
    /// The field code used as the default key in the field-to-component
    /// maps of <see cref="DocumentDefinition"/>.
    /// </summary>
    public const string DEFAULT_FIELD = "*";

    // the count of rows passed at once from the reader to the workers
    private const int ROW_BATCH_SIZE = 100;

    #region Properties
    /// <summary>
    /// The count of input records partitions in building an index. If 1,
    /// no partitioning will be applied; otherwise, the total count of
    /// records to be processed will be divided by this value, and each
    /// resulting subset will be processed in parallel.
    /// </summary>
    public int PartitionCount
    {
        get => _partitionCount;
        set => _partitionCount = value < 1 ? 1 : value;
    }

    /// <summary>
    /// The minium size for a partition in building an index. When partitions
    /// size is lower than this value, no partitioning occurs, whatever
    /// the value of <see cref="PartitionCount"/>. This avoids adding
    /// multi-threading overhead in case of a relatively low total number
    /// of records to be processed.
    /// </summary>
    public int MinPartitionSize { get; set; }

    /// <summary>
    /// Gets or sets the records limit. If greater than 0, no more than
    /// this number of records will be processed. This is essentially a
    /// test feature, to allow processing just a few records at once.
    /// </summary>
    public int RecordLimit { get; set; }

    /// <summary>
    /// Gets or sets the logger.
    /// </summary>
    public ILogger? Logger { get; set; }
    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexBuilder"/> class.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <param name="connFactory">The connection factory.</param>
    /// <param name="writer">The index writer.</param>
    /// <param name="ownsWriter">True if this builder owns
    /// <paramref name="writer"/>, and thus must dispose it when disposed.
    /// Pass false when sharing the same writer among several builders.</param>
    /// <exception cref="ArgumentNullException">profile, connFactory or
    /// writer</exception>
    public IndexBuilder(
        EmbixProfile profile,
        IDbConnectionFactory connFactory,
        IIndexWriter writer,
        bool ownsWriter = true)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _connFactory = connFactory
            ?? throw new ArgumentNullException(nameof(connFactory));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _ownsWriter = ownsWriter;
        PartitionCount = 2;
        MinPartitionSize = 100;
    }

    /// <summary>
    /// Gets the count of records from the specified SQL command.
    /// </summary>
    /// <param name="countSql">The select count SQL command.</param>
    /// <param name="connection">The connection to be used, or null to use
    /// the internal provider to get one.</param>
    /// <returns>Count.</returns>
    /// <exception cref="ArgumentNullException">countSql</exception>
    public int GetRecordCount(string countSql, IDbConnection? connection = null)
    {
        ArgumentNullException.ThrowIfNull(countSql);

        IDbConnection? owned = null;
        bool mustClose = false;
        try
        {
            if (connection == null)
            {
                connection = owned = _connFactory.GetConnection();
            }
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
                mustClose = true;
            }

            using IDbCommand cmd = connection.CreateCommand();
            cmd.CommandText = countSql;
            int result = Convert.ToInt32(cmd.ExecuteScalar(),
                CultureInfo.InvariantCulture);

            // apply limit if any
            if (RecordLimit > 0 && result > RecordLimit) result = RecordLimit;
            return result;
        }
        finally
        {
            if (mustClose) connection?.Close();
            owned?.Dispose();
        }
    }

    /// <summary>
    /// Calculates the partition count to be used by
    /// <see cref="BuildAsync(string, CancellationToken, IProgress{ProgressReport})"/>.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <returns>Tuple with 1=partition count and 2=total records count.</returns>
    /// <exception cref="ArgumentNullException">documentId</exception>
    /// <exception cref="InvalidOperationException">Document not found</exception>
    public Tuple<int, int> CalculatePartitionCount(string documentId)
    {
        ArgumentNullException.ThrowIfNull(documentId);

        DocumentDefinition doc = _profile.GetDocument(documentId)
            ?? throw new InvalidOperationException(
                "Document not found: " + documentId);

        int total = GetRecordCount(doc.CountSql!);
        return Tuple.Create(GetPartitionCount(total), total);
    }

    private int GetPartitionCount(int total) =>
        total < MinPartitionSize ? 1 : Math.Max(1, Math.Min(_partitionCount, total));

    private static void Report(IProgress<ProgressReport>? progress,
        string message, int percent = 0)
    {
        // a new report each time, as Progress<T> handlers run asynchronously
        progress?.Report(new ProgressReport
        {
            Message = message,
            Percent = percent
        });
    }

    /// <summary>
    /// Builds the index asynchronously. A single reader streams the
    /// document's records from the database, while
    /// <see cref="PartitionCount"/> parallel workers process them. This way,
    /// records are read once, in any order, and no paging is required:
    /// the <c>{0}</c> (offset) and <c>{1}</c> (limit) placeholders in
    /// <see cref="DocumentDefinition.DataSql"/> are optional, and if present
    /// they are replaced with 0 and the count of records to process. When all
    /// the records have been processed, the writer is flushed.
    /// </summary>
    /// <param name="documentId">The ID of the "document" in the profile.
    /// </param>
    /// <param name="cancel">The cancel token.</param>
    /// <param name="progress">The optional progress reporter.</param>
    /// <exception cref="ArgumentNullException">document ID</exception>
    /// <exception cref="InvalidOperationException">document ID not found
    /// </exception>
    public async Task BuildAsync(
        string documentId,
        CancellationToken cancel = default,
        IProgress<ProgressReport>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        ObjectDisposedException.ThrowIf(_disposed, this);

        DocumentDefinition doc = _profile.GetDocument(documentId)
            ?? throw new InvalidOperationException(
                "Document not found: " + documentId);
        if (string.IsNullOrEmpty(doc.CountSql) || string.IsNullOrEmpty(doc.DataSql))
        {
            throw new InvalidOperationException(
                $"Document {documentId} has no CountSql or DataSql");
        }

        // calculate partitions
        Report(progress, "Calculating partitions");
        int total = GetRecordCount(doc.CountSql);
        int partitionCount = GetPartitionCount(total);
        Report(progress, "Partitions: " + partitionCount);
        if (total == 0) return;

        // the reader produces batches of rows, consumed by the workers;
        // the channel is bounded to limit memory usage when the reader
        // is faster than workers
        Channel<RowBatch> channel = Channel.CreateBounded<RowBatch>(
            new BoundedChannelOptions(partitionCount * 4)
            {
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait
            });
        // any failure cancels all the tasks, so that the reader cannot remain
        // blocked on a full channel
        using CancellationTokenSource failure =
            CancellationTokenSource.CreateLinkedTokenSource(cancel);
        BuildContext context = new(documentId, doc, total, progress,
            failure.Token);

        Task[] tasks = new Task[partitionCount + 1];
        for (int i = 0; i < partitionCount; i++)
        {
            int partitionNr = i + 1;
            tasks[i] = RunOrCancel(() => ConsumeAsync(channel.Reader,
                partitionNr, context), failure);
        }
        tasks[partitionCount] = RunOrCancel(() => ProduceAsync(channel.Writer,
            context), failure);

        Task all = Task.WhenAll(tasks);
        try
        {
            await all.ConfigureAwait(false);
        }
        catch
        {
            cancel.ThrowIfCancellationRequested();
            // rethrow the original failure rather than the cancellations
            // or channel closures it caused
            Exception? cause = all.Exception?.InnerExceptions.FirstOrDefault(
                e => e is not OperationCanceledException
                    and not ChannelClosedException);
            if (cause != null) ExceptionDispatchInfo.Capture(cause).Throw();
            throw;
        }

        _writer.Flush();
    }

    private static Task RunOrCancel(Func<Task> action,
        CancellationTokenSource failure)
    {
        return Task.Run(async () =>
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch
            {
                failure.Cancel();
                throw;
            }
        });
    }

    private async Task ProduceAsync(ChannelWriter<RowBatch> writer,
        BuildContext context)
    {
        try
        {
            using IDbConnection connection = _connFactory.GetConnection();
            connection.Open();
            using IDbCommand cmd = connection.CreateCommand();
            // {0}=skip, {1}=limit
            cmd.CommandText = string.Format(CultureInfo.InvariantCulture,
                context.Document.DataSql!, 0, context.Total);
            using IDataReader reader = cmd.ExecuteReader();

            string[] names = new string[reader.FieldCount];
            for (int i = 0; i < names.Length; i++) names[i] = reader.GetName(i);

            int count = 0;
            List<object[]> rows = new(ROW_BATCH_SIZE);
            while (count < context.Total && reader.Read())
            {
                context.Cancel.ThrowIfCancellationRequested();
                object[] values = new object[names.Length];
                reader.GetValues(values);
                rows.Add(values);
                count++;

                if (rows.Count == ROW_BATCH_SIZE)
                {
                    await writer.WriteAsync(new RowBatch(names, [.. rows]),
                        context.Cancel).ConfigureAwait(false);
                    rows.Clear();
                }
            }
            if (rows.Count > 0)
            {
                await writer.WriteAsync(new RowBatch(names, [.. rows]),
                    context.Cancel).ConfigureAwait(false);
            }
            writer.Complete();
            Logger?.LogInformation("Read {Count} records for {DocumentId}",
                count, context.DocumentId);
        }
        catch (Exception ex)
        {
            writer.TryComplete(ex);
            throw;
        }
    }

    private async Task ConsumeAsync(ChannelReader<RowBatch> reader,
        int partitionNr, BuildContext context)
    {
        Logger?.LogInformation("Starting partition {DocumentId}.{PartitionNr}",
            context.DocumentId, partitionNr);
        string msgPrefix = $"[{partitionNr:000}.{context.DocumentId}] ";
        Report(context.Progress, msgPrefix + "Starting indexer");

        // the pipelines are created for each partition, as tokenizers
        // are stateful and not thread-safe
        PartitionIndexer indexer = new(this, context, partitionNr);
        int count = 0;

        await foreach (RowBatch batch in reader.ReadAllAsync(context.Cancel)
            .ConfigureAwait(false))
        {
            foreach (object[] row in batch.Rows)
            {
                context.Cancel.ThrowIfCancellationRequested();
                indexer.Index(batch.Names, row);
            }
            count += batch.Rows.Length;

            int processed = context.AddProcessed(batch.Rows.Length);
            Report(context.Progress, msgPrefix + count,
                (int)(processed * 100L / context.Total));
        }

        Report(context.Progress, msgPrefix + "Completed indexer", 100);
        Logger?.LogInformation(
            "Completed partition {DocumentId}.{PartitionNr}: {Count} records",
            context.DocumentId, partitionNr, count);
    }

    /// <summary>
    /// The indexer of a single partition, i.e. of the records processed
    /// by a single worker.
    /// </summary>
    private sealed class PartitionIndexer(IndexBuilder builder,
        BuildContext context, int partitionNr)
    {
        private readonly DocumentPipelines _pipelines =
            new(builder._profile, context.Document);
        private readonly Dictionary<string, object> _metadata = [];
        private readonly StringBuilder _text = new();
        private string?[]? _metaKeys;
        private FieldPipeline?[]? _fieldPipelines;

        private void ResolveFields(string[] names)
        {
            _metaKeys = new string?[names.Length];
            _fieldPipelines = new FieldPipeline?[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].StartsWith(META_FIELD_PREFIX, StringComparison.Ordinal))
                    _metaKeys[i] = names[i][META_FIELD_PREFIX.Length..];
                else
                    _fieldPipelines[i] = _pipelines.Get(names[i]);
            }
        }

        public void Index(string[] names, object[] row)
        {
            // resolve fields once, as all the rows have the same fields
            if (_fieldPipelines == null) ResolveFields(names);
            IIndexWriter writer = builder._writer;

            // load metadata for this record in advance, as we cannot say
            // which metadata might be used by which data field in writing
            _metadata.Clear();
            for (int i = 0; i < row.Length; i++)
            {
                if (_metaKeys![i] != null && row[i] is not DBNull)
                    _metadata[_metaKeys[i]!] = row[i];
            }

            // for each data field in record: non-metadata fields have text
            // to be processed
            for (int i = 0; i < row.Length; i++)
            {
                FieldPipeline? pipeline = _fieldPipelines![i];
                if (pipeline == null || row[i] is DBNull) continue;

                _text.Clear().Append(row[i] as string
                    ?? Convert.ToString(row[i], CultureInfo.InvariantCulture));

                pipeline.Filter.Apply(_text);
                if (_text.Length == 0) continue;

                foreach (string token in pipeline.Tokenizer.Tokenize(_text))
                {
                    if (pipeline.Multiplier == null)
                    {
                        writer.Write(context.DocumentId, partitionNr, names[i],
                            token, _metadata);
                        continue;
                    }
                    foreach (string result in pipeline.Multiplier.Multiply(token))
                    {
                        writer.Write(context.DocumentId, partitionNr, names[i],
                            result, _metadata);
                    }
                }
            }
        }
    }

    private sealed record RowBatch(string[] Names, object[][] Rows);

    private sealed class BuildContext(
        string documentId,
        DocumentDefinition document,
        int total,
        IProgress<ProgressReport>? progress,
        CancellationToken cancel)
    {
        private int _processed;

        public string DocumentId { get; } = documentId;
        public DocumentDefinition Document { get; } = document;
        public int Total { get; } = total;
        public IProgress<ProgressReport>? Progress { get; } = progress;
        public CancellationToken Cancel { get; } = cancel;

        public int AddProcessed(int count) =>
            Interlocked.Add(ref _processed, count);
    }

    private sealed record FieldPipeline(
        CompositeTextFilter Filter,
        IStringTokenizer Tokenizer,
        IStringTokenMultiplier? Multiplier);

    /// <summary>
    /// The text processing components of a document, resolved for each of its
    /// fields, falling back to the components defined for
    /// <see cref="DEFAULT_FIELD"/>.
    /// </summary>
    private sealed class DocumentPipelines(EmbixProfile profile,
        DocumentDefinition doc)
    {
        private readonly Dictionary<string, CompositeTextFilter> _filters = [];
        private readonly Dictionary<string, IStringTokenizer> _tokenizers = [];
        private readonly Dictionary<string, IStringTokenMultiplier?> _multipliers = [];

        private static string? GetComponentId(Dictionary<string, string> map,
            string field)
        {
            if (map.TryGetValue(field, out string? id)) return id;
            return map.TryGetValue(DEFAULT_FIELD, out id) ? id : null;
        }

        private static T GetOrCreate<T>(Dictionary<string, T> cache, string? id,
            Func<string, T> create, T empty)
        {
            if (string.IsNullOrEmpty(id)) return empty;
            if (!cache.TryGetValue(id, out T? component))
            {
                component = create(id);
                cache[id] = component;
            }
            return component;
        }

        public FieldPipeline Get(string field)
        {
            CompositeTextFilter filter = GetOrCreate(_filters,
                GetComponentId(doc.TextFilterChains, field),
                id => new CompositeTextFilter([.. profile.GetFilters(id)]),
                new CompositeTextFilter());

            IStringTokenizer tokenizer = GetOrCreate(_tokenizers,
                GetComponentId(doc.Tokenizers, field),
                id => profile.GetTokenizer(id) ?? new NullStringTokenizer(),
                new NullStringTokenizer());

            IStringTokenMultiplier? multiplier = GetOrCreate(_multipliers,
                GetComponentId(doc.TokenMultipliers, field),
                profile.GetTokenMultiplier,
                null);

            return new FieldPipeline(filter, tokenizer, multiplier);
        }
    }

    /// <summary>
    /// Disposes this builder, and its writer when owned.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        if (_ownsWriter) _writer.Dispose();
        _disposed = true;
    }
}
