using Fusi.Tools.Config;
using System;
using System.Text;

namespace Embix.Core.Filters;

/// <summary>
/// HTML/XML tag stripper filter.
/// </summary>
/// <seealso cref="ITextFilter" />
[Tag("text-filter.tag")]
public sealed class TagTextFilter : ITextFilter
{
    /// <summary>
    /// Applies this filter to the specified text.
    /// </summary>
    /// <param name="text">The text wrapped in a <see cref="StringBuilder" />.</param>
    /// <exception cref="ArgumentNullException">text</exception>
    public void Apply(StringBuilder text)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '<')
            {
                int start = i++;
                while (i < text.Length && text[i] != '>') i++;
                if (i < text.Length)
                {
                    text.Remove(start, i + 1 - start);
                    i = start;
                }
            }
            else i++;
        }
    }
}
