using System.Collections.Generic;

namespace Embix.Core.Config;

/// <summary>
/// Document definition used in <see cref="EmbixProfile"/>.
/// </summary>
public class DocumentDefinition
{
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the select count SQL command to get the total count
    /// of records to be processed.
    /// </summary>
    public string? CountSql { get; set; }

    /// <summary>
    /// Gets or sets the data SQL. Records are read once by a single reader,
    /// in any order (no <c>ORDER BY</c> is required). The SQL may have two
    /// optional placeholders, <c>{0}</c>=skip count (always 0) and
    /// <c>{1}</c>=limit (the count of records to be processed, i.e. the
    /// result of <see cref="CountSql"/>, eventually capped by the builder's
    /// records limit).
    /// </summary>
    public string? DataSql { get; set; }

    /// <summary>
    /// Gets or sets the text filters chains map, where each field code
    /// is mapped to the corresponding filters chain ID. You can use <c>*</c>
    /// as the field code to represent the default chain ID.
    /// </summary>
    public Dictionary<string, string> TextFilterChains { get; set; }

    /// <summary>
    /// Gets or sets the tokenizer filters chains map, where each field code
    /// is mapped to the corresponding filters chain ID. You can use <c>*</c>
    /// as the field code to represent the default chain ID.
    /// </summary>
    public Dictionary<string, string> Tokenizers { get; set; }

    /// <summary>
    /// Gets or sets the token multipliers map, where each field code
    /// is mapped to the corresponding token multiplier ID if any.
    /// You can use <c>*</c> as the field code to represent the default
    /// multiplier.
    /// </summary>
    public Dictionary<string, string> TokenMultipliers { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentDefinition"/>
    /// class.
    /// </summary>
    public DocumentDefinition()
    {
        TextFilterChains = new Dictionary<string, string>();
        Tokenizers = new Dictionary<string, string>();
        TokenMultipliers = new Dictionary<string, string>();
    }

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>
    /// A <see cref="string" /> that represents this instance.
    /// </returns>
    public override string ToString()
    {
        return $"{Id} {DataSql}";
    }
}
