using System;
using System.Collections.Generic;
using System.Text;

namespace Embix.Core;

/// <summary>
/// A record to be written to the index.
/// </summary>
public class IndexRecord
{
    /// <summary>
    /// Gets or sets the token's fields values.
    /// </summary>
    public IList<object>? Token { get; set; }

    /// <summary>
    /// Gets or sets the occurrence's fields values.
    /// </summary>
    public IList<object>? Occurrence { get; set; }

    /// <summary>
    /// Gets the names for the token and occurrence fields.
    /// </summary>
    public IndexRecordNames Names { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexRecord"/> class.
    /// </summary>
    /// <param name="names">The names.</param>
    /// <exception cref="ArgumentNullException">names</exception>
    public IndexRecord(IndexRecordNames names)
    {
        Names = names ?? throw new ArgumentNullException(nameof(names));
    }

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>
    /// A <see cref="string" /> that represents this instance.
    /// </returns>
    public override string ToString()
    {
        StringBuilder sb = new();

        if (Token != null)
        {
            sb.Append('#').Append(Token[0])
                .Append(" [").Append(Token[2]).Append("] ").Append(Token[1]);
        }

        if (Occurrence != null)
        {
            sb.Append(": >#").Append(Occurrence[0]).Append(" [")
                .Append(Occurrence[1]).Append("] => ").Append(Occurrence[2]);
        }

        return sb.ToString();
    }
}

/// <summary>
/// The names defined for the various fields of an <see cref="IndexRecord"/>.
/// </summary>
public class IndexRecordNames
{
    /// <summary>
    /// Gets the token's fields names.
    /// </summary>
    public IList<string> TokenNames { get; }

    /// <summary>
    /// Gets the occurrence's fields names.
    /// </summary>
    public IList<string> OccurrenceNames { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexRecordNames"/> class.
    /// </summary>
    /// <param name="tokenNames">The token names.</param>
    /// <param name="occNames">The occurrence names.</param>
    /// <exception cref="ArgumentNullException">tokenNames or occNames</exception>
    public IndexRecordNames(string[] tokenNames, string[] occNames)
    {
        TokenNames = tokenNames
            ?? throw new ArgumentNullException(nameof(tokenNames));
        OccurrenceNames = occNames
            ?? throw new ArgumentNullException(nameof(occNames));
    }
}
