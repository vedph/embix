using Embix.Core.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Embix.Core.Config;

/// <summary>
/// Embedded index builder profile. This defines any number of text filter
/// chains, each chain having an arbitrary ID; and any number of "documents",
/// i.e. the sources of texts to be indexed. Each of these documents is a
/// <see cref="DocumentDefinition"/>, having an arbitrary ID, SQL code
/// for retrieving the total count of records and the records data, and
/// the chains to be used for filtering (both in preprocessing the whole
/// text and in tokenizing).
/// </summary>
public sealed class EmbixProfile
{
    private readonly Dictionary<string, DocumentDefinition> _documents;
    // documents in their definition order
    private readonly List<DocumentDefinition> _documentList = [];
    private readonly EmbixFilterFactory _filterFactory;

    /// <summary>
    /// Gets or sets the token fields lengths.
    /// </summary>
    public Dictionary<string, int> TokenFieldLengths { get; set; }

    /// <summary>
    /// Gets or sets the occurrence fields lengths.
    /// </summary>
    public Dictionary<string, int> OccurrenceFieldLengths { get; set; }

    /// <summary>
    /// Gets the metadata fields in their insertion order.
    /// Each of these field names must correspond to the metadata key
    /// used to store the occurrence's metadata when indexing. For instance,
    /// we might have an occurrence table with additional fields for rank,
    /// yearMin, yearMax. In this case, the value of this property should
    /// list all these fields, while excluding the essential metadata keys
    /// <see cref="SqlIndexWriter.META_LANGUAGE"/>,
    /// <see cref="SqlIndexWriter.META_TARGET_ID"/> and
    /// <see cref="SqlIndexWriter.META_TOKEN_ID"/>.
    /// </summary>
    public IList<string> MetadataFields { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbixProfile"/> class.
    /// </summary>
    /// <param name="json">The json.</param>
    /// <param name="factory">The filters factory.</param>
    /// <exception cref="ArgumentNullException">json or factory</exception>
    public EmbixProfile(string json, EmbixFilterFactory factory)
    {
        if (json == null) throw new ArgumentNullException(nameof(json));

        _documents = new Dictionary<string, DocumentDefinition>();
        _filterFactory = factory ?? throw new ArgumentNullException(nameof(factory));

        // default token field lengths
        TokenFieldLengths = new Dictionary<string, int>
        {
            { "value", 100 },
            { "language", 5 }
        };
        OccurrenceFieldLengths = new Dictionary<string, int>();
        MetadataFields = new List<string>();

        LoadDocuments(json);
    }

    /// <summary>
    /// Gets the documents definitions.
    /// </summary>
    public IReadOnlyList<DocumentDefinition> GetDocuments() =>
        _documentList.AsReadOnly();

    private static Dictionary<string, int>? ReadLengths(JsonElement root,
        string name)
    {
        if (!root.TryGetProperty(name, out JsonElement lengths)
            || lengths.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        return lengths.Deserialize<Dictionary<string, int>>();
    }

    private void LoadDocuments(string json)
    {
        _documents.Clear();
        _documentList.Clear();

        using JsonDocument doc = JsonDocument.Parse(json);

        // TokenFieldLengths, OccurrenceFieldLengths: specified lengths
        // override the defaults
        foreach (var (name, target) in new[]
        {
            (nameof(TokenFieldLengths), TokenFieldLengths),
            (nameof(OccurrenceFieldLengths), OccurrenceFieldLengths)
        })
        {
            Dictionary<string, int>? lengths = ReadLengths(doc.RootElement, name);
            if (lengths == null) continue;
            foreach (var kv in lengths) target[kv.Key] = kv.Value;
        }

        // MetadataFields
        if (doc.RootElement.TryGetProperty("MetadataFields", out JsonElement meta))
            MetadataFields = JsonSerializer.Deserialize<string[]>(meta.GetRawText())
                ?? Array.Empty<string>();
        else
            MetadataFields = Array.Empty<string>();

        // Documents
        if (!doc.RootElement.TryGetProperty("Documents", out JsonElement docs))
            return;
        foreach (JsonElement child in docs.EnumerateArray())
        {
            DocumentDefinition dd = child.Deserialize<DocumentDefinition>()!;
            if (string.IsNullOrEmpty(dd.Id))
                throw new InvalidOperationException("Document without Id in profile");
            if (!_documents.TryAdd(dd.Id, dd))
                throw new InvalidOperationException("Duplicate document Id: " + dd.Id);
            _documentList.Add(dd);
        }
    }

    /// <summary>
    /// Gets the document with the specified ID.
    /// </summary>
    /// <param name="id">The document identifier.</param>
    /// <returns>Document or null if not found.</returns>
    public DocumentDefinition? GetDocument(string id)
    {
        return _documents.TryGetValue(id, out DocumentDefinition? doc) ? doc : null;
    }

    /// <summary>
    /// Gets the document filters for the specified document ID.
    /// </summary>
    /// <param name="chainId">The filters chain ID.</param>
    /// <returns>Filters or null if chain ID not found.</returns>
    /// <exception cref="ArgumentNullException">id</exception>
    public IList<ITextFilter> GetFilters(string chainId)
    {
        if (chainId == null) throw new ArgumentNullException(nameof(chainId));
        return _filterFactory.GetTextFilters(chainId);
    }

    /// <summary>
    /// Gets the tokenizer with the specified ID.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The tokenizer or null if not found.</returns>
    /// <exception cref="ArgumentNullException">id</exception>
    public IStringTokenizer? GetTokenizer(string id)
    {
        if (id == null) throw new ArgumentNullException(nameof(id));

        return _filterFactory.GetTokenizer(id);
    }

    /// <summary>
    /// Gets the token multiplier with the specified ID.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The multiplier or null if not found.</returns>
    /// <exception cref="ArgumentNullException">id</exception>
    public IStringTokenMultiplier? GetTokenMultiplier(string id)
    {
        if (id == null) throw new ArgumentNullException(nameof(id));

        return _filterFactory.GetTokenMultiplier(id);
    }
}
