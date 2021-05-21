using Embix.Core.Config;
using Microsoft.Extensions.Logging;
using SqlKata;
using SqlKata.Compilers;
using SqlKata.Execution;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Embix.Core
{
    /// <summary>
    /// SQL-based index writer.
    /// This implements a cache so that data is not written at each new token,
    /// thus improving performance.
    /// </summary>
    public class SqlIndexWriter : IIndexWriter
    {
        /// <summary>
        /// The key for the language entry in token's metadata.
        /// </summary>
        public const string META_TOKEN_ID = "tokenId";

        /// <summary>
        /// The key for the language entry in token's metadata.
        /// </summary>
        public const string META_LANGUAGE = "language";

        /// <summary>
        /// The key for the target ID entry in token's metadata.
        /// </summary>
        public const string META_TARGET_ID = "targetId";

        private readonly IDbConnectionFactory _connFactory;
        private readonly ConcurrentQueue<object[]> _tokenQueue;
        private readonly ConcurrentQueue<object[]> _occQueue;
        private readonly string[] _occFields;
        private readonly string[] _tokFields;
        private static readonly object _locker = new object();
        private bool _disposed;

        /// <summary>
        /// Gets the profile.
        /// </summary>
        protected EmbixProfile Profile { get; }

        /// <summary>
        /// Gets the SQL compiler.
        /// </summary>
        protected Compiler SqlCompiler { get; }

        /// <summary>
        /// Gets or sets the automatic number generator.
        /// </summary>
        protected IAutoNumber AutoNumber { get; set; }

        /// <summary>
        /// Gets the token language + value to token ID map. This is used to
        /// keep track of all the token IDs, so that we can generate them here
        /// on client side, thus avoiding costly roundtrips to the server to
        /// get the generated autonumber ID back after each insert.
        /// </summary>
        protected ConcurrentDictionary<Tuple<string,string>,int> TokenIds { get; }

        /// <summary>
        /// Gets the stored tokens IDs.
        /// </summary>
        protected ConcurrentDictionary<int, bool> StoredTokenIds { get; }

        /// <summary>
        /// Gets or sets the size of the token cache. You can set this to 0
        /// to disable caching (not recommended for production).
        /// </summary>
        protected int TokenCacheSize { get; set; }

        /// <summary>
        /// Gets or sets the optional metadata supplier to be used.
        /// </summary>
        public IMetadataSupplier MetadataSupplier { get; set; }

        /// <summary>
        /// Gets or sets the optional logger.
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlIndexWriter" /> class.
        /// </summary>
        /// <param name="profile">The profile to be used.</param>
        /// <param name="connFactory">The connection factory.</param>
        /// <param name="compiler">The SQL kata compiler to be used.</param>
        /// <exception cref="ArgumentNullException">profile or getConnection or
        /// compiler</exception>
        public SqlIndexWriter(
            EmbixProfile profile,
            IDbConnectionFactory connFactory,
            Compiler compiler)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _connFactory = connFactory
                ?? throw new ArgumentNullException(nameof(connFactory));
            SqlCompiler = compiler
                ?? throw new ArgumentNullException(nameof(compiler));
            _tokenQueue = new ConcurrentQueue<object[]>();
            _occQueue = new ConcurrentQueue<object[]>();
            AutoNumber = new StandardAutoNumber();
            TokenIds = new ConcurrentDictionary<Tuple<string, string>, int>();
            StoredTokenIds = new ConcurrentDictionary<int, bool>();
            TokenCacheSize = 100;

            _occFields = GetOccurrenceFields();
            _tokFields = new[] { "id", "value", "language" };
        }

        private string[] GetOccurrenceFields()
        {
            List<string> fields = new List<string>(new[]
            {
                "tokenId", "field", "targetId"
            });
            fields.AddRange(Profile.MetadataFields);
            return fields.ToArray();
        }

        /// <summary>
        /// Builds the internal cache key for the specified token's language
        /// and value.
        /// </summary>
        /// <param name="language">The language.</param>
        /// <param name="value">The value.</param>
        /// <returns>Tuple with 1=language, 2=value.</returns>
        protected static Tuple<string, string> BuildTokenKey(
            string language, string value) => Tuple.Create(language, value);

        /// <summary>
        /// Gets the identifier for the token with the specified key (as built
        /// by <see cref="BuildTokenKey(string, string)"/>). If this token has
        /// not yet been stored, a new unique ID will be assigned to it, and
        /// the map will be updated.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The token ID.</returns>
        protected int GetTokenId(Tuple<string,string> key)
        {
            if (!TokenIds.ContainsKey(key))
            {
                int id = AutoNumber.GetNextId();
                TokenIds[key] = id;
                Logger?.LogDebug($"New token {key.Item1}.{key.Item2}: {id}");
            }
            return TokenIds[key];
        }

        private static object GetMetadataValue(
            string key,
            IDictionary<string, object> metadata)
        {
            return metadata.ContainsKey(key) ? metadata[key] : null;
        }

        private static T GetMetadataValue<T>(
            string key,
            IDictionary<string, object> metadata)
        {
            return metadata.ContainsKey(key) ? (T)metadata[key] : default;
        }

        private string GetSafeLengthString(bool token, string name, string value)
        {
            if (string.IsNullOrEmpty(value)) return value;

            var lengths = token
                ? Profile.TokenFieldLengths
                : Profile.OccurrenceFieldLengths;
            if (lengths == null || lengths.Count == 0) return value;

            if (lengths.ContainsKey(name) && value.Length > lengths[name])
            {
                Logger?.LogError(
                    $"Value for {(token ? "token" : "occurrence")}.{name} " +
                    $"has length {value.Length} > max ({lengths[name]}): \"{value}\"");
                return value.Substring(0, lengths[name]);
            }
            return value;
        }

        /// <summary>
        /// Enqueues the specified token and its occurrence.
        /// </summary>
        /// <param name="documentId">The document identifier.</param>
        /// <param name="field">The field.</param>
        /// <param name="token">The token.</param>
        /// <param name="metadata">The metadata. These must include at least the
        /// target record ID.</param>
        protected void EnqueueToken(string documentId, string field, string token,
            IDictionary<string, object> metadata)
        {
            MetadataSupplier?.Supply(documentId, field, token, metadata);

            // token: id, targetId, value, language
            var key = BuildTokenKey(
                GetMetadataValue<string>(META_LANGUAGE, metadata), token);
            bool isNew = !TokenIds.ContainsKey(key);
            int tokenId = GetTokenId(key);
            if (isNew)
            {
                _tokenQueue.Enqueue(new object[]
                    {
                        // id
                        tokenId,
                        // value
                        GetSafeLengthString(true, "value", key.Item2),
                        // language
                        GetSafeLengthString(true, "language", key.Item1)
                    });
            }

            // occurrence: (id is AI), tokenId, field, ...metadata
            List<object> data = new List<object>();
            data.AddRange(new object[]
            {
                tokenId,
                field,
                // targetId
                GetMetadataValue(META_TARGET_ID, metadata)
            });
            Logger?.LogDebug($"  occ: {tokenId} [{field}]");
            if (_occFields.Length > 2)
            {
                foreach (string fld in _occFields
                    .Where(f => f != "tokenId" && f != "field" && f != "targetId"))
                {
                    object value = null;
                    if (metadata.ContainsKey(fld))
                    {
                        value = metadata[fld] is string
                            ? GetSafeLengthString(false, fld, (string)metadata[fld])
                            : metadata[fld];
                    }
                    data.Add(value);
                }
            }
            _occQueue.Enqueue(data.ToArray());

            // flush when cache size limit reached
            if (_tokenQueue.Count >= TokenCacheSize) Flush(false);
        }

        /// <summary>
        /// Flushes this writer so that pending data is written.
        /// </summary>
        /// <param name="final">True if this is the final flush.</param>
        public void Flush(bool final)
        {
            lock (_locker)
            {
                int occLimit = _occQueue.Count;
                int tokLimit = _tokenQueue.Count;

                // https://sqlkata.com/docs/update
                Logger?.LogInformation($"Flushing {_tokenQueue.Count} tokens, " +
                    $"{_occQueue.Count} occurrences");

                List<object[]> data = new List<object[]>();
                object[] datum;

                QueryFactory queryFactory = new QueryFactory(
                    _connFactory.GetConnection(),
                    SqlCompiler);

                // tokens
                while ((final || data.Count < tokLimit)
                    && _tokenQueue.TryDequeue(out datum))
                {
                    data.Add(datum);
                    StoredTokenIds[(int)datum[0]] = true;
                }
                if (data.Count > 0)
                {
                    Query query = queryFactory.Query("token")
                        .AsInsert(_tokFields, data);
                    queryFactory.Execute(query);
                }
                Logger?.LogInformation("Tokens flushed");

                // occurrences
                data.Clear();
                while ((final || data.Count < occLimit)
                    && _occQueue.TryDequeue(out datum))
                {
                    // store only if its token has been stored, else enqueue again
                    if (StoredTokenIds.ContainsKey((int)datum[0]))
                        data.Add(datum);
                    else
                        _occQueue.Enqueue(datum);
                }
                if (data.Count > 0)
                {
                    Query query = queryFactory.Query("occurrence")
                        .AsInsert(_occFields, data);
                    queryFactory.Execute(query);
                }
                Logger?.LogInformation("Occurrences flushed");

                Logger?.LogInformation("Flushing completed " +
                    $"({_tokenQueue.Count} {_occQueue.Count})");
            }
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
        public void Write(string documentId, int partitionNr, string field, string token,
            IDictionary<string, object> metadata = null)
        {
            Logger?.LogDebug($"[#{partitionNr:000}] Write: {documentId}.{field}: {token}");
            EnqueueToken(documentId, field, token, metadata);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Flush(true);
                }
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
