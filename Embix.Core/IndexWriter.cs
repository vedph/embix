using Embix.Core.Config;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Embix.Core
{
    /// <summary>
    /// Base class for <see cref="IIndexWriter"/> implementors.
    /// </summary>
    public abstract class IndexWriter
    {
        /// <summary>
        /// The key for the language entry in token's metadata.
        /// </summary>
        public const string META_TOKEN_ID = "token_id";

        /// <summary>
        /// The key for the language entry in token's metadata.
        /// </summary>
        public const string META_LANGUAGE = "language";

        /// <summary>
        /// The key for the target ID entry in token's metadata.
        /// </summary>
        public const string META_TARGET_ID = "target_id";

        /// <summary>
        /// Gets or sets the optional metadata supplier to be used.
        /// </summary>
        public IMetadataSupplier? MetadataSupplier { get; set; }

        /// <summary>
        /// Gets or sets the optional logger.
        /// </summary>
        public ILogger? Logger { get; set; }

        /// <summary>
        /// Gets the list of fields building up a token and occurrence record.
        /// </summary>
        protected IndexRecordNames RecordNames { get; }

        /// <summary>
        /// Gets the profile.
        /// </summary>
        protected EmbixProfile Profile { get; }

        /// <summary>
        /// Gets or sets the automatic number generator.
        /// </summary>
        protected IAutoNumber? AutoNumber { get; set; }

        /// <summary>
        /// Gets the token language + value to token ID map. This is used to
        /// keep track of all the token IDs, so that we can generate them here
        /// on client side, thus avoiding costly roundtrips to the server to
        /// get the generated autonumber ID back after each insert.
        /// </summary>
        protected ConcurrentDictionary<Tuple<string, string>, int> TokenIds { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexWriter"/> class.
        /// </summary>
        /// <param name="profile">The profile.</param>
        /// <exception cref="ArgumentNullException">profile</exception>
        protected IndexWriter(EmbixProfile profile)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            AutoNumber = new StandardAutoNumber();
            TokenIds = new ConcurrentDictionary<Tuple<string, string>, int>();

            // setup the names
            RecordNames = new IndexRecordNames(
                new[] { "id", "value", "language" },
                GetOccurrenceFields());
        }

        private string[] GetOccurrenceFields()
        {
            List<string> fields = new(new[]
            {
                "token_id", "field", "target_id"
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
        protected int GetTokenId(Tuple<string, string> key)
        {
            if (!TokenIds.ContainsKey(key))
            {
                int id = AutoNumber!.GetNextId();
                TokenIds[key] = id;
                Logger?.LogDebug($"New token {key.Item1}.{key.Item2}: {id}");
            }
            return TokenIds[key];
        }

        private static object? GetMetadataValue(
            string key,
            IDictionary<string, object>? metadata)
        {
            return metadata?.ContainsKey(key) == true? metadata[key] : null;
        }

        private static T? GetMetadataValue<T>(
            string key,
            IDictionary<string, object>? metadata)
        {
            return metadata?.ContainsKey(key) == true ? (T)metadata[key] : default;
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
                return value[..lengths[name]];
            }
            return value;
        }

        /// <summary>
        /// Gets a token record from the specified token.
        /// </summary>
        /// <param name="documentId">The document identifier.</param>
        /// <param name="field">The field.</param>
        /// <param name="token">The token.</param>
        /// <param name="metadata">The metadata.</param>
        /// <returns>Tuple where 1=token or null (if the token already exists),
        /// and 2=occurrence.</returns>
        protected IndexRecord GetTokenRecord(string documentId,
            string field,
            string token,
            IDictionary<string, object>? metadata)
        {
            MetadataSupplier?.Supply(documentId, field, token, metadata);

            object[]? t = null, o = null;

            // token: id, target_id, value, language
            var key = BuildTokenKey(
                GetMetadataValue<string>(META_LANGUAGE, metadata) ?? "", token);
            bool isNew = !TokenIds.ContainsKey(key);
            int tokenId = GetTokenId(key);
            if (isNew)
            {
                t = new object[]
                {
                    // id
                    tokenId,
                    // value
                    GetSafeLengthString(true, "value", key.Item2),
                    // language
                    GetSafeLengthString(true, "language", key.Item1)
                };
            }

            // occurrence: (id is AI), token_id, field, ...metadata
            List<object> data = new();
            data.AddRange(new object[]
            {
                tokenId,
                field,
                // target_id
                GetMetadataValue(META_TARGET_ID, metadata) ?? ""
            });
            Logger?.LogDebug($"  O: {tokenId} [{field}]");
            if (RecordNames.OccurrenceNames.Count > 2)
            {
                foreach (string fld in RecordNames.OccurrenceNames
                    .Where(f => f != "token_id" && f != "field" && f != "target_id"))
                {
                    object? value = null;
                    if (metadata?.ContainsKey(fld) == true)
                    {
                        value = metadata[fld] is string
                            ? GetSafeLengthString(false, fld, (string)metadata[fld])
                            : metadata[fld];
                    }
                    data.Add(value!);
                }
            }
            o = data.ToArray();

            return new IndexRecord(RecordNames)
            {
                Token = t,
                Occurrence = o
            };
        }
    }
}
