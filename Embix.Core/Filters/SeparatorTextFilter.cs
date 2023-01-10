using Fusi.Tools.Config;
using System;
using System.Collections.Generic;
using System.Text;

namespace Embix.Core.Filters;

/// <summary>
/// Separators text filter. This replaces a set of predefined characters,
/// acting as name separators, with space.
/// <para>Tag: <c>text-filter.separator</c>.</para>
/// </summary>
/// <seealso cref="ITextFilter" />
[Tag("text-filter.separator")]
public sealed class SeparatorTextFilter : ITextFilter,
    IConfigurable<SeparatorTextFilterOptions>
{
    /// <summary>
    /// Gets the separator characters.
    /// </summary>
    public HashSet<char> Separators { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SeparatorTextFilter"/>
    /// class.
    /// </summary>
    public SeparatorTextFilter()
    {
        Separators = new HashSet<char>(new[]{'/', ','});
    }

    /// <summary>
    /// Configures this filter with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">options</exception>
    public void Configure(SeparatorTextFilterOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        Separators.Clear();
        foreach (char c in options.Separators ?? "")
            Separators.Add(c);
    }

    /// <summary>
    /// Applies the specified text.
    /// </summary>
    /// <param name="text">The text.</param>
    public void Apply(StringBuilder text)
    {
        for (int i = 0; i < text.Length; i++)
            if (Separators.Contains(text[i])) text[i] = ' ';
    }
}

/// <summary>
/// Options for <see cref="SeparatorTextFilter"/>.
/// </summary>
public class SeparatorTextFilterOptions
{
    /// <summary>
    /// Gets or sets the separator characters.
    /// </summary>
    public string Separators { get; set; }
}
