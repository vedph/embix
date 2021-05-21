using Embix.Core.Config;
using Embix.Core.Filters;
using Fusi.Tools;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Embix.Core
{
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
        private int _partitionCount;
        private bool _disposed;

        /// <summary>
        /// The prefix assigned to field names when they must be consumed
        /// as metadata by the builder.
        /// </summary>
        public const string META_FIELD_PREFIX = "m_";

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
        public ILogger Logger { get; set; }
        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexBuilder"/> class.
        /// </summary>
        /// <param name="connFactory">The connection factory.</param>
        /// <exception cref="ArgumentNullException">connFactory</exception>
        public IndexBuilder(
            EmbixProfile profile,
            IDbConnectionFactory connFactory,
            IIndexWriter writer)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _connFactory = connFactory
                ?? throw new ArgumentNullException(nameof(connFactory));
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
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
        public int GetRecordCount(string countSql, IDbConnection connection = null)
        {
            if (countSql == null)
                throw new ArgumentNullException(nameof(countSql));

            bool mustClose = false, mustDispose = false;

            // open connection
            if (connection == null)
            {
                connection = _connFactory.GetConnection();
                mustDispose = true;
            }
            if (connection.State == ConnectionState.Closed)
            {
                connection.Open();
                mustClose = true;
            }

            // execute command
            IDbCommand cmd = connection.CreateCommand();
            cmd.CommandText = countSql;
            int result = (int)(long)cmd.ExecuteScalar();

            // apply limit if any
            if (RecordLimit > 0 && result > RecordLimit) result = RecordLimit;

            // close connection
            if (mustClose) connection.Close();
            if (mustDispose) connection.Dispose();

            return result;
        }

        private static void DoIndex(IndexTaskOptions options)
        {
            ILogger logger = options.Logger;
            logger?.LogInformation("Starting DoIndex for " +
                $"{options.DocumentId}.{options.PartitionNumber}");

            StringBuilder text = new StringBuilder();
            int count = 0;
            ProgressReport report = options.Progress != null
                ? new ProgressReport() : null;
            string msgPrefix = options.Progress != null
                ? $"[{options.PartitionNumber:000}.{options.DocumentId}] "
                : null;
            Dictionary<string, object> metadata = new Dictionary<string, object>();

            if (options.Progress != null)
            {
                report.Message = msgPrefix + "Starting indexer";
                options.Progress.Report(report);
            }

            // get the document definition
            DocumentDefinition doc = options.Profile.Documents
                .First(d => d.Id == options.DocumentId);
            CompositeTextFilter emptyFilter = new CompositeTextFilter();
            IStringTokenizer nullTokenizer = new NullStringTokenizer();

            // create the sets of filters for the requested document fields
            Dictionary<string, CompositeTextFilter> filters
                = new Dictionary<string, CompositeTextFilter>();
            foreach (var kv in doc.TextFilterChains)
            {
                filters[kv.Key] = string.IsNullOrEmpty(kv.Value)
                    ? emptyFilter
                    : new CompositeTextFilter(
                    options.Profile.GetFilters(kv.Value).ToArray());
            }

            // create the sets of tokenizers for the requested document fields
            Dictionary<string, IStringTokenizer> tokenizers
                = new Dictionary<string, IStringTokenizer>();
            foreach (var kv in doc.Tokenizers)
            {
                tokenizers[kv.Key] = string.IsNullOrEmpty(kv.Value)
                    ? nullTokenizer
                    : options.Profile.GetTokenizer(kv.Value) ?? nullTokenizer;
            }

            // for each record
            IDataReader reader = options.Reader;
            while (reader.Read())
            {
                metadata.Clear();
                count++;

                // load metadata for this record in advance, as we cannot say
                // which metadata might be used by which data field in writing
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (reader.IsDBNull(i)) continue;

                    // get its name; by convention, metadata fields are
                    // prefixed with m_; all the other fields are typically
                    // aliases assigned so that the field name can be used
                    // as its short code (e.g. place.description AS pldsc)
                    string field = reader.GetName(i);
                    if (field.StartsWith(META_FIELD_PREFIX))
                    {
                        metadata[field.Substring(META_FIELD_PREFIX.Length)]
                            = reader.GetValue(i);
                    }
                }

                // for each data field in record
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (reader.IsDBNull(i)) continue;

                    string field = reader.GetName(i);

                    // non-metadata fields have text to be processed
                    if (!field.StartsWith(META_FIELD_PREFIX))
                    {
                        // get the text
                        text.Clear();
                        text.Append(reader.GetString(i));

                        // filter it
                        CompositeTextFilter filter = filters.ContainsKey(field) ?
                            filters[field] : filters["*"] ?? emptyFilter;
                        filter.Apply(text);
                        if (text.Length == 0) continue;

                        // tokenize it and write each token
                        IStringTokenizer tokenizer = tokenizers.ContainsKey(field) ?
                            tokenizers[field] : tokenizers["*"] ?? nullTokenizer;

                        foreach (string token in tokenizer.Tokenize(text))
                        {
                            options.Writer.Write(options.DocumentId,
                                options.PartitionNumber,
                                field, token, metadata);
                        }
                    }
                }

                if (options.Progress != null && count % 10 == 0)
                {
                    report.Message = msgPrefix + count;
                    report.Percent = count * 100 / options.PartitionSize;
                    options.Progress.Report(report);
                }
            } // record

            if (options.Progress != null)
            {
                report.Message = msgPrefix +  "Completed indexer";
                report.Percent = 100;
                options.Progress.Report(report);
            }
            logger?.LogInformation("Completed DoIndex for " +
                $"{options.DocumentId}.{options.PartitionNumber}");
        }

        /// <summary>
        /// Builds the index asynchronously.
        /// </summary>
        /// <param name="documentId">The ID of the "document" in the profile.
        /// </param>
        /// <param name="cancel">The cancel token.</param>
        /// <param name="progress">The optional progress reporter.</param>
        /// <exception cref="ApplicationException">document ID not found</exception>
        public Task BuildAsync(
            string documentId,
            CancellationToken cancel,
            IProgress<ProgressReport> progress)
        {
            // get document
            DocumentDefinition doc = _profile.GetDocument(documentId);
            if (doc == null)
                throw new ApplicationException("Document not found: " + documentId);

            // connect to database
            ProgressReport report = progress != null ? new ProgressReport() : null;
            int total;

            using (IDbConnection connection = _connFactory.GetConnection())
            {
                connection.Open();

                // calculate partitions
                report.Message = "Calculating partitions";
                progress?.Report(report);
                total = GetRecordCount(doc.CountSql, connection);
            }

            int partitionCount = total < MinPartitionSize ? 1 : _partitionCount;
            int partitionSize = (int)Math.Ceiling((double)total / partitionCount);

            report.Message = "Partitions: " + partitionCount;
            progress?.Report(report);

            // setup autonumber
            IAutoNumber auto = new StandardAutoNumber();

            // spawn 1 task per partition
            IDbConnection[] connections = new IDbConnection[partitionCount];
            IDataReader[] readers = new IDataReader[partitionCount];
            Task[] tasks = new Task[partitionCount];

            for (int i = 0; i < partitionCount; i++)
            {
                connections[i] = _connFactory.GetConnection();
                connections[i].Open();

                IDbCommand cmd = connections[i].CreateCommand();
                // {0}=skip, {1}=limit
                cmd.CommandText = string.Format(doc.DataSql,
                    i * partitionSize, partitionSize);
                readers[i] = cmd.ExecuteReader(CommandBehavior.CloseConnection);

                var options = new IndexTaskOptions
                {
                    PartitionNumber = i + 1,
                    PartitionSize = partitionSize,
                    DocumentId = documentId,
                    Profile = _profile,
                    Reader = readers[i],
                    Writer = _writer,
                    Progress = progress,
                    Logger = Logger
                };
                tasks[i] = new Task(() => DoIndex(options), cancel);
                tasks[i].Start();
            }

            // https://stackoverflow.com/questions/12337671/using-async-await-for-multiple-tasks
            return Task.WhenAll(tasks);
        }

        internal sealed class IndexTaskOptions
        {
            public int PartitionNumber { get; set; }
            public int PartitionSize { get; set; }
            public string DocumentId { get; set; }
            public EmbixProfile Profile { get; set; }
            public IDataReader Reader { get; set; }
            public IIndexWriter Writer { get; set; }
            public IProgress<ProgressReport> Progress { get; set; }
            public ILogger Logger { get; set; }
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing) _writer.Flush(true);
                _disposed = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
