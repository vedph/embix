using Embix.Core.Config;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;

namespace Embix.Core;

/// <summary>
/// Base class for <see cref="IIndexWriter"/> implementors.
/// </summary>
public abstract class IndexWriter
{
    /// <summary>
    /// The key for the token ID entry in token's metadata.
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

    // occurrence's fixed fields: token_id, field, target_id
    private const int OCC_FIXED_COUNT = 3;

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
    protected ConcurrentDictionary<(string Language, string Value), int> TokenIds
    { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexWriter"/> class.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <exception cref="ArgumentNullException">profile</exception>
    protected IndexWriter(EmbixProfile profile)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        AutoNumber = new StandardAutoNumber();
        TokenIds = new ConcurrentDictionary<(string, string), int>();

        // setup the names
        RecordNames = new IndexRecordNames(
            ["id", "value", "language"],
            GetOccurrenceFields());
    }

    private string[] GetOccurrenceFields()
    {
        List<string> fields = [META_TOKEN_ID, "field", META_TARGET_ID];
        fields.AddRange(Profile.MetadataFields);
        return [.. fields];
    }

    /// <summary>
    /// Gets the identifier for the token with the specified key. If this
    /// token has not yet been stored, a new unique ID will be assigned to it,
    /// and the map will be updated. This is thread-safe: when several threads
    /// race for the same new key, exactly one of them gets
    /// <paramref name="isNew"/>=true, and all get the same ID.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="isNew">True if the ID was assigned by this call.</param>
    /// <returns>The token ID.</returns>
    protected int GetTokenId((string Language, string Value) key, out bool isNew)
    {
        if (TokenIds.TryGetValue(key, out int id))
        {
            isNew = false;
            return id;
        }

        // a losing thread just wastes an ID, which is harmless
        int newId = AutoNumber!.GetNextId();
        if (TokenIds.TryAdd(key, newId))
        {
            isNew = true;
            if (Logger?.IsEnabled(LogLevel.Debug) == true)
            {
                Logger.LogDebug("New token {Language}.{Value}: {Id}",
                    key.Language, key.Value, newId);
            }
            return newId;
        }
        isNew = false;
        return TokenIds[key];
    }

    private string? GetSafeLengthString(bool token, string name, string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        Dictionary<string, int>? lengths = token
            ? Profile.TokenFieldLengths
            : Profile.OccurrenceFieldLengths;

        if (lengths?.TryGetValue(name, out int max) == true
            && max > 0 && value.Length > max)
        {
            Logger?.LogError(
                "Value for {Table}.{Name} has length {Length} > max ({Max}): \"{Value}\"",
                token ? "token" : "occurrence", name, value.Length, max, value);
            return value[..max];
        }
        return value;
    }

    private static string? ToInvariantString(object? value) =>
        value == null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets a token record from the specified token.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="field">The field.</param>
    /// <param name="token">The token.</param>
    /// <param name="metadata">The metadata.</param>
    /// <returns>Record where token is null when the token already exists.
    /// </returns>
    protected IndexRecord GetTokenRecord(string documentId,
        string field,
        string token,
        IDictionary<string, object>? metadata)
    {
        MetadataSupplier?.Supply(documentId, field, token, metadata);

        // token: id, value, language
        object? language = null;
        metadata?.TryGetValue(META_LANGUAGE, out language);
        var key = (ToInvariantString(language) ?? "", token);
        int tokenId = GetTokenId(key, out bool isNew);

        object[]? t = null;
        if (isNew)
        {
            t =
            [
                tokenId,
                GetSafeLengthString(true, "value", key.Item2)!,
                GetSafeLengthString(true, "language", key.Item1)!
            ];
        }

        // occurrence: (id is generated by the DB), token_id, field,
        // target_id, ...metadata
        IList<string> names = RecordNames.OccurrenceNames;
        object[] o = new object[names.Count];
        o[0] = tokenId;
        o[1] = field;
        object? targetId = null;
        metadata?.TryGetValue(META_TARGET_ID, out targetId);
        o[2] = ToInvariantString(targetId) ?? "";

        for (int i = OCC_FIXED_COUNT; i < names.Count; i++)
        {
            object? value = null;
            if (metadata?.TryGetValue(names[i], out value) == true
                && value is string s)
            {
                value = GetSafeLengthString(false, names[i], s);
            }
            o[i] = value!;
        }

        if (Logger?.IsEnabled(LogLevel.Debug) == true)
            Logger.LogDebug("  O: {TokenId} [{Field}]", tokenId, field);

        return new IndexRecord(RecordNames)
        {
            Token = t,
            Occurrence = o
        };
    }
}
